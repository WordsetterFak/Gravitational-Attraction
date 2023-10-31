using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class StarVisuals : MonoBehaviour
{
    [SerializeField] private Color[] starColors;
    [SerializeField] private Material originalMaterial;

    private Light2D lightShader;

    private void Awake()
    {
        lightShader = GetComponent<Light2D>();
    }

    public void AssignColor(float mass)
    {
        float massRange = Simulation.Instance.GetMassRange();
        float sectionLength = massRange / starColors.Length;
        int index = (int)(mass / sectionLength);
        index = (int)Mathf.Clamp(index, 0, starColors.Length - 1);

        Color color = starColors[index];
        Material material = new Material(originalMaterial);
        material.color = color;
        GetComponent<Renderer>().material = material;
        lightShader.color = color;

    }

    public void AssignRadius(float radius)
    {
        transform.localScale *= radius;
        lightShader.pointLightInnerRadius *= radius;
        lightShader.pointLightOuterRadius *= radius;
        lightShader.intensity *= radius;
    }
}
