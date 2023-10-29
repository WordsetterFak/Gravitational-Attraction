using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    public static Simulation Instance { get; private set; }

    [SerializeField] private int starCount;
    [SerializeField] private float maxSpawnRange;
    [SerializeField] private float despawnDistance;
    [SerializeField] private float cameraSizeOffset;
    [SerializeField] private float gravitationalConstant;
    [SerializeField] private float gravitationalConstantIncreasePerSecond;
    [SerializeField] private float maxGravitationConstant;
    [SerializeField] private Vector2 massRange;
    [SerializeField] private Vector2 initialSpeedRange;
    [SerializeField] private GameObject starPrefab;
    [SerializeField] private float distanceStretch;

    [SerializeField] private int gridSubdivisions;
    private int cellCount;
    private Vector2 extremeX, extremeY;

    private GameObject[] stars;
    private Vector2[] velocities;
    private float[] masses;

    private float despawnDistanceSquared;
    private float distanceStretchSquared;
    private Vector2 universeSize;

    private float radius = 1;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        despawnDistanceSquared = despawnDistance * despawnDistance;
        distanceStretchSquared = distanceStretch * distanceStretch;
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

            GameObject newStar = Instantiate(starPrefab);
            newStar.transform.position = initialPosition;
            stars[i] = newStar;
            float initialSpeed = Random.Range(initialSpeedRange.x, initialSpeedRange.y);
            velocities[i] = initialSpeed * Random.insideUnitCircle.normalized;
            masses[i] = Random.Range(massRange.x, massRange.y);

            newStar.GetComponent<StarVisuals>().AssignColor(masses[i]);
            UpdateExtremePositions(initialPosition);
        }
    }

    private void SimulationPhysicsStep()
    {
        // Initialize new grid
        float universeWidth = extremeX.y - extremeX.x;
        float universeHeight = extremeY.y - extremeY.x;
        universeSize = new Vector2(universeWidth, universeHeight);
        Vector2 gridOrigin = new Vector2(extremeX.x, extremeY.x);
        SpaceGrid grid = new SpaceGrid(universeSize, gridOrigin, gridSubdivisions, starCount);

        // Setup local caches
        Vector2[] forces = new Vector2[starCount];
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
            float distanceFromOriginSquared = starLocation.sqrMagnitude;

            if (distanceFromOriginSquared > despawnDistanceSquared)
            {
                Destroy(stars[i]);
                continue;
            }

            grid.AddStarToGrid(i);
        }

        // Calculate gravitational and reaction forces enacted on each planet
        for (int thisStarIndex = 0; thisStarIndex < stars.Length; thisStarIndex++)
        {
            if (stars[thisStarIndex] == null)
            {
                // In case star i was destroyed
                continue;
            }

            Vector2 thisStarPosition = GetStarLocation(thisStarIndex);
            float thisStarMass = masses[thisStarIndex];
            int?[] adjacentCellHashes = grid.GetAdjacentCellHashesFromStar(thisStarIndex);

            int? collisionIndex = null;

            foreach (int? potentialCellHash in adjacentCellHashes)
            {
                if (potentialCellHash == null)
                {
                    // This neighbor does not exist
                    continue;
                }

                int cellHash = (int)potentialCellHash;

                for (int j = 0; j < grid.GetCellStarCount(cellHash); j++)
                {
                    int otherStarIndex = grid.GetCellStar(cellHash, j);

                    if (thisStarIndex == otherStarIndex)
                    {
                        // Prevent star interacting with itself
                        continue;
                    }

                    Vector2 otherStarPosition = GetStarLocation(thisStarIndex);
                    float otherStarMass = masses[otherStarIndex];

                    Vector2 direction = otherStarPosition - thisStarPosition;
                    Vector2 directionNormalized = direction.normalized;

                    if (direction.magnitude <= radius)
                    {
                        collisionIndex = otherStarIndex;
                        break;
                    }

                    float numerator = gravitationalConstant * otherStarMass * thisStarMass;
                    float denominator = direction.sqrMagnitude * distanceStretchSquared;
                    float gravitationForceMagnitude = numerator / denominator;
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

    public Vector2 GetStarLocation(int index)
    {
        return stars[index].transform.position;
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

    public float GetMassRange()
    {
        return massRange.y - massRange.x;
    }
}
