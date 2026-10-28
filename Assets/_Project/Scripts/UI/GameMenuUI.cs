using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Thai title screen and independent pause modal, with a shared help/confirmation page.</summary>
[DefaultExecutionOrder(1000),DisallowMultipleComponent]
public sealed class GameMenuUI : MonoBehaviour
{
    public const string MainScene="MainMenu",WorldScene="WorldPrototype";
    [SerializeField] private bool pauseMode;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private UIAudioFeedback audioFeedback;
    [SerializeField] private GameObject window,choices,helpPage,confirmPage;
    [SerializeField] private Button playButton,helpButton,quitButton,backButton,confirmYes,confirmNo,mainMenuButton,openPauseButton;
    [SerializeField] private TMP_Text confirmText,status;
    [SerializeField] private GameObject optionsPage;
    [SerializeField] private Button optionsButton,optionsBack,saveButton,customizeButton;
    [SerializeField] private CharacterCreationUI characterCreation;
    private InputAction escape;
    private bool loading,quitPending;
    private GameObject previousSelection;
    public bool IsOpen=>window!=null&&window.activeSelf;
    public bool IsLoading=>loading;
    private void Awake()
    {
        playButton.onClick.AddListener(()=>{if(pauseMode)Resume();else PlayGame();});helpButton.onClick.AddListener(ShowHelp);quitButton.onClick.AddListener(AskQuit);
        backButton.onClick.AddListener(Back);confirmNo.onClick.AddListener(Back);confirmYes.onClick.AddListener(Confirm);
        if(mainMenuButton!=null)mainMenuButton.onClick.AddListener(AskMainMenu);if(openPauseButton!=null)openPauseButton.onClick.AddListener(()=>TryPause());
        if(optionsButton!=null)optionsButton.onClick.AddListener(ShowOptions);
        if(optionsBack!=null)optionsBack.onClick.AddListener(Back);
        if(saveButton!=null)saveButton.onClick.AddListener(SaveProgressNow);
        if(customizeButton!=null)customizeButton.onClick.AddListener(()=>ShowCustomization(false));
        escape=new InputAction("Pause menu",InputActionType.Button,"<Keyboard>/escape");
        choices.SetActive(true);helpPage.SetActive(false);confirmPage.SetActive(false);window.SetActive(!pauseMode);
        if(optionsPage!=null)optionsPage.SetActive(false);
    }
    private void Start()
    {
        if(!pauseMode)
        {
            Time.timeScale=1;AudioListener.pause=false;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }
    }
    private void OnEnable(){escape?.Enable();if(player!=null)player.Died+=OnDeath;}
    private void OnDisable(){if(player!=null)player.Died-=OnDeath;escape?.Disable();if(pauseMode&&pause!=null)pause.SetPaused(GamePauseReason.PauseMenu,false);}
    private void OnDestroy()=>escape?.Dispose();
    private void OnDeath(){if(pauseMode)Resume();}
    private void LateUpdate()
    {
        if(loading||!escape.WasPressedThisFrame())return;
        if(characterCreation!=null&&characterCreation.IsOpen){characterCreation.Cancel();return;}
        if(IsOpen){if(helpPage.activeSelf||confirmPage.activeSelf||optionsPage!=null&&optionsPage.activeSelf)Back();else if(pauseMode)Resume();return;}
        // Existing menus consume Esc first. Never reopen pause on the same frame
        // that inventory/shop/dialogue/journal just closed.
        if(pauseMode&&pause.LastReasonChangeFrame!=Time.frameCount)TryPause();
    }
    public bool TryPause()
    {
        if(!pauseMode||IsOpen||loading||pause==null||pause.IsPaused||player==null||!player.IsAlive)return false;
        previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        pause.SetPaused(GamePauseReason.PauseMenu,true);window.SetActive(true);transform.SetAsLastSibling();Back();return true;
    }
    public void Resume()
    {
        if(!pauseMode||!IsOpen||loading)return;
        window.SetActive(false);pause.SetPaused(GamePauseReason.PauseMenu,false);
        if(EventSystem.current!=null&&player.IsAlive)EventSystem.current.SetSelectedGameObject(previousSelection!=null&&previousSelection.activeInHierarchy?previousSelection:null);
    }
    public void ShowHelp()
    {
        if(optionsPage!=null)optionsPage.SetActive(false);
        if(loading)return;choices.SetActive(false);helpPage.SetActive(true);confirmPage.SetActive(false);
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }
    public void Back()
    {
        if(optionsPage!=null)optionsPage.SetActive(false);
        if(loading)return;choices.SetActive(true);helpPage.SetActive(false);confirmPage.SetActive(false);
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }
    public void ShowOptions()
    {
        if(loading||optionsPage==null)return;choices.SetActive(false);helpPage.SetActive(false);confirmPage.SetActive(false);optionsPage.SetActive(true);
        var firstSlider=optionsPage.GetComponentInChildren<Slider>();
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(firstSlider!=null?firstSlider.gameObject:optionsBack.gameObject);
    }
    public void ShowCustomization(bool enterGame)
    {
        if(loading||pauseMode||characterCreation==null)return;
        choices.SetActive(false);helpPage.SetActive(false);confirmPage.SetActive(false);if(optionsPage!=null)optionsPage.SetActive(false);
        characterCreation.Open(enterGame);
    }
    public void SaveProgressNow()
    {
        if(!pauseMode||loading||player==null||!player.IsAlive)return;
        FlushProgress();FindAnyObjectByType<GameAlertsUI>()?.Saved();
    }
    public void AskMainMenu()=>Ask(false);
    public void AskQuit()=>Ask(true);
    private void Ask(bool quit)
    {
        if(loading)return;quitPending=quit;choices.SetActive(false);helpPage.SetActive(false);confirmPage.SetActive(true);
        if(optionsPage!=null)optionsPage.SetActive(false);
        confirmText.text=quit?"ต้องการออกจากเกมหรือไม่?\nความคืบหน้าจะถูกบันทึกก่อนออก":"กลับเมนูหลักหรือไม่?\nบันทึกเลเวล ไอเทม และเควสไว้\nเมื่อเข้าเกมอีกครั้งจะเริ่มที่จุดอัญเชิญ";
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(confirmNo.gameObject);
    }
    public void PlayGame()
    {
        if(pauseMode||loading)return;
        if(characterCreation!=null&&!characterCreation.HasSavedAppearance){ShowCustomization(true);return;}
        StartCoroutine(Travel(WorldScene));
    }
    public void StartAdventureAfterCustomization(){if(!pauseMode&&!loading)StartCoroutine(Travel(WorldScene));}
    public void Confirm()
    {
        if(loading)return;
        if(quitPending)
        {
            FlushProgress();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        else StartCoroutine(Travel(MainScene));
    }
    private IEnumerator Travel(string scene)
    {
        loading=true;status.text="กำลังบันทึกและโหลด...";
        if(!Application.CanStreamedLevelBeLoaded(scene)){status.text="ยังไม่พบฉากเกม กรุณาตรวจ Build Settings";loading=false;yield break;}
        FlushProgress();audioFeedback?.Play(UIAudioCue.Close);
        var music=!pauseMode?FindAnyObjectByType<MainMenuMusic>():null;
        if(music!=null)music.BeginExit();
        yield return new WaitForSecondsRealtime(music!=null?music.FadeOutSeconds:.15f);
        if(pause!=null)pause.ResetForRestart();Time.timeScale=1;AudioListener.pause=false;
        yield return SceneManager.LoadSceneAsync(scene);
    }
    private void FlushProgress()
    {
        if(player!=null)
        {
            player.GetComponent<PlayerProgressionState>()?.FlushSave();player.GetComponent<PlayerGoldWallet>()?.Commit();player.GetComponent<PlayerStoryProgress>()?.FlushSave();
        }
        PlayerPrefs.Save();
    }
}
