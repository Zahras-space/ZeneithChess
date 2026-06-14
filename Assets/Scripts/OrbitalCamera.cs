using UnityEngine;

public class OrbitalCamera : MonoBehaviour
{
    public Transform target;         // drag 3DChessBoard here
    public float distance = 8f;
    public float rotateSpeed = 0.4f;
    public float zoomSpeed = 2f;
    public float minDistance = 4f;
    public float maxDistance = 15f;

    private float currentYaw = 0f;
    private float currentPitch = 20f;
    private bool isDragging = false;
    private Vector3 lastMousePos;

    void LateUpdate()
    {
        // Right-click or middle-click to orbit
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        { isDragging = true; lastMousePos = Input.mousePosition; }
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            isDragging = false;

        if (isDragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            currentYaw += delta.x * rotateSpeed;
            currentPitch -= delta.y * rotateSpeed;
            currentPitch = Mathf.Clamp(currentPitch, -80f, 80f);
            lastMousePos = Input.mousePosition;
        }

        // Scroll to zoom
        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // Apply
        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        transform.position = target.position + rot * new Vector3(0, 0, -distance);
        transform.LookAt(target.position);
    }
}