using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SimpleMoveLogger : MonoBehaviour
{
    [Header("References")]
    public TMP_Text logText;

    [Header("Settings")]
    public int maxLines = 12;

    [Header("Colors")]
    public Color whiteColor = Color.white;
    public Color blackColor = new Color(0.75f, 0.75f, 0.85f);
    public Color captureColor = new Color(1f, 0.45f, 0.45f);
    public Color turnBannerColor = new Color(1f, 0.85f, 0.3f);

    private List<string> lines = new List<string>();

    public void LogMove(PieceColor color, string message, bool isCapture)
    {
        Color c = isCapture ? captureColor : (color == PieceColor.White ? whiteColor : blackColor);
        string hex = ColorUtility.ToHtmlStringRGB(c);
        AddLine($"<color=#{hex}>{message}</color>");
    }

    public void LogTurnBanner(PieceColor color)
    {
        string hex = ColorUtility.ToHtmlStringRGB(turnBannerColor);
        AddLine($"<color=#{hex}><b>— {color}'s turn —</b></color>");
    }

    public void Log(string message)
    {
        AddLine(message);
    }

    private void AddLine(string formatted)
    {
        lines.Add(formatted);
        if (lines.Count > maxLines)
            lines.RemoveAt(0);

        if (logText != null)
            logText.text = string.Join("\n", lines);
    }

    public void ClearLog()
    {
        lines.Clear();
        if (logText != null)
            logText.text = "";
    }
}