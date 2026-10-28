#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StoryQuestSetup
{
    public const string Folder="Assets/_Project/Story/Quests";
    private static TMP_FontAsset font;
    private static readonly Color Ink=new Color(.22f,.13f,.065f);
    private static void Set(UnityEngine.Object owner,string field,object value)
    {
        var so=new SerializedObject(owner);var p=so.FindProperty(field);
        if(value is string s)p.stringValue=s;else if(value is int n)p.intValue=n;else if(value is float f)p.floatValue=f;else if(value is bool b)p.boolValue=b;else p.objectReferenceValue=(UnityEngine.Object)value;
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);
    }
    private static void Array(UnityEngine.Object owner,string field,UnityEngine.Object[] items)
    {var so=new SerializedObject(owner);var p=so.FindProperty(field);p.arraySize=items.Length;for(int i=0;i<items.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=items[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static Sprite Art(string name)=>AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/"+name+".png").OfType<Sprite>().First();
    private static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {
        var r=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
    }
    private static RectTransform Full(string name,Transform parent)
    {var r=Rect(name,parent,0,0,0,0);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    private static TMP_Text Text(string name,Transform parent,float x,float y,float width,float height,string content,float size,Color? color=null)
    {
        var t=Rect(name,parent,x,y,width,height).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=content;t.color=color??Ink;
        t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.alignment=TextAlignmentOptions.TopLeft;return t;
    }
    private static Image Paper(RectTransform r)
    {var i=r.gameObject.AddComponent<Image>();i.sprite=Art("dialogue_box_frame");i.type=Image.Type.Sliced;i.raycastTarget=false;return i;}
    private static Button Button(string name,Transform parent,float x,float y,float width,float height,string content,out TMP_Text label)
    {
        var r=Rect(name,parent,x,y,width,height);var image=r.gameObject.AddComponent<Image>();image.sprite=Art("tooltip_frame");image.type=Image.Type.Sliced;
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
        label=Text("Label",r,14,8,width-28,height-16,content,25);label.alignment=TextAlignmentOptions.Midline;
        var c=button.colors;c.highlightedColor=new Color(1,.87f,.5f);c.pressedColor=new Color(.87f,.7f,.4f);button.colors=c;return button;
    }
    private static RectTransform Window(string name,Transform parent,float width,float height,out RectTransform panel)
    {
        var window=Full(name,parent);var shade=window.gameObject.AddComponent<Image>();shade.color=new Color(.02f,.015f,.01f,.76f);
        panel=Rect("Parchment",window,0,0,width,height);panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one*.5f;panel.anchoredPosition=Vector2.zero;Paper(panel);return window;
    }
    [MenuItem("Tools/Story/Install First Three Story Steps")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        if(UnityEngine.Object.FindAnyObjectByType<PlayerStoryProgress>()!=null)throw new InvalidOperationException("Story already installed; preserve Inspector configuration.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");
        EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeStoryQuests_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();var wallet=player.GetComponent<PlayerGoldWallet>();
        var guard=UnityEngine.Object.FindObjectsByType<StoryActor>(FindObjectsSortMode.None).Single(a=>a.ActorId=="oakshade_guard");
        var director=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>();var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        var fang=ScriptableObject.CreateInstance<ItemDefinition>();fang.name="wolf_fang";
        var oldFang=AssetDatabase.LoadAssetAtPath<ItemDefinition>(MonsterLootSetup.Folder+"/Items/monster_fang.asset");
        Set(fang,"itemId","wolf_fang");Set(fang,"displayName","Wolf Fang");Set(fang,"description","เขี้ยวของราชาหมาป่า รางวัลจากทหารยามแห่ง Oakshade ขายให้กิลด์ได้");
        Set(fang,"icon",oldFang.Icon);Set(fang,"category",(int)InventoryItemCategory.Resource);Set(fang,"maxStack",20);Set(fang,"sellPrice",60);
        AssetDatabase.CreateAsset(fang,Folder+"/wolf_fang.asset");
        var catalog=new SerializedObject(wallet).FindProperty("itemCatalog");var items=Enumerable.Range(0,catalog.arraySize).Select(i=>catalog.GetArrayElementAtIndex(i).objectReferenceValue).Concat(new UnityEngine.Object[]{fang}).ToArray();Array(wallet,"itemCatalog",items);
        var defense=Quest("defend_oakshade","Defend the Village","ช่วย Oakshade Village กำจัดผู้บุกรุก 3 ระลอก ระลอกละ 2 ตัว แล้วรายงานทหารยาม",6,100,AssetDatabase.LoadAssetAtPath<ItemDefinition>(MonsterLootSetup.Folder+"/Items/beast_hide.asset"),2);
        var wolf=Quest("hunt_alpha_wolf","Hunt the Alpha Wolf","โค่น Alpha Wolf ใน Thornveil Forest หลังรับภารกิจ แล้วกลับไปรับรางวัลจากทหารยาม",1,180,fang,1);
        var progress=Undo.AddComponent<PlayerStoryProgress>(player.gameObject);
        var root=new GameObject("Oakshade Village Defense");root.transform.SetParent(layout.village,true);
        if(!NavMesh.SamplePosition(guard.transform.position+guard.transform.forward*3,out var center,5,NavMesh.AllAreas))throw new InvalidOperationException("No navigation near village guard.");
        root.transform.position=center.position;var encounter=root.AddComponent<VillageDefenseEncounter>();
        Set(encounter,"story",progress);Set(progress,"defendVillage",defense);Set(progress,"huntWolf",wolf);Set(progress,"monsters",director);Set(progress,"defense",encounter);Set(progress,"guard",guard);
        var prefabs=new[]{RaidPrefab("GoblinDemon",24,5),RaidPrefab("Skeleton",32,6)};Array(encounter,"enemyPrefabs",prefabs);
        var spawnPoints=Enumerable.Range(0,6).Select(i=>
        {
            float angle=i*60*Mathf.Deg2Rad;Vector3 wanted=center.position+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*10;
            Vector3 point=FindGround(wanted,center.position,layout.terrain,6,15);
            var t=new GameObject("Raid Spawn "+(i+1)).transform;t.SetParent(root.transform,true);t.position=point;return t;
        }).ToArray();Array(encounter,"spawnPoints",spawnPoints);
        var pause=UnityEngine.Object.FindAnyObjectByType<GamePauseController>();var canvas=pause.GetComponent<Canvas>();
        var dialogueRoot=Full("Story Dialogue",canvas.transform);var dialogWindow=Window("Guard Conversation",dialogueRoot,1190,760,out var panel);
        var speaker=Text("Speaker",panel,65,52,1060,52,"Oakshade Guard",34);speaker.fontStyle=FontStyles.Bold;
        var body=Text("Dialogue",panel,65,145,1060,270,"",30);
        var reward=Text("Reward Preview",panel,65,430,1060,48,"",26);
        var status=Text("Result",panel,65,488,1060,76,"",25);
        var primary=Button("Quest Choice",panel,65,582,1060,64,"รับภารกิจ",out var primaryLabel);
        var leave=Button("Leave",panel,65,665,1060,54,"ไว้คราวหน้า / ออกจากบทสนทนา  [Esc]",out _);
        var dialogue=dialogueRoot.gameObject.AddComponent<StoryDialogueUI>();Set(dialogue,"story",progress);Set(dialogue,"pause",pause);Set(dialogue,"window",dialogWindow.gameObject);
        foreach(var entry in new[]{("speaker",speaker),("body",body),("reward",reward),("status",status),("primaryLabel",primaryLabel)})Set(dialogue,entry.Item1,entry.Item2);
        Set(dialogue,"primary",primary);Set(dialogue,"leave",leave);dialogWindow.gameObject.SetActive(false);
        var interaction=Undo.AddComponent<StoryDialogueInteractable>(guard.gameObject);Set(interaction,"dialogue",dialogue);Set(interaction,"interactionPrompt","คุยกับทหารยาม Oakshade");
        var trigger=new GameObject("Story Interaction Trigger");trigger.layer=8;trigger.transform.SetParent(guard.transform,false);
        var capsule=trigger.AddComponent<CapsuleCollider>();capsule.isTrigger=true;capsule.height=1.8f;capsule.radius=.45f;capsule.center=Vector3.up*.9f;
        var hudRoot=Full("Story Quests",canvas.transform);
        var card=Rect("Objective Card",hudRoot,22,350,460,232);var backdrop=card.gameObject.AddComponent<Image>();backdrop.color=new Color(.035f,.045f,.035f,.76f);backdrop.raycastTarget=false;
        var objective=Text("Objective",card,16,12,428,170,"",23,new Color(.95f,.94f,.84f));
        var direction=Text("Target Direction",card,16,188,428,32,"",22,new Color(.95f,.84f,.51f));
        var openJournal=Button("Open Journal",hudRoot,22,590,295,48,"Q  บันทึกภารกิจ",out _);
        var journalWindow=Window("Quest Journal",hudRoot,1200,930,out var journalPanel);
        Text("Title",journalPanel,65,48,1070,55,"บันทึกการเดินทาง — EVERWOOD",34).fontStyle=FontStyles.Bold;
        var journalBody=Text("Quest Entries",journalPanel,65,135,1070,672,"",25);
        var closeJournal=Button("Close Journal",journalPanel,65,835,1070,55,"กลับสู่การเดินทาง  [Q / Esc]",out _);
        var mini=UnityEngine.Object.FindAnyObjectByType<KingdomMinimapUI>();var mapArea=(RectTransform)new SerializedObject(mini).FindProperty("mapArea").objectReferenceValue;
        var marker=Rect("Current Quest Target",mapArea,0,0,22,22);marker.anchorMin=marker.anchorMax=marker.pivot=Vector2.one*.5f;
        var markerText=marker.gameObject.AddComponent<TextMeshProUGUI>();markerText.font=font;markerText.fontSize=25;markerText.alignment=TextAlignmentOptions.Center;markerText.text="!";markerText.color=new Color(1,.78f,.1f);markerText.raycastTarget=false;
        var ui=hudRoot.gameObject.AddComponent<StoryQuestUI>();Set(ui,"story",progress);Set(ui,"pause",pause);Set(ui,"objective",objective);Set(ui,"direction",direction);
        Set(ui,"journalBody",journalBody);Set(ui,"openJournal",openJournal);Set(ui,"closeJournal",closeJournal);Set(ui,"journalWindow",journalWindow.gameObject);Set(ui,"minimap",mini);Set(ui,"objectiveMarker",marker);
        journalWindow.gameObject.SetActive(false);
        var spawn=UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>();BuildTrainingTarget(progress,spawn,layout.terrain);
        // Keep existing inventory/shop surfaces above the quest readout; each modal
        // also moves its own window last when opened.
        hudRoot.SetAsFirstSibling();dialogueRoot.SetAsLastSibling();
        EditorUtility.SetDirty(wallet);PrefabUtility.RecordPrefabInstancePropertyModifications(wallet);
        EditorUtility.SetDirty(progress);PrefabUtility.RecordPrefabInstancePropertyModifications(progress);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report=new StringBuilder("First three story steps installed "+DateTime.Now.ToString("s")+"\n");
        report.AppendLine("Guard="+guard.transform.position+" encounter="+center.position+" spawn="+spawn.Position);
        foreach(var point in spawnPoints)report.AppendLine(point.name+"="+point.position);
        report.AppendLine("Defense: 3 waves × 2; 100 Gold + Beast Hide ×2. Wolf: 180 Gold + Wolf Fang ×1.");
        File.WriteAllText("Logs/StoryQuestInstallation.txt",report.ToString());Debug.Log(report.ToString());
    }
    private static StoryQuestDefinition Quest(string id,string title,string description,int count,int gold,ItemDefinition item,int amount)
    {
        var q=ScriptableObject.CreateInstance<StoryQuestDefinition>();q.questId=id;q.title=title;q.description=description;q.requiredCount=count;q.rewardGold=gold;q.rewardItem=item;q.rewardQuantity=amount;
        AssetDatabase.CreateAsset(q,Folder+"/"+id+".asset");return q;
    }
    private static GameObject RaidPrefab(string kind,int hp,int damage)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_"+kind+".prefab");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);instance.name="Raid_"+kind;instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        Set(instance.GetComponent<EnemyHealth>(),"maxHealth",(float)hp);Set(instance.GetComponent<EnemyHealth>(),"experienceReward",12);Set(instance.GetComponent<EnemyAttack>(),"damage",damage);
        Set(instance.GetComponent<EnemyAI>(),"huntCivilians",false);Set(instance.GetComponent<EnemyAI>(),"detectionRange",20f);Set(instance.GetComponent<EnemyAI>(),"loseTargetRange",28f);
        Set(instance.GetComponent<EnemyAI>(),"patrolRadius",5f);Set(instance.GetComponent<EnemyAI>(),"chaseSpeed",3.1f);
        var result=PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/Raid_"+kind+".prefab");UnityEngine.Object.DestroyImmediate(instance);return result;
    }
    private static Vector3 FindGround(Vector3 desired,Vector3 connected,Terrain terrain,float min,float max)
    {
        var path=new NavMeshPath();
        for(int i=0;i<100;i++)
        {
            float a=i*2.399f;Vector3 p=desired+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*(i*.15f);
            p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
            float d=WorldMonsterDirector.HorizontalDistance(p,connected);if(d<min||d>max)continue;
            if(!NavMesh.SamplePosition(p,out var nav,1,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-p.y)>.5f)continue;
            var colliders=Physics.OverlapCapsule(nav.position+Vector3.up*.7f,nav.position+Vector3.up*1.7f,.55f,Physics.AllLayers,QueryTriggerInteraction.Ignore);
            if(colliders.Any(c=>!(c is TerrainCollider)))continue;
            if(!NavMesh.CalculatePath(nav.position,connected,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            return nav.position;
        }
        throw new InvalidOperationException("No unobstructed reachable ground near "+desired);
    }
    private static void BuildTrainingTarget(PlayerStoryProgress story,PlayerSceneSpawnPoint spawn,Terrain terrain)
    {
        var target=new GameObject("Arcane Practice Target"){layer=6};target.transform.SetParent(spawn.transform,true);
        target.transform.position=FindGround(spawn.Position+new Vector3(5,0,5),spawn.Position,terrain,4,13);
        target.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(spawn.Position-target.transform.position,Vector3.up));
        var health=target.AddComponent<EnemyHealth>();Set(health,"experienceReward",0);
        var aim=new GameObject("Aim Point").transform;aim.SetParent(target.transform,false);aim.localPosition=Vector3.up*1.2f;
        var targetable=target.AddComponent<Targetable>();Set(targetable,"aimPoint",aim);
        var collider=target.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.9f;collider.height=1.8f;collider.radius=.55f;
        Color[] colors={new Color(.26f,.12f,.055f),new Color(.8f,.74f,.54f),new Color(.5f,.13f,.09f)};
        var mats=colors.Select((c,i)=>{var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Practice Target "+i};m.color=c;AssetDatabase.CreateAsset(m,Folder+"/PracticeTarget_"+i+".mat");return m;}).ToArray();
        Primitive("Post",PrimitiveType.Cube,target.transform,new Vector3(0,.8f,0),new Vector3(.22f,1.6f,.22f),Quaternion.identity,mats[0]);
        Primitive("Target Board",PrimitiveType.Cylinder,target.transform,new Vector3(0,1.2f,.12f),new Vector3(1.1f,.07f,1.1f),Quaternion.Euler(90,0,0),mats[1]);
        Primitive("Bullseye",PrimitiveType.Cylinder,target.transform,new Vector3(0,1.2f,.2f),new Vector3(.45f,.012f,.45f),Quaternion.Euler(90,0,0),mats[2]);
        var practice=target.AddComponent<StoryTrainingTarget>();Set(practice,"story",story);
    }
    private static void Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
    {var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.layer=6;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;obj.transform.localRotation=rotation;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=material;}
    [MenuItem("Tools/Story/Refine Story Text Layout")]
    public static void RefineTextLayout()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var dialogue=UnityEngine.Object.FindAnyObjectByType<StoryDialogueUI>();var quests=UnityEngine.Object.FindAnyObjectByType<StoryQuestUI>();
        foreach(var owner in new MonoBehaviour[]{dialogue,quests})foreach(var text in owner.GetComponentsInChildren<TMP_Text>(true))
        {
            text.overflowMode=TextOverflowModes.Overflow;EditorUtility.SetDirty(text);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
#endif
