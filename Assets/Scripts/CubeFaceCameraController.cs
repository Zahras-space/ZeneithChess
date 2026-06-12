using UnityEngine;
using System.Collections;

public class CubeFaceCameraController : MonoBehaviour
{
    public Transform cubeCenter;

    public float distance = 12f;
    public float moveSpeed = 3f;

    private Coroutine moveRoutine;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 normal = hit.normal;

                Vector3 targetPosition =
                    cubeCenter.position + normal * distance;

                Quaternion targetRotation =
                    Quaternion.LookRotation(cubeCenter.position - targetPosition);

                if (moveRoutine != null)
                    StopCoroutine(moveRoutine);

                moveRoutine = StartCoroutine(
                    MoveCamera(targetPosition, targetRotation)
                );
            }
        }
    }

    IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;

            transform.position =
                Vector3.Lerp(startPos, targetPos, t);

            transform.rotation =
                Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }
    }
}