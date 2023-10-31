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
        UpdateUniverseSize();
        SimulationPhysicsStep();
        UpdateGravitationalConstant();
    }

    public float GetStarMass(int index)
    {
        return masses[index];
    }

    public Vector2 GetStarPosition(int index)
    {
        return stars[index].transform.position;
    }

    public Vector2 GetStarVelocity(int index)
    {
        return velocities[index];
    }

    private void SetStarPosition(int index, Vector2 position)
    {
        stars[index].transform.position = position;
        UpdateExtremePositions(position);
    }

    private void SetStarMass(int index, float mass)
    {
        masses[index] = mass;
    }

    private void SetStarVelocity(int index, Vector2 velocity)
    {
        velocities[index] = velocity;
    }

    private void SpawnStars()
    {
        for (int i = 0; i < starCount; i++)
        {
            Vector2 initialPosition = maxSpawnRange * Random.insideUnitCircle;
            bool overlap = false;

            for (int j = 0; j < i; j++)
            {
                if ((initialPosition - GetStarPosition(j)).magnitude <= radius)
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
            stars[i] = newStar;
            SetStarPosition(i, initialPosition);

            float initialSpeed = Random.Range(initialSpeedRange.x, initialSpeedRange.y);
            SetStarVelocity(i, initialSpeed * Random.insideUnitCircle.normalized);

            SetStarMass(i, Random.Range(massRange.x, massRange.y));

            newStar.GetComponent<StarVisuals>().AssignColor(masses[i]);
        }
    }

    private void SimulationPhysicsStep()
    {
        // Initialize new grid
        Vector2 gridOrigin = CalculateGridOrigin();
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

            float distanceFromOriginSquared = GetStarPosition(i).sqrMagnitude;

            if (distanceFromOriginSquared > despawnDistanceSquared)
            {
                DestroyStar(i);
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

            Vector2 thisStarPosition = GetStarPosition(thisStarIndex);
            float thisStarMass = GetStarMass(thisStarIndex);
            int?[] adjacentCellHashes = grid.GetAdjacentCellHashesFromStar(thisStarIndex);

            int? collisionIndex = null;

            foreach (int? potentialCellHash in adjacentCellHashes)
            {
                // Iterate over every neighboring cell

                if (potentialCellHash == null)
                {
                    // This neighbor does not exist (outside the grid)
                    continue;
                }

                int cellHash = (int)potentialCellHash;

                for (int j = 0; j < grid.GetCellStarCount(cellHash); j++)
                {
                    // Iterate over all stars in given neighbor cell
                    int otherStarIndex = grid.GetCellStar(cellHash, j);

                    if (thisStarIndex == otherStarIndex)
                    {
                        // Prevent star interacting with itself
                        continue;
                    }
                    
                    Vector2 otherStarPosition = GetStarPosition(otherStarIndex);
                    float otherStarMass = GetStarMass(otherStarIndex);

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

            Vector2 acceleration = forces[i] / GetStarMass(i);
            Vector2 velocity = GetStarVelocity(i) + acceleration * Time.fixedDeltaTime;
            Vector2 position = GetStarPosition(i) + velocity * Time.fixedDeltaTime;
            
            SetStarVelocity(i, velocity);
            SetStarPosition(i, position);
        }

    }

    private void UpdateUniverseSize()
    {
        float universeWidth = extremeX.y - extremeX.x;
        float universeHeight = extremeY.y - extremeY.x;
        universeSize = new Vector2(universeWidth, universeHeight);
    }

    private void UpdateGravitationalConstant()
    {
        float gravitationalConstantIncrease = gravitationalConstantIncreasePerSecond * Time.fixedDeltaTime;
        float newGravitationalConstant = gravitationalConstant + gravitationalConstantIncrease;
        gravitationalConstant = Mathf.Clamp(newGravitationalConstant, -maxGravitationConstant, maxGravitationConstant);
    }

    private Vector2 CalculateGridOrigin()
    {
        return new Vector2(extremeX.x, extremeY.x);
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
        DestroyStar(i);
        DestroyStar(j);
    }

    private void DestroyStar(int index)
    {
        Destroy(stars[index]);
    }

    public float GetMassRange()
    {
        return massRange.y - massRange.x;
    }
}
