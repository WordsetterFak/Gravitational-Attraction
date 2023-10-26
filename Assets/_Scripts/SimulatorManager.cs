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

    private int[,] grid;
    private GameObject[] stars;
    private Vector2[] forces;
    private Vector2[] velocities;
    private float[] masses;
    private Vector2[] collisionPairs;

    private float radius = 1;

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
        }
    }

    private void SimulationPhysicsStep()
    {
        forces = new Vector2[starCount];  // Reset previous forces

        collisionPairs = new Vector2[starCount];  // Reset previous collisions
        int collisionCount = 0;

        // Calculate gravitational and reaction forces enacted on each planet
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                // In case star i was destroyed
                continue;
            }

            int? collisionIndex = null;

            for (int j = 0; j < stars.Length; j++)
            {
                if (i == j || stars[j] == null)
                {
                    // Prevent star interacting with itself
                    continue;
                }

                Vector2 direction = stars[j].transform.position - stars[i].transform.position;
                Vector2 directionNormalized = direction.normalized;

                if (direction.magnitude <= radius)
                {
                    collisionIndex = j;
                    break;
                }

                float gravitationForceMagnitude = (gravitationalConstant * masses[i] * masses[j]) / direction.sqrMagnitude;
                Vector2 gravitationalForce = gravitationForceMagnitude * directionNormalized;

                forces[i] += gravitationalForce;
                forces[j] += -gravitationalForce;  // Newton's 3rd Law 
            }


            if (collisionIndex != null)
            {
                collisionPairs[collisionCount] = new Vector2(i, (int)collisionIndex);
                collisionCount++;
                break;
            }
        }

        // Resolve collisions
        for (int i = 0; i < collisionPairs.Length; i++)
        {
            Vector2 collisionPair = collisionPairs[i];  // 2 Integers

            if (collisionPair == Vector2.zero)
            {
                // Reaching a (0, 0) pair means that there are no more collisions
                break;
            }

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
