using UnityEngine;
using System.Collections.Generic;

public class ChessBoardManager : MonoBehaviour
{
    [Header("References")]
    public GameObject boardParent; // drag your 3DChessBoard here

    [Header("Piece Prefabs - White")]
    public GameObject whitePawnPrefab;
    public GameObject whiteRookPrefab;
    public GameObject whiteQueenPrefab;
    public GameObject whiteKingPrefab;

    [Header("Piece Prefabs - Black")]
    public GameObject blackPawnPrefab;
    public GameObject blackRookPrefab;
    public GameObject blackQueenPrefab;
    public GameObject blackKingPrefab;

    [Header("Settings")]
    public float pieceHeightOffset = 0.6f;

    // Internal
    private BoardCell[,,] cells = new BoardCell[7, 7, 7];
    private List<BoardCell> frontFaceCells = new List<BoardCell>();
    private List<BoardCell> backFaceCells = new List<BoardCell>();

    private BoardCell selectedCell = null;
    private List<BoardCell> highlightedMoves = new List<BoardCell>();
    private PieceColor currentTurn = PieceColor.White;

    private Material highlightMaterial;
    private Dictionary<BoardCell, Material> originalMaterials = new Dictionary<BoardCell, Material>();

    void Start()
    {
        highlightMaterial = new Material(Shader.Find("Standard"));
        highlightMaterial.color = new Color(0f, 1f, 0.5f, 0.6f);

        CollectCells();
        SpawnPieces();
    }

    // ── CELL COLLECTION ──────────────────────────────────────────────
    void CollectCells()
    {
        foreach (Transform child in boardParent.transform)
        {
            BoardCell cell = child.GetComponent<BoardCell>();
            if (cell == null) continue;

            cells[cell.x, cell.y, cell.z] = cell;

            int boardSize = 6; // max index
            // Assign face label
            if (cell.z == boardSize) cell.face = "front";
            else if (cell.z == 0) cell.face = "back";
            else if (cell.x == 0) cell.face = "left";
            else if (cell.x == boardSize) cell.face = "right";
            else if (cell.y == boardSize) cell.face = "top";
            else if (cell.y == 0) cell.face = "bottom";

            if (cell.face == "front") frontFaceCells.Add(cell);
            if (cell.face == "back") backFaceCells.Add(cell);
        }
    }

    // ── PIECE SPAWNING ───────────────────────────────────────────────
    void SpawnPieces()
    {
        // WHITE on FRONT face (z=6), bottom two rows (y=0,1)
        SpawnRow(frontFaceCells, PieceColor.White, isBackRow: true, rowY: 1);
        SpawnRow(frontFaceCells, PieceColor.White, isBackRow: false, rowY: 0);

        // BLACK on BACK face (z=0), bottom two rows (y=0,1)
        SpawnRow(backFaceCells, PieceColor.Black, isBackRow: true, rowY: 1);
        SpawnRow(backFaceCells, PieceColor.Black, isBackRow: false, rowY: 0);
    }

    void SpawnRow(List<BoardCell> faceCells, PieceColor color, bool isBackRow, int rowY)
    {
        // Get cells in this row sorted by x
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        row.Sort((a, b) => a.x.CompareTo(b.x));

        if (isBackRow)
        {
            // Back row: Rook, empty, Queen, King, empty, empty, Rook
            PieceType[] layout = {
                PieceType.Rook, PieceType.Rook, PieceType.Queen,
                PieceType.King, PieceType.Rook, PieceType.Rook, PieceType.Rook
            };
            // Simplified: Rook, -, Queen, King, -, -, Rook
            PieceType?[] layout2 = {
                PieceType.Rook, null, PieceType.Queen,
                PieceType.King, null, null, PieceType.Rook
            };

            for (int i = 0; i < row.Count && i < layout2.Length; i++)
            {
                if (layout2[i].HasValue)
                    PlacePiece(layout2[i].Value, color, row[i]);
            }
        }
        else
        {
            // Pawn row
            foreach (BoardCell cell in row)
                PlacePiece(PieceType.Pawn, color, cell);
        }
    }

