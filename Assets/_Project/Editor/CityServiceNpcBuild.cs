#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class CityServiceNpcSetup
{
    private static bool previewBackground,previewActive;
    static CityServiceNpcSetup()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingPlayMode&&previewActive){Application.runInBackground=previewBackground;previewActive=false;}};
    }
    [MenuItem("Tools/NPC/City Services/Link Contacts to Scene Player")]
    public static void LinkPlayer()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        foreach(var npc in UnityEngine.Object.FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None))
        {var look=npc.GetComponent<NpcIdleLook>();Undo.RecordObject(look,"Link service contact to player");look.SetLookTarget(player.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(look);}
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
    }
    [MenuItem("Tools/NPC/City Services/Use Relaxed Merchant Idle")]
    public static void RelaxedIdle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        string path=Folder+"/Service_Contacts.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller==null)
        {
            controller=AnimatorController.CreateAnimatorControllerAtPath(path);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            var machine=controller.layers[0].stateMachine;var idle=machine.AddState("Locomotion");idle.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(NpcMerchantSetup.Folder+"/Merchant_Idle.anim");machine.defaultState=idle;
            machine.AddState("Talking").motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(TownNpcSetup.Folder+"/Animations/Citizen_Idle_Talking_Loop.anim");
            EditorUtility.SetDirty(controller);
        }
        foreach(var asset in AssetDatabase.FindAssets("t:Prefab",new[]{Folder+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var contents=PrefabUtility.LoadPrefabContents(asset);
            try{var animator=contents.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=controller;animator.Rebind();animator.Update(0);animator.Play("Locomotion",0,.15f);animator.Update(0);PrefabUtility.SaveAsPrefabAsset(contents,asset);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());ValidateAndCapture();
    }
    [MenuItem("Tools/NPC/City Services/Preview Hotel Host in Play Mode")]
    public static void PreviewHotelHost()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        if(!previewActive){previewBackground=Application.runInBackground;previewActive=true;}Application.runInBackground=true;
        foreach(var spawner in UnityEngine.Object.FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        var npc=Building("Hotel").GetComponentInChildren<CityServiceNpc>();var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();var controller=player.GetComponent<CharacterController>();
        controller.enabled=false;player.transform.position=npc.InteractionPoint.position+Vector3.up*.05f;controller.enabled=true;Physics.SyncTransforms();
        Selection.activeGameObject=npc.gameObject;
    }
    [MenuItem("Tools/NPC/City Services/Clear Hotel Host from Market Awning")]
    public static void ClearHotelAwning()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var hotel=Building("Hotel");var npc=hotel.GetComponentInChildren<CityServiceNpc>();var door=Door(hotel);
        var forward=Vector3.ProjectOnPlane(door.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
        var position=door.position+forward*1.6f-right*1.65f;
        if(!NavMesh.SamplePosition(position,out var nav,1,NavMesh.AllAreas))throw new InvalidOperationException("No hotel approach navigation.");position=nav.position;
        if(Physics.Raycast(position+Vector3.up*.45f,Vector3.down,out var floor,1.3f,1<<7,QueryTriggerInteraction.Ignore))position.y=floor.point.y+.01f;
        Undo.RecordObject(npc.transform,"Clear hotel host from neighbouring market canopy");npc.transform.position=position;PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
        EditorSceneManager.MarkSceneDirty(hotel.gameObject.scene);EditorSceneManager.SaveScene(hotel.gameObject.scene);ValidateAndCapture();
    }
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private static Transform Door(Transform building)
    {
        var apothecary=building.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="04_Apothecary");
        var scope=apothecary!=null?apothecary:building;
        return scope.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.Contains("Wall_")&&t.name.Contains("_Door"))??throw new InvalidOperationException("No door in "+building.name);
    }
    private static Vector3 StandPoint(Transform door)
    {
        var forward=Vector3.ProjectOnPlane(door.forward,Vector3.up).normalized;
        var right=Vector3.Cross(Vector3.up,forward);
        foreach(float distance in new[]{1.3f,1.6f,1.9f})foreach(float side in new[]{1.35f,-1.35f,1.8f,-1.8f})
        {
            var point=door.position+forward*distance+right*side;
            if(!NavMesh.SamplePosition(point,out var nav,1.1f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-point.y)>.55f)continue;
            point=nav.position;
            if(Physics.Raycast(point+Vector3.up*.45f,Vector3.down,out var floor,1.3f,1<<7,QueryTriggerInteraction.Ignore))point.y=floor.point.y+.01f;
            if(!Physics.CheckCapsule(point+Vector3.up*.35f,point+Vector3.up*1.48f,.31f,1<<7,QueryTriggerInteraction.Ignore))return point;
        }
        throw new InvalidOperationException("Cannot stand beside "+PathOf(door));
    }
    private static GameObject Prefab(string palette,CityServiceRole role,string id,string title)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(TownNpcDiversitySetup.Folder+"/Prefabs/Citizen_Female_"+palette+".prefab");
        var go=UnityEngine.Object.Instantiate(source);go.name="NPC_"+role+"_Female";
        try
        {
            if(PrefabUtility.IsPartOfPrefabInstance(go))PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<TownNpcBrain>());UnityEngine.Object.DestroyImmediate(go.GetComponent<CivilianHealth>());UnityEngine.Object.DestroyImmediate(go.GetComponent<NavMeshAgent>());
            var look=go.AddComponent<NpcIdleLook>();var lookData=new SerializedObject(look);
            lookData.FindProperty("npcId").stringValue=id;lookData.FindProperty("displayName").stringValue=title;lookData.FindProperty("facingRoot").objectReferenceValue=go.transform.Find("Facing");
            lookData.FindProperty("noticeRadius").floatValue=3.5f;lookData.FindProperty("releaseRadius").floatValue=4.5f;lookData.ApplyModifiedPropertiesWithoutUndo();
            var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Capsule;obstacle.radius=.34f;obstacle.height=1.78f;obstacle.center=Vector3.up*.89f;obstacle.carving=true;obstacle.carveOnlyStationary=true;
            var contact=go.AddComponent<CityServiceNpc>();var data=new SerializedObject(contact);data.FindProperty("role").enumValueIndex=(int)role;data.FindProperty("animator").objectReferenceValue=go.GetComponentInChildren<Animator>();
            var point=new GameObject("Service_Interaction_Point").transform;point.SetParent(go.transform,false);point.localPosition=Vector3.forward*1.45f;point.localRotation=Quaternion.Euler(0,180,0);data.FindProperty("interactionPoint").objectReferenceValue=point;data.ApplyModifiedPropertiesWithoutUndo();
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
            return PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Prefabs/"+go.name+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/NPC/City Services/Place Three Female Service NPCs")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(UnityEngine.Object.FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None).Length>0)throw new InvalidOperationException("Service NPCs already placed; preserve hand edits.");
        var buildings=new[]{Building("Hotel"),Building("Gild"),Building("Potion Store")};
        var doors=buildings.Select(Door).ToArray();var positions=doors.Select(StandPoint).ToArray();
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Logs/SceneBackups/WorldPrototype_BeforeServiceNPCs_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var original=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var poses=original.Keys.OfType<Transform>().ToDictionary(t=>t,t=>t.localToWorldMatrix);
        var parents=poses.Keys.ToDictionary(t=>t,t=>t.parent);
        EnsureFolder(Folder+"/Prefabs");
        var prefabs=new[]{Prefab("Amber_Buns",CityServiceRole.HotelSave,"city_hotel_host_01","Hotel Host"),Prefab("Plum_Buns",CityServiceRole.GuildBuyer,"city_guild_clerk_01","Guild Buyer"),Prefab("Teal_Long",CityServiceRole.PotionStore,"city_potion_merchant_01","Potion Merchant")};
        for(int i=0;i<buildings.Length;i++)
        {
            var npc=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[i],buildings[i]);Undo.RegisterCreatedObjectUndo(npc,"Place female service NPC");
            npc.transform.position=positions[i];npc.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(doors[i].forward,Vector3.up));
            var scale=buildings[i].lossyScale;npc.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var data=new SerializedObject(npc.GetComponent<CityServiceNpc>());data.FindProperty("building").objectReferenceValue=buildings[i];data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(npc.GetComponent<CityServiceNpc>());
        }
        // New children change only their building's child list; all existing placements/components stay intact.
        bool preserved=original.All(p=>p.Key!=null&&(p.Key is Transform t?poses[t]==t.localToWorldMatrix&&parents[t]==t.parent:EditorJsonUtility.ToJson(p.Key)==p.Value));
        File.WriteAllText("Logs/CityServiceNpcScope.txt",(preserved?"PASS":"FAIL")+" existing non-transform components preserved; original building and world transforms were not edited. Compared="+original.Count+"\n");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        ValidateAndCapture();Selection.activeGameObject=buildings[0].GetComponentInChildren<CityServiceNpc>().gameObject;
    }
    [MenuItem("Tools/NPC/City Services/Validate and Capture Service NPCs")]
    public static void ValidateAndCapture()
    {
        Physics.SyncTransforms();var report=new StringBuilder("Female city service NPC placement. Playing="+Application.isPlaying+"\n");bool passed=true;
        var contacts=UnityEngine.Object.FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None);passed&=contacts.Length==3;
        foreach(var npc in contacts)
        {
            var animator=npc.GetComponentInChildren<Animator>();var look=npc.GetComponent<NpcIdleLook>();
            bool valid=animator!=null&&animator.avatar.isHuman&&animator.avatar.isValid&&npc.Building!=null&&npc.transform.IsChildOf(npc.Building)&&npc.InteractionPoint!=null&&npc.GetComponent<TownNpcBrain>()==null&&npc.GetComponent<NavMeshAgent>()==null&&npc.GetComponent<EnemyHealth>()==null&&npc.GetComponent<Targetable>()==null&&npc.GetComponent<CapsuleCollider>()!=null;
            passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+npc.Role+" female humanoid, stationary contact with future interaction anchor; pos="+npc.transform.position.ToString("F3")+" building="+PathOf(npc.Building));
            if(Application.isPlaying)report.AppendLine("LIVE "+npc.Role+" watchingPlayer="+look.IsWatching+" talkingAnimation="+animator.GetCurrentAnimatorStateInfo(0).IsName("Talking")+" initialized="+animator.isInitialized+" controller="+animator.runtimeAnimatorController.name+" state="+animator.GetCurrentAnimatorStateInfo(0).shortNameHash+" playerHealth="+UnityEngine.Object.FindAnyObjectByType<PlayerHealth>().CurrentHealth);
            bool clear=!Physics.CheckCapsule(npc.transform.position+Vector3.up*.35f,npc.transform.position+Vector3.up*1.48f,.3f,1<<7,QueryTriggerInteraction.Ignore);
            bool reachable=NavMesh.SamplePosition(npc.InteractionPoint.position,out _,.9f,NavMesh.AllAreas);
            passed&=clear&&reachable;report.AppendLine((clear&&reachable?"PASS ":"FAIL ")+"clear of scenery; service approach has walkable navigation");
            Render("CityServices_"+npc.Role,npc.transform.position+Vector3.up*1.3f,npc.transform.forward*7+Vector3.up*4+npc.transform.right*3,4.7f);
        }
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/CityServiceNpcPlacement.txt",report.ToString());
        if(!passed)throw new InvalidOperationException("Check CityServiceNpcPlacement.txt");Debug.Log("Three female service NPCs placed and validated.");
    }
}
#endif
