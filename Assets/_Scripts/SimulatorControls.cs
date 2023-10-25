using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulatorControls : MonoBehaviour
{
    [SerializeField] private float cameraMovementSpeed;
    [SerializeField] [Range(0, .5f)] private float distanceNormalizedMoveTrigger;

    private void Update()
    {
        Vector3 screenCursorPosition = Input.mousePosition + Camera.main.orthographicSize * Vector3.forward;
        Vector3 viewportCursorPosition = Camera.main.ScreenToViewportPoint(screenCursorPosition);
        // Vector3 worldCursorPosition = Camera.main.ScreenToWorldPoint(screenCursorPosition);

        MoveCamera(viewportCursorPosition);
    }

    private void MoveCamera(Vector3 viewportCursorPosition)
    {
        Vector2 viewportCenter = new Vector3(.5f, .5f);

        if (((Vector2)viewportCursorPosition - viewportCenter).magnitude < distanceNormalizedMoveTrigger)
        {
            return;
        }

        if (!Camera.main.rect.Contains(viewportCursorPosition))
        {
            return;
        }

        Vector2 direction = (Vector2)viewportCursorPosition - Camera.main.rect.center;
        Vector2 directionNormalized = direction.normalized;

        float moveMagnitude = cameraMovementSpeed * Time.deltaTime;
        Camera.main.transform.position += (Vector3)(moveMagnitude * directionNormalized);
    }

}
