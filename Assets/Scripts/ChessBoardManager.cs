using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardManager : MonoBehaviour
{
    [Header("Camera")]
    public OrbitalCamera cameraController; // Updated to reference OrbitalCamera directly

    [Header("Audio")]
    public AudioSource clickSound;
    public AudioSource moveSound;

    [Header("HUD")]
    public MoveLogUI moveLogUI;

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
    public float pieceHeightOffset = 0.3f;
    public float pieceScale = 0.4f;
    public bool aiEnabled = true;

    private BoardCell[,,] cells = new BoardCell[7, 7, 7];
    private List<BoardCell> frontFaceCells = new List<BoardCell>();
    private List<BoardCell> backFaceCells = new List<BoardCell>();

    private BoardCell selectedCell = null;
    private List<BoardCell> highlightedMoves = new List<BoardCell>();
    private Dictionary<BoardCell, Material> originalMaterials = new Dictionary<BoardCell, Material>();

    private PieceColor currentTurn = PieceColor.White;
    private bool gameOver = false;
    private bool aiThinking = false;

    private int turnCount = 0;
    private int coreShiftInterval = 4;
    private bool lowGravityMode = false;

    public Material highlightMaterial;

    void Start()
    {
        CollectCells();
        SpawnPieces();

        // Explicitly initialize the camera settings safely on frame 1
        if (cameraController != null)
        {
            cameraController.ResetToWhite();
        }
    }

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

            if (cz == 6) cell.face = "front";
            else if (cz == 0) cell.face = "back";
            else if (cx == 6) cell.face = "right";
            else if (cx == 0) cell.face = "left";
            else if (cy == 6) cell.face = "top";
            else if (cy == 0) cell.face = "bottom";
            else cell.face = "inner";

            if (cell.face == "front") frontFaceCells.Add(cell);
            if (cell.face == "back") backFaceCells.Add(cell);
        }
    }

    void SpawnPieces()
    {
        SpawnBackRow(frontFaceCells, PieceColor.White, rowY: 2);
        SpawnPawnRow(frontFaceCells, PieceColor.White, rowY: 3);
        SpawnBackRow(backFaceCells, PieceColor.Black, rowY: 4);
        SpawnPawnRow(backFaceCells, PieceColor.Black, rowY: 3);
    }

    void SpawnBackRow(List<BoardCell> faceCells, PieceColor color, int rowY)
    {
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        row.Sort((a, b) => a.x.CompareTo(b.x));

        PieceType?[] layout = { PieceType.Rook, null, PieceType.Queen, PieceType.King, null, null, PieceType.Rook };

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
        if (prefab == null) return;

        Vector3 outDir = GetOutwardDir(cell);
        GameObject go = Instantiate(prefab, cell.transform.position, Quaternion.identity);
        go.name = $"{color}_{type}_{cell.x}{cell.y}{cell.z}";

        go.transform.up = outDir;
        go.transform.localScale = Vector3.one * pieceScale;

        Vector3 spawnPos = cell.transform.position + outDir * GetPiecePlacementOffset(go, outDir);
        go.transform.position = spawnPos;

        ChessPiece cp = go.AddComponent<ChessPiece>();
        cp.pieceType = type;
        cp.pieceColor = color;
        cp.currentCell = cell;
        cell.currentPiece = go;
    }

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

    float GetPiecePlacementOffset(GameObject piece, Vector3 outDir)
    {
        if (piece == null) return pieceHeightOffset;

        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return pieceHeightOffset;

        Bounds bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);

        float extentAlongDir = Mathf.Abs(outDir.x) * bounds.extents.x
            + Mathf.Abs(outDir.y) * bounds.extents.y
            + Mathf.Abs(outDir.z) * bounds.extents.z;

        return pieceHeightOffset + extentAlongDir;
    }

    GameObject GetPrefab(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            return type switch
            {
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
                PieceType.Rook => blackRookPrefab,
                PieceType.Queen => blackQueenPrefab,
                PieceType.King => blackKingPrefab,
                _ => blackPawnPrefab
            };
        }
    }

    void Update()
    {
        if (gameOver || aiThinking) return;

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
            BoardCell cell = hit.collider.GetComponent<BoardCell>();
            if (cell != null) { clickedCell = cell; break; }

            ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
            if (piece != null) { clickedCell = piece.currentCell; break; }
        }

        if (clickedCell == null) return;

        if (highlightedMoves.Contains(clickedCell))
        {
            ExecuteMove(selectedCell, clickedCell);
            return;
        }

        ClearHighlights();
        selectedCell = null;

        if (clickedCell.IsOccupied)
        {
            ChessPiece piece = clickedCell.currentPiece.GetComponent<ChessPiece>();
            if (piece != null && piece.pieceColor == currentTurn)
            {
                selectedCell = clickedCell;
                ChessPiece selectedPiece = clickedCell.currentPiece.GetComponent<ChessPiece>();
                Debug.Log($"[Select] {selectedPiece.pieceColor} {selectedPiece.pieceType} selected at ({clickedCell.x},{clickedCell.y},{clickedCell.z})");
                HighlightMoves(clickedCell);
                Debug.Log($"[Moves] {highlightedMoves.Count} legal moves available.");
                clickSound?.Play();
            }
        }
    }

    void SwitchCameraToCurrentTurn()
    {
        if (cameraController == null) return;
        cameraController.SnapToFace(currentTurn == PieceColor.White ? "front" : "back");
    }

    void ExecuteMove(BoardCell from, BoardCell to)
    {
        if (from == null || to == null) return;
  ChessPiece movingPiece = from.currentPiece.GetComponent<ChessPiece>();
    bool wasCapture = to.IsOccupied;
    string capturedInfo = "";

    if (wasCapture)
    {
        ChessPiece captured = to.currentPiece.GetComponent<ChessPiece>();
        capturedInfo = $" (captures {captured.pieceColor} {captured.pieceType})";
        Debug.Log($"[Capture] {captured.pieceColor} {captured.pieceType} captured!");
        Destroy(to.currentPiece);
        to.currentPiece = null;
    }

    string moveText = $"{movingPiece.pieceType} ({from.x},{from.y},{from.z}) → ({to.x},{to.y},{to.z}){capturedInfo}";
    moveLogUI?.LogMove(movingPiece.pieceColor, moveText, wasCapture);

        if (to.IsOccupied)
        {
            ChessPiece captured = to.currentPiece.GetComponent<ChessPiece>();
            Debug.Log($"[Capture] {captured.pieceColor} {captured.pieceType} captured!");
            Destroy(to.currentPiece);
            to.currentPiece = null;     
        }

        Vector3 outDir = GetOutwardDir(to);
        Vector3 targetPos = to.transform.position + outDir * GetPiecePlacementOffset(from.currentPiece, outDir);

        from.currentPiece.transform.position = targetPos;
        from.currentPiece.transform.up = outDir;

        to.currentPiece = from.currentPiece;
        from.currentPiece = null;
        movingPiece.currentCell = to;
        movingPiece.hasMoved = true;
       // bool wasCapture = to.IsOccupied; 
        moveSound?.Play();
        ClearHighlights();
        selectedCell = null;

        CheckWinCondition();
        if (gameOver) return;

        TriggerCoreShift();

        currentTurn = (currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        Debug.Log($"[Turn] {currentTurn}'s turn");
        SwitchCameraToCurrentTurn();

        if (currentTurn == PieceColor.Black && aiEnabled)
            StartCoroutine(AIMove());
    }

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

        moves.RemoveAll(m => m != null && m.IsOccupied &&
            m.currentPiece.GetComponent<ChessPiece>().pieceColor == piece.pieceColor);

        return moves;
    }

    List<BoardCell> GetPawnMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();

        // 1. Single Step Forward
        BoardCell fwd = GetPawnForwardCell(cell, piece);
        if (fwd != null && !fwd.IsOccupied)
        {
            moves.Add(fwd);

            // 2. Double Step Forward (Only allowed if the pawn hasn't moved AND the first step isn't blocked)
            if (!piece.hasMoved)
            {
                BoardCell dbl = GetPawnForwardCell(fwd, piece);
                if (dbl != null && !dbl.IsOccupied)
                {
                    moves.Add(dbl);

                    // Low Gravity Bonus Step
                    if (lowGravityMode)
                    {
                        BoardCell triple = GetPawnForwardCell(dbl, piece);
                        if (triple != null && !triple.IsOccupied)
                            moves.Add(triple);
                    }
                }
            }
        }

        // 3. Strict Diagonal Captures
        int dir = (piece.pieceColor == PieceColor.White) ? 1 : -1;
        foreach (int dx in new[] { -1, 1 })
        {
            BoardCell diag = GetWrappedCell(cell, new Vector2Int(dx, dir));
            if (diag != null && diag.IsOccupied)
            {
                ChessPiece target = diag.currentPiece.GetComponent<ChessPiece>();
                if (target.pieceColor != piece.pieceColor)
                {
                    // Safety Check: Ensure the wrapped cell is a true geometric diagonal 
                    // and hasn't accidentally warped straight ahead due to face translation
                    if (diag.x != cell.x || diag.y != cell.y || diag.z != cell.z)
                    {
                        moves.Add(diag);
                    }
                }
            }
        }

        return moves;
    }

    BoardCell GetPawnForwardCell(BoardCell cell, ChessPiece piece, int step = 1)
    {
        if (cell == null || piece == null) return null;

        int dir = (piece.pieceColor == PieceColor.White) ? 1 : -1;
        Vector2Int move = cell.face switch
        {
            "front" => new Vector2Int(0, dir),
            "back" => new Vector2Int(0, -dir),
            "top" => new Vector2Int(0, -dir),
            "bottom" => new Vector2Int(0, dir),
            "left" => new Vector2Int(dir, 0),
            "right" => new Vector2Int(-dir, 0),
            _ => new Vector2Int(0, dir)
        };

        return GetWrappedCell(cell, new Vector2Int(move.x * step, move.y * step));
    }

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

    List<BoardCell> GetKnightMoves(BoardCell cell)
    {
        List<BoardCell> moves = new List<BoardCell>();
        Vector2Int[] offsets = {
            new Vector2Int(1,2), new Vector2Int(2,1), new Vector2Int(-1,2), new Vector2Int(-2,1),
            new Vector2Int(1,-2), new Vector2Int(2,-1), new Vector2Int(-1,-2), new Vector2Int(-2,-1)
        };
        foreach (Vector2Int off in offsets)
        {
            BoardCell c = GetWrappedCell(cell, off);
            if (c != null) moves.Add(c);
        }
        return moves;
    }

    List<BoardCell> GetSlidingMoves(BoardCell cell, bool straight, bool diagonal)
    {
        List<BoardCell> moves = new List<BoardCell>();
        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
        HashSet<BoardCell> visited = new HashSet<BoardCell> { cell };

        List<Vector2Int> dirs = new List<Vector2Int>();
        if (straight) dirs.AddRange(new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) });
        if (diagonal) dirs.AddRange(new[] { new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) });

        foreach (Vector2Int dir in dirs)
        {
            BoardCell current = cell;
            for (int step = 0; step < 42; step++)
            {
                BoardCell next = GetWrappedCell(current, dir);
                if (next == null || visited.Contains(next)) break;

                if (next.IsOccupied)
                {
                    if (next.currentPiece.GetComponent<ChessPiece>().pieceColor != piece.pieceColor)
                        moves.Add(next);
                    break;
                }

                moves.Add(next);
                visited.Add(next);
                current = next;
            }
            visited.Clear();
            visited.Add(cell);
        }
        return moves;
    }

    BoardCell GetWrappedCell(BoardCell fromCell, Vector2Int dir)
    {
        if (fromCell == null) return null;

        bool valid = TryWrapMove(
            fromCell.face, fromCell.x, fromCell.y, fromCell.z, dir,
            out string newFace, out int newX, out int newY, out int newZ);

        if (!valid) return null;
        return GetCellOnFace(newFace, newX, newY, newZ);
    }

    bool TryWrapMove(string fromFace, int x, int y, int z, Vector2Int dir,
                     out string newFace, out int newX, out int newY, out int newZ)
    {
        const int MAX = 6;
        newFace = fromFace; newX = x; newY = y; newZ = z;

        int sx = x + dir.x;
        int sy = y + dir.y;

        switch (fromFace)
        {
            case "front":
                if (sx >= 0 && sx <= MAX && sy >= 0 && sy <= MAX) { newX = sx; newY = sy; newZ = MAX; return true; }
                if (sy > MAX) { newFace = "top"; newX = x; newY = MAX; newZ = MAX - 1; return true; }
                else if (sy < 0) { newFace = "bottom"; newX = x; newY = 0; newZ = MAX - 1; return true; }
                else if (sx > MAX) { newFace = "right"; newX = MAX; newY = y; newZ = MAX - 1; return true; }
                else if (sx < 0) { newFace = "left"; newX = 0; newY = y; newZ = MAX - 1; return true; }
                break;
            case "back":
                if (sx >= 0 && sx <= MAX && sy >= 0 && sy <= MAX) { newX = sx; newY = sy; newZ = 0; return true; }
                if (sy > MAX) { newFace = "top"; newX = x; newY = MAX; newZ = 1; return true; }
                else if (sy < 0) { newFace = "bottom"; newX = x; newY = 0; newZ = 1; return true; }
                else if (sx > MAX) { newFace = "right"; newX = MAX; newY = y; newZ = 1; return true; }
                else if (sx < 0) { newFace = "left"; newX = 0; newY = y; newZ = 1; return true; }
                break;
            case "top":
                int stx = x + dir.x; int stz = z + dir.y;
                if (stx >= 0 && stx <= MAX && stz >= 0 && stz <= MAX) { newX = stx; newY = MAX; newZ = stz; return true; }
                if (stz < 0) { newFace = "front"; newX = x; newY = MAX - 1; newZ = MAX; return true; }
                else if (stz > MAX) { newFace = "back"; newX = x; newY = MAX - 1; newZ = 0; return true; }
                else if (stx > MAX) { newFace = "right"; newX = MAX; newY = MAX; newZ = z; return true; }
                else if (stx < 0) { newFace = "left"; newX = 0; newY = MAX; newZ = z; return true; }
                break;
            case "bottom":
                int sbx = x + dir.x; int sbz = z + dir.y;
                if (sbx >= 0 && sbx <= MAX && sbz >= 0 && sbz <= MAX) { newX = sbx; newY = 0; newZ = sbz; return true; }
                if (sbz < 0) { newFace = "front"; newX = x; newY = 1; newZ = MAX; return true; }
                else if (sbz > MAX) { newFace = "back"; newX = x; newY = 1; newZ = 0; return true; }
                else if (sbx > MAX) { newFace = "right"; newX = MAX; newY = 0; newZ = z; return true; }
                else if (sbx < 0) { newFace = "left"; newX = 0; newY = 0; newZ = z; return true; }
                break;
            case "left":
                int slz = z + dir.x; int sly = y + dir.y;
                if (slz >= 0 && slz <= MAX && sly >= 0 && sly <= MAX) { newX = 0; newY = sly; newZ = slz; return true; }
                if (sly > MAX) { newFace = "top"; newX = 0; newY = MAX; newZ = z; return true; }
                else if (sly < 0) { newFace = "bottom"; newX = 0; newY = 0; newZ = z; return true; }
                else if (slz > MAX) { newFace = "front"; newX = 1; newY = y; newZ = MAX; return true; }
                else if (slz < 0) { newFace = "back"; newX = 1; newY = y; newZ = 0; return true; }
                break;
            case "right":
                int srz = z + dir.x; int sry = y + dir.y;
                if (srz >= 0 && srz <= MAX && sry >= 0 && sry <= MAX) { newX = MAX; newY = sry; newZ = srz; return true; }
                if (sry > MAX) { newFace = "top"; newX = MAX; newY = MAX; newZ = z; return true; }
                else if (sry < 0) { newFace = "bottom"; newX = MAX; newY = 0; newZ = z; return true; }
                else if (srz > MAX) { newFace = "back"; newX = MAX - 1; newY = y; newZ = 0; return true; }
                else if (srz < 0) { newFace = "front"; newX = MAX - 1; newY = y; newZ = MAX; return true; }
                break;
        }
        return false;
    }

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
    }

    void TriggerCoreShift()
    {
        turnCount++;
        if (turnCount % coreShiftInterval != 0) return;

        lowGravityMode = !lowGravityMode;

        if (lowGravityMode)
            Debug.Log("[CORE SHIFT] Low Gravity — Pawns can jump 3 squares!");
        else
            Debug.Log("[CORE SHIFT] Normal Gravity restored.");

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell != null && cell.IsOccupied)
                    {
                        Vector3 outDir = GetOutwardDir(cell);
                        cell.currentPiece.transform.position = cell.transform.position + outDir * GetPiecePlacementOffset(cell.currentPiece, outDir);
                    }
                }
    }

    IEnumerator AIMove()
    {
        if (gameOver || aiThinking) yield break;

        aiThinking = true;
        Debug.Log("[AI] Thinking...");
        yield return new WaitForSeconds(1.0f); // Slight extra padding for smooth transition

        if (gameOver) { aiThinking = false; yield break; }

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
            ShowWinScreen("White");
            aiThinking = false;
            yield break;
        }

        var kingCapture = allMoves.Find(m => m.to.IsOccupied && m.to.currentPiece.GetComponent<ChessPiece>().pieceType == PieceType.King);
        var anyCapture = allMoves.FindAll(m => m.to.IsOccupied);

        (BoardCell from, BoardCell to) chosen;

        if (kingCapture.from != null)
            chosen = kingCapture;
        else if (anyCapture.Count > 0)
            chosen = anyCapture[Random.Range(0, anyCapture.Count)];
        else
            chosen = allMoves[Random.Range(0, allMoves.Count)];

        ChessPiece aiPiece = chosen.from.currentPiece.GetComponent<ChessPiece>();
        Debug.Log($"[AI] Chose {aiPiece.pieceType} from ({chosen.from.x},{chosen.from.y},{chosen.from.z}) to ({chosen.to.x},{chosen.to.y},{chosen.to.z})");

        // Turn off AI thinking flag right before the move happens 
        // so the system knows the human player's turn phase is fully opening up.
        aiThinking = false;

        // Execute move will change currentTurn to White and automatically trigger SwitchCameraToCurrentTurn()
        ExecuteMove(chosen.from, chosen.to);
    }
}