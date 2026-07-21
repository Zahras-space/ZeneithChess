using UnityEngine;
using UnityEngine.Video;

public class IntroCutscene : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;

    [Tooltip("Parent object containing your menu UI (title, buttons, background).")]
    public GameObject menuRoot;

    [Tooltip("The UI/GameObject displaying the cutscene.")]
    public GameObject cutsceneCanvas;

    [Header("Settings")]
    public bool allowSkip = true;

    private bool hasEnded = false;

    void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

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
        if (hasEnded || !allowSkip)
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

        // Reveal the menu
        if (menuRoot != null)
            menuRoot.SetActive(true);

        // Hide cutscene
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);

        // Remove event listener
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}