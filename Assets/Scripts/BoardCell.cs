using UnityEngine;

public class BoardCell : MonoBehaviour
{
    public int x;
    public int y;
    public int z;

    public GameObject currentPiece;   // IMPORTANT

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.25f);
    }
}