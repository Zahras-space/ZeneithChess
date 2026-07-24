using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class IntroCutscene : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;

    [Tooltip("Parent object containing your menu UI (title, buttons, background).")]
    public GameObject menuRoot;

    [Tooltip("The UI/GameObject displaying the cutscene.")]
    public GameObject cutsceneCanvas;

    [Header("Scene To Load After Video")]
    public string loadingSceneName = "LoadingScreen";

    [Header("Settings")]
    public bool allowSkip = true;

    private bool hasEnded = false;
    private bool isPlaying = false;

    void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);
    }
    public void PlayIntro()
    {
        if (isPlaying) return;

        hasEnded = false;
        isPlaying = true;

        // Hide the menu while the cutscene plays
        if (menuRoot != null)
            menuRoot.SetActive(false);

        // Show cutscene
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(true);

        // Listen for video completion
        videoPlayer.loopPointReached += OnVideoFinished;

        // Play the video
        videoPlayer.Play();
    }

    void Update()
    {
        if (!isPlaying || hasEnded || !allowSkip)
            return;

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetMouseButtonDown(0))
        {
            SkipCutscene();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        EndCutscene();
    }

    public void SkipCutscene()
    {
        if (hasEnded)
            return;

        if (videoPlayer.isPlaying)
            videoPlayer.Stop();

        EndCutscene();
    }

    private void EndCutscene()
    {
        if (hasEnded)
            return;

        hasEnded = true;
        isPlaying = false;

        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;

        SceneManager.LoadScene(loadingSceneName);
    }
}