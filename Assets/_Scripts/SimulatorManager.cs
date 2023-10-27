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
    [SerializeField] private Vector2 massRange;
    [SerializeField] private Vector2 initialSpeedRange;
    [SerializeField] private GameObject starPrefab;

    [SerializeField] private int gridSubdivisions;
    private int cellCount;
    private Vector2 extremeX, extremeY;

    private GameObject[] stars;
    private Vector2[] velocities;
    private float[] masses;

    private float radius = 1;
    bool run = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        cellCount = gridSubdivisions * gridSubdivisions;
        stars = new GameObject[starCount];
        velocities = new Vector2[starCount];
        masses = new float[starCount];

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
        // Setup local caches
        Vector2[] forces = new Vector2[starCount];
        int[,] cellStars = new int[cellCount, starCount];
        int[] cellStarCount = new int[cellCount];
        int[] starCellHashes = new int[starCount];
        Vector2[] collisionPairs = new Vector2[starCount];  // Reset previous collisions
        int collisionCount = 0;

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                // Do not index destroyed stars
                continue;
            }

            Vector2 starLocation = stars[i].transform.position;
            Vector2 gridCoordinates = CalculateGridCoordinates(starLocation);
            int starCellHash = GetCellHash(gridCoordinates);
            starCellHashes[i] = starCellHash;
            cellStars[starCellHash, cellStarCount[starCellHash]] = i;
            cellStarCount[starCellHash] += 1;
        }

        // Calculate gravitational and reaction forces enacted on each planet
        for (int thisStarIndex = 0; thisStarIndex < stars.Length; thisStarIndex++)
        {
            if (stars[thisStarIndex] == null)
            {
                // In case star i was destroyed
                continue;
            }

            Vector2 thisStarPosition = stars[thisStarIndex].transform.position;
            float thisStarMass = masses[thisStarIndex];
            int starCellHash = starCellHashes[thisStarIndex];
            int?[] adjacentCellHashes = GetAdjacentCellHashes(starCellHash);

            int? collisionIndex = null;

            foreach (int? potentialCellHash in adjacentCellHashes)
            {
                if (potentialCellHash == null)
                {
                    // This neighbor does not exist
                    continue;
                }

                int cellHash = (int)potentialCellHash;

                for (int j = 0; j < cellStarCount[cellHash]; j++)
                {
                    int otherStarIndex = cellStars[cellHash, j];

                    if (thisStarIndex == otherStarIndex)
                    {
                        // Prevent star interacting with itself
                        continue;
                    }

                    Vector2 otherStarPosition = stars[otherStarIndex].transform.position;
                    float otherStarMass = masses[otherStarIndex];

                    Vector2 direction = otherStarPosition - thisStarPosition;
                    Vector2 directionNormalized = direction.normalized;

                    if (direction.magnitude <= radius)
                    {
                        collisionIndex = otherStarIndex;
                        break;
                    }

                    float numerator = gravitationalConstant * otherStarMass * thisStarMass;
                    float gravitationForceMagnitude = numerator / direction.sqrMagnitude;
                    Vector2 gravitationalForce = gravitationForceMagnitude * directionNormalized;

                    forces[thisStarIndex] += gravitationalForce;
                    forces[otherStarIndex] -= gravitationalForce;  // Newton's 3rd Law 
                }
            }

            if (collisionIndex != null)
            {
                collisionPairs[collisionCount] = new Vector2(thisStarIndex, (int)collisionIndex);
                collisionCount++;
                break;
            }
        }

        // Resolve collisions
        for (int i = 0; i < collisionCount; i++)
        {
            Vector2 collisionPair = collisionPairs[i];  // 2 Integers
            Collide((int)collisionPair.x, (int)collisionPair.y);
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

    private int GetCellHash(Vector2 gridCoordinates)
    {
        return GetCellHash((int)gridCoordinates.x, (int)gridCoordinates.y);
    }

    private int GetCellHash(int cellX, int cellY)
    {
        return cellX + cellY * gridSubdivisions;
    }

    private Vector2 CalculateGridCoordinates(Vector2 worldposition)
    {
        float universeWidth = extremeX.y - extremeX.x;
        float universeHeight = extremeY.y - extremeY.x;
        Vector2 universeSize = new Vector2(universeWidth, universeHeight);
        Vector2 cellSize = universeSize / gridSubdivisions;

        // Consider a new coordinate system, where the origin was moved
        Vector2 gridOrigin = new Vector2(extremeX.x, extremeY.x);
        Vector2 gridOriginPosition = worldposition - gridOrigin;

        int cellX = (int)(gridOriginPosition.x / cellSize.x);
        int cellY = (int)(gridOriginPosition.y / cellSize.y);

        // If given worldposition corresponds to an extreme value
        // then an incorrect value (gridSubdivisions) is returned
        cellX = Mathf.Clamp(cellX, 0, gridSubdivisions - 1);
        cellY = Mathf.Clamp(cellY, 0, gridSubdivisions - 1);

        return new Vector2(cellX, cellY);
    }

    private Vector2 GetCellGridCoordinates(int cellHash)
    {
        int cellY = Mathf.FloorToInt(cellHash / gridSubdivisions);
        int cellX = cellHash - cellY * gridSubdivisions;
        return new Vector2(cellX, cellY);
    }

    private int?[] GetAdjacentCellHashes(Vector2 gridCoordinates)
    {
        return GetAdjacentCellHashes(GetCellHash(gridCoordinates));
    }

    private int?[] GetAdjacentCellHashes(int cellHash)
    {
        const int MAX_NEIGHBORS = 9;
        // In a 2 dimensional grid, a cell can have up to 9 neighbors including itself
        // 0 1 2 
        // 3 4 5
        // 6 7 8
        int?[] neighbors = new int?[MAX_NEIGHBORS];
        Vector2 gridCoordinates = GetCellGridCoordinates(cellHash);
        int index = 0;

        // Shift through the 3 rows and columns
        for (int row = -1; row <= 1; row++)
        {

            for (int column = -1; column <= 1; column++)
            {
                Vector2 displacement = new Vector2(row, column);
                Vector2 neighborGridCoordinates = gridCoordinates + displacement;

                bool rowExists = neighborGridCoordinates.x >= 0 && neighborGridCoordinates.x < gridSubdivisions;
                bool columnExists = neighborGridCoordinates.y >= 0 && neighborGridCoordinates.y < gridSubdivisions;

                if (rowExists && columnExists)
                {
                    neighbors[index] = GetCellHash(neighborGridCoordinates);
                }

                index++;
            }
        }

        return neighbors;
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
        Destroy(stars[i]);
        Destroy(stars[j]);
    }
}
