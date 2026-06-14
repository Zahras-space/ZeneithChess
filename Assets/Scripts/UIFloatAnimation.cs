using UnityEngine;
using UnityEngine.UI;

public class UIFloatAnimation : MonoBehaviour
{
    [Header("Floating")]
    public bool enableFloat = true;
    public float floatHeight = 20f;      // how high it moves up/down in pixels
    public float floatSpeed = 1.5f;      // how fast it floats

    [Header("Throbbing (Scale Pulse)")]
    public bool enableThrob = true;
    public float throbAmount = 0.08f;    // how much it scales up (0.08 = 8%)
    public float throbSpeed = 2f;        // how fast it pulses

    [Header("Rotation (Optional)")]
    public bool enableRotation = false;
    public float rotationSpeed = 30f;    // degrees per second

    private Vector3 startPosition;
    private Vector3 startScale;

    void Start()
    {
        startPosition = transform.localPosition;
        startScale = transform.localScale;
    }

    void Update()
    {
        // Floating up and down
        if (enableFloat)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
            transform.localPosition = new Vector3(startPosition.x, newY, startPosition.z);
        }

        // Throbbing scale pulse
        if (enableThrob)
        {
            float pulse = 1f + Mathf.Sin(Time.time * throbSpeed) * throbAmount;
            transform.localScale = startScale * pulse;
        }

        // Slow rotation (good for a 3D cube image)
        if (enableRotation)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}