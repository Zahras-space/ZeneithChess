using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject menuPanel;
    public Slider volumeSlider;

    [Header("Audio")]
    public AudioSource[] allAudioSources; // drag all AudioSources here

    private bool isPaused = false;

    void Start()
    {
        // Menu starts hidden
        menuPanel.SetActive(false);

        // Set slider to current volume and listen for changes
        volumeSlider.value = AudioListener.volume;
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    void Update()
    {
        // Press Escape to toggle menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        menuPanel.SetActive(true);
        Time.timeScale = 0f;    // freezes all game time
        isPaused = true;
    }

    public void ResumeGame()
    {
        menuPanel.SetActive(false);
        Time.timeScale = 1f;    // resumes game time
        isPaused = false;
    }

    public void OnVolumeChanged(float value)
    {
        // Controls ALL audio in the scene at once
        AudioListener.volume = value;
    }

    public void QuitToMenu()
    {
        // Restore time before switching scenes
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartScreen");
    }
}