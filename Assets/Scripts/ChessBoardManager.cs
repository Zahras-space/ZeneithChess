using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardManager : MonoBehaviour
{
    [Header("Camera")]
    public OrbitalCamera cameraController;

    [Header("Win Screen")]
    public WinScreenUI winScreenUI;


    [Header("Audio")]
    public AudioSource clickSound;
    public AudioSource moveSound;

    [Header("HUD")]
    public MoveLogUI moveLogUI;
    public SimpleMoveLogger simpleLogger;

    [Header("Gravity Notification")]
    [Tooltip("The parent Panel GameObject (keep this ACTIVE in the scene)")]
    public GameObject gravityPanel;
    [Tooltip("The Text component inside the panel")]
    public TMPro.TextMeshProUGUI gravityText;

    [Header("References")]
    public GameObject boardParent;

    [Header("Piece Prefabs - White")]
    public GameObject whitePawnPrefab;
    public GameObject whiteRookPrefab;
    public GameObject whiteKnightPrefab;
    public GameObject whiteBishopPrefab;
    public GameObject whiteQueenPrefab;
    public GameObject whiteKingPrefab;

    [Header("Piece Prefabs - Black")]
    public GameObject blackPawnPrefab;
    public GameObject blackRookPrefab;
    public GameObject blackKnightPrefab;
    public GameObject blackBishopPrefab;
    public GameObject blackQueenPrefab;
    public GameObject blackKingPrefab;

    [Header("Settings")]
    public float pieceHeightOffset = 1.0f;
    public float pieceScale = 0.4f;
    public bool aiEnabled = true;

    [Header("AI - Negamax")]
    [Tooltip("How many plies (half-moves) the AI looks ahead. 1 = just the AI's own move (greedy), 2 = AI move + opponent's best reply, 3+ gets progressively slower. 2-3 is a good balance for this board size.")]
    public int aiSearchDepth = 2;

    [Header("Movement Animation")]
    public float moveDuration = 0.4f;
    public float arcHeight = 1.5f;
    public float bounceDuration = 0.25f;
    public float bounceHeight = 0.3f;

    [Header("Movement Rules")]
    [Tooltip("Maximum number of squares a sliding piece (Rook/Bishop/Queen) can travel in one move.")]
    public int maxSlideDistance = 8;

    private BoardCell[,,] cells = new BoardCell[7, 7, 7];
    private List<BoardCell> frontFaceCells = new List<BoardCell>();
    private List<BoardCell> backFaceCells = new List<BoardCell>();
    private List<BoardCell> topFaceCells = new List<BoardCell>();
    private List<BoardCell> rightFaceCells = new List<BoardCell>();
    private List<BoardCell> leftFaceCells = new List<BoardCell>();
    private List<BoardCell> bottomFaceCells = new List<BoardCell>();
    private BoardCell selectedCell = null;
    private HashSet<BoardCell> highlightedMoves = new HashSet<BoardCell>();
    private Dictionary<BoardCell, Material> originalMaterials = new Dictionary<BoardCell, Material>();
    private HashSet<BoardCell> unsafeKingMoveCells = new HashSet<BoardCell>();

    private HashSet<string> visiblePawnFaces = new HashSet<string>();

    private PieceColor currentTurn = PieceColor.White;
    private bool gameOver = false;
    private bool aiThinking = false;
    private bool isAnimating = false;

    private int turnCount = 0;
    private int coreShiftInterval = 4;
    private bool lowGravityMode = false;

    public Material highlightMaterial;

    [Header("Check Highlight")]
    [Tooltip("Bright red material for the king's cell when in check")]
    public Material checkMaterial;

    [Header("Checkmate Popup")]
    [Tooltip("The CheckmateText (or its wrapping panel) — shown briefly before the win screen appears")]
    public GameObject checkmatePopup;
    [Tooltip("How many seconds to show the checkmate popup before the win screen appears")]
    public float checkmatePopupDuration = 2f;


    private GameObject selectionIndicator;
    private BoardCell checkHighlightedCell = null;
    private Material checkOriginalMaterial = null;

    // ── LIFECYCLE ────────────────────────────────────────────────────
    void Start()
    {
        CollectCells();
        visiblePawnFaces = DetermineVisibleFaces();
        SpawnPieces();
        // Initialize gravity panel to default state
        UpdateGravityPanel("NORMAL GRAVITY", new Color(0.9f, 0.88f, 1f));
        // Defensive: make sure the checkmate popup starts hidden even if
        // someone forgets to disable it in the scene.
        if (checkmatePopup != null)
            checkmatePopup.SetActive(false);
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

            // Single "primary" face label — still exclusive, still used by
            // GetOutwardDir() and pawn-direction logic elsewhere, unchanged.
            if (cz == 6) cell.face = "front";
            else if (cz == 0) cell.face = "back";
            else if (cx == 6) cell.face = "right";
            else if (cx == 0) cell.face = "left";
            else if (cy == 6) cell.face = "top";
            else if (cy == 0) cell.face = "bottom";
            else cell.face = "inner";

            // Face-LIST membership is intentionally inclusive: a cell on a
            // shared edge (e.g. z == 6 AND y == 6) legitimately belongs to
            // both the "front" list and the "top" list. Without this, the
            // old else-if chain above silently starved top/bottom/left/right
            // of their edge cells (see the 25/35-cell counts in the Console),
            // which is exactly why full-width rows failed to spawn.
            if (cz == 6) frontFaceCells.Add(cell);
            if (cz == 0) backFaceCells.Add(cell);
            if (cy == 6) topFaceCells.Add(cell);
            if (cy == 0) bottomFaceCells.Add(cell);
            if (cx == 6) rightFaceCells.Add(cell);
            if (cx == 0) leftFaceCells.Add(cell);
        }

        // Diagnostic: each face should hold exactly 49 cells (7x7) if every
        // BoardCell's x/y/z was assigned correctly in the scene.
        Debug.Log($"[CollectCells] top={topFaceCells.Count}, bottom={bottomFaceCells.Count}, front={frontFaceCells.Count}, back={backFaceCells.Count}, left={leftFaceCells.Count}, right={rightFaceCells.Count} (expected 49 each)");
    }



    // ── PIECE SPAWNING ───────────────────────────────────────────────
    void SpawnPieces()
    {
        // CollectCells() assigns each cell exactly one "face" label using an
        // exclusive priority chain (front/back checked before left/right
        // before top/bottom). That means a cell on a shared edge — e.g.
        // x=0 or x=6 on the top row — gets labeled "left"/"right" instead of
        // "top", even though it's visually and functionally a top-face cell.
        // Movement (GetPawnForwardCell, TryWrapMove) and outward-direction
        // (GetOutwardDir, used by core shift) both key off that single
        // label, so a wrong label silently breaks legal-move generation and
        // sends the piece the wrong way during a core shift.
        //
        // Fix: once we know which two faces are actually being used for
        // this game (White's "top", and whichever face gets chosen for
        // Black below), force every cell on those faces to report the
        // correct label. This corrects exactly the cells that matter for
        // gameplay without touching how faces are chosen or spawned.
        foreach (BoardCell c in topFaceCells)
            c.face = "top";

        SpawnFaceBackRow(topFaceCells, PieceColor.White, "top", rowLine: 5);
        SpawnFacePawnRow(topFaceCells, PieceColor.White, "top", rowLine: 4);

        string blackFace = ChooseVisibleSideFace();
        List<BoardCell> blackFaceCells = GetFaceCells(blackFace);

        foreach (BoardCell c in blackFaceCells)
            c.face = blackFace;

        SpawnFaceBackRow(blackFaceCells, PieceColor.Black, blackFace, rowLine: 1);
        SpawnFacePawnRow(blackFaceCells, PieceColor.Black, blackFace, rowLine: 2);
    }

    void SpawnBackRow(List<BoardCell> faceCells, PieceColor color, int rowY)
    {
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        row.Sort((a, b) => a.x.CompareTo(b.x));

        PieceType?[] layout = GetBackRowLayout();

        for (int i = 0; i < row.Count && i < layout.Length; i++)
            if (layout[i].HasValue)
                PlacePiece(layout[i].Value, color, row[i]);
    }

    void SpawnPawnRow(List<BoardCell> faceCells, PieceColor color, int rowY)
    {
        List<BoardCell> row = faceCells.FindAll(c => c.y == rowY);
        row.Sort((a, b) => a.x.CompareTo(b.x));
        foreach (BoardCell cell in row)
            PlacePiece(PieceType.Pawn, color, cell);
    }

    void SpawnFaceBackRow(List<BoardCell> faceCells, PieceColor color, string faceName, int rowLine)
    {
        List<BoardCell> row = GetFaceRow(faceCells, faceName, rowLine);

        // Diagnostic: warn loudly if the row doesn't have the full 7 cells
        // expected on a 7x7 face — this is what causes pieces to bunch up
        // at one end instead of spanning the whole row.
        if (row.Count != 7)
        {
            Debug.LogWarning($"[SpawnFaceBackRow] {color} back row on face '{faceName}' at rowLine {rowLine} only found {row.Count} cell(s) instead of 7. " +
                              "This means the BoardCell x/y/z values for this face are not fully populated in the scene — check the BoardCell components under boardParent.");
        }

        PieceType?[] layout = GetBackRowLayout();
        Vector3 outDir = GetOutwardDirForFace(faceName);

        for (int i = 0; i < row.Count && i < layout.Length; i++)
            if (layout[i].HasValue)
                PlacePiece(layout[i].Value, color, row[i], outDir);
    }

    void SpawnFacePawnRow(List<BoardCell> faceCells, PieceColor color, string faceName, int rowLine)
    {
        List<BoardCell> row = GetFaceRow(faceCells, faceName, rowLine);

        if (row.Count != 7)
        {
            Debug.LogWarning($"[SpawnFacePawnRow] {color} pawn row on face '{faceName}' at rowLine {rowLine} only found {row.Count} cell(s) instead of 7. " +
                              "This means the BoardCell x/y/z values for this face are not fully populated in the scene — check the BoardCell components under boardParent.");
        }

        Vector3 outDir = GetOutwardDirForFace(faceName);

        foreach (BoardCell cell in row)
            PlacePiece(PieceType.Pawn, color, cell, outDir);
    }

    // Full 7-wide back rank: Rook, Knight, Bishop, Queen, King, Bishop, Knight.
    // A 7x7 face can only hold 7 pieces per rank, so this keeps King/Queen
    // centered with a full mirrored set of minor pieces, at the cost of a
    // single Rook instead of two.
    PieceType?[] GetBackRowLayout()
    {
        return new PieceType?[]
        {
            PieceType.Rook,
            PieceType.Knight,
            PieceType.Bishop,
            PieceType.Queen,
            PieceType.King,
            PieceType.Bishop,
            PieceType.Knight
        };
    }

    List<BoardCell> GetFaceRow(List<BoardCell> faceCells, string faceName, int rowLine)
    {
        List<BoardCell> row = faceName switch
        {
            "top" => faceCells.FindAll(c => c.z == rowLine),
            "bottom" => faceCells.FindAll(c => c.z == rowLine),
            "left" => faceCells.FindAll(c => c.z == rowLine),
            "right" => faceCells.FindAll(c => c.z == rowLine),
            "front" => faceCells.FindAll(c => c.y == rowLine),
            "back" => faceCells.FindAll(c => c.y == rowLine),
            _ => new List<BoardCell>()
        };

        row.Sort(faceName switch
        {
            "top" or "bottom" or "front" or "back" => (Comparison<BoardCell>)((a, b) => a.x.CompareTo(b.x)),
            "left" or "right" => (Comparison<BoardCell>)((a, b) => a.y.CompareTo(b.y)),
            _ => (Comparison<BoardCell>)((a, b) => a.x.CompareTo(b.x))
        });

        return row;
    }

    List<BoardCell> GetFaceCells(string faceName)
    {
        return faceName switch
        {
            "front" => frontFaceCells,
            "back" => backFaceCells,
            "top" => topFaceCells,
            "right" => rightFaceCells,
            "left" => leftFaceCells,
            "bottom" => bottomFaceCells,
            _ => rightFaceCells
        };
    }

    string ChooseVisibleSideFace()
    {
        Dictionary<string, float> scores = DetermineFaceScores();
        string[] sideFaces = { "front", "right", "left", "back" };

        string bestFace = "right";
        float bestScore = float.MinValue;

        foreach (string face in sideFaces)
        {
            if (!scores.TryGetValue(face, out float score)) continue;
            if (score > bestScore)
            {
                bestScore = score;
                bestFace = face;
            }
        }

        return bestFace;
    }

    HashSet<string> DetermineVisibleFaces()
    {
        Dictionary<string, float> scores = DetermineFaceScores();
        HashSet<string> faces = new HashSet<string>();
        foreach (var pair in scores)
            if (pair.Value > 0f)
                faces.Add(pair.Key);
        return faces;
    }

    Dictionary<string, float> DetermineFaceScores()
    {
        Vector3 cameraPosition = Camera.main != null ? Camera.main.transform.position : (cameraController != null ? cameraController.transform.position : Vector3.zero);
        Vector3 center = boardParent.transform.position;
        Vector3 viewDir = (cameraPosition - center).normalized;

        var normals = new Dictionary<string, Vector3>
        {
            { "front", boardParent.transform.forward },
            { "back", -boardParent.transform.forward },
            { "right", boardParent.transform.right },
            { "left", -boardParent.transform.right },
            { "top", boardParent.transform.up },
            { "bottom", -boardParent.transform.up }
        };

        var scores = new Dictionary<string, float>();
        foreach (var pair in normals)
            scores[pair.Key] = Vector3.Dot(pair.Value, viewDir);

        return scores;
    }

    void PlacePiece(PieceType type, PieceColor color, BoardCell cell)
    {
        PlacePiece(type, color, cell, GetOutwardDir(cell));
    }

    // Used when spawning a row: uses the FACE THE ROW BELONGS TO for outward
    // direction, rather than the cell's own single "face" label, which can
    // be wrong for cells on a shared edge (e.g. a top-row cell at x=0 also
    // qualifies as "left" and would otherwise get pushed sideways instead
    // of upward).
    void PlacePiece(PieceType type, PieceColor color, BoardCell cell, Vector3 outDir)
    {
        GameObject prefab = GetPrefab(type, color);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlacePiece] No prefab assigned for {color} {type} — check the Piece Prefabs fields in the Inspector.");
            return;
        }

        GameObject go = Instantiate(prefab, cell.transform.position, Quaternion.identity);
        go.name = $"{color}_{type}_{cell.x}{cell.y}{cell.z}";

        go.transform.up = outDir;
        go.transform.localScale = Vector3.one * pieceScale;

        Vector3 spawnPos = cell.transform.position + outDir * GetPiecePlacementOffset(go, outDir, type);
        go.transform.position = spawnPos;

        ChessPiece cp = go.AddComponent<ChessPiece>();
        cp.pieceType = type;
        cp.pieceColor = color;
        cp.currentCell = cell;
        cell.currentPiece = go;
    }

    // ── OUTWARD DIRECTION ─────────────────────────────────────────────
    Vector3 GetOutwardDir(BoardCell cell)
    {
        // Use the cell's true geometric position on the cube rather than its
        // stored `face` label. `cell.face` is assigned once, exclusively, by
        // CollectCells()/SpawnPieces(), and is only guaranteed correct for the
        // two faces pieces initially spawn on — cells on any of the other four
        // faces (which pieces absolutely do travel across, via the wraparound
        // sliding/pawn logic) can carry a stale/wrong label. GetOutwardDir
        // drives both a piece's landing position AND — critically — its
        // transform.up once it lands, which every subsequent sliding/pawn
        // move calculation reads back via GetFaceFromNormal(). A wrong label
        // here silently corrupts which face the piece "thinks" it's on for
        // its NEXT move, which maps that move's local 2D direction onto the
        // wrong pair of world axes — exactly what caused pieces to teleport
        // to unrelated squares after sliding across less-common faces.
        // GetCellFace() derives the face straight from the cell's actual
        // position, so it can never go stale like the stored label can.
        return GetOutwardDirForFace(GetCellFace(cell));
    }

    Vector3 GetOutwardDirForFace(string faceName)
    {
        Transform t = boardParent.transform;
        return faceName switch
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

    float GetPiecePlacementOffset(GameObject piece, Vector3 outDir, PieceType type)
    {
        return pieceHeightOffset;
    }

    GameObject GetPrefab(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
            return type switch
            {
                PieceType.Rook => whiteRookPrefab,
                PieceType.Knight => whiteKnightPrefab,
                PieceType.Bishop => whiteBishopPrefab,
                PieceType.Queen => whiteQueenPrefab,
                PieceType.King => whiteKingPrefab,
                _ => whitePawnPrefab
            };
        else
            return type switch
            {
                PieceType.Rook => blackRookPrefab,
                PieceType.Knight => blackKnightPrefab,
                PieceType.Bishop => blackBishopPrefab,
                PieceType.Queen => blackQueenPrefab,
                PieceType.King => blackKingPrefab,
                _ => blackPawnPrefab
            };
    }

    // ── INPUT ─────────────────────────────────────────────────────────
    void Update()
    {
        if (gameOver || aiThinking || isAnimating) return;
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
                Debug.Log($"[Select] {piece.pieceColor} {piece.pieceType} at ({clickedCell.x},{clickedCell.y},{clickedCell.z})");
                HighlightMoves(clickedCell);
                Debug.Log($"[Moves] {highlightedMoves.Count} legal moves available.");
                clickSound?.Play();
            }
        }
    }

    // ── CAMERA ───────────────────────────────────────────────────────
    void SwitchCameraToCurrentTurn()
    {
        // Keep the camera fixed; do not rotate to a new face during turns.
    }

    // ── MOVE EXECUTION ────────────────────────────────────────────────
    void ExecuteMove(BoardCell from, BoardCell to)
    {
        if (from == null || to == null) return;
        ClearCheckHighlight(); // remove stale check highlight before the response move

        ChessPiece movingPiece = from.currentPiece.GetComponent<ChessPiece>();
        bool wasCapture = to.IsOccupied;
        string capturedInfo = "";
        GameObject capturedPieceObj = to.currentPiece;

        if (wasCapture)
        {
            ChessPiece captured = to.currentPiece.GetComponent<ChessPiece>();
            capturedInfo = $" (captures {captured.pieceColor} {captured.pieceType})";
            Debug.Log($"[Capture] {captured.pieceColor} {captured.pieceType} captured!");
            StartCoroutine(CaptureEffect(to.currentPiece, GetOutwardDir(to)));
            to.currentPiece = null;
        }

        string moveText = $"{movingPiece.pieceType} ({from.x},{from.y},{from.z}) → ({to.x},{to.y},{to.z}){capturedInfo}";
        moveLogUI?.LogMove(movingPiece.pieceColor, moveText, wasCapture);

        // Friendly, player-readable version of the same move, sent to the simple on-screen logger
        string friendlyMoveText = BuildFriendlyMoveText(movingPiece, from, to, wasCapture, capturedPieceObj);
        simpleLogger?.LogMove(movingPiece.pieceColor, friendlyMoveText, wasCapture);

        Vector3 outDir = GetOutwardDir(to);
        Vector3 targetPos = to.transform.position + outDir * GetPiecePlacementOffset(from.currentPiece, outDir, movingPiece.pieceType);
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, outDir);

        // Update board references immediately so logic stays correct
        to.currentPiece = from.currentPiece;
        from.currentPiece = null;
        movingPiece.currentCell = to;
        movingPiece.hasMoved = true;

        moveSound?.Play();
        ClearHighlights();
        selectedCell = null;

        // Animate — all post-move logic runs in callback after animation
        StartCoroutine(AnimateMoveSequence(to.currentPiece, targetPos, targetRot, outDir, () =>
        {
            CheckWinCondition();
            if (gameOver) return;

            TriggerCoreShift();

            currentTurn = (currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            Debug.Log($"[Turn] {currentTurn}'s turn");
            simpleLogger?.LogTurnBanner(currentTurn);
            SwitchCameraToCurrentTurn();

            CheckForCheckmate();
            if (gameOver) return;

            if (currentTurn == PieceColor.Black && aiEnabled)
                StartCoroutine(AIMove());
        }));
    }

    // ── FRIENDLY MOVE TEXT ───────────────────────────────────────────
    string BuildFriendlyMoveText(ChessPiece piece, BoardCell from, BoardCell to, bool wasCapture, GameObject capturedPieceObj)
    {
        string pieceName = FormatPieceName(piece.pieceType);
        string fromFace = FormatFaceName(from.face);
        string toFace = FormatFaceName(to.face);

        string location = (from.face == to.face)
            ? $"on the {toFace} face"
            : $"from {fromFace} to {toFace}";

        if (wasCapture)
        {
            ChessPiece captured = capturedPieceObj != null ? capturedPieceObj.GetComponent<ChessPiece>() : null;
            string capturedName = captured != null ? FormatPieceName(captured.pieceType) : "a piece";
            return $"{pieceName} captures {capturedName} ({location})";
        }

        return $"{pieceName} moves {location}";
    }

    string FormatPieceName(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => "Pawn",
            PieceType.Rook => "Rook",
            PieceType.Knight => "Knight",
            PieceType.Bishop => "Bishop",
            PieceType.Queen => "Queen",
            PieceType.King => "King",
            _ => type.ToString()
        };
    }

    string FormatFaceName(string face)
    {
        return face switch
        {
            "front" => "Front",
            "back" => "Back",
            "left" => "Left",
            "right" => "Right",
            "top" => "Top",
            "bottom" => "Bottom",
            _ => "Center"
        };
    }

    // ── MOVEMENT ANIMATIONS ───────────────────────────────────────────

    // Master sequence: arc move → bounce → callback
    IEnumerator AnimateMoveSequence(GameObject piece, Vector3 targetPos,
                                    Quaternion targetRot, Vector3 outDir,
                                    System.Action onComplete)
    {
        isAnimating = true;

        TrailRenderer trail = AddTrail(piece);
        yield return StartCoroutine(AnimateMove(piece, targetPos, targetRot));
        if (trail != null) trail.enabled = false;

        yield return StartCoroutine(BounceEffect(piece, targetPos, outDir));

        isAnimating = false;
        onComplete?.Invoke();
    }

    // Arc movement
    IEnumerator AnimateMove(GameObject piece, Vector3 targetPos,
                             Quaternion targetRot)
    {
        Vector3 startPos = piece.transform.position;
        Quaternion startRot = piece.transform.rotation;
        Vector3 upDir = piece.transform.up;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            Vector3 pos = Vector3.Lerp(startPos, targetPos, smooth);
            float arc = Mathf.Sin(smooth * Mathf.PI) * arcHeight;
            pos += upDir * arc;

            piece.transform.position = pos;
            piece.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
            yield return null;
        }

        piece.transform.position = targetPos;
        piece.transform.rotation = targetRot;
    }

    // Bounce on landing
    IEnumerator BounceEffect(GameObject piece, Vector3 landPos, Vector3 outDir)
    {
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDuration;
            float bounce = Mathf.Sin(t * Mathf.PI * 2f) * bounceHeight * (1f - t);
            piece.transform.position = landPos + outDir * bounce;
            yield return null;
        }

        piece.transform.position = landPos;
    }

    // Capture effect — piece flies outward and shrinks
    IEnumerator CaptureEffect(GameObject captured, Vector3 outDir)
    {
        float elapsed = 0f;
        float duration = 0.3f;
        Vector3 startPos = captured.transform.position;
        Vector3 flyDir = (outDir + Vector3.up).normalized;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            captured.transform.position = startPos + flyDir * t * 2f;
            captured.transform.localScale = Vector3.one * pieceScale * (1f - t);
            yield return null;
        }

        Destroy(captured);
    }

    // Trail during movement
    TrailRenderer AddTrail(GameObject piece)
    {
        if (piece == null) return null;

        TrailRenderer trail = piece.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = piece.AddComponent<TrailRenderer>();

        trail.time = 0.3f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(0f, 1f, 0.5f, 1f);
        trail.endColor = new Color(0f, 1f, 0.5f, 0f);
        trail.enabled = true;
        return trail;
    }

    // ── SELECTION INDICATOR ───────────────────────────────────────────
    void ShowSelectionIndicator(BoardCell cell)
    {
        if (selectionIndicator == null)
        {
            selectionIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            selectionIndicator.transform.localScale = Vector3.one * 0.2f;
            Destroy(selectionIndicator.GetComponent<Collider>());

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 1f, 0f, 0.8f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0f));
            selectionIndicator.GetComponent<Renderer>().material = mat;
        }

        // Same reasoning as TriggerCoreShift: the piece here is already
        // resting on a specific face, so reuse its own established
        // orientation rather than re-deriving a (potentially ambiguous,
        // for edge cells) face from raw position.
        Vector3 outDir = cell.currentPiece.transform.up;
        selectionIndicator.transform.position =
            cell.transform.position + outDir *
            (GetPiecePlacementOffset(cell.currentPiece, outDir, cell.currentPiece.GetComponent<ChessPiece>().pieceType) + 0.4f);

        selectionIndicator.SetActive(true);
    }

    void HideSelectionIndicator()
    {
        if (selectionIndicator != null)
            selectionIndicator.SetActive(false);
    }