    void PlacePiece(PieceType type, PieceColor color, BoardCell cell)
    {
        GameObject prefab = GetPrefab(type, color);
        if (prefab == null) { Debug.LogWarning($"Missing prefab: {color} {type}"); return; }

        Vector3 spawnPos = cell.transform.position + cell.transform.up * pieceHeightOffset;
        GameObject pieceGO = Instantiate(prefab, spawnPos, Quaternion.identity);
        pieceGO.name = $"{color}_{type}";

        // Orient piece to stand on the face
        pieceGO.transform.up = GetFaceNormal(cell.face);

        ChessPiece piece = pieceGO.AddComponent<ChessPiece>();
        piece.pieceType = type;
        piece.pieceColor = color;
        piece.currentCell = cell;
        cell.currentPiece = pieceGO;
    }

    GameObject GetPrefab(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            return type switch
            {
                PieceType.Pawn => whitePawnPrefab,
                PieceType.Rook => whiteRookPrefab,
                PieceType.Queen => whiteQueenPrefab,
                PieceType.King => whiteKingPrefab,
                _ => whitePawnPrefab
            };
        }
        else
        {
            return type switch
            {
                PieceType.Pawn => blackPawnPrefab,
                PieceType.Rook => blackRookPrefab,
                PieceType.Queen => blackQueenPrefab,
                PieceType.King => blackKingPrefab,
                _ => blackPawnPrefab
            };
        }
    }

    Vector3 GetFaceNormal(string face)
    {
        return face switch
        {
            "front" => Vector3.forward,
            "back" => -Vector3.forward,
            "right" => Vector3.right,
            "left" => -Vector3.right,
            "top" => Vector3.up,
            "bottom" => -Vector3.up,
            _ => Vector3.up
        };
    }

    // ── INPUT & SELECTION ────────────────────────────────────────────
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        BoardCell clickedCell = hit.collider.GetComponent<BoardCell>();
        if (clickedCell == null)
        {
            // Maybe clicked the piece on top — find its cell
            ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
            if (piece != null) clickedCell = piece.currentCell;
        }
        if (clickedCell == null) return;

        // If a move is highlighted and we clicked it — move there
        if (highlightedMoves.Contains(clickedCell))
        {
            ExecuteMove(selectedCell, clickedCell);
            return;
        }

        // Select a piece
        ClearHighlights();
        selectedCell = null;

        if (clickedCell.IsOccupied)
        {
            ChessPiece piece = clickedCell.currentPiece.GetComponent<ChessPiece>();
            if (piece != null && piece.pieceColor == currentTurn)
            {
                selectedCell = clickedCell;
                HighlightMoves(clickedCell);
            }
        }
    }

    // ── MOVEMENT ─────────────────────────────────────────────────────
    void ExecuteMove(BoardCell from, BoardCell to)
    {
        ChessPiece movingPiece = from.currentPiece.GetComponent<ChessPiece>();

        // Capture
        if (to.IsOccupied)
        {
            ChessPiece captured = to.currentPiece.GetComponent<ChessPiece>();
            Debug.Log($"Captured {captured.pieceColor} {captured.pieceType}!");
            Destroy(to.currentPiece);
        }

        // Move
        Vector3 targetPos = to.transform.position + GetFaceNormal(to.face) * pieceHeightOffset;
        from.currentPiece.transform.position = targetPos;
        from.currentPiece.transform.up = GetFaceNormal(to.face);

        to.currentPiece = from.currentPiece;
        from.currentPiece = null;
        movingPiece.currentCell = to;
        movingPiece.hasMoved = true;

        ClearHighlights();
        selectedCell = null;

        // Switch turn
        currentTurn = currentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
        Debug.Log($"Turn: {currentTurn}");
    }

    // ── LEGAL MOVE CALCULATION ────────────────────────────────────────
    void HighlightMoves(BoardCell cell)
    {
        highlightedMoves = GetLegalMoves(cell);
        foreach (BoardCell move in highlightedMoves)
        {
            Renderer r = move.GetComponent<Renderer>();
            if (r != null)
            {
                originalMaterials[move] = r.material;
                r.material = highlightMaterial;
            }
        }
    }

    void ClearHighlights()
    {
        foreach (BoardCell cell in highlightedMoves)
        {
            Renderer r = cell.GetComponent<Renderer>();
            if (r != null && originalMaterials.ContainsKey(cell))
                r.material = originalMaterials[cell];
        }
        highlightedMoves.Clear();
        originalMaterials.Clear();
    }

    List<BoardCell> GetLegalMoves(BoardCell cell)
    {
        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
        List<BoardCell> moves = new List<BoardCell>();
        string face = cell.face;

        switch (piece.pieceType)
        {
            case PieceType.Pawn: moves = GetPawnMoves(cell, piece); break;
            case PieceType.Rook: moves = GetSlidingMoves(cell, true, false); break;
            case PieceType.Bishop: moves = GetSlidingMoves(cell, false, true); break;
            case PieceType.Queen: moves = GetSlidingMoves(cell, true, true); break;
            case PieceType.King: moves = GetKingMoves(cell, piece); break;
            case PieceType.Knight: moves = GetKnightMoves(cell); break;
        }

        // Remove moves that would capture own pieces
        moves.RemoveAll(m => m.IsOccupied &&
            m.currentPiece.GetComponent<ChessPiece>().pieceColor == piece.pieceColor);

        return moves;
    }

    // Pawns move forward along Y on their face
    List<BoardCell> GetPawnMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();
        int dir = piece.pieceColor == PieceColor.White ? 1 : -1;

        // Forward
        BoardCell fwd = GetCellOnFace(cell.face, cell.x, cell.y + dir, cell.z);
        if (fwd != null && !fwd.IsOccupied) moves.Add(fwd);

        // Double move from start
        if (!piece.hasMoved && fwd != null && !fwd.IsOccupied)
        {
            BoardCell dbl = GetCellOnFace(cell.face, cell.x, cell.y + dir * 2, cell.z);
            if (dbl != null && !dbl.IsOccupied) moves.Add(dbl);
        }

        // Diagonal captures
        foreach (int dx in new[] { -1, 1 })
        {
            BoardCell diag = GetCellOnFace(cell.face, cell.x + dx, cell.y + dir, cell.z);
            if (diag != null && diag.IsOccupied &&
                diag.currentPiece.GetComponent<ChessPiece>().pieceColor != piece.pieceColor)
                moves.Add(diag);
        }
        return moves;
    }

    List<BoardCell> GetKingMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                BoardCell c = GetCellOnFace(cell.face, cell.x + dx, cell.y + dy, cell.z);
                if (c != null) moves.Add(c);
            }
        return moves;
    }

    List<BoardCell> GetKnightMoves(BoardCell cell)
    {
        List<BoardCell> moves = new List<BoardCell>();
        int[,] offsets = { { 1, 2 }, { 2, 1 }, { -1, 2 }, { -2, 1 }, { 1, -2 }, { 2, -1 }, { -1, -2 }, { -2, -1 } };
        for (int i = 0; i < 8; i++)
        {
            BoardCell c = GetCellOnFace(cell.face,
                cell.x + offsets[i, 0], cell.y + offsets[i, 1], cell.z);
            if (c != null) moves.Add(c);
        }
        return moves;
    }

    List<BoardCell> GetSlidingMoves(BoardCell cell, bool straight, bool diagonal)
    {
        List<BoardCell> moves = new List<BoardCell>();
        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();

        List<Vector2Int> directions = new List<Vector2Int>();
        if (straight) directions.AddRange(new[]{ new Vector2Int(1,0), new Vector2Int(-1,0),
                                                   new Vector2Int(0,1), new Vector2Int(0,-1) });
        if (diagonal) directions.AddRange(new[]{ new Vector2Int(1,1), new Vector2Int(-1,1),
                                                   new Vector2Int(1,-1), new Vector2Int(-1,-1) });

        foreach (Vector2Int dir in directions)
        {
            int cx = cell.x, cy = cell.y;
            for (int step = 1; step < 7; step++)
            {
                cx += dir.x; cy += dir.y;
                BoardCell next = GetCellOnFace(cell.face, cx, cy, cell.z);
                if (next == null) break;
                if (next.IsOccupied)
                {
                    if (next.currentPiece.GetComponent<ChessPiece>().pieceColor != piece.pieceColor)
                        moves.Add(next); // capture
                    break;
                }
                moves.Add(next);
            }
        }
        return moves;
    }

    // ── CELL LOOKUP ──────────────────────────────────────────────────
    BoardCell GetCellOnFace(string face, int x, int y, int z)
    {
        // Map face + 2D coords back to 3D grid index
        int gx, gy, gz;
        int max = 6;

        switch (face)
        {
            case "front": gx = x; gy = y; gz = max; break;
            case "back": gx = x; gy = y; gz = 0; break;
            case "right": gx = max; gy = y; gz = z; break;
            case "left": gx = 0; gy = y; gz = z; break;
            case "top": gx = x; gy = max; gz = z; break;
            case "bottom": gx = x; gy = 0; gz = z; break;
            default: return null;
        }

        if (gx < 0 || gx > max || gy < 0 || gy > max || gz < 0 || gz > max) return null;
        return cells[gx, gy, gz];
    }
}