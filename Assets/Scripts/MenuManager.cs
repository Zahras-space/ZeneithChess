using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string loadingSceneName = "LoadingScreen";

    [Header("Button Sound (optional)")]
    public AudioSource clickSound;

    public void OnStartGamePressed()
    {
        clickSound?.Play();
        SceneManager.LoadScene(loadingSceneName);
    }

    public void OnExitGamePressed()
    {
        clickSound?.Play();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}