// ── HIGHLIGHTING ──────────────────────────────────────────────────
    void HighlightMoves(BoardCell cell)
    {
        // Defensive: always clear any previous highlights first, even if
        // the caller already did. Without this, if HighlightMoves() ever
        // runs while a cell is still green from a prior selection, its
        // "original" material gets captured as green itself — which is
        // exactly what causes cells to stay stuck highlighted after a move.
        ClearHighlights();

        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();

        if (piece != null && piece.pieceType == PieceType.King)
        {
            // The King is special-cased: rather than silently dropping any
            // square that would walk into check (what GetLegalMovesSafe
            // does for every other piece), show EVERY one-square direction
            // the King could physically move to, and colour each one
            // according to whether it's actually safe. Squares that would
            // put the King in check are shown in the existing check-red
            // material for visibility, but are deliberately left out of
            // `highlightedMoves` so HandleClick() can't select them —
            // they're informational only, not legal destinations.
            List<BoardCell> pseudoMoves = GetKingMoves(cell, piece);
            pseudoMoves.RemoveAll(m => m != null && m.IsOccupied &&
                m.currentPiece.GetComponent<ChessPiece>().pieceColor == piece.pieceColor);

            foreach (BoardCell move in pseudoMoves)
            {
                Renderer r = move.GetComponent<Renderer>();
                if (r == null) continue;

                if (!originalMaterials.ContainsKey(move))
                    originalMaterials[move] = r.material;

                if (WouldBeInCheckAfterMove(cell, move, piece.pieceColor))
                {
                    r.material = checkMaterial;
                    unsafeKingMoveCells.Add(move);
                    PieceColor enemyColor = (piece.pieceColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                    if (IsCellAttacked(move, enemyColor))
                    Debug.Log($"[KingSafety] ({move.x},{move.y},{move.z}) is defended by at least one {enemyColor} piece — king capture there is correctly blocked.");

                }
                else
                {
                    r.material = highlightMaterial;
                    highlightedMoves.Add(move);
                }
            }
        }
        else
        {
            highlightedMoves = new HashSet<BoardCell>(GetLegalMovesSafe(cell));
            foreach (BoardCell move in highlightedMoves)
            {
                Renderer r = move.GetComponent<Renderer>();
                if (r == null) continue;

                // Only capture the "original" material the first time — never
                // overwrite it while it might already be the highlight material.
                if (!originalMaterials.ContainsKey(move))
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
        foreach (BoardCell cell in unsafeKingMoveCells)
        {
            Renderer r = cell.GetComponent<Renderer>();
            if (r != null && originalMaterials.ContainsKey(cell))
                r.material = originalMaterials[cell];
        }
        highlightedMoves.Clear();
        unsafeKingMoveCells.Clear();
        originalMaterials.Clear();
    }


    // ── FACE DETECTION ───────────────────────────────────────────────
    // Determines what face a cell is on based on its physical position.
    // This completely bypasses the cell.face overwrite bug!
    string GetCellFace(BoardCell cell)
    {
        if (cell == null) return "top";
        Vector3 center = boardParent.transform.position;
        Vector3 dir = (cell.transform.position - center).normalized;

        Transform t = boardParent.transform;

        // Compare the direction to the board's 6 directional vectors
        float dotUp = Vector3.Dot(dir, t.up);
        float dotDown = Vector3.Dot(dir, -t.up);
        float dotForward = Vector3.Dot(dir, t.forward);
        float dotBack = Vector3.Dot(dir, -t.forward);
        float dotRight = Vector3.Dot(dir, t.right);
        float dotLeft = Vector3.Dot(dir, -t.right);

        float maxDot = Mathf.Max(dotUp, dotDown, dotForward, dotBack, dotRight, dotLeft);

        if (maxDot == dotUp) return "top";
        if (maxDot == dotDown) return "bottom";
        if (maxDot == dotForward) return "front";
        if (maxDot == dotBack) return "back";
        if (maxDot == dotRight) return "right";
        if (maxDot == dotLeft) return "left";

        return "top"; // Fallback
    }

    // ── FACE DETECTION ───────────────────────────────────────────────
    // Determines what face a piece is on based on its physical orientation.
    string GetFaceFromNormal(Vector3 normal)
    {
        Transform t = boardParent.transform;
        if (Vector3.Angle(normal, t.up) < 45f) return "top";
        if (Vector3.Angle(normal, -t.up) < 45f) return "bottom";
        if (Vector3.Angle(normal, t.forward) < 45f) return "front";
        if (Vector3.Angle(normal, -t.forward) < 45f) return "back";
        if (Vector3.Angle(normal, t.right) < 45f) return "right";
        if (Vector3.Angle(normal, -t.right) < 45f) return "left";
        return "top"; // Fallback
    }


    // ── CHECK HIGHLIGHT ───────────────────────────────────────────────
    void ShowCheckHighlight(BoardCell kingCell)
    {
        if (kingCell == null) return;
        if (checkHighlightedCell == kingCell) return; // already red

        ClearCheckHighlight();

        Renderer r = kingCell.GetComponent<Renderer>();
        if (r == null) return;

        checkOriginalMaterial = r.material;
        checkHighlightedCell = kingCell;
        r.material = checkMaterial;
    }

    void ClearCheckHighlight()
    {
        if (checkHighlightedCell != null)
        {
            Renderer r = checkHighlightedCell.GetComponent<Renderer>();
            if (r != null && checkOriginalMaterial != null)
                r.material = checkOriginalMaterial;
        }
        checkHighlightedCell = null;
        checkOriginalMaterial = null;
    }

    // ── LEGAL MOVES ───────────────────────────────────────────────────
    List<BoardCell> GetLegalMoves(BoardCell cell)
    {
        if (!cell.IsOccupied) return new List<BoardCell>();

        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
        List<BoardCell> moves = piece.pieceType switch
        {
            PieceType.Pawn => GetPawnMoves(cell, piece),
            PieceType.Rook => GetSlidingMoves(cell, piece, true, false),
            PieceType.Bishop => GetSlidingMoves(cell, piece, false, true),
            PieceType.Queen => GetSlidingMoves(cell, piece, true, true),
            PieceType.King => GetKingMoves(cell, piece),
            PieceType.Knight => GetKnightMoves(cell, piece),
            _ => new List<BoardCell>()
        };

        moves.RemoveAll(m => m != null && m.IsOccupied &&
            m.currentPiece.GetComponent<ChessPiece>().pieceColor == piece.pieceColor);

        return moves;
    }

    List<BoardCell> GetPawnMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();
        string pieceFace = GetFaceFromNormal(piece.transform.up);

        // ── Forward moves ─────────────────────────────────────────────
        // Each successive forward step threads the FACE RETURNED BY THE
        // PREVIOUS STEP into the next call, instead of re-deriving the
        // face from the piece's own (unchanged, since it hasn't actually
        // moved yet) transform.up every time. Without this, a double/triple
        // pawn move whose FIRST step already wraps onto a different face
        // would compute its second/third step using the wrong face's
        // coordinate convention — landing on a square that isn't truly the
        // next one forward, which is exactly what made the highlighted
        // "legal move" appear one square further away than it should,
        // skipping the real adjacent square for edge-row pawns.
        BoardCell fwd = GetPawnForwardCell(cell, piece, pieceFace, out string fwdFace);
        if (fwd != null && !fwd.IsOccupied)
        {
            moves.Add(fwd);

            BoardCell dbl = GetPawnForwardCell(fwd, piece, fwdFace, out string dblFace);
            if (dbl != null && !dbl.IsOccupied)
            {
                if (!piece.hasMoved || lowGravityMode)
                {
                    moves.Add(dbl);

                    if (lowGravityMode)
                    {
                        BoardCell triple = GetPawnForwardCell(dbl, piece, dblFace, out _);
                        if (triple != null && !triple.IsOccupied)
                            moves.Add(triple);
                    }
                }
            }
        }

        // ── Diagonal captures (face-aware) ──────────────────────────
        // Always exactly one step from the piece's actual resting cell, so
        // using pieceFace directly here (no threading needed) is correct.
        Vector2Int forwardOffset = GetPawnForwardOffset(pieceFace, piece.pieceColor);
        Vector2Int leftDiag = forwardOffset + new Vector2Int(-forwardOffset.y, forwardOffset.x);
        Vector2Int rightDiag = forwardOffset + new Vector2Int(forwardOffset.y, -forwardOffset.x);

        foreach (Vector2Int diagOffset in new[] { leftDiag, rightDiag })
        {
            BoardCell diag = GetWrappedCell(cell, diagOffset, pieceFace, out _);
            if (diag != null && diag.IsOccupied)
            {
                ChessPiece target = diag.currentPiece.GetComponent<ChessPiece>();
                if (target.pieceColor != piece.pieceColor)
                    if (diag.x != cell.x || diag.y != cell.y || diag.z != cell.z)
                        moves.Add(diag);
            }
        }

        moves.RemoveAll(m => m == null || !visiblePawnFaces.Contains(m.face));
        return moves;
    }

    // Steps forward from `cell`, interpreting it as being on `fromFace`
    // (rather than re-deriving the face from the piece's own orientation,
    // which is stale for any step past the first once a wrap has occurred).
    // Outputs the face the landed cell is actually on, so the caller can
    // thread it into the NEXT forward step if needed (double/triple move).
BoardCell GetPawnForwardCell(BoardCell cell, ChessPiece piece, string fromFace, out string resultFace, int step = 1)
{
    resultFace = fromFace;
    if (cell == null || piece == null) return null;

    Vector2Int baseMove = GetPawnForwardOffset(fromFace, piece.pieceColor);
    BoardCell result = GetWrappedCell(cell, new Vector2Int(baseMove.x * step, baseMove.y * step), fromFace, out string newFace);
    resultFace = newFace;
    return result;
}

Vector2Int GetPawnForwardOffset(string face, PieceColor color)
{
    return (face, color) switch
    {
        // White on top → toward decreasing Z
        ("top",    PieceColor.White) => new Vector2Int(0, -1),
        ("top",    PieceColor.Black) => new Vector2Int(0,  1),

        // Bottom face → rowLine is Z
        ("bottom", PieceColor.White) => new Vector2Int(0,  1),
        ("bottom", PieceColor.Black) => new Vector2Int(0, -1),

        // Front face → rowLine is Y; both colors advance toward +Y (top edge)
        ("front",  PieceColor.White) => new Vector2Int(0,  1),
        ("front",  PieceColor.Black) => new Vector2Int(0,  1),

        // Back face → rowLine is Y; White would go -Y, Black goes +Y
        ("back",   PieceColor.White) => new Vector2Int(0, -1),
        ("back",   PieceColor.Black) => new Vector2Int(0,  1),

        // Left face → rowLine is Z; both colors advance toward +Z (front edge)
        ("left",   PieceColor.White) => new Vector2Int(1,  0),
        ("left",   PieceColor.Black) => new Vector2Int(1,  0),

        // Right face → rowLine is Z; White would go -Z, Black goes +Z
        ("right",  PieceColor.White) => new Vector2Int(-1, 0),
        ("right",  PieceColor.Black) => new Vector2Int( 1, 0),

        _ => new Vector2Int(0, -1)
    };
}

    List<BoardCell> GetKingMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();
        string pieceFace = GetFaceFromNormal(piece.transform.up);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                BoardCell c = GetWrappedCell(cell, new Vector2Int(dx, dy), pieceFace, out _);
                if (c != null) moves.Add(c);
            }
        return moves;
    }

    List<BoardCell> GetKnightMoves(BoardCell cell, ChessPiece piece)
    {
        List<BoardCell> moves = new List<BoardCell>();
        string pieceFace = GetFaceFromNormal(piece.transform.up);
        Vector2Int[] offsets =
        {
            new Vector2Int( 1, 2), new Vector2Int( 2, 1),
            new Vector2Int(-1, 2), new Vector2Int(-2, 1),
            new Vector2Int( 1,-2), new Vector2Int( 2,-1),
            new Vector2Int(-1,-2), new Vector2Int(-2,-1)
        };
        foreach (Vector2Int off in offsets)
        {
            BoardCell c = GetWrappedCell(cell, off, pieceFace, out _);
            if (c != null) moves.Add(c);
        }
        return moves;
    }

    List<BoardCell> GetSlidingMoves(BoardCell cell, ChessPiece piece, bool straight, bool diagonal)
    {
        List<BoardCell> moves = new List<BoardCell>();
        if (cell == null || !cell.IsOccupied) return moves;
        if (piece == null) return moves;

        // Use the piece's physical orientation to determine the starting face.
        // This is 100% immune to the cell.face overwrite bug.
        string startFace = GetFaceFromNormal(piece.transform.up);

        List<Vector2Int> dirs = new List<Vector2Int>();
        if (straight)
        {
            dirs.Add(new Vector2Int(1, 0));
            dirs.Add(new Vector2Int(-1, 0));
            dirs.Add(new Vector2Int(0, 1));
            dirs.Add(new Vector2Int(0, -1));
        }
        if (diagonal)
        {
            dirs.Add(new Vector2Int(1, 1));
            dirs.Add(new Vector2Int(-1, 1));
            dirs.Add(new Vector2Int(1, -1));
            dirs.Add(new Vector2Int(-1, -1));
        }

        Vector3Int startPos = new Vector3Int(cell.x, cell.y, cell.z);

        foreach (Vector2Int localDir in dirs)
        {
            // Every step of this ray re-derives its own 3D direction from
            // StepOnSurface below, so a diagonal (Bishop/Queen) that
            // crosses one or more cube edges keeps rotating onto the
            // correct new diagonal each time, instead of reusing a single
            // face-local Vector2Int the whole way (the old bug).
            Vector3Int pos = startPos;
            Vector3Int dir3 = LocalDirToWorld3D(startFace, localDir);
            string face = startFace;

            for (int step = 0; step < maxSlideDistance; step++)
            {
                bool ok = StepOnSurface(pos, dir3, face, out Vector3Int nextPos, out Vector3Int nextDir3, out string nextFace);
                if (!ok) break; // ran off the surface, or hit an exact cube corner (direction is ambiguous there)

                BoardCell next = GetCellOnFace(nextFace, nextPos.x, nextPos.y, nextPos.z);
                if (next == null || next == cell) break;

                if (next.IsOccupied)
                {
                    ChessPiece occupant = next.currentPiece.GetComponent<ChessPiece>();
                    if (occupant != null && occupant.pieceColor != piece.pieceColor)
                    {
                        moves.Add(next);
                    }
                    break; // Stop sliding in this direction — blocked, regardless of color
                }

                moves.Add(next);
                pos = nextPos;
                dir3 = nextDir3;
                face = nextFace;
            }
        }

        return moves;
    }

    // ── WRAP AROUND ───────────────────────────────────────────────────
    // The board is a literal axis-aligned cube (cell.x/y/z each 0-6), so
    // instead of hand-writing 24 separate face-pair transition rules (the
    // old TryWrapMove, which is what dropped a direction's tangential
    // component whenever a move crossed an edge — the exact bug that made
    // diagonal Bishop/Queen moves turn sideways), movement is expressed as
    // a real 3D grid vector and edge crossings are handled with one
    // general rule: rotate that vector 90° around the shared edge. This is
    // the same thing that happens physically if you draw a straight line
    // across a paper cube net and fold it up — the line keeps going in a
    // single, unambiguous direction across every face it crosses.

    // Which grid axis (0=X, 1=Y, 2=Z) and which side (+1/-1) a face's
    // outward normal points along.
    void FaceToNormalAxis(string face, out int axis, out int sign)
    {
        switch (face)
        {
            case "right": axis = 0; sign = 1; break;
            case "left": axis = 0; sign = -1; break;
            case "top": axis = 1; sign = 1; break;
            case "bottom": axis = 1; sign = -1; break;
            case "front": axis = 2; sign = 1; break;
            case "back": axis = 2; sign = -1; break;
            default: axis = 1; sign = 1; break;
        }
    }

    string NormalAxisToFace(int axis, int sign)
    {
        if (axis == 0) return sign > 0 ? "right" : "left";
        if (axis == 1) return sign > 0 ? "top" : "bottom";
        return sign > 0 ? "front" : "back";
    }

    // Converts a face-local 2D direction (as used everywhere else in this
    // file — Pawn/King/Knight offsets, Rook/Bishop/Queen ray directions)
    // into a real 3D grid direction. Kept consistent with the axis
    // meanings the rest of the file already uses per face (front/back:
    // x,y — top/bottom: x,z — left/right: z,y).
    Vector3Int LocalDirToWorld3D(string face, Vector2Int dir)
    {
        switch (face)
        {
            case "front":
            case "back":
                return new Vector3Int(dir.x, dir.y, 0);
            case "top":
            case "bottom":
                return new Vector3Int(dir.x, 0, dir.y);
            case "left":
            case "right":
                return new Vector3Int(0, dir.y, dir.x);
            default:
                return new Vector3Int(dir.x, dir.y, 0);
        }
    }

    // Takes one step on the cube's surface from (pos, dir3) on the given
    // face. dir3 must be tangent to that face (its component along the
    // face's normal axis is 0). If the step stays within the face, it's
    // returned unchanged. If it crosses exactly one edge, dir3 is rotated
    // 90° around that edge so it keeps pointing the same physical
    // direction on the new face — this is what makes a diagonal continue
    // as a diagonal instead of getting bent sideways. If the step would
    // cross two edges at once (an exact cube corner), the direction is
    // genuinely ambiguous, so the slide simply stops there.
    bool StepOnSurface(Vector3Int pos, Vector3Int dir3, string face,
                        out Vector3Int newPos, out Vector3Int newDir3, out string newFace)
    {
        const int MAX = 6;
        Vector3Int candidate = pos + dir3;
        newPos = candidate;
        newDir3 = dir3;
        newFace = face;

        int overflowCount = 0;
        int overflowAxis = -1;
        int overflowSign = 0;

        for (int axis = 0; axis < 3; axis++)
        {
            int v = candidate[axis];
            if (v > MAX) { overflowCount++; overflowAxis = axis; overflowSign = 1; }
            else if (v < 0) { overflowCount++; overflowAxis = axis; overflowSign = -1; }
        }

        if (overflowCount == 0)
            return true; // stayed on the same face

        if (overflowCount > 1)
            return false; // hit a corner exactly — ambiguous, stop the slide here

        FaceToNormalAxis(face, out int oldNormalAxis, out _);
        int A = overflowAxis;                    // axis that overflowed — becomes the new face's normal
        int E = 3 - A - oldNormalAxis;            // the remaining axis — the shared edge, unchanged in direction
        int oldBoundaryValue = pos[oldNormalAxis]; // 0 or MAX on the face we're leaving

        Vector3Int fixedPos = pos;
        fixedPos[A] = overflowSign > 0 ? MAX : 0;
        fixedPos[E] = pos[E] + dir3[E];
        fixedPos[oldNormalAxis] = (oldBoundaryValue == MAX) ? MAX - 1 : 1;

        if (fixedPos[E] < 0 || fixedPos[E] > MAX)
            return false; // would also spill off the edge axis — corner case, stop here

        Vector3Int fixedDir = Vector3Int.zero;
        fixedDir[E] = dir3[E];                                    // tangential component carries straight across
        fixedDir[oldNormalAxis] = (oldBoundaryValue == MAX) ? -1 : 1; // the crossing component rotates into the old normal
        fixedDir[A] = 0;                                          // A is now the new face's normal — no longer tangent

        newPos = fixedPos;
        newDir3 = fixedDir;
        newFace = NormalAxisToFace(A, overflowSign);
        return true;
    }

    BoardCell GetWrappedCell(BoardCell fromCell, Vector2Int dir, string fromFace, out string newFace)
    {
        newFace = fromFace;
        if (fromCell == null) return null;

        Vector3Int pos = new Vector3Int(fromCell.x, fromCell.y, fromCell.z);
        Vector3Int dir3 = LocalDirToWorld3D(fromFace, dir);

        bool ok = StepOnSurface(pos, dir3, fromFace, out Vector3Int newPos, out _, out newFace);
        if (!ok) return null;

        return GetCellOnFace(newFace, newPos.x, newPos.y, newPos.z);
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
        ClearCheckHighlight();
        Debug.Log($"[WIN] GAME OVER — {winner} wins!");
        winScreenUI?.ShowWinner(winner);

    }

    // ── CHECK / CHECKMATE ────────────────────────────────────────────
    BoardCell FindKingCell(PieceColor color)
    {
        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;
                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece != null && piece.pieceType == PieceType.King && piece.pieceColor == color)
                        return cell;
                }
        return null;
    }

    // Does any piece of 'byColor' currently attack 'target'? Pawns are
    // handled separately since their diagonal attack squares count even
    // when the target is empty (GetPawnMoves only adds a diagonal cell
    // when it's occupied by an enemy — correct for actual moves, but wrong
    // for "is this square defended" purposes, which is what check/checkmate
    // detection needs). Everything else reuses the existing move
    // generators directly — sliding pieces already stop at the first
    // occupied cell in each direction, which is exactly the blocking
    // behaviour attack-detection needs too.
    bool IsCellAttacked(BoardCell target, PieceColor byColor)
    {
        if (target == null) return false;

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null || piece.pieceColor != byColor) continue;

                    if (piece.pieceType == PieceType.Pawn)
                    {
                        string pieceFace = GetFaceFromNormal(piece.transform.up);
                        Vector2Int forwardOffset = GetPawnForwardOffset(pieceFace, piece.pieceColor);
                        Vector2Int leftDiag = forwardOffset + new Vector2Int(-forwardOffset.y, forwardOffset.x);
                        Vector2Int rightDiag = forwardOffset + new Vector2Int(forwardOffset.y, -forwardOffset.x);

                        foreach (Vector2Int diagOffset in new[] { leftDiag, rightDiag })
                        {
                            BoardCell diag = GetWrappedCell(cell, diagOffset, pieceFace, out _);
                            if (diag == target) return true;
                        }
                        continue;
                    }

                    List<BoardCell> attackSquares = piece.pieceType switch
                    {
                        PieceType.Rook => GetSlidingMoves(cell, piece, true, false),
                        PieceType.Bishop => GetSlidingMoves(cell, piece, false, true),
                        PieceType.Queen => GetSlidingMoves(cell, piece, true, true),
                        PieceType.King => GetKingMoves(cell, piece),
                        PieceType.Knight => GetKnightMoves(cell, piece),
                        _ => new List<BoardCell>()
                    };

                    if (attackSquares.Contains(target)) return true;
                }

        return false;
    }

    bool IsKingInCheck(PieceColor color)
    {
        BoardCell kingCell = FindKingCell(color);
        if (kingCell == null) return false; // king already gone — CheckWinCondition() handles that case

        PieceColor enemyColor = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        return IsCellAttacked(kingCell, enemyColor);
    }

    // Actually performs the move on the live board's cell references (cheap
    // — just swapping currentPiece pointers, no GameObjects touched), checks
    // whether the mover's own king would be in check afterward, then
    // reverts. Simulating the real move — rather than only statically
    // checking "is the destination square attacked" — is what correctly
    // handles pins and a king retreating in a straight line away from the
    // piece that's checking it.
    bool WouldBeInCheckAfterMove(BoardCell from, BoardCell to, PieceColor kingColor)
    {
        GameObject movingObj = from.currentPiece;
        GameObject capturedObj = to.currentPiece;

        to.currentPiece = movingObj;
        from.currentPiece = null;

        bool inCheck = IsKingInCheck(kingColor);

        from.currentPiece = movingObj;
        to.currentPiece = capturedObj;

        return inCheck;
    }

    // Same as GetLegalMoves(), but filters out any move that would leave
    // the mover's own king in check. GetLegalMoves() only ever produced
    // pseudo-legal moves (piece-pattern + blocking, no king safety); this
    // is the piece that was missing for real check/checkmate rules.
    List<BoardCell> GetLegalMovesSafe(BoardCell cell)
    {
        if (!cell.IsOccupied) return new List<BoardCell>();

        ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
        List<BoardCell> pseudoMoves = GetLegalMoves(cell);
        List<BoardCell> safeMoves = new List<BoardCell>();

        foreach (BoardCell move in pseudoMoves)
        {
            if (!WouldBeInCheckAfterMove(cell, move, piece.pieceColor))
                safeMoves.Add(move);
        }

        return safeMoves;
    }

    bool HasAnyLegalMoves(PieceColor color)
    {
        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null || piece.pieceColor != color) continue;

                    if (GetLegalMovesSafe(cell).Count > 0) return true;
                }
        return false;
    }

    // Called right after the turn flips, for whoever is about to move next.
    // No legal moves + currently in check => checkmate, the other color
    // wins. No legal moves + not in check => stalemate (draw). Otherwise,
    // if merely in check, just logs it so the check is visible on-screen.
    void CheckForCheckmate()
    {
        bool inCheck = IsKingInCheck(currentTurn);
        bool hasMoves = HasAnyLegalMoves(currentTurn);

        if (!hasMoves)
        {
            ClearCheckHighlight(); // board is frozen — clean up
            if (inCheck)
            {
                PieceColor winner = (currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                Debug.Log($"[CHECKMATE] {currentTurn} is checkmated — {winner} wins!");

                // Freeze the board immediately (so no further clicks/AI
                // moves sneak in during the popup), but hold off on the
                // actual win screen until the "CHECKMATE!" popup has had
                // its moment on screen.
                gameOver = true;
                StartCoroutine(ShowCheckmateThenWinScreen(winner.ToString()));
            }
            else
            {
                Debug.Log($"[STALEMATE] {currentTurn} has no legal moves — the game is a draw.");
                gameOver = true;
                winScreenUI?.ShowWinner("Draw");
            }
        }
        else if (inCheck)
        {
            Debug.Log($"[CHECK] {currentTurn} is in check!");
            BoardCell kingCell = FindKingCell(currentTurn);
            ShowCheckHighlight(kingCell);
        }
        else
        {
            ClearCheckHighlight(); // no longer in check
        }
    }    IEnumerator ShowCheckmateThenWinScreen(string winner)
    {
        if (checkmatePopup != null)
            checkmatePopup.SetActive(true);

        yield return new WaitForSeconds(checkmatePopupDuration);

        if (checkmatePopup != null)
            checkmatePopup.SetActive(false);

        ShowWinScreen(winner);
    }

    // ── GRAVITY NOTIFICATION UI ───────────────────────────────────────
    void UpdateGravityPanel(string message, Color textColor)
    {
        if (gravityPanel == null || gravityText == null) return;
        gravityText.text = message;
        gravityText.color = textColor;
    }
    // ── CORE SHIFT ────────────────────────────────────────────────────
    void TriggerCoreShift()
    {
        turnCount++;

        // Cycle: 4 moves normal, 2 moves low gravity, repeating
        int phase = turnCount % 6;
        bool stateChanged = false;

        if (phase == 4)
        {
            lowGravityMode = true;
            stateChanged = true;
            Debug.Log("[CORE SHIFT] Low Gravity — Pawns can jump 3 squares!");
            UpdateGravityPanel("LOW GRAVITY", new Color(1f, 0f, 0f)); // bright red

        }
        else if (phase == 0)
        {
            lowGravityMode = false;
            stateChanged = true;
            Debug.Log("[CORE SHIFT] Normal Gravity restored.");
            UpdateGravityPanel("NORMAL GRAVITY", new Color(0.9f, 0.88f, 1f)); // soft lavender-white

        }

        if (!stateChanged) return;

        // Re-snap pieces to their cells when gravity changes
        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell != null && cell.IsOccupied)
                    {
                        // A gravity shift never changes which face a piece is
                        // resting on — it only changes how far off the
                        // surface it sits. Reuse the piece's OWN established
                        // outward orientation (transform.up) instead of
                        // recomputing a fresh face from raw position: a cell
                        // right on a shared edge (e.g. the top/bottom row of
                        // a side face) is genuinely ambiguous between two
                        // faces, and re-deriving it here can disagree with
                        // the face the piece was actually placed on — which
                        // is exactly what was pushing edge pieces along the
                        // wrong axis and making them appear to sink into
                        // the cube.
                        GameObject pieceObj = cell.currentPiece;
                        Vector3 outDir = pieceObj.transform.up;
                        var pieceComp = pieceObj.GetComponent<ChessPiece>();
                        pieceObj.transform.position =
                            cell.transform.position + outDir * GetPiecePlacementOffset(pieceObj, outDir, pieceComp != null ? pieceComp.pieceType : PieceType.Pawn);
                    }
                }
    }

    // ── NEGAMAX AI ────────────────────────────────────────────────────
    // A lightweight material-value evaluation plus a proper Negamax search
    // with alpha-beta pruning — Negamax is the standard formulation real
    // chess engines use, and is mathematically identical to Minimax; it
    // just collapses White's "maximize" branch and Black's "minimize"
    // branch into one recursive function by always maximizing from the
    // current side-to-move's own perspective (see ColorSign/Negamax below
    // for how that works). The search operates on the LIVE board (the
    // same `cells` array and GameObjects the rest of the game uses), via a
    // make/unmake pair (SimulateMove/UndoMove) rather than cloning the
    // whole board into a separate data structure. That keeps this change
    // localized and reuses all of the existing move-generation code
    // (GetLegalMovesSafe, IsKingInCheck, etc.) exactly as-is, at the cost
    // of doing/undoing real GameObject state during the search. Because
    // the search runs to completion synchronously between coroutine
    // yields, nothing is ever rendered mid-search — Unity only draws a
    // frame after the coroutine yields control back, by which point every
    // simulated move has already been undone.

    // Convention: EvaluateBoard() is always "White's advantage" — positive
    // is good for White, negative is good for Black. ColorSign() flips
    // that into whichever color is asking, which is what lets Negamax
    // treat both colors identically.
    float GetPieceValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => 1f,
            PieceType.Knight => 3f,
            PieceType.Bishop => 3.25f,
            PieceType.Rook => 5f,
            PieceType.Queen => 9f,
            PieceType.King => 1000f,
            _ => 0f
        };
    }

    float EvaluateBoard()
    {
        float score = 0f;

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null) continue;

                    float value = GetPieceValue(piece.pieceType);
                    score += (piece.pieceColor == PieceColor.White) ? value : -value;
                }

        return score;
    }

    // Gathers every legal (from, to) pair for the given color, across the
    // whole board. Shared by the AI's root move choice and by every
    // recursive minimax ply.
    List<(BoardCell from, BoardCell to)> GetAllLegalMovesFor(PieceColor color)
    {
        var moves = new List<(BoardCell, BoardCell)>();

        for (int x = 0; x <= 6; x++)
            for (int y = 0; y <= 6; y++)
                for (int z = 0; z <= 6; z++)
                {
                    BoardCell cell = cells[x, y, z];
                    if (cell == null || !cell.IsOccupied) continue;

                    ChessPiece piece = cell.currentPiece.GetComponent<ChessPiece>();
                    if (piece == null || piece.pieceColor != color) continue;

                    foreach (BoardCell target in GetLegalMovesSafe(cell))
                        moves.Add((cell, target));
                }

        return moves;
    }

    // Everything needed to undo a simulated move: which GameObjects sat on
    // 'from' and 'to' before the move, and the moving piece's prior
    // orientation/hasMoved flag (both of which SimulateMove changes so
    // that deeper plies see correct face-relative moves for that piece).
    private struct MoveUndo
    {
        public BoardCell from;
        public BoardCell to;
        public GameObject movingObj;
        public GameObject capturedObj;
        public Vector3 savedUp;
        public bool savedHasMoved;
    }

    // Make a move on the live board for search purposes: swaps
    // cell.currentPiece pointers (no Instantiate/Destroy, no position
    // animation) and re-orients the moving piece's transform.up to match
    // its new face, since GetFaceFromNormal (used by pawn/sliding move
    // generation) depends on that orientation. Always pair with UndoMove.
    MoveUndo SimulateMove(BoardCell from, BoardCell to)
    {
        MoveUndo undo = new MoveUndo
        {
            from = from,
            to = to,
            movingObj = from.currentPiece,
            capturedObj = to.currentPiece
        };

        ChessPiece movingPiece = undo.movingObj.GetComponent<ChessPiece>();
        undo.savedUp = undo.movingObj.transform.up;
        undo.savedHasMoved = movingPiece.hasMoved;

        to.currentPiece = undo.movingObj;
        from.currentPiece = null;

        undo.movingObj.transform.up = GetOutwardDir(to);
        movingPiece.hasMoved = true;

        return undo;
    }

    // Reverts exactly what SimulateMove did, restoring the board (and the
    // moved piece's orientation/hasMoved) to its pre-move state.
    void UndoMove(MoveUndo undo)
    {
        undo.from.currentPiece = undo.movingObj;
        undo.to.currentPiece = undo.capturedObj;

        ChessPiece movingPiece = undo.movingObj.GetComponent<ChessPiece>();
        undo.movingObj.transform.up = undo.savedUp;
        movingPiece.hasMoved = undo.savedHasMoved;
    }

    // Sign used to flip a White-relative score into "the side to move's own
    // perspective" — the trick that lets Negamax use a single recursive
    // branch instead of separate maximize/minimize code paths for White
    // and Black.
    float ColorSign(PieceColor color)
    {
        return (color == PieceColor.White) ? 1f : -1f;
    }

    // Negamax with alpha-beta pruning — the standard formulation used in
    // real chess engines, and mathematically equivalent to Minimax. The
    // simplification: at every node, the side to move always wants to
    // MAXIMIZE its own score, so instead of a maximizing branch for White
    // and a mirrored minimizing branch for Black, every recursive call is
    // identical and just negates the value returned by the opponent's
    // best reply (a move that's great for the opponent is, by definition,
    // exactly as bad for you). alpha/beta are negated and swapped on each
    // call for the same reason.
    //
    // Returns the best achievable score FROM colorToMove's OWN perspective
    // (positive is always good for whoever is about to move at this node).
    float Negamax(int depth, float alpha, float beta, PieceColor colorToMove)
    {
        if (depth <= 0)
            return ColorSign(colorToMove) * EvaluateBoard();

        List<(BoardCell from, BoardCell to)> moves = GetAllLegalMovesFor(colorToMove);

        if (moves.Count == 0)
        {
            // No legal moves: checkmate or stalemate. Scale the mate score
            // by remaining depth so the search prefers a mate found sooner
            // over one found deeper in the tree. Being checkmated is
            // always the worst possible outcome FOR THE SIDE TO MOVE, so
            // this is simply a large negative number regardless of color.
            bool inCheck = IsKingInCheck(colorToMove);
            if (!inCheck) return 0f; // stalemate — treat as a draw
            return -(100000f + depth);
        }

        // Cheap move ordering — try captures first so alpha-beta has a
        // better chance of pruning early.
        moves.Sort((a, b) =>
        {
            int aCap = a.to.IsOccupied ? 1 : 0;
            int bCap = b.to.IsOccupied ? 1 : 0;
            return bCap.CompareTo(aCap);
        });

        PieceColor opponent = (colorToMove == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        float best = float.NegativeInfinity;
        foreach (var move in moves)
        {
            MoveUndo undo = SimulateMove(move.from, move.to);
            float score = -Negamax(depth - 1, -beta, -alpha, opponent);
            UndoMove(undo);

            if (score > best) best = score;
            if (score > alpha) alpha = score;
            if (alpha >= beta) break; // cutoff — opponent won't let us reach this line
        }
        return best;
    }

    // ── AI ────────────────────────────────────────────────────────────
    IEnumerator AIMove()
    {
        if (gameOver || aiThinking) yield break;

        aiThinking = true;
        Debug.Log("[AI] Thinking...");
        yield return new WaitForSeconds(0.5f);

        if (gameOver) { aiThinking = false; yield break; }

        List<(BoardCell from, BoardCell to)> allMoves = GetAllLegalMovesFor(PieceColor.Black);

        if (allMoves.Count == 0)
        {
            // No legal moves for Black. CheckForCheckmate() already runs
            // right after the turn flips and will have shown the
            // win/stalemate screen in this situation, so this is just a
            // safety net in case AIMove somehow still gets called.
            aiThinking = false;
            yield break;
        }

        // Root-level move ordering (captures first) for better pruning.
        allMoves.Sort((a, b) =>
        {
            int aCap = a.to.IsOccupied ? 1 : 0;
            int bCap = b.to.IsOccupied ? 1 : 0;
            return bCap.CompareTo(aCap);
        });

        int depth = Mathf.Max(1, aiSearchDepth);

        // Root loop mirrors Negamax's own loop exactly (maximize the score
        // from Black's own perspective), just kept separate so we can
        // track WHICH move produced the best score, not only the score
        // itself.
        float alpha = float.NegativeInfinity;
        const float beta = float.PositiveInfinity;

        float bestScore = float.NegativeInfinity;
        List<(BoardCell from, BoardCell to)> bestMoves = new List<(BoardCell, BoardCell)>();

        foreach (var move in allMoves)
        {
            MoveUndo undo = SimulateMove(move.from, move.to);
            float score = -Negamax(depth - 1, -beta, -alpha, PieceColor.White);
            UndoMove(undo);

            const float epsilon = 0.0001f;
            if (score > bestScore + epsilon)
            {
                bestScore = score;
                bestMoves.Clear();
                bestMoves.Add(move);
            }
            else if (Mathf.Abs(score - bestScore) <= epsilon)
            {
                bestMoves.Add(move);
            }

            if (score > alpha) alpha = score;
        }

        // Break ties randomly among equally-good moves so the AI doesn't
        // always play the same line in identical positions.
        var chosen = bestMoves[UnityEngine.Random.Range(0, bestMoves.Count)];

        ChessPiece aiPiece = chosen.from.currentPiece.GetComponent<ChessPiece>();
        Debug.Log($"[AI] Negamax depth {depth}, score {bestScore:F2}: {aiPiece.pieceType} ({chosen.from.x},{chosen.from.y},{chosen.from.z}) → ({chosen.to.x},{chosen.to.y},{chosen.to.z})");

        aiThinking = false;
        ExecuteMove(chosen.from, chosen.to);
    }
}