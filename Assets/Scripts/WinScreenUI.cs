using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class WinScreenUI : MonoBehaviour
{
    [Header("References")]
    public GameObject winCanvasRoot;   // the WinCanvas panel itself
    public TextMeshProUGUI winnerText;

    [Header("Scene Names")]
    public string menuSceneName = "MenuScene";

    void Awake()
    {
        if (winCanvasRoot != null)
            winCanvasRoot.SetActive(false);
    }

    public void ShowWinner(string winnerColor)
    {
        if (winCanvasRoot != null)
            winCanvasRoot.SetActive(true);

        if (winnerText != null)
            winnerText.text = $"{winnerColor} Wins!";

        Time.timeScale = 0f; // freeze gameplay behind the win screen
    }

    public void OnRestartPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenuPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}