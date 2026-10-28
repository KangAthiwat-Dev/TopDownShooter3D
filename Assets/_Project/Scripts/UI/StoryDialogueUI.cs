using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Guard quest dialogue; shares the existing menu pause and release-to-resume input gate.</summary>
public sealed class StoryDialogueUI : MonoBehaviour
{
    [SerializeField] private PlayerStoryProgress story;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private GameObject window;
    [SerializeField] private TMP_Text speaker,body,reward,status,primaryLabel;
    [SerializeField] private Button primary,leave;
    private StoryDialogueInteractable contact;
    private PlayerHealth health;
    private InputAction close;
    private GameObject previousSelection;
    private bool busy;
    public bool IsOpen {get;private set;}
    public GameObject PlayerObject=>story.gameObject;
    public string Body=>body.text;
    public string Status=>status.text;
    public Button PrimaryButton=>primary;
    private void Awake()
    {
        health=story.GetComponent<PlayerHealth>();close=story.GetComponent<PlayerInput>().actions.FindAction("Menus/Close",true);
        primary.onClick.AddListener(ChoosePrimary);leave.onClick.AddListener(Close);window.SetActive(false);
    }
    private void OnEnable(){close.performed+=OnClose;health.Died+=Close;story.Changed+=Refresh;}
    private void OnDisable(){Close();if(close!=null)close.performed-=OnClose;if(health!=null)health.Died-=Close;if(story!=null)story.Changed-=Refresh;}
    private void OnClose(InputAction.CallbackContext _){if(IsOpen)Close();}
    private void Update(){if(IsOpen&&(contact==null||!contact.IsPlayerInRange(PlayerObject)||!health.IsAlive))Close();}
    public bool TryOpen(StoryDialogueInteractable npc)
    {
        if(IsOpen||!isActiveAndEnabled||npc==null||!npc.IsPlayerInRange(PlayerObject)||!health.IsAlive||pause.IsPaused||
            !PlayerObject.GetComponent<PlayerInteractor>().CanStartInteraction)return false;
        contact=npc;previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        IsOpen=true;pause.SetPaused(GamePauseReason.Dialogue,true);window.SetActive(true);transform.SetAsLastSibling();window.transform.SetAsLastSibling();status.text="";
        Refresh();story.MeetGuard();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(primary.interactable?primary.gameObject:leave.gameObject);return true;
    }
    public void Close()
    {
        if(!IsOpen)return;IsOpen=false;contact=null;window.SetActive(false);
        if(pause!=null){if(!health.IsAlive)pause.SetPaused(GamePauseReason.GameOver,true);pause.SetPaused(GamePauseReason.Dialogue,false);}
        if(EventSystem.current!=null&&health!=null&&health.IsAlive)EventSystem.current.SetSelectedGameObject(previousSelection!=null&&previousSelection.activeInHierarchy?previousSelection:null);
    }
    private void Refresh()
    {
        if(!IsOpen)return;speaker.text=contact.Actor.DisplayName;primary.gameObject.SetActive(true);primary.interactable=true;
        reward.text="";
        if(!story.TutorialCompleted)
        {
            body.text="นักเดินทาง จงฝึกควบคุมพลังของเจ้าให้พร้อมก่อน\nฝึกเคลื่อนที่ ยิง และพุ่งหลบ แล้วข้าจะฝากภารกิจให้เจ้า";
            primary.gameObject.SetActive(false);return;
        }
        switch(story.DefenseStatus)
        {
            case StoryQuestStatus.Available:
                body.text="กองทัพอสูรกำลังกลืนกินหมู่บ้านแห่งนี้... แนวป้องกันของพวกเรากำลังจะแตกพ่าย!\n\nหากเจ้ายังมีพลังหลงเหลืออยู่... ข้าขอฝากชะตาของหมู่บ้านไว้ในมือเจ้า!";
                primaryLabel.text="รับภารกิจ — Defend the Village";reward.text="รางวัล: "+story.DefenseQuest.RewardText;return;
            case StoryQuestStatus.Active:
                body.text=$"ผู้บุกรุกยังอยู่ใกล้ทางเข้าหมู่บ้าน จงจัดการพวกมันให้หมด!\n\nกำจัดแล้ว {story.DefenseCount}/{story.DefenseQuest.requiredCount} ตัว\nติดตามสัญลักษณ์สีทองบนแผนที่ แล้วกลับมาหาข้าเมื่อสำเร็จ";
                primary.gameObject.SetActive(false);return;
            case StoryQuestStatus.ReadyToTurnIn:
                body.text="เจ้าโค่นพวกมันได้ทั้งหมดจริง ๆ งั้นรึ... ช่างเป็นพลังที่น่าเหลือเชื่อ!\n\nบางที... เจ้าอาจเป็นความหวังที่อาณาจักรแห่งนี้เฝ้ารอมาตลอด";
                primaryLabel.text="ส่งภารกิจและรับรางวัล";reward.text="รางวัล: "+story.DefenseQuest.RewardText;return;
        }
        switch(story.WolfStatus)
        {
            case StoryQuestStatus.Available:
                body.text="ในป่าลึกมีอสูรที่เหล่านักล่าต่างหวาดกลัว... Alpha Wolf ราชาแห่งฝูงหมาป่า!\n\nหากเจ้าสามารถโค่นมันลงได้... ความหวาดกลัวที่ปกคลุมหมู่บ้านแห่งนี้อาจสิ้นสุดลง!";
                primaryLabel.text="รับภารกิจ — Hunt the Alpha Wolf";reward.text="รางวัล: "+story.WolfQuest.RewardText;break;
            case StoryQuestStatus.Active:
                body.text="ราชาหมาป่ายังอยู่ใน Thornveil Forest\nตามสัญลักษณ์สีทองบนแผนที่ แล้วนำชัยชนะกลับมาที่หมู่บ้าน\n\nหากเพิ่งมีผู้โค่นมัน จงรอให้มันกลับมาในป่าอีกครั้ง";primary.gameObject.SetActive(false);break;
            case StoryQuestStatus.ReadyToTurnIn:
                body.text="เป็นไปไม่ได้... เจ้าเอาชนะราชาแห่งฝูงหมาป่าได้จริง ๆ!\n\nดูเหมือนโชคชะตาจะเลือกผู้กล้าที่ไม่ธรรมดามาเสียแล้ว...";
                primaryLabel.text="ส่งภารกิจและรับรางวัล";reward.text="รางวัล: "+story.WolfQuest.RewardText;break;
            default:
                body.text="Oakshade จะจดจำสิ่งที่เจ้าทำไว้เสมอ ผู้กล้า\nเตรียมตัวที่ Silverwell Town ก่อนออกเดินทางต่อ\n\nMalachar ยังรออยู่ที่ปราสาท... ขอให้เจ้าเดินทางปลอดภัย";primary.gameObject.SetActive(false);break;
        }
    }
    public void ChoosePrimary()
    {
        if(busy||!IsOpen||contact==null||!contact.IsPlayerInRange(PlayerObject)||!health.IsAlive)return;
        busy=true;
        try
        {
            if(story.DefenseStatus==StoryQuestStatus.Available){if(story.AcceptDefense())Close();return;}
            if(story.DefenseStatus==StoryQuestStatus.ReadyToTurnIn){story.TryClaimReward(story.DefenseQuest,out string text);status.text=text;return;}
            if(story.WolfStatus==StoryQuestStatus.Available){if(story.AcceptWolf())Close();return;}
            if(story.WolfStatus==StoryQuestStatus.ReadyToTurnIn){story.TryClaimReward(story.WolfQuest,out string text);status.text=text;}
        }
        finally{busy=false;}
    }
}
