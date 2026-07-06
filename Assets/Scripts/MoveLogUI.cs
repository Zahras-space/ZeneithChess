using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the two-column move log HUD:
/// - White moves on the right
/// - Black (AI) moves on the left
/// Each column has a pawn icon header + a scrolling list of move entries.
/// Attach this to the HUD_MoveLog object in the Canvas.
/// </summary>
public class MoveLogUI : MonoBehaviour
{
    [Header("White Panel (Right)")]
    public Transform whiteContent;      // WhitePanel/WhiteScrollView/Viewport/Content
    public ScrollRect whiteScrollRect;
    public Image whitePawnIcon;

    [Header("Black Panel (Left)")]
    public Transform blackContent;      // BlackPanel/BlackScrollView/Viewport/Content
    public ScrollRect blackScrollRect;
    public Image blackPawnIcon;

    [Header("Prefab")]
    public GameObject moveEntryPrefab;  // Prefab with a single TextMeshProUGUI

    [Header("Sprites")]
    public Sprite whitePawnSprite;
    public Sprite blackPawnSprite;

    [Header("Colors (optional)")]
    public Color normalMoveColor = Color.white;
    public Color captureColor = new Color(1f, 0.4f, 0.4f); // reddish for captures

    [Header("Settings")]
    public int maxEntriesPerColumn = 100; // trims oldest entries beyond this

    private int whiteMoveNumber = 0;
    private int blackMoveNumber = 0;

    void Awake()
    {
        if (whitePawnIcon != null && whitePawnSprite != null)
            whitePawnIcon.sprite = whitePawnSprite;

        if (blackPawnIcon != null && blackPawnSprite != null)
            blackPawnIcon.sprite = blackPawnSprite;
    }

    /// <summary>Call this for every White (human) move.</summary>
    public void LogWhiteMove(string message, bool isCapture = false)
    {
        whiteMoveNumber++;
        AddEntry(whiteContent, whiteScrollRect, $"{whiteMoveNumber}. {message}", isCapture);
        TrimOldEntries(whiteContent);
    }

    /// <summary>Call this for every Black (AI) move.</summary>
    public void LogBlackMove(string message, bool isCapture = false)
    {
        blackMoveNumber++;
        AddEntry(blackContent, blackScrollRect, $"{blackMoveNumber}. {message}", isCapture);
        TrimOldEntries(blackContent);
    }

    /// <summary>Generic entry point if you want to route by PieceColor directly.</summary>
    public void LogMove(PieceColor color, string message, bool isCapture = false)
    {
        if (color == PieceColor.White) LogWhiteMove(message, isCapture);
        else LogBlackMove(message, isCapture);
    }

    private void AddEntry(Transform content, ScrollRect scrollRect, string message, bool isCapture)
    {
        if (content == null || moveEntryPrefab == null) return;

        GameObject entry = Instantiate(moveEntryPrefab, content);
        TextMeshProUGUI tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = message;
            tmp.color = isCapture ? captureColor : normalMoveColor;
        }

        // Scroll to bottom after layout rebuilds next frame
        if (scrollRect != null)
            StartCoroutine(ScrollToBottomNextFrame(scrollRect));
    }

    private System.Collections.IEnumerator ScrollToBottomNextFrame(ScrollRect scrollRect)
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void TrimOldEntries(Transform content)
    {
        if (content == null) return;
        while (content.childCount > maxEntriesPerColumn)
        {
            Destroy(content.GetChild(0).gameObject);
        }
    }

    public void ClearLogs()
    {
        ClearContent(whiteContent);
        ClearContent(blackContent);
        whiteMoveNumber = 0;
        blackMoveNumber = 0;
    }

    private void ClearContent(Transform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}