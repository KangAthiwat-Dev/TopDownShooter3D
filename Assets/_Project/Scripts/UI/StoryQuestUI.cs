using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StoryQuestUI : MonoBehaviour
{
    [SerializeField] private PlayerStoryProgress story;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private TMP_Text objective,direction,journalBody;
    [SerializeField] private Button openJournal,closeJournal;
    [SerializeField] private GameObject journalWindow;
    [SerializeField] private KingdomMinimapUI minimap;
    [SerializeField] private RectTransform objectiveMarker;
    private PlayerHealth health;
    private InputAction journalAction,closeAction;
    private GameObject previousSelection;
    public bool IsOpen {get;private set;}
    private void Awake()
    {
        health=story.GetComponent<PlayerHealth>();journalAction=new InputAction("Quest Journal",InputActionType.Button,"<Keyboard>/q");
        closeAction=story.GetComponent<PlayerInput>().actions.FindAction("Menus/Close",true);
        openJournal.onClick.AddListener(Toggle);closeJournal.onClick.AddListener(Close);journalWindow.SetActive(false);
    }
    private void OnEnable(){journalAction.Enable();journalAction.performed+=OnJournal;closeAction.performed+=OnClose;health.Died+=Close;}
    private void OnDisable(){Close();journalAction.performed-=OnJournal;journalAction.Disable();closeAction.performed-=OnClose;health.Died-=Close;}
    private void OnDestroy()=>journalAction.Dispose();
    private void OnJournal(InputAction.CallbackContext _)=>Toggle();
    private void OnClose(InputAction.CallbackContext _){if(IsOpen)Close();}
    public void Toggle()
    {
        if(IsOpen){Close();return;}
        if(pause.IsPaused||!health.IsAlive||!story.GetComponent<PlayerInteractor>().CanStartInteraction)return;
        previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        IsOpen=true;pause.SetPaused(GamePauseReason.Journal,true);journalWindow.SetActive(true);transform.SetAsLastSibling();journalWindow.transform.SetAsLastSibling();
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(closeJournal.gameObject);
    }
    public void Close()
    {
        if(!IsOpen)return;IsOpen=false;journalWindow.SetActive(false);pause.SetPaused(GamePauseReason.Journal,false);transform.SetAsFirstSibling();
        if(EventSystem.current!=null&&health.IsAlive)EventSystem.current.SetSelectedGameObject(previousSelection!=null&&previousSelection.activeInHierarchy?previousSelection:null);
    }
    private static string Status(StoryQuestStatus state)=>state switch
    {
        StoryQuestStatus.Locked=>"ยังไม่ปลดล็อก",StoryQuestStatus.Available=>"รับได้ที่ทหารยาม",StoryQuestStatus.Active=>"กำลังทำ",
        StoryQuestStatus.ReadyToTurnIn=>"กลับไปรับรางวัล",_=>"สำเร็จ / รับรางวัลแล้ว"
    };
    private void LateUpdate()
    {
        objective.text="<color=#F4D88C>MAIN  Defeat Malachar</color>\n"+story.ObjectiveText;
        var target=story.ObjectiveTarget;objectiveMarker.gameObject.SetActive(target!=null);
        if(target!=null)
        {
            objectiveMarker.anchoredPosition=minimap.WorldToMap(target.position);
            Vector3 delta=target.position-story.transform.position;float angle=Mathf.Repeat(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg+360,360);
            string[] compass={"N","NE","E","SE","S","SW","W","NW"};
            direction.text=$"เป้าหมาย  {compass[Mathf.RoundToInt(angle/45)%8]}  ·  {Vector3.ProjectOnPlane(delta,Vector3.up).magnitude:0} ม.";
        }
        else direction.text=story.WolfStatus==StoryQuestStatus.Active?"ราชาหมาป่ากำลังกลับมา...":"Q  บันทึกภารกิจ";
        if(!IsOpen)return;
        journalBody.text="<color=#79510F><b>MAIN — Defeat Malachar</b></color>\nเดินทางผ่าน Everwood และเตรียมพร้อมเผชิญหน้าจอมมาร\n\n"+
            "<b>Summoning Ruins — "+(story.TutorialCompleted?"สำเร็จ":"ฝึกควบคุม")+"</b>\n"+story.TutorialText+"\n\n"+
            "<b>Defend the Village — "+Status(story.DefenseStatus)+"</b>\n"+story.DefenseQuest.description+"\nรางวัล: "+story.DefenseQuest.RewardText+"\n\n"+
            "<b>Hunt the Alpha Wolf — "+Status(story.WolfStatus)+"</b>\n"+story.WolfQuest.description+"\nรางวัล: "+story.WolfQuest.RewardText+"\n\n"+
            "ความคืบหน้าบันทึกอัตโนมัติ · E สนทนา · ใช้เมาส์เลือกคำตอบ";
    }
}
