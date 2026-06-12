using UnityEngine;
using UnityEditor;

public class ChessBoard3DGenerator
{
    public static Material whiteMaterial;
    public static Material blackMaterial;

    [MenuItem("Tools/Generate 3D Chess Board")]
    static void GenerateBoard()
    {
        int boardSize = 7;
        float spacing = 0.9f;

        // 🔹 Reuse or create parent
        GameObject parent = GameObject.Find("3DChessBoard");

        if (parent == null)
        {
            parent = new GameObject("3DChessBoard");
        }
        else
        {
            // Clear previous board
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
            }
        }

        float offset = (boardSize - 1) * spacing * 0.5f;

        // Load materials (place in Resources folder)
        whiteMaterial = Resources.Load<Material>("WhiteMaterial");
        blackMaterial = Resources.Load<Material>("BlackMaterial");

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                for (int z = 0; z < boardSize; z++)
                {
                    GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);

                    cell.name = $"Cell_{x}_{y}_{z}";
                    cell.transform.SetParent(parent.transform);

                    cell.transform.localPosition = new Vector3(
                        x * spacing - offset,
                        y * spacing - offset,
                        z * spacing - offset
                    );

                    cell.transform.localScale = Vector3.one * 0.95f;

                    Renderer renderer = cell.GetComponent<Renderer>();

                    bool isOuter =
                        x == 0 || x == boardSize - 1 ||
                        y == 0 || y == boardSize - 1 ||
                        z == 0 || z == boardSize - 1;

                    if (!isOuter)
                    {
                        renderer.enabled = false;
                    }
                    else
                    {
                        Material mat = whiteMaterial;

                        // FRONT / BACK
                        if (z == boardSize - 1 || z == 0)
                            mat = ((x + y) % 2 == 0) ? whiteMaterial : blackMaterial;

                        // LEFT / RIGHT
                        else if (x == 0 || x == boardSize - 1)
                            mat = ((z + y) % 2 == 0) ? whiteMaterial : blackMaterial;

                        // TOP / BOTTOM
                        else if (y == boardSize - 1 || y == 0)
                            mat = ((x + z) % 2 == 0) ? whiteMaterial : blackMaterial;

                        renderer.sharedMaterial = mat;
                    }

                    // Optional component
                    cell.AddComponent<BoardCell>();
                }
            }
        }

        Selection.activeGameObject = parent;

        Debug.Log("3D Chess Cube Generated Successfully");
    }
}