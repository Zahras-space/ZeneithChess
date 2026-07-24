using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public class PauseMenuUI : MonoBehaviour
{
    [Header("References")]
    public GameObject pauseMenuRoot;   // the PauseMenu Canvas itself
    public Slider volumeSlider;

    [Header("ClickAudio")]
    public AudioSource clickSound;

    [Header("Audio (optional)")]
    public AudioMixer audioMixer;
    public string volumeParam = "MasterVolume";

    [Header("Scene Names")]
    public string menuSceneName = "MenuScene";

    private bool isPaused = false;

    void Awake()
    {
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);

        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
         clickSound?.Play();
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        isPaused = true;
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        isPaused = false;
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;
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

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat(volumeParam, Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f);
    }
}