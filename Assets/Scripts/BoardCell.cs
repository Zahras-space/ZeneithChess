using UnityEngine;

public class BoardCell : MonoBehaviour
{
    public int x, y, z;
    public GameObject currentPiece;
    public string face; // "front","back","left","right","top","bottom"

    public bool IsOccupied => currentPiece != null;

    private void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? Color.yellow : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.25f);
    }
}