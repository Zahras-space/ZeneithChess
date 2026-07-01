using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardManager : MonoBehaviour
{

    [Header("Audio")]
    public AudioSource clickSound;   
    public AudioSource moveSound;   

    [Header("References")]
    public GameObject boardParent; 

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
    public float pieceHeightOffset = 0.55f; 
    private BoardCell[,,] cells = new BoardCell[7, 7, 7];
    private List<BoardCell> frontFaceCells = new List<BoardCell>();
    private List<BoardCell> backFaceCells  = new List<BoardCell>();

    private BoardCell selectedCell = null;
    private List<BoardCell> highlightedMoves = new List<BoardCell>();
    private PieceColor currentTurn = PieceColor.White;

    private Material highlightMaterial;
    private Dictionary<BoardCell, Material> originalMaterials = new Dictionary<BoardCell, Material>();

    private bool gameOver = false;
    private int turnCount = 0;
    private int coreShiftInterval = 4; // shifts every 4 turns
    private bool lowGravityMode = false;
    public bool aiEnabled = true;
    private bool aiThinking = false;

    void Start()
    {
        highlightMaterial = new Material(Shader.Find("Standard"));
        highlightMaterial.color = new Color(0f, 1f, 0.5f, 0.6f);

        CollectCells();
        SpawnPieces();
    }


    void CollectCells()
    {
        foreach (Transform child in boardParent.transform)
        {
            BoardCell cell = child.GetComponent<BoardCell>();
            if (cell == null) continue;

            cells[cell.x, cell.y, cell.z] = cell;

            int boardSize = 6; 
            if      (cell.z == boardSize) cell.face = "front";
            else if (cell.z == 0)         cell.face = "back";
            else if (cell.x == 0)         cell.face = "left";
            else if (cell.x == boardSize) cell.face = "right";
            else if (cell.y == boardSize) cell.face = "top";
            else if (cell.y == 0)         cell.face = "bottom";

            if (cell.face == "front") frontFaceCells.Add(cell);
            if (cell.face == "back")  backFaceCells.Add(cell);
        }
    }
    void SpawnPieces()
    {
{
    
    SpawnBackRow(frontFaceCells, PieceColor.White, rowY: 0);
    SpawnPawnRow(frontFaceCells, PieceColor.White, rowY: 1);

    // BLACK on BACK face — mirrored: back row at y=6, pawns at y=5
    SpawnBackRow(backFaceCells, PieceColor.Black, rowY: 6);
    SpawnPawnRow(backFaceCells, PieceColor.Black, rowY: 5);


Debug.Log($"Back row for White: {frontFaceCells.FindAll(c => c.y == 0).Count} cells at y=0");
Debug.Log($"Pawn row for White: {frontFaceCells.FindAll(c => c.y == 1).Count} cells at y=1");
Debug.Log($"Back row for Black: {backFaceCells.FindAll(c => c.y == 6).Count} cells at y=6");
Debug.Log($"Pawn row for Black: {backFaceCells.FindAll(c => c.y == 5).Count} cells at y=5");}
    }
    void SpawnBackRow(List<BoardCell> faceCells, PieceColor color, int rowY)
    {
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        row.Sort((a, b) => a.x.CompareTo(b.x));
        PieceType?[] layout = {
            PieceType.Rook, null, PieceType.Queen,
            PieceType.King, null, null, PieceType.Rook
        };

        for (int i = 0; i < row.Count && i < layout.Length; i++)
        {
            if (layout[i].HasValue)
                PlacePiece(layout[i].Value, color, row[i]);
        }
    }

    void SpawnPawnRow(List<BoardCell> faceCells, PieceColor color, int rowY)
    {
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        foreach (BoardCell cell in row)
            PlacePiece(PieceType.Pawn, color, cell);
    }

   
    void PlacePiece(PieceType type, PieceColor color, BoardCell cell)
    {
        GameObject prefab = GetPrefab(type, color);
        if (prefab == null)
        {
            Debug.LogWarning($"Missing prefab: {color} {type}");
            return;
        }

        Vector3 faceNormal = GetFaceNormal(cell.face);

        Vector3 spawnPos = cell.transform.position + faceNormal * pieceHeightOffset;

        GameObject pieceGO = Instantiate(prefab, spawnPos, Quaternion.identity);
        pieceGO.name = $"{color}_{type}_{cell.x}{cell.y}{cell.z}";

        pieceGO.transform.up = faceNormal;

        pieceGO.transform.localScale = Vector3.one * 0.4f;

        ChessPiece piece    = pieceGO.AddComponent<ChessPiece>();
        piece.pieceType     = type;
        piece.pieceColor    = color;
        piece.currentCell   = cell;
        cell.currentPiece   = pieceGO;
    }

    GameObject GetPrefab(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            return type switch
            {
                PieceType.Pawn   => whitePawnPrefab,
                PieceType.Rook   => whiteRookPrefab,
                PieceType.Queen  => whiteQueenPrefab,
                PieceType.King   => whiteKingPrefab,
                _                => whitePawnPrefab
            };
        }
        else
        {
            return type switch
            {
                PieceType.Pawn   => blackPawnPrefab,
                PieceType.Rook   => blackRookPrefab,
                PieceType.Queen  => blackQueenPrefab,
                PieceType.King   => blackKingPrefab,
                _                => blackPawnPrefab
            };
        }
    }
    
    Vector3 GetFaceNormal(string face)
    {
        return face switch
        {
            "front"  =>  Vector3.forward,   // +Z
            "back"   => -Vector3.forward,   // -Z
            "right"  =>  Vector3.right,     // +X
            "left"   => -Vector3.right,     // -X
            "top"    =>  Vector3.up,        // +Y
            "bottom" => -Vector3.up,        // -Y
            _        =>  Vector3.up
        };
    }

    // Iinput & Mouse click  
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }
    void HandleClick()
    {
        if (gameOver) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        // Sort all hits by distance — closest first
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        BoardCell clickedCell = null;
        foreach (RaycastHit hit in hits)
        {
            // Check if we hit a cell directly
            BoardCell cell = hit.collider.GetComponent<BoardCell>();
            if (cell != null)
            {
                clickedCell = cell;
                break;
            }
            ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
            if (piece != null)
            {
                clickedCell = piece.currentCell;
                break;
            }
        }

        if (clickedCell == null) return;
        if (highlightedMoves.Contains(clickedCell))
        {
            ExecuteMove(selectedCell, clickedCell);
            return;
        }

        // Otherwise try to select a piece on the clicked cell
        ClearHighlights();
        selectedCell = null;

        if (clickedCell.IsOccupied)
        {
            ChessPiece piece = clickedCell.currentPiece.GetComponent<ChessPiece>();
            if (piece != null && piece.pieceColor == currentTurn)
            {
                selectedCell = clickedCell;
                HighlightMoves(clickedCell);
                  clickSound?.Play();
            }
        }
    }

    void ExecuteMove(BoardCell from, BoardCell to)
    {
        ChessPiece movingPiece = from.currentPiece.GetComponent<ChessPiece>();

        // Capture — destroy enemy piece
        if (to.IsOccupied)
        {
            ChessPiece captured = to.currentPiece.GetComponent<ChessPiece>();
            Debug.Log($"Captured {captured.pieceColor} {captured.pieceType}!");
            Destroy(to.currentPiece);
            to.currentPiece = null;
        }

        // Move piece to new position
        Vector3 targetPos = to.transform.position + GetFaceNormal(to.face) * pieceHeightOffset;
        from.currentPiece.transform.position = targetPos;
        from.currentPiece.transform.up = GetFaceNormal(to.face);

        to.currentPiece = from.currentPiece;
        from.currentPiece = null;
        movingPiece.currentCell = to;
        movingPiece.hasMoved = true;

        moveSound?.Play();
        ClearHighlights();
        selectedCell = null;

        // Check win condition after every move
        CheckWinCondition();

        // Switch turn
        currentTurn = currentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
        Debug.Log($"Turn: {currentTurn}");

        // If it's now AI's turn, trigger AI
        if (currentTurn == PieceColor.Black)
            StartCoroutine(AIMove());
        TriggerCoreShift();
    }

    void TriggerCoreShift()
    {
        turnCount++;
        if (turnCount % coreShiftInterval != 0) return;

        // Toggle between normal and low gravity
        lowGravityMode = !lowGravityMode;

        if (lowGravityMode)
        {
            Debug.Log("CORE SHIFT — Low Gravity Mode! Pawns can jump 3 squares!");
            pieceHeightOffset = 1.2f; // pieces float higher visually
        }
        else
        {
            Debug.Log("CORE SHIFT — Normal Gravity restored!");
            pieceHeightOffset = 0.55f;
        }
    }


    void CheckWinCondition()
    {
        bool whiteKingAlive = false;
        bool blackKingAlive = false;

        // Search all cells for kings
        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null) continue;

                    if (piece.pieceType == PieceType.King)
                    {
                        if (piece.pieceColor == PieceColor.White) whiteKingAlive = true;
                        if (piece.pieceColor == PieceColor.Black) blackKingAlive = true;
                    }
                }

        if (!blackKingAlive)
        {
            Debug.Log("WHITE WINS!");
            ShowWinScreen("White");
        }
        else if (!whiteKingAlive)
        {
            Debug.Log("BLACK WINS!");
            ShowWinScreen("Black");
        }
    }

    IEnumerator AIMove()
    {
        if (gameOver) yield break;
        if (!aiEnabled || gameOver) yield break;
        if (aiThinking) yield break;

        aiThinking = true;
        Debug.Log("AI thinking...");

        // Small delay so it feels natural
        yield return new WaitForSeconds(0.8f);

        // Get all black pieces
        List<(BoardCell from, BoardCell to)> allMoves = new List<(BoardCell, BoardCell)>();

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null || piece.pieceColor != PieceColor.Black) continue;

                    List<BoardCell> moves = GetLegalMoves(cell);
                    foreach (BoardCell move in moves)
                        allMoves.Add((cell, move));
                }

        if (allMoves.Count == 0)
        {
            Debug.Log("AI has no moves — White wins!");
            ShowWinScreen("White");
            aiThinking = false;
            yield break;
        }

        // Priority: capture moves first, otherwise random
        var captureMoves = allMoves.FindAll(m => m.to.IsOccupied);

        (BoardCell from, BoardCell to) chosenMove;

        if (captureMoves.Count > 0)
        {
            // Prioritize capturing the King
            var kingCapture = captureMoves.Find(m =>
                m.to.currentPiece.GetComponent<ChessPiece>().pieceType == PieceType.King);

            if (kingCapture.from != null)
                chosenMove = kingCapture;
            else
                chosenMove = captureMoves[Random.Range(0, captureMoves.Count)];
        }
        else
        {
            chosenMove = allMoves[Random.Range(0, allMoves.Count)];
        }

        ExecuteMove(chosenMove.from, chosenMove.to);
        aiThinking = false;
    }


    void ShowWinScreen(string winner)
    {
        // Stop the game
        gameOver = true;
        Debug.Log($"GAME OVER — {winner} wins!");
        // You can hook up a UI panel here later
    }

    BoardCell GetWrappedCell(BoardCell fromCell, Vector2Int dir)
    {
        string newFace;
        int newX, newY, newZ;

        bool valid = TryWrapMove(
            fromCell.face,
            fromCell.x, fromCell.y, fromCell.z,
            dir,
            out newFace, out newX, out newY, out newZ
        );

        if (!valid) return null;
        return GetCellOnFace(newFace, newX, newY, newZ);
    }

    // Replace your entire TryWrapMove method with this:
    bool TryWrapMove(string fromFace, int x, int y, int z,
                     Vector2Int dir,
                     out string newFace, out int newX, out int newY, out int newZ)
    {
        int max = 6;
        newFace = fromFace;
        newX = x; newY = y; newZ = z;

        // Compute stepped coords on the same face first
        int sx = x + dir.x;
        int sy = y + dir.y;

        // Still within same face — no wrap needed
        if (sx >= 0 && sx <= max && sy >= 0 && sy <= max)
        {
            newX = sx; newY = sy;
            // newZ stays the same (face's fixed coord)
            switch (fromFace)
            {
                case "front": newZ = max; break;
                case "back": newZ = 0; break;
                case "right": newX = max; newX = sx; break; // x is free on front/back
            }
            // Just let GetCellOnFace handle the fixed axis
            newX = sx; newY = sy; newZ = z;
            return true;
        }

        // --- FRONT face (z = max), local axes: x=x, y=y ---
        if (fromFace == "front")
        {
            if (sy > max) { newFace = "top"; newX = x; newY = max; newZ = max - 1; return true; }
            if (sy < 0) { newFace = "bottom"; newX = x; newY = 0; newZ = max - 1; return true; }
            if (sx > max) { newFace = "right"; newX = max; newY = y; newZ = max - 1; return true; }
            if (sx < 0) { newFace = "left"; newX = 0; newY = y; newZ = max - 1; return true; }
        }

        // --- BACK face (z = 0), local axes: x=x, y=y ---
        if (fromFace == "back")
        {
            if (sy > max) { newFace = "top"; newX = x; newY = max; newZ = 1; return true; }
            if (sy < 0) { newFace = "bottom"; newX = x; newY = 0; newZ = 1; return true; }
            if (sx > max) { newFace = "right"; newX = max; newY = y; newZ = 1; return true; }
            if (sx < 0) { newFace = "left"; newX = 0; newY = y; newZ = 1; return true; }
        }

        // --- TOP face (y = max), local axes: x=x, y=z ---
        if (fromFace == "top")
        {
            // On top face, dir.y moves along Z axis
            int sz = z + dir.y;
            int stx = x + dir.x;
            if (sz > max) { newFace = "back"; newX = x; newY = max - 1; newZ = 0; return true; }
            if (sz < 0) { newFace = "front"; newX = x; newY = max - 1; newZ = max; return true; }
            if (stx > max) { newFace = "right"; newX = max; newY = max; newZ = z; return true; }
            if (stx < 0) { newFace = "left"; newX = 0; newY = max; newZ = z; return true; }
        }

        // --- BOTTOM face (y = 0), local axes: x=x, y=z ---
        if (fromFace == "bottom")
        {
            int sz = z + dir.y;
            int stx = x + dir.x;
            if (sz > max) { newFace = "back"; newX = x; newY = 1; newZ = 0; return true; }
            if (sz < 0) { newFace = "front"; newX = x; newY = 1; newZ = max; return true; }
            if (stx > max) { newFace = "right"; newX = max; newY = 0; newZ = z; return true; }
            if (stx < 0) { newFace = "left"; newX = 0; newY = 0; newZ = z; return true; }
        }

        // --- LEFT face (x = 0), local axes: y=y, x=z ---
        if (fromFace == "left")
        {
            int sz = z + dir.x; // left/right movement on left face moves along Z
            int sty = y + dir.y;
            if (sty > max) { newFace = "top"; newX = 0; newY = max; newZ = z; return true; }
            if (sty < 0) { newFace = "bottom"; newX = 0; newY = 0; newZ = z; return true; }
            if (sz > max) { newFace = "front"; newX = 1; newY = y; newZ = max; return true; }
            if (sz < 0) { newFace = "back"; newX = 1; newY = y; newZ = 0; return true; }
        }

        // --- RIGHT face (x = max), local axes: y=y, x=z ---
        if (fromFace == "right")
        {
            int sz = z + dir.x;
            int sty = y + dir.y;
            if (sty > max) { newFace = "top"; newX = max; newY = max; newZ = z; return true; }
            if (sty < 0) { newFace = "bottom"; newX = max; newY = 0; newZ = z; return true; }
            if (sz > max) { newFace = "back"; newX = max - 1; newY = y; newZ = 0; return true; }
            if (sz < 0) { newFace = "front"; newX = max - 1; newY = y; newZ = max; return true; }
        }

        return false;
    }

    // allowed move onli
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

        switch (piece.pieceType)
        {
            case PieceType.Pawn:   moves = GetPawnMoves(cell, piece);           break;
            case PieceType.Rook:   moves = GetSlidingMoves(cell, true, false);  break;
            case PieceType.Bishop: moves = GetSlidingMoves(cell, false, true);  break;
            case PieceType.Queen:  moves = GetSlidingMoves(cell, true, true);   break;
            case PieceType.King:   moves = GetKingMoves(cell, piece);           break;
            case PieceType.Knight: moves = GetKnightMoves(cell);                break;
        }

        // Remove moves that would capture own pieces
        moves.RemoveAll(m => m.IsOccupied &&
            m.currentPiece.GetComponent<ChessPiece>().pieceColor == piece.pieceColor);

        return moves;
    }

    List<BoardCell> GetPawnMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();
        int dir = piece.pieceColor == PieceColor.White ? 1 : -1;

        // Forward with wrap
        BoardCell fwd = GetWrappedCell(cell, new Vector2Int(0, dir));
        if (fwd != null && !fwd.IsOccupied)
        {
            moves.Add(fwd);

            BoardCell dbl = null;

            // Double move on first turn
            if (!piece.hasMoved)
            {
                dbl = GetWrappedCell(fwd, new Vector2Int(0, dir));
                if (dbl != null && !dbl.IsOccupied)
                    moves.Add(dbl);
            }
            // Triple move in low gravity mode
            if (lowGravityMode && !piece.hasMoved)
            {
                BoardCell triple = GetWrappedCell(dbl, new Vector2Int(0, dir));
                if (triple != null && !triple.IsOccupied)
                    moves.Add(triple);
            }

        }

        // Diagonal captures with wrap
        foreach (int dx in new[] { -1, 1 })
        {
            BoardCell diag = GetWrappedCell(cell, new Vector2Int(dx, dir));
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
        int[,] offsets = { { 1,2 }, { 2,1 }, { -1,2 }, { -2,1 }, { 1,-2 }, { 2,-1 }, { -1,-2 }, { -2,-1 } };
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
        if (straight) directions.AddRange(new[]{
        new Vector2Int(1,0), new Vector2Int(-1,0),
        new Vector2Int(0,1), new Vector2Int(0,-1) });
        if (diagonal) directions.AddRange(new[]{
        new Vector2Int(1,1),  new Vector2Int(-1,1),
        new Vector2Int(1,-1), new Vector2Int(-1,-1) });

        foreach (Vector2Int dir in directions)
        {
            BoardCell current = cell;
            for (int step = 1; step < 42; step++) // 42 = full cube perimeter
            {
                BoardCell next = GetWrappedCell(current, dir);
                if (next == null) break;
                if (next == cell) break; // looped back to start

                if (next.IsOccupied)
                {
                    if (next.currentPiece.GetComponent<ChessPiece>().pieceColor != piece.pieceColor)
                        moves.Add(next);
                    break;
                }
                moves.Add(next);
                current = next; // continue from new cell
            }
        }
        return moves;
    }

    // ── CELL LOOKUP ──────────────────────────────────────────────────
    BoardCell GetCellOnFace(string face, int x, int y, int z)
    {
        int gx, gy, gz;
        int max = 6;

        switch (face)
        {
            case "front":  gx = x;   gy = y;   gz = max; break;
            case "back":   gx = x;   gy = y;   gz = 0;   break;
            case "right":  gx = max; gy = y;   gz = z;   break;
            case "left":   gx = 0;   gy = y;   gz = z;   break;
            case "top":    gx = x;   gy = max; gz = z;   break;
            case "bottom": gx = x;   gy = 0;   gz = z;   break;
            default: return null;
        }

        if (gx < 0 || gx > max || gy < 0 || gy > max || gz < 0 || gz > max) return null;
        return cells[gx, gy, gz];
    }
}