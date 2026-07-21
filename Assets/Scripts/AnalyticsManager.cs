using UnityEngine;
using TMPro;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    [Header("Tracked Analytics")]
    public float playTime;             // Total elapsed match time 
    public int playerCaptures;         // "Enemy Kills" (White capturing Black) [cite: 15, 30]
    public int playerDeaths;           // "Player Deaths" (AI capturing White) 
    public int score;                  // Dynamic score based on piece values [cite: 15, 31]
    public int totalTurns;             // Total turns elapsed
    public int gravityCoreShifts;      // Number of core gravity shifts triggered

    [Header("Optional UI Display")]
    public TextMeshProUGUI analyticsText; // Drag a text element here to display real-time analytics

    private bool isTracking = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persists across menus and match scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        StartTracking();
    }

    void Update()
    {
        if (isTracking)
        {
            playTime += Time.deltaTime; // Track raw play duration [cite: 15, 29]
            UpdateUI();
        }
    }

    public void StartTracking()
    {
        playTime = 0f;
        playerCaptures = 0;
        playerDeaths = 0;
        score = 0;
        totalTurns = 0;
        gravityCoreShifts = 0;
        isTracking = true;
    }

    public void StopTracking()
    {
        isTracking = false;
        DisplayFinalAnalytics();
    }

    // Call this when a piece is captured on the board
    public void RecordCapture(PieceColor attackerColor, PieceType victimType)
    {
    // 10 points for a Pawn, 30 for a Rook/Knight, 90 for a Queen [cite: 18]
int pieceValue = victimType switch
        {
            PieceType.Pawn => 10,
            PieceType.Rook => 30,
            PieceType.Queen => 90,
            PieceType.King => 100, // Capturing a King wins the game
            _ => 10
        };

        if (attackerColor == PieceColor.White) // Player (White) performs a capture [cite: 30]
        {
            playerCaptures++;
            score += pieceValue; // Increase score for player actions [cite: 31]
        }
        else // AI (Black) captures a player piece
        {
            playerDeaths++;
        }
    }

    public void RecordTurn()
    {
        totalTurns++;
    }

    public void RecordGravityShift()
    {
        gravityCoreShifts++;
    }

    private void UpdateUI()
    {
        if (analyticsText != null)
        {
            analyticsText.text = $"Time: {playTime:F1}s\n" +
                                 $"Score: {score}\n" +
                                 $"Captures: {playerCaptures}\n" +
                                 $"Losses: {playerDeaths}\n" +
                                 $"Turns: {totalTurns}";
        }
    }

    public void DisplayFinalAnalytics()
    {
        Debug.Log("========================================\n" +
                  "        MATCH REPORT & ANALYTICS        \n" +
                  "========================================\n" +
                  $"Total Play Time: {playTime:F1} seconds\n" +
                  $"Final Player Score: {score}\n" +
                  $"Pieces Captured (Kills): {playerCaptures}\n" +
                  $"Your Pieces Lost (Deaths): {playerDeaths}\n" +
                  $"Total Moves Made: {totalTurns}\n" +
                  $"Gravity Shifts Experienced: {gravityCoreShifts}\n" +
                  "========================================");
    }
}