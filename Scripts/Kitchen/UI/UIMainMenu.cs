using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class UIMainMenu : MonoBehaviour
{
    [SerializeField] UIStageList stageList;
    
    [SerializeField] PButton playButton;
    [SerializeField] PButton quitButton;

    void Awake()
    {
        stageList.gameObject.SetActive(false);
        playButton.gameObject.SetActive(true);
        quitButton.gameObject.SetActive(true);
    }
    
    public void Build()
    {
        playButton.OnClick = _ =>
        {
            stageList.gameObject.SetActive(!stageList.gameObject.activeSelf);
        };
        
        quitButton.OnClick = _ =>
        {
            Application.Quit();
        };
    }
}
