using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationCamera : MonoBehaviour
{
    public static SimulationCamera Instance { get; private set; }

    [SerializeField] private float cameraMovementSpeed;
    [SerializeField] [Range(0, .5f)] private float distanceNormalizedMoveTrigger;
    [SerializeField] private Vector2 zoomRange;
    [SerializeField] private float zoomChangeSensitivity;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        Vector3 screenCursorPosition = Input.mousePosition + Camera.main.orthographicSize * Vector3.forward;
        Vector3 viewportCursorPosition = Camera.main.ScreenToViewportPoint(screenCursorPosition);
        // Vector3 worldCursorPosition = Camera.main.ScreenToWorldPoint(screenCursorPosition);

        MoveCamera(viewportCursorPosition);

        if (Input.GetKey(KeyCode.Z))
        {
            ZoomIn();
        }

        if (Input.GetKey(KeyCode.X))
        {
            ZoomOut();
        }

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

    private void ZoomIn()
    {
        ChangeCameraZoom(true);
    }

    private void ZoomOut()
    {
        ChangeCameraZoom(false);
    }

    private void ChangeCameraZoom(bool zoomIn)
    {
        int sign = zoomIn ? -1 : 1;
        float zoomChange = sign * Time.deltaTime * zoomChangeSensitivity;
        float newZoom = Camera.main.orthographicSize + zoomChange;
        float newZoomClamped = Mathf.Clamp(newZoom, zoomRange.x, zoomRange.y);
        Camera.main.orthographicSize = newZoomClamped;
    }

}
