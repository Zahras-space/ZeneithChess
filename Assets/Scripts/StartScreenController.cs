using UnityEngine;
using UnityEngine.SceneManagement;

public class StartScreenController : MonoBehaviour
{
[Header("Audio")]
public AudioSource clickSound;
public IntroCutscene introVideo;

    public void OnStartButtonClicked()
    
    {
        clickSound?.Play();
        introVideo.PlayIntro();
    }
}