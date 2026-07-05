using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardManager : MonoBehaviour
{
    // ── AUDIO ────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioSource clickSound;
    public AudioSource moveSound;

    // ── REFERENCES ───────────────────────────────────────────────────
    [Header("References")]
    public GameObject boardParent;   // drag 3DChessBoard here

    // ── PREFABS ──────────────────────────────────────────────────────
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

    // ── SETTINGS ─────────────────────────────────────────────────────
    [Header("Settings")]
    public float pieceHeightOffset = 0.6f;
    public float pieceScale = 0.4f;
    public bool aiEnabled = true;

    // ── INTERNAL STATE ────────────────────────────────────────────────
    private BoardCell[,,] cells = new BoardCell[7, 7, 7];
    private List<BoardCell> frontFaceCells = new List<BoardCell>();
    private List<BoardCell> backFaceCells = new List<BoardCell>();

    private BoardCell selectedCell = null;
    private List<BoardCell> highlightedMoves = new List<BoardCell>();
    private Dictionary<BoardCell, Material> originalMaterials = new Dictionary<BoardCell, Material>();

    private PieceColor currentTurn = PieceColor.White;
    private bool gameOver = false;
    private bool aiThinking = false;

    // Core shift
    private int turnCount = 0;
    private int coreShiftInterval = 4;
    private bool lowGravityMode = false;

    // Highlight material
    private Material highlightMaterial;

    // ── LIFECYCLE ────────────────────────────────────────────────────
    void Start()
    {
        highlightMaterial = new Material(Shader.Find("Standard"));
        highlightMaterial.color = new Color(0f, 1f, 0.5f, 0.8f);

        CollectCells();
        SpawnPieces();
    }

    // ── CELL COLLECTION ──────────────────────────────────────────────
    void CollectCells()
    {
        frontFaceCells.Clear();
        backFaceCells.Clear();

        foreach (Transform child in boardParent.transform)
        {
            BoardCell cell = child.GetComponent<BoardCell>();
            if (cell == null) continue;

            int cx = cell.x, cy = cell.y, cz = cell.z;
            if (cx < 0 || cx > 6 || cy < 0 || cy > 6 || cz < 0 || cz > 6) continue;

            cells[cx, cy, cz] = cell;

            // Assign face — z faces checked first, then x, then y
            // Edge/corner cells get the FIRST matching face label
            if (cz == 6) cell.face = "front";
            else if (cz == 0) cell.face = "back";
            else if (cx == 6) cell.face = "right";
            else if (cx == 0) cell.face = "left";
            else if (cy == 6) cell.face = "top";
            else if (cy == 0) cell.face = "bottom";
            else cell.face = "inner";   // hidden interior cell

            if (cell.face == "front") frontFaceCells.Add(cell);
            if (cell.face == "back") backFaceCells.Add(cell);
        }

        Debug.Log($"[Board] Front: {frontFaceCells.Count} | Back: {backFaceCells.Count}");
    }

    // ── PIECE SPAWNING ───────────────────────────────────────────────
    // Pieces spawn at y=2 (back row) and y=3 (pawns) on both faces
    // This avoids y=0/y=6 edge cells which have ambiguous normals
    void SpawnPieces()
    {
        // WHITE on front face — back row at y=2, pawns at y=3
        SpawnBackRow(frontFaceCells, PieceColor.White, rowY: 2);
        SpawnPawnRow(frontFaceCells, PieceColor.White, rowY: 3);

        // BLACK on back face — back row at y=4, pawns at y=3
        // y=4 is black's "home row", pawns at y=3 face each other
        SpawnBackRow(backFaceCells, PieceColor.Black, rowY: 4);
        SpawnPawnRow(backFaceCells, PieceColor.Black, rowY: 3);

        Debug.Log($"[Spawn] White back row: {frontFaceCells.FindAll(c => c.y == 2).Count} cells");
        Debug.Log($"[Spawn] White pawns:    {frontFaceCells.FindAll(c => c.y == 3).Count} cells");
        Debug.Log($"[Spawn] Black back row: {backFaceCells.FindAll(c => c.y == 4).Count} cells");
        Debug.Log($"[Spawn] Black pawns:    {backFaceCells.FindAll(c => c.y == 3).Count} cells");
    }

    void SpawnBackRow(List<BoardCell> faceCells, PieceColor color, int rowY)
    {
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        row.Sort((a, b) => a.x.CompareTo(b.x));

        // Layout across 7 columns: Rook _ Queen King _ _ Rook
        PieceType?[] layout =
        {
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
        row.Sort((a, b) => a.x.CompareTo(b.x));
        foreach (BoardCell cell in row)
            PlacePiece(PieceType.Pawn, color, cell);
    }

    void PlacePiece(PieceType type, PieceColor color, BoardCell cell)
    {
        GameObject prefab = GetPrefab(type, color);
        if (prefab == null)
        {
            Debug.LogWarning($"[Spawn] Missing prefab: {color} {type}");
            return;
        }

        Vector3 outDir = GetOutwardDir(cell);
        Vector3 spawnPos = cell.transform.position + outDir * pieceHeightOffset;

        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.name = $"{color}_{type}_{cell.x}{cell.y}{cell.z}";

        // Stand piece perpendicular to its face
        go.transform.up = outDir;
        go.transform.localScale = Vector3.one * pieceScale;

        ChessPiece cp = go.AddComponent<ChessPiece>();
        cp.pieceType = type;
        cp.pieceColor = color;
        cp.currentCell = cell;
        cell.currentPiece = go;
    }

    // ── FACE NORMALS ─────────────────────────────────────────────────
    // Uses the cube's actual world transform so rotation doesn't break normals
    Vector3 GetOutwardDir(BoardCell cell)
    {
        Transform t = boardParent.transform;
        return cell.face switch
        {
            "front" => t.forward,
            "back" => -t.forward,
            "right" => t.right,
            "left" => -t.right,
            "top" => t.up,
            "bottom" => -t.up,
            _ => t.up
        };
    }

    GameObject GetPrefab(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
            return type switch
            {
                PieceType.Pawn => whitePawnPrefab,
                PieceType.Rook => whiteRookPrefab,
                PieceType.Queen => whiteQueenPrefab,
                PieceType.King => whiteKingPrefab,
                _ => whitePawnPrefab
            };
        else
            return type switch
            {
                PieceType.Pawn => blackPawnPrefab,
                PieceType.Rook => blackRookPrefab,
                PieceType.Queen => blackQueenPrefab,
                PieceType.King => blackKingPrefab,
                _ => blackPawnPrefab
            };
    }

    // ── INPUT ────────────────────────────────────────────────────────
    void Update()
    {
        if (gameOver) return;
        if (aiThinking) return;   // block input while AI is thinking

        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        BoardCell clickedCell = null;

        foreach (RaycastHit hit in hits)
        {
            // Hit a board cell directly
            BoardCell cell = hit.collider.GetComponent<BoardCell>();
            if (cell != null) { clickedCell = cell; break; }

            // Hit a chess piece — use its cell
            ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
            if (piece != null) { clickedCell = piece.currentCell; break; }
        }

        if (clickedCell == null) return;

        // Clicked a highlighted move target → execute move
        if (highlightedMoves.Contains(clickedCell))
        {
            ExecuteMove(selectedCell, clickedCell);
            return;
        }

        // Otherwise try to select a piece
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

    // ── MOVE EXECUTION ────────────────────────────────────────────────
    void ExecuteMove(BoardCell from, BoardCell to)
    {
        if (from == null || to == null) return;

        ChessPiece movingPiece = from.currentPiece.GetComponent<ChessPiece>();

        // Capture enemy piece
        if (to.IsOccupied)
        {
            ChessPiece captured = to.currentPiece.GetComponent<ChessPiece>();
            Debug.Log($"[Capture] {captured.pieceColor} {captured.pieceType} captured!");
            Destroy(to.currentPiece);
            to.currentPiece = null;
        }

        // Reposition piece on new cell
        Vector3 outDir = GetOutwardDir(to);
        Vector3 targetPos = to.transform.position + outDir * pieceHeightOffset;

        from.currentPiece.transform.position = targetPos;
        from.currentPiece.transform.up = outDir;

        // Update references
        to.currentPiece = from.currentPiece;
        from.currentPiece = null;
        movingPiece.currentCell = to;
        movingPiece.hasMoved = true;

        moveSound?.Play();
        ClearHighlights();
        selectedCell = null;

        // Check for win BEFORE switching turn
        CheckWinCondition();
        if (gameOver) return;

        // Core gravity shift
        TriggerCoreShift();

        // Switch turn
        currentTurn = (currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        Debug.Log($"[Turn] Now: {currentTurn}");

        // Trigger AI if it's black's turn
        if (currentTurn == PieceColor.Black && aiEnabled)
            StartCoroutine(AIMove());
    }

    // ── HIGHLIGHTING ──────────────────────────────────────────────────
    void HighlightMoves(BoardCell cell)
    {
        highlightedMoves = GetLegalMoves(cell);
        foreach (BoardCell move in highlightedMoves)
        {
            Renderer r = move.GetComponent<Renderer>();
            if (r == null) continue;
            originalMaterials[move] = r.material;
            r.material = highlightMaterial;
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

    // ── LEGAL MOVE CALCULATION ────────────────────────────────────────
    List<BoardCell> GetLegalMoves(BoardCell cell)
    {
        if (!cell.IsOccupied) return new List<BoardCell>();

        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
        List<BoardCell> moves = piece.pieceType switch
        {
            PieceType.Pawn => GetPawnMoves(cell, piece),
            PieceType.Rook => GetSlidingMoves(cell, true, false),
            PieceType.Bishop => GetSlidingMoves(cell, false, true),
            PieceType.Queen => GetSlidingMoves(cell, true, true),
            PieceType.King => GetKingMoves(cell),
            PieceType.Knight => GetKnightMoves(cell),
            _ => new List<BoardCell>()
        };

        // Remove moves that land on own pieces
        moves.RemoveAll(m => m != null && m.IsOccupied &&
            m.currentPiece.GetComponent<ChessPiece>().pieceColor == piece.pieceColor);

        return moves;
    }

    // PAWN
    List<BoardCell> GetPawnMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();

        // White moves +Y, Black moves -Y on their respective faces
        int dir = (piece.pieceColor == PieceColor.White) ? 1 : -1;

        // One step forward
        BoardCell fwd = GetWrappedCell(cell, new Vector2Int(0, dir));
        if (fwd != null && !fwd.IsOccupied)
        {
            moves.Add(fwd);

            // Two steps forward on first move
            if (!piece.hasMoved)
            {
                BoardCell dbl = GetWrappedCell(fwd, new Vector2Int(0, dir));
                if (dbl != null && !dbl.IsOccupied)
                {
                    moves.Add(dbl);

                    // Three steps in low gravity mode
                    if (lowGravityMode)
                    {
                        BoardCell triple = GetWrappedCell(dbl, new Vector2Int(0, dir));
                        if (triple != null && !triple.IsOccupied)
                            moves.Add(triple);
                    }
                }
            }
        }

        // Diagonal captures
        foreach (int dx in new[] { -1, 1 })
        {
            BoardCell diag = GetWrappedCell(cell, new Vector2Int(dx, dir));
            if (diag != null && diag.IsOccupied)
            {
                ChessPiece target = diag.currentPiece.GetComponent<ChessPiece>();
                if (target.pieceColor != piece.pieceColor)
                    moves.Add(diag);
            }
        }

        return moves;
    }

    // KING
    List<BoardCell> GetKingMoves(BoardCell cell)
    {
        List<BoardCell> moves = new List<BoardCell>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                BoardCell c = GetWrappedCell(cell, new Vector2Int(dx, dy));
                if (c != null) moves.Add(c);
            }
        return moves;
    }

    // KNIGHT
    List<BoardCell> GetKnightMoves(BoardCell cell)
    {
        List<BoardCell> moves = new List<BoardCell>();
        Vector2Int[] offsets =
        {
            new Vector2Int( 1, 2), new Vector2Int( 2, 1),
            new Vector2Int(-1, 2), new Vector2Int(-2, 1),
            new Vector2Int( 1,-2), new Vector2Int( 2,-1),
            new Vector2Int(-1,-2), new Vector2Int(-2,-1)
        };
        foreach (Vector2Int off in offsets)
        {
            BoardCell c = GetWrappedCell(cell, off);
            if (c != null) moves.Add(c);
        }
        return moves;
    }

    // ROOK / BISHOP / QUEEN
    List<BoardCell> GetSlidingMoves(BoardCell cell, bool straight, bool diagonal)
    {
        List<BoardCell> moves = new List<BoardCell>();
        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();

        List<Vector2Int> dirs = new List<Vector2Int>();
        if (straight) dirs.AddRange(new[]
        {
            new Vector2Int( 1, 0), new Vector2Int(-1, 0),
            new Vector2Int( 0, 1), new Vector2Int( 0,-1)
        });
        if (diagonal) dirs.AddRange(new[]
        {
            new Vector2Int( 1, 1), new Vector2Int(-1, 1),
            new Vector2Int( 1,-1), new Vector2Int(-1,-1)
        });

        foreach (Vector2Int dir in dirs)
        {
            BoardCell current = cell;
            for (int step = 0; step < 42; step++) // 42 covers full cube loop
            {
                BoardCell next = GetWrappedCell(current, dir);
                if (next == null || next == cell) break; // null or looped back

                if (next.IsOccupied)
                {
                    // Can capture enemy
                    if (next.currentPiece.GetComponent<ChessPiece>().pieceColor != piece.pieceColor)
                        moves.Add(next);
                    break; // blocked either way
                }

                moves.Add(next);
                current = next;
            }
        }
        return moves;
    }

    // ── WRAP-AROUND MOVEMENT ─────────────────────────────────────────
    // GetWrappedCell: given a cell and a 2D direction, returns the next cell
    // crossing face boundaries if needed
    BoardCell GetWrappedCell(BoardCell fromCell, Vector2Int dir)
    {
        if (fromCell == null) return null;

        bool valid = TryWrapMove(
            fromCell.face,
            fromCell.x, fromCell.y, fromCell.z,
            dir,
            out string newFace, out int newX, out int newY, out int newZ);

        if (!valid) return null;
        return GetCellOnFace(newFace, newX, newY, newZ);
    }

    // TryWrapMove: maps a direction step from one face to the correct 3D coordinate
    // dir.x = horizontal movement on the face, dir.y = vertical movement on the face
    bool TryWrapMove(string fromFace, int x, int y, int z,
                     Vector2Int dir,
                     out string newFace, out int newX, out int newY, out int newZ)
    {
        const int MAX = 6;
        newFace = fromFace;
        newX = x; newY = y; newZ = z;

        int sx = x + dir.x;
        int sy = y + dir.y;

        switch (fromFace)
        {
            // ── FRONT face (z=6): local x→x, local y→y ──────────────
            case "front":
                if (sx >= 0 && sx <= MAX && sy >= 0 && sy <= MAX)
                { newX = sx; newY = sy; newZ = MAX; return true; }

                if (sy > MAX) { newFace = "top"; newX = x; newY = MAX; newZ = MAX - 1; return true; }
                else if (sy < 0) { newFace = "bottom"; newX = x; newY = 0; newZ = MAX - 1; return true; }
                else if (sx > MAX) { newFace = "right"; newX = MAX; newY = y; newZ = MAX - 1; return true; }
                else if (sx < 0) { newFace = "left"; newX = 0; newY = y; newZ = MAX - 1; return true; }
                break;

            // ── BACK face (z=0): local x→x, local y→y ───────────────
            case "back":
                if (sx >= 0 && sx <= MAX && sy >= 0 && sy <= MAX)
                { newX = sx; newY = sy; newZ = 0; return true; }

                if (sy > MAX) { newFace = "top"; newX = x; newY = MAX; newZ = 1; return true; }
                else if (sy < 0) { newFace = "bottom"; newX = x; newY = 0; newZ = 1; return true; }
                else if (sx > MAX) { newFace = "right"; newX = MAX; newY = y; newZ = 1; return true; }
                else if (sx < 0) { newFace = "left"; newX = 0; newY = y; newZ = 1; return true; }
                break;

            // ── TOP face (y=6): local x→x, local y→z ────────────────
            case "top":
                {
                    int stx = x + dir.x;
                    int stz = z + dir.y;   // vertical on top face moves along Z

                    if (stx >= 0 && stx <= MAX && stz >= 0 && stz <= MAX)
                    { newX = stx; newY = MAX; newZ = stz; return true; }

                    if (stz < 0) { newFace = "front"; newX = x; newY = MAX - 1; newZ = MAX; return true; }
                    else if (stz > MAX) { newFace = "back"; newX = x; newY = MAX - 1; newZ = 0; return true; }
                    else if (stx > MAX) { newFace = "right"; newX = MAX; newY = MAX; newZ = z; return true; }
                    else if (stx < 0) { newFace = "left"; newX = 0; newY = MAX; newZ = z; return true; }
                    break;
                }

            // ── BOTTOM face (y=0): local x→x, local y→z ─────────────
            case "bottom":
                {
                    int stx = x + dir.x;
                    int stz = z + dir.y;

                    if (stx >= 0 && stx <= MAX && stz >= 0 && stz <= MAX)
                    { newX = stx; newY = 0; newZ = stz; return true; }

                    if (stz < 0) { newFace = "front"; newX = x; newY = 1; newZ = MAX; return true; }
                    else if (stz > MAX) { newFace = "back"; newX = x; newY = 1; newZ = 0; return true; }
                    else if (stx > MAX) { newFace = "right"; newX = MAX; newY = 0; newZ = z; return true; }
                    else if (stx < 0) { newFace = "left"; newX = 0; newY = 0; newZ = z; return true; }
                    break;
                }

            // ── LEFT face (x=0): local x→z, local y→y ───────────────
            case "left":
                {
                    int stz = z + dir.x;  // horizontal on left face moves along Z
                    int sty = y + dir.y;

                    if (stz >= 0 && stz <= MAX && sty >= 0 && sty <= MAX)
                    { newX = 0; newY = sty; newZ = stz; return true; }

                    if (sty > MAX) { newFace = "top"; newX = 0; newY = MAX; newZ = z; return true; }
                    else if (sty < 0) { newFace = "bottom"; newX = 0; newY = 0; newZ = z; return true; }
                    else if (stz > MAX) { newFace = "front"; newX = 1; newY = y; newZ = MAX; return true; }
                    else if (stz < 0) { newFace = "back"; newX = 1; newY = y; newZ = 0; return true; }
                    break;
                }

            // ── RIGHT face (x=6): local x→z, local y→y ──────────────
            case "right":
                {
                    int stz = z + dir.x;
                    int sty = y + dir.y;

                    if (stz >= 0 && stz <= MAX && sty >= 0 && sty <= MAX)
                    { newX = MAX; newY = sty; newZ = stz; return true; }

                    if (sty > MAX) { newFace = "top"; newX = MAX; newY = MAX; newZ = z; return true; }
                    else if (sty < 0) { newFace = "bottom"; newX = MAX; newY = 0; newZ = z; return true; }
                    else if (stz > MAX) { newFace = "back"; newX = MAX - 1; newY = y; newZ = 0; return true; }
                    else if (stz < 0) { newFace = "front"; newX = MAX - 1; newY = y; newZ = MAX; return true; }
                    break;
                }
        }

        return false;
    }

    // ── CELL LOOKUP ───────────────────────────────────────────────────
    BoardCell GetCellOnFace(string face, int x, int y, int z)
    {
        const int MAX = 6;
        int gx, gy, gz;

        switch (face)
        {
            case "front": gx = x; gy = y; gz = MAX; break;
            case "back": gx = x; gy = y; gz = 0; break;
            case "right": gx = MAX; gy = y; gz = z; break;
            case "left": gx = 0; gy = y; gz = z; break;
            case "top": gx = x; gy = MAX; gz = z; break;
            case "bottom": gx = x; gy = 0; gz = z; break;
            default: return null;
        }

        if (gx < 0 || gx > MAX || gy < 0 || gy > MAX || gz < 0 || gz > MAX) return null;
        return cells[gx, gy, gz];
    }

    // ── WIN CONDITION ─────────────────────────────────────────────────
    void CheckWinCondition()
    {
        bool whiteKingAlive = false;
        bool blackKingAlive = false;

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null || piece.pieceType != PieceType.King) continue;

                    if (piece.pieceColor == PieceColor.White) whiteKingAlive = true;
                    if (piece.pieceColor == PieceColor.Black) blackKingAlive = true;
                }

        if (!blackKingAlive) { ShowWinScreen("White"); return; }
        if (!whiteKingAlive) { ShowWinScreen("Black"); }
    }

    void ShowWinScreen(string winner)
    {
        gameOver = true;
        Debug.Log($"[WIN] GAME OVER — {winner} wins!");
        // Hook up your win UI panel here
    }

    // ── CORE SHIFT ────────────────────────────────────────────────────
    void TriggerCoreShift()
    {
        turnCount++;
        if (turnCount % coreShiftInterval != 0) return;

        lowGravityMode = !lowGravityMode;

        if (lowGravityMode)
        {
            pieceHeightOffset = 1.2f;
            Debug.Log("[CORE SHIFT] Low Gravity — Pawns can jump 3 squares!");
        }
        else
        {
            pieceHeightOffset = 0.6f;
            Debug.Log("[CORE SHIFT] Normal Gravity restored.");
        }
    }

    // ── AI ────────────────────────────────────────────────────────────
    IEnumerator AIMove()
    {
        if (gameOver || aiThinking) yield break;

        aiThinking = true;
        Debug.Log("[AI] Thinking...");
        yield return new WaitForSeconds(0.8f);

        if (gameOver) { aiThinking = false; yield break; }

        // Collect all legal moves for black
        var allMoves = new List<(BoardCell from, BoardCell to)>();

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null || piece.pieceColor != PieceColor.Black) continue;

                    foreach (BoardCell move in GetLegalMoves(cell))
                        allMoves.Add((cell, move));
                }

        if (allMoves.Count == 0)
        {
            Debug.Log("[AI] No moves available — White wins!");
            ShowWinScreen("White");
            aiThinking = false;
            yield break;
        }

        // Priority 1 — capture King
        var kingCapture = allMoves.Find(m =>
            m.to.IsOccupied &&
            m.to.currentPiece.GetComponent<ChessPiece>().pieceType == PieceType.King);

        // Priority 2 — any capture
        var anyCapture = allMoves.FindAll(m => m.to.IsOccupied);

        (BoardCell from, BoardCell to) chosen;

        if (kingCapture.from != null)
            chosen = kingCapture;
        else if (anyCapture.Count > 0)
            chosen = anyCapture[Random.Range(0, anyCapture.Count)];
        else
            chosen = allMoves[Random.Range(0, allMoves.Count)];

        Debug.Log($"[AI] Moving {chosen.from.currentPiece.GetComponent<ChessPiece>().pieceType} " +
                  $"from ({chosen.from.x},{chosen.from.y},{chosen.from.z}) " +
                  $"to ({chosen.to.x},{chosen.to.y},{chosen.to.z})");

        ExecuteMove(chosen.from, chosen.to);
        aiThinking = false;
    }
}