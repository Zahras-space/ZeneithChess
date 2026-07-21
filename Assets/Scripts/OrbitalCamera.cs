using System.Collections;
using UnityEngine;

public class OrbitalCamera : MonoBehaviour
{
    [Header("Targets & Limits")]
    public Transform target;         // Drag 3DChessBoard here
    public float distance = 18f;
    public float minDistance = 18f;
    public float maxDistance = 18f;

    [Header("Speeds")]
    public float rotateSpeed = 0.4f;
    public float zoomSpeed = 4f;
    public float autoMoveSpeed = 4f;

    public float currentYaw = 180f; // Defaulting to 180 to face White on awake
    public float currentPitch = 70f;
    private bool isDragging = false;
    private Vector3 lastMousePos;

    private Coroutine autoMoveRoutine;

    void Start()
    {
        if (target != null)
        {
            distance = Vector3.Distance(transform.position, target.position);
            minDistance = distance;
            maxDistance = distance;

            Vector3 direction = (transform.position - target.position).normalized;
            currentPitch = Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            currentYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + 180f;
        }
    }

    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }

        if (isDragging && target != null)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            currentYaw += delta.x * rotateSpeed;
            currentPitch -= delta.y * rotateSpeed;
            currentPitch = Mathf.Clamp(currentPitch, -80f, 80f);
            lastMousePos = Input.mousePosition;
        }

        if (target != null)
        {
            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
            transform.position = target.position + rot * new Vector3(0, 0, -distance);
            transform.LookAt(target.position);
        }
    }

    // Called externally by ChessBoardManager to shift perspectives automatically
    public void SnapToFace(string face)
    {
        // Disabled: keep the camera at the editor-configured position and rotation.
    }

    public void ResetToWhite()
    {
        // Disabled: preserve the current camera transform set in the editor.
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