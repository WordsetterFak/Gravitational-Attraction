using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class StarVisuals : MonoBehaviour
{
    [SerializeField] private Color[] starColors;

    private void Awake()
    {
        Color starColor = starColors[Random.Range(0, starColors.Length)];
        GetComponent<Light2D>().color = starColor;
    }
}
