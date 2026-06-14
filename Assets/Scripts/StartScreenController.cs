using UnityEngine;
using UnityEngine.SceneManagement;

public class StartScreenController : MonoBehaviour
{
[Header("Audio")]
public AudioSource clickSound;
    public void OnStartButtonClicked()
    
    {
        clickSound?.Play();
        SceneManager.LoadScene("LoadingScene");
    }
}