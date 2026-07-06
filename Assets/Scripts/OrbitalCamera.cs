using System.Collections;
using UnityEngine;

public class OrbitalCamera : MonoBehaviour
{
    [Header("Targets & Limits")]
    public Transform target;         // Drag 3DChessBoard here
    public float distance = 8f;
    public float minDistance = 4f;
    public float maxDistance = 15f;

    [Header("Speeds")]
    public float rotateSpeed = 0.4f;
    public float zoomSpeed = 4f;
    public float autoMoveSpeed = 4f;

    private float currentYaw = 180f; // Defaulting to 180 to face White on awake
    private float currentPitch = 25f;
    private bool isDragging = false;
    private Vector3 lastMousePos;

    private Coroutine autoMoveRoutine;

    void LateUpdate()
    {
        // Right-click or middle-click to manual orbit
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            lastMousePos = Input.mousePosition;

            // Interrupt auto-movement if the player manually takes control
            if (autoMoveRoutine != null) StopCoroutine(autoMoveRoutine);
        }

        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }

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

        // Apply calculated rotation and translation position
        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        transform.position = target.position + rot * new Vector3(0, 0, -distance);
        transform.LookAt(target.position);
    }

    // Called externally by ChessBoardManager to shift perspectives automatically
    public void SnapToFace(string face)
    {
        float targetYaw = currentYaw;
        float targetPitch = currentPitch;

        // Adjusted to match your actual 3D environment layout orientation
        switch (face.ToLower())
        {
            case "front": targetYaw = 180f; targetPitch = 25f; break; // View White's true front side
            case "back": targetYaw = 0f; targetPitch = 25f; break; // View Black's side
            case "left": targetYaw = 90f; targetPitch = 25f; break;
            case "right": targetYaw = -90f; targetPitch = 25f; break;
            case "top": targetYaw = currentYaw; targetPitch = 85f; break;
            case "bottom": targetYaw = currentYaw; targetPitch = -85f; break;
        }

        if (autoMoveRoutine != null) StopCoroutine(autoMoveRoutine);
        autoMoveRoutine = StartCoroutine(SmoothTransition(targetYaw, targetPitch));
    }

    public void ResetToWhite()
    {
        // Explicitly set the initial state to look at the White pieces from an isometric angle
        currentYaw = 180f;
        currentPitch = 25f;

        if (autoMoveRoutine != null) StopCoroutine(autoMoveRoutine);

        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        transform.position = target.position + rot * new Vector3(0, 0, -distance);
        transform.LookAt(target.position);
    }

    private IEnumerator SmoothTransition(float targetYaw, float targetPitch)
    {
        float t = 0f;
        float startYaw = currentYaw;
        float startPitch = currentPitch;

        // Handle spherical angle wrapping smoothly (prevent spinning 360 degrees the wrong way)
        float shortYaw = startYaw + Mathf.DeltaAngle(startYaw, targetYaw);

        while (t < 1f)
        {
            t += Time.deltaTime * autoMoveSpeed;
            currentYaw = Mathf.Lerp(startYaw, shortYaw, t);
            currentPitch = Mathf.Lerp(startPitch, targetPitch, t);
            yield return null;
        }

        currentYaw = targetYaw;
        currentPitch = targetPitch;
    }
}