using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuHandler : MonoBehaviour
{
    
    public GameObject settingsPanel;
    
    public string url = "https://www.youtube.com/watch?v=AviE1mtioXU";

    public void OpenLink()
    {
        Application.OpenURL(url);
    }
    
    public void StartGame()
    {
        SceneManager.LoadScene("sisfruit"); 
    }
    
    public void OpenSettings()
    {
        Debug.Log("Settings menu opened!");
        settingsPanel.SetActive(true); 
    }
    
    public void CloseSettings()
    {
        settingsPanel.SetActive(false); 
    }
    
    public void QuitGame()
    {
        Debug.Log("Game Quit!");
        Application.Quit(); 
    }
    //TODO: some visual
}
