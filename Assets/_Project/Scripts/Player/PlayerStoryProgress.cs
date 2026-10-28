using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Story state only. Currency/items keep their existing save; reward receipts bridge the two atomically.</summary>
[DisallowMultipleComponent]
public sealed class PlayerStoryProgress : MonoBehaviour
{
    public const string DefaultSaveKey = "TopDownShooter3D.Story.v1";
    [SerializeField] private bool persistStory = true;
    [SerializeField] private string saveKey = DefaultSaveKey;
    [SerializeField] private StoryQuestDefinition defendVillage, huntWolf;
    [SerializeField] private WorldMonsterDirector monsters;
    [SerializeField] private VillageDefenseEncounter defense;
    [SerializeField] private StoryActor guard;
    [Serializable] private sealed class SaveData
    {
        public int version = 1;
        public float moved;
        public bool shot, dashed, guardMet;
        public StoryQuestStatus defense, wolf;
        public int defenseMask;
    }
    private SaveData state = new SaveData();
    private PlayerMovement movement;
    private PlayerRangedWeapon weapon;
    private PlayerHealth health;
    private PlayerGoldWallet wallet;
    private PlayerInventory inventory;
    private PlayerInputGate gate;
    private InputAction move;
    private Vector3 previousPosition;
    private int savedMeters;
    private float nextWolfLookup;
    private Transform wolfTarget;
    public event Action Changed;
    public bool TutorialCompleted => state.moved >= 8 && state.shot && state.dashed;
    public float TutorialDistance => state.moved;
    public bool TutorialShot => state.shot;
    public bool TutorialDash => state.dashed;
    public bool GuardMet => state.guardMet;
    public StoryQuestDefinition DefenseQuest => defendVillage;
    public StoryQuestDefinition WolfQuest => huntWolf;
    public StoryQuestStatus DefenseStatus => state.defense;
    public StoryQuestStatus WolfStatus => state.wolf;
    public int DefenseMask => state.defenseMask;
    public int DefenseCount { get { int count=0; for(int i=0;i<defendVillage.requiredCount;i++) if(IsDefenseSlotDefeated(i)) count++; return count; } }
    public Transform Guard => guard != null ? guard.transform : null;
    public GameObject PlayerObject => gameObject;
    public string TutorialText => $"W A S D  เคลื่อนที่  {Mathf.FloorToInt(Mathf.Min(8,state.moved))}/8 ม.\nJ  ยิงเป้าฝึก  {(state.shot ? "สำเร็จ" : "0/1")}\nSpace  พุ่งหลบ  {(state.dashed ? "สำเร็จ" : "0/1")}";
    private void Awake()
    {
        movement=GetComponent<PlayerMovement>(); weapon=GetComponent<PlayerRangedWeapon>();health=GetComponent<PlayerHealth>();
        wallet=GetComponent<PlayerGoldWallet>(); inventory=GetComponent<PlayerInventory>();gate=GetComponent<PlayerInputGate>();
        move=GetComponent<PlayerInput>().actions.FindAction("Gameplay/Move",true);
        if(persistStory&&PlayerPrefs.HasKey(saveKey))RestoreSave(PlayerPrefs.GetString(saveKey));
        previousPosition=transform.position;
    }
    private void Start()
    {
        // Wallet's loadout has finished loading by the first frame. Receipts repair a
        // shutdown between the wallet transaction and the subsequent story save.
        ReconcileReceipts();Save();
    }
    private void OnEnable()
    {
        movement.DashStarted+=OnDash;weapon.ProjectileFired+=OnShot;
        if(monsters!=null)monsters.MonsterDefeated+=OnMonsterDefeated;
    }
    private void OnDisable()
    {
        if(movement!=null)movement.DashStarted-=OnDash;if(weapon!=null)weapon.ProjectileFired-=OnShot;
        if(monsters!=null)monsters.MonsterDefeated-=OnMonsterDefeated;
    }
    private void OnApplicationPause(bool paused) { if(paused)Save(); }
    private void OnApplicationQuit()=>Save();
    private bool Playing => health.IsAlive && movement.enabled && (gate==null||!gate.BlocksGameplay);
    private void Update()
    {
        float distance=Vector3.ProjectOnPlane(transform.position-previousPosition,Vector3.up).magnitude;
        previousPosition=transform.position;
        if(TutorialCompleted||!Playing||state.moved>=8||movement.IsDashing||movement.IsShadowDashing||move.ReadValue<Vector2>().sqrMagnitude<.01f||distance>1.5f)return;
        bool before=TutorialCompleted; state.moved=Mathf.Min(8,state.moved+distance);
        if(Mathf.FloorToInt(state.moved)!=savedMeters||TutorialCompleted!=before){savedMeters=Mathf.FloorToInt(state.moved);Publish();}
    }
    private void OnShot(Projectile _) { if(!Playing||state.shot)return;state.shot=true;Publish(); }
    private void OnDash() { if(!Playing||state.dashed)return;state.dashed=true;Publish(); }
    private void OnMonsterDefeated(WorldMonsterMember member)
    {
        if(state.wolf!=StoryQuestStatus.Active||member==null||member.SpeciesId!="alpha_wolf"||!member.Defeated)return;
        var source=member.Health.LastDamageSource;
        if(source==null||source.GetComponentInParent<PlayerStoryProgress>()!=this)return;
        state.wolf=StoryQuestStatus.ReadyToTurnIn;Publish();
    }
    public bool AcceptDefense()
    {
        if(!TutorialCompleted||DefenseStatus!=StoryQuestStatus.Available)return false;
        state.defense=StoryQuestStatus.Active;state.defenseMask=0;Publish();return true;
    }
    public bool AcceptWolf()
    {
        if(DefenseStatus!=StoryQuestStatus.Completed||WolfStatus!=StoryQuestStatus.Available)return false;
        state.wolf=StoryQuestStatus.Active;Publish();return true;
    }
    public void MeetGuard() { if(state.guardMet)return;state.guardMet=true;Publish(); }
    public bool IsDefenseSlotDefeated(int index)=>index>=0&&index<defendVillage.requiredCount&&(state.defenseMask&(1<<index))!=0;
    // Called only by a registered encounter member on EnemyHealth.Died, never on despawn.
    internal void RecordDefenseDeath(VillageDefenseMember member)
    {
        if(DefenseStatus!=StoryQuestStatus.Active||member==null||member.Owner!=defense||!member.HealthDead||IsDefenseSlotDefeated(member.Slot))return;
        state.defenseMask|=1<<member.Slot;
        if(DefenseCount>=defendVillage.requiredCount)state.defense=StoryQuestStatus.ReadyToTurnIn;
        Publish();
    }
    public bool TryClaimReward(StoryQuestDefinition quest,out string result)
    {
        bool defend=quest==defendVillage;
        if(quest==null||(!defend&&quest!=huntWolf)||(defend?DefenseStatus:WolfStatus)!=StoryQuestStatus.ReadyToTurnIn)
        {result="ยังไม่พร้อมรับรางวัล";return false;}
        if(!inventory.TryGrantQuestReward(quest.questId,quest.rewardItem,quest.rewardQuantity,quest.rewardGold,wallet))
        {result=wallet.CanReceive(quest.rewardGold)?"กระเป๋าเต็ม กรุณาจัดช่องว่าง แล้วกลับมารับรางวัลได้เสมอ":"Gold เต็ม กรุณาใช้ Gold บางส่วนก่อนรับรางวัล";return false;}
        if(defend){state.defense=StoryQuestStatus.Completed;state.wolf=StoryQuestStatus.Available;}
        else state.wolf=StoryQuestStatus.Completed;
        Publish();result="ได้รับ "+quest.RewardText;return true;
    }
    private void ReconcileReceipts()
    {
        if(wallet.HasQuestReceipt(defendVillage.questId)){state.defense=StoryQuestStatus.Completed;state.defenseMask=(1<<defendVillage.requiredCount)-1;}
        if(wallet.HasQuestReceipt(huntWolf.questId)){state.defense=StoryQuestStatus.Completed;state.wolf=StoryQuestStatus.Completed;}
        if(TutorialCompleted&&state.defense==StoryQuestStatus.Locked)state.defense=StoryQuestStatus.Available;
        if(state.defense==StoryQuestStatus.Completed&&state.wolf==StoryQuestStatus.Locked)state.wolf=StoryQuestStatus.Available;
    }
    private void Publish() { ReconcileReceipts();Save();Changed?.Invoke(); }
    private void Save() { if(persistStory){PlayerPrefs.SetString(saveKey,CaptureSave());PlayerPrefs.Save();} }
    public string CaptureSave()=>JsonUtility.ToJson(state);
    public void FlushSave()=>Save();
    public bool RestoreSave(string json)
    {
        SaveData data;try{data=JsonUtility.FromJson<SaveData>(json);}catch(ArgumentException){return false;}
        int mask=(1<<defendVillage.requiredCount)-1;
        if(data==null||data.version!=1||float.IsNaN(data.moved)||float.IsInfinity(data.moved)||data.moved<0||data.moved>8||
            (int)data.defense<0||(int)data.defense>4||(int)data.wolf<0||(int)data.wolf>4||(data.defenseMask&~mask)!=0)return false;
        // Reject impossible forward transitions rather than accepting a broken mid-quest record.
        bool tutorial=data.moved>=8&&data.shot&&data.dashed;
        if(!tutorial&&(data.defense!=StoryQuestStatus.Locked||data.wolf!=StoryQuestStatus.Locked||data.defenseMask!=0))return false;
        if(data.defense!=StoryQuestStatus.Completed&&data.wolf!=StoryQuestStatus.Locked)return false;
        if(data.defense<StoryQuestStatus.Active&&data.defenseMask!=0)return false;
        if(data.defense>=StoryQuestStatus.ReadyToTurnIn&&data.defenseMask!=mask)return false;
        if(data.defense==StoryQuestStatus.Active&&data.defenseMask==mask)data.defense=StoryQuestStatus.ReadyToTurnIn;
        state=data;savedMeters=Mathf.FloorToInt(state.moved);previousPosition=transform.position;
        if(wallet!=null)ReconcileReceipts();Changed?.Invoke();return true;
    }
    public Transform ObjectiveTarget
    {
        get
        {
            if(!TutorialCompleted)return null;
            if(DefenseStatus==StoryQuestStatus.Active)return defense.NearestLivingTarget(transform.position)??Guard;
            if(WolfStatus==StoryQuestStatus.Active)
            {
                if(Time.unscaledTime>=nextWolfLookup)
                {
                    nextWolfLookup=Time.unscaledTime+1;wolfTarget=null;
                    foreach(var m in FindObjectsByType<WorldMonsterMember>(FindObjectsSortMode.None))
                        if(m.SpeciesId=="alpha_wolf"&&!m.Defeated&&m.Health.IsAlive){wolfTarget=m.transform;break;}
                }
                return wolfTarget;
            }
            return WolfStatus==StoryQuestStatus.Completed?null:Guard;
        }
    }
    public string ObjectiveText
    {
        get
        {
            if(!TutorialCompleted)return "ฝึกควบคุมผู้กล้า\n"+TutorialText;
            if(DefenseStatus==StoryQuestStatus.Active)return $"Defend the Village\nกำจัดผู้บุกรุก  {DefenseCount}/{defendVillage.requiredCount}";
            if(DefenseStatus==StoryQuestStatus.ReadyToTurnIn)return "Defend the Village\nกลับไปหาทหารยามเพื่อรับรางวัล";
            if(WolfStatus==StoryQuestStatus.Active)return "Hunt the Alpha Wolf\nตามรอยและโค่นราชาหมาป่าใน Thornveil Forest";
            if(WolfStatus==StoryQuestStatus.ReadyToTurnIn)return "Hunt the Alpha Wolf\nกลับไปหาทหารยามเพื่อรับรางวัล";
            if(WolfStatus==StoryQuestStatus.Completed)return "Explore Everwood\nเดินทางต่อเมื่อพร้อม";
            return DefenseStatus==StoryQuestStatus.Completed?"Hunt the Alpha Wolf\nคุยกับทหารยามเพื่อรับภารกิจต่อไป":"Explore Everwood\nไปพบทหารยามที่ Oakshade Village";
        }
    }
}
