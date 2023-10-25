using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private int starCount;
    [SerializeField] private float maxSpawnRange;
    [SerializeField] private float cameraSizeOffset;
    [SerializeField] private float gravitationalConstant;
    [SerializeField] private Vector2 massRange;
    [SerializeField] private GameObject starPrefab;

    private GameObject[] stars;
    private Vector2[] velocities;
    private float[] masses;

    private float radius = 1;

    private void Start()
    {
        stars = new GameObject[starCount];
        velocities = new Vector2[starCount];
        masses = new float[starCount];

        for(int i = 0; i < starCount; i++)
        {
            Vector2 initialPosition = maxSpawnRange * Random.insideUnitCircle;
            bool overlap = false;

            for(int j = 0; j < i; j++)
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
            velocities[i] = Vector2.zero;
            masses[i] = Random.Range(massRange.x, massRange.y);
        }

        Camera.main.orthographicSize = maxSpawnRange + cameraSizeOffset;
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                continue;
            }

            Vector2 acceleration = Vector2.zero;
            bool collision = false;

            for (int j = 0; j < stars.Length; j++)
            {
                if (i == j || stars[j] == null)
                {
                    continue;
                }

                Vector2 direction = stars[j].transform.position - stars[i].transform.position;
                Vector2 directionNormalized = direction.normalized;

                if(direction.magnitude <= radius)
                {
                    Destroy(stars[j]);
                    collision = true;
                    break;
                }

                float squareDistance = direction.magnitude * direction.magnitude;
                float magnitude = (gravitationalConstant * masses[i] * masses[j]) / squareDistance;
                acceleration += magnitude * directionNormalized;
            }

            if (collision)
            {
                Destroy(stars[i]);
                break;
            }

            velocities[i] += acceleration * Time.fixedDeltaTime;
            stars[i].transform.position += (Vector3) velocities[i] * Time.fixedDeltaTime;
        }
    }
}
