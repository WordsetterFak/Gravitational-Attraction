using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulatorManager : MonoBehaviour
{
    public static SimulatorManager Instance { get; private set; }

    [SerializeField] private int starCount;
    [SerializeField] private float maxSpawnRange;
    [SerializeField] private float cameraSizeOffset;
    [SerializeField] private float gravitationalConstant;
    [SerializeField] private float gravitationalConstantIncreasePerSecond;
    [SerializeField] private float maxGravitationConstant;
    [SerializeField] private float starSurvivalMassRatio;
    [SerializeField] [Range(0, 1)] private float collisionMassRetention;
    [SerializeField] private Vector2 massRange;
    [SerializeField] private Vector2 initialSpeedRange;
    [SerializeField] private GameObject starPrefab;
    [SerializeField] private int subdivisions;

    private int cellCount;
    private Vector2 extremeX;
    private Vector2 extremeY;
    private Vector2[] starsGridCoordinates;
    private int[,] cellStars;
    private int[] cellStarCounts;
    private float[] cellWeightedXs;
    private float[] cellWeightedYs;
    private float[] cellMasses;
    private Vector2[] cellCentersOfMass;
    private bool[] cellHasCachedCenterOfMass;
    private GameObject[] stars;
    private Vector2[] forces;
    private Vector2[] velocities;
    private float[] masses;
    private Vector2[] collisionPairs;

    private float radius = 1;
    private bool run = true;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        stars = new GameObject[starCount];
        velocities = new Vector2[starCount];
        forces = new Vector2[starCount];
        masses = new float[starCount];
        collisionPairs = new Vector2[starCount];
        ResetCellInfo();
        SpawnStars();
        Camera.main.orthographicSize = maxSpawnRange + cameraSizeOffset;
    }

    private void FixedUpdate()
    {
        SimulationPhysicsStep();

        float gravitationalConstantIncrease = gravitationalConstantIncreasePerSecond * Time.fixedDeltaTime;
        float newGravitationalConstant = gravitationalConstant + gravitationalConstantIncrease;
        gravitationalConstant = Mathf.Clamp(newGravitationalConstant, -maxGravitationConstant, maxGravitationConstant);
    }

    private void SpawnStars()
    {
        for (int i = 0; i < starCount; i++)
        {
            Vector2 initialPosition = maxSpawnRange * Random.insideUnitCircle;
            bool overlap = false;

            for (int j = 0; j < i; j++)
            {
                if ((initialPosition - (Vector2)stars[j].transform.position).magnitude <= radius)
                {
                    overlap = true;
                    break;
                }
            }

            if (overlap)
            {
                i--;
                continue;
            }

            GameObject newPlanet = Instantiate(starPrefab);
            newPlanet.transform.position = initialPosition;
            stars[i] = newPlanet;
            float initialSpeed = Random.Range(initialSpeedRange.x, initialSpeedRange.y);
            velocities[i] = initialSpeed * Random.insideUnitCircle.normalized;
            masses[i] = Random.Range(massRange.x, massRange.y);

            UpdateExtremePositions(initialPosition);
        }
    }

    private void SimulationPhysicsStep()
    {
        if (!run)
        {
            return;
        }

        forces = new Vector2[starCount];  // Reset previous forces
        ResetCellInfo();
        collisionPairs = new Vector2[starCount];  // Reset previous collisions
        int collisionCount = 0;

        // Insert all stars in their responding cells and prepare data for the next step
        for(int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                // Ensure star is still alive
                continue;
            }

            Vector2 starPosition = stars[i].transform.position;
            float starMass = masses[i];

            Vector2 cellCoordinates = CalculateGridCoordinates(starPosition);
            int cellHash = GetCellHash(cellCoordinates);

            cellMasses[cellHash] += starMass;
            cellWeightedXs[cellHash] += starPosition.x * starMass;  // m1*x1 + ... + mn*xn
            cellWeightedYs[cellHash] += starPosition.y * starMass;  // m1*y1 + ... + mn*yn
            starsGridCoordinates[i] = cellCoordinates;
            cellStars[cellHash, cellStarCounts[cellHash]] = i;
            cellStarCounts[cellHash]++;
        }

        for (int thisStarIndex = 0; thisStarIndex < stars.Length; thisStarIndex++)
        {
            Vector2 starPosition = stars[thisStarIndex].transform.position;
            Vector2 starGridCoordinates = CalculateGridCoordinates(starPosition);
            int starCellHash = GetCellHash(starGridCoordinates);
            Vector2 thisStarPosition = stars[thisStarIndex].transform.position;
            float thisStarMass = masses[thisStarIndex];

            // Interact with all stars in the same cell
            for (int j = 0; j < cellStarCounts[starCellHash]; j++)
            {
                int otherStarIndex = cellStars[starCellHash, j];

                if (otherStarIndex == thisStarIndex)
                {
                    // Prevent star with interacting with itself
                    continue;
                }

                Vector2 otherStarPosition = stars[otherStarIndex].transform.position;
                float otherStarMass = masses[otherStarIndex];
                float massProduct = thisStarMass * otherStarMass;

                Vector2 direction = otherStarPosition - thisStarPosition;
                if (direction == Vector2.zero) { direction = Vector2.down; }
                Vector2 directionNormalized = direction.normalized;

                float gravitationForceMagnitude = (gravitationalConstant * massProduct) / direction.sqrMagnitude;
                Vector2 gravitationalForce = gravitationForceMagnitude * directionNormalized;

                forces[thisStarIndex] += gravitationalForce;
                forces[otherStarIndex] += -gravitationalForce;  // Newton's 3rd Law 
            }

            // Interact with other cells
            for (int cellHash = 0; cellHash < cellCount; cellHash++)
            {
                Vector2 otherCellCenterOfMass = CalculateCellCenterOfMass(cellHash);
                float otherCellMass = cellMasses[cellHash];
                float massProduct = thisStarMass * otherCellMass;

                Vector2 direction = otherCellCenterOfMass - thisStarPosition;
                Vector2 directionNormalized = direction.normalized;

                float gravitationForceMagnitude = (gravitationalConstant * massProduct) / direction.sqrMagnitude;
                Vector2 gravitationalForce = gravitationForceMagnitude * directionNormalized;

                forces[thisStarIndex] += gravitationalForce;
            }
        }

        // Calculate the new positions using Semi-Implicit Euler Method
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                // In case star i was just destroyed
                continue;
            }

            Vector2 acceleration = forces[i] / masses[i];

            velocities[i] += acceleration * Time.fixedDeltaTime;
            stars[i].transform.position += (Vector3)velocities[i] * Time.fixedDeltaTime;
            UpdateExtremePositions(stars[i].transform.position);
        }

    }

    private void ResetCellInfo()
    {
        starsGridCoordinates = new Vector2[starCount];
        cellStars = new int[cellCount, starCount];
        cellStarCounts = new int[cellCount];
        cellWeightedXs = new float[cellCount];
        cellMasses = new float[cellCount];
        cellCentersOfMass = new Vector2[cellCount];
        cellHasCachedCenterOfMass = new bool[cellCount];

    }

    private int GetCellHash(Vector2 gridCoordinates)
    {
        return GetCellHash((int)gridCoordinates.x, (int)gridCoordinates.y);
    }

    private int GetCellHash(int cellX, int cellY)
    {
        return cellX + cellY * subdivisions;
    }

    private Vector2 CalculateGridCoordinates(Vector2 worldposition)
    {
        float universeWidth = extremeX.y - extremeX.x;
        float universeHeight = extremeY.y - extremeY.x;
        Vector2 universeSize = new Vector2(universeWidth, universeHeight);
        Vector2 cellSize = universeSize / subdivisions;

        int cellX = (int)(universeSize.x / worldposition.x);
        int cellY = (int)(universeSize.y / worldposition.y);

        return new Vector2(cellX, cellY);
    }

    private int[] GetNeighboringCellHashes(int cellHash)
    {
        int maxNeighbors = 8;  // In a 2 dimensional
        int[] neighboringCellHashes = new int[maxNeighbors];
        int index = 0;

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int neighboringHash = cellHash + i * subdivisions + j;

                // Left column hashes will be equal to 0 mod subdivisions
                // Right column hashes will be equal to subdivision - 1 mod subdivisions
                float leftHashDivision = cellHash / subdivisions;
                float rightHashDivision = (cellHash + 1) / subdivisions;
                float leftCellHashRemainder = leftHashDivision - Mathf.Floor(leftHashDivision);
                float rightCellHashRemainder = rightHashDivision - Mathf.Floor(rightHashDivision);

                if (neighboringHash < 0 || neighboringHash >= cellCount)
                {
                    // Hash doesn't correspond to any cell, occurs when
                    // the given cell is in the top/bottom row
                    continue;
                }
                else if (leftCellHashRemainder == 0 && j == -1)
                {
                    // Hash doesn't correspond to any cell, occurs when
                    // the given cell is in the left column
                    continue;
                }
                else if (rightCellHashRemainder == 0 && j == 1)
                {
                    // Hash doesn't correspond to any cell, occurs when
                    // the given cell is in the right column
                    continue;
                }

                neighboringCellHashes[index] = neighboringHash;
                index++;
            }
        }

        int[] filteredNeighboringCellHashes = new int[index];
        for (int i = 0; i < index; i++)
        {
            filteredNeighboringCellHashes[i] = neighboringCellHashes[i];
        }

        return filteredNeighboringCellHashes;
    }

    private Vector2 CalculateCellCenterOfMass(int cellHash)
    {
        if (!cellHasCachedCenterOfMass[cellHash])
        {
            float cellMass = cellMasses[cellHash];

            float centerOfMassX = cellWeightedXs[cellHash] / cellMass;
            float centerOfMassY = cellWeightedYs[cellHash] / cellMass;
            Vector2 centerOfMass = new Vector2(centerOfMassX, centerOfMassY);

            cellCentersOfMass[cellHash] = centerOfMass;
            cellHasCachedCenterOfMass[cellHash] = true;
        }

        return cellCentersOfMass[cellHash];
    }

    private void UpdateExtremePositions(Vector2 position)
    {
        if (position.x < extremeX.x)
        {
            extremeX.x = position.x;
        }

        if (position.x > extremeX.y)
        {
            extremeX.y = position.x;
        }

        if (position.y < extremeY.x)
        {
            extremeY.x = position.y;
        }

        if (position.y > extremeY.y)
        {
            extremeY.y = position.y;
        }
    }

    private void Collide(int i, int j)
    {

        if (masses[i] < masses[j])
        {
            int temp = i;
            i = j;
            j = temp;
        }

        float massRatio = masses[i] / masses[j];

        if (massRatio < starSurvivalMassRatio)
        {
            Destroy(stars[i]);
            Destroy(stars[j]);
        }
        else
        {
            // Transfer mass and preserve kinetic Energy
            float survivingStarkineticEnergy = 1 / 2 * masses[i] * velocities[i].sqrMagnitude;
            masses[i] = masses[i] * collisionMassRetention + masses[j] * collisionMassRetention;
            float destroyedStarKineticEnergy = 1 / 2 * masses[j] * velocities[j].sqrMagnitude;
            float newKineticEnergy = survivingStarkineticEnergy + destroyedStarKineticEnergy * collisionMassRetention;
            float newSpeed = Mathf.Sqrt(2 * newKineticEnergy / masses[i]);
            velocities[i] = velocities[i].normalized * newSpeed;

            Destroy(stars[j]);
        }

    }
}
