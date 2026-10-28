#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class StoryCreatureSetup
{
    public const string SpiderPrefab = "Assets/_Project/Prefabs/Enemies/Enemy_Spider.prefab";
    public const string WolfPrefab = "Assets/_Project/Prefabs/Enemies/Enemy_AlphaWolf.prefab";
    public const string WizardPrefab = "Assets/_Project/Story/NPCs/NPC_ElderWizard_Placeholder.prefab";
    private static void Ref(Component owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Number(Component owner,string field,float value)
    {var s=new SerializedObject(owner);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Integer(Component owner,string field,int value)
    {var s=new SerializedObject(owner);s.FindProperty(field).intValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Text(Component owner,string field,string value)
    {var s=new SerializedObject(owner);s.FindProperty(field).stringValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Actor(GameObject root,string id,string name,string role)
    {
        var actor=root.GetComponent<StoryActor>()??root.AddComponent<StoryActor>();
        Text(actor,"actorId",id);Text(actor,"displayName",name);Text(actor,"storyRole",role);
    }
    private static void ImportRig(string path,string root)
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.motionNodeName=root;importer.optimizeGameObjects=false;importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
    }
    private static Texture2D Texture(string path,bool normal=false)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer==null)throw new InvalidOperationException("Texture not imported: "+path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=!normal;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Compressed;
        importer.mipmapEnabled=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Material CreatureMaterial(string kind)
    {
        string path=Folder+"/"+kind+"/"+kind+"_URP.mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=kind+"_URP",enableInstancing=true};
        material.SetTexture("_BaseMap",Texture(kind=="GiantSpider"?Folder+"/GiantSpider/Textures/GiantSpider_BaseColor.png":WolfPack+"/Textures/Wolfboss_Albedo.png"));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",kind=="GiantSpider"?.18f:.12f);
        if(kind=="GiantSpider")
        {
            material.SetTexture("_BumpMap",Texture(Folder+"/GiantSpider/Textures/GiantSpider_Normal.png",true));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.75f);
            material.SetFloat("_Cull",0);
        }
        else {material.SetTexture("_BumpMap",Texture(WolfPack+"/Textures/Wolfboss_Normal.png",true));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.7f);}
        AssetDatabase.CreateAsset(material,path);return material;
    }
    private static GameObject AddModel(GameObject root,string source,string kind,float height,Dictionary<string,AnimationClip> clips=null)
    {
        // Prefab roots can retain an authoring-scene offset. Normalize before measuring the new visual.
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var holder=root.transform.Find("ModelRoot");
        foreach(Transform child in holder.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        var pivot=new GameObject("CreatureVisual").transform;pivot.SetParent(holder,false);
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source),pivot);model.name=kind+" Visual";
        if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(component);
        var animator=model.GetComponent<Animator>()??model.AddComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.sharedMaterial=CreatureMaterial(kind);skin.updateWhenOffscreen=true;}
        if(clips==null)
        {
            if(kind=="GiantSpider")clips=new Dictionary<string,AnimationClip>{
                ["Idle"]=CopyClip(source,"idle_loop",kind,"Idle",true),["Walk"]=CopyClip(source,"walk_loop",kind,"Walk",true),
                ["Run"]=CopyClip(source,"run_loop",kind,"Run",true),["Attack"]=CopyClip(source,"bite",kind,"Attack",false),["Death"]=SpiderDeath(model)};
            else clips=WolfAnimations(model);
        }
        animator.runtimeAnimatorController=CreatureController(kind,clips);animator.Rebind();animator.Play("Locomotion",0,0);animator.Update(0);
        var head=Bone(model,kind=="GiantSpider"?"head":"Bip01 Head");var body=Bone(model,kind=="GiantSpider"?"body":"Bip01 Pelvis");
        if(Vector3.Dot(head.position-body.position,Vector3.forward)<0)pivot.localRotation=Quaternion.Euler(0,180,0);
        var bounds=NpcMerchantSetup.GeometryBounds(model);
        pivot.localScale=Vector3.one*height/bounds.size.y;bounds=NpcMerchantSetup.GeometryBounds(model);pivot.localPosition-=Vector3.up*bounds.min.y;
        if(kind=="WolfBoss")pivot.localPosition-=new Vector3(body.position.x,0,body.position.z);
        foreach(var owner in new Component[]{root.GetComponent<EnemyAttack>(),root.GetComponent<EnemyAnimationController>(),root.GetComponent<EnemyDeathController>()})Ref(owner,"animator",animator);
        foreach(var node in model.GetComponentsInChildren<Transform>(true))node.gameObject.layer=6;pivot.gameObject.layer=6;
        return model;
    }
    [MenuItem("Tools/Story Creatures/Install Wolf Spider and Elder Wizard")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefab)!=null)throw new InvalidOperationException("Story creatures already installed. Preserve manual edits to the resulting prefabs.");
        string backup="Logs/SceneBackups/StoryCreatures_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        EditorSceneManager.SaveScene(scene,backup+"/Before.unity",true);File.Copy(SpiderPrefab,backup+"/Enemy_Spider.prefab");
        foreach(string kind in new[]{"WolfBoss","GiantSpider"})Directory.CreateDirectory(Folder+"/"+kind);AssetDatabase.Refresh();
        ImportRig(SpiderSource,"root");ImportRig(WolfSource,"Bip01");
        var spider=PrefabUtility.LoadPrefabContents(SpiderPrefab);
        try
        {
            AddModel(spider,SpiderSource,"GiantSpider",.8f);
            // Reuse the balanced existing spider's health, damage, navigation, voices, drop table and GUID.
            Number(spider.GetComponent<EnemyAttack>(),"hitDelay",.42f);
            spider.transform.Find("AimPoint").localPosition=new Vector3(0,.52f,.15f);
            PrefabUtility.SaveAsPrefabAsset(spider,SpiderPrefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(spider);}
        BuildWolf();BuildWizard();
        var director=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>();
        var species=director.species.ToList();
        species.Add(new WorldMonsterSpecies{id="alpha_wolf",prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefab),population=1,habitat=WorldMonsterHabitat.Forest});
        director.species=species.ToArray();EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/StoryCreatureInstallation.txt","Replaced Enemy_Spider visual with Giant Spider Lite, preserving its prefab GUID, balanced combat, sounds and loot.\nAlpha Wolf: native Wolf Boss animations. One forest population slot.\nElder wizard: temporary existing HumanMale with white hair/beard, cloth and staff near summoning ruins; replace the visual later.\nQuest/cinematic progression remains separate work.\nBackup: "+backup+"\n");
        CaptureCreatures();Debug.Log("Wolf, Giant Spider and elder wizard installed. Source models and dragon animations preserved.");
    }
    private static void BuildWolf()
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/EnemyBase.prefab"));root.name="Enemy_AlphaWolf";
        try
        {
            if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            AddModel(root,WolfSource,"WolfBoss",1.5f);
            var agent=root.GetComponent<NavMeshAgent>();agent.height=1.3f;agent.radius=.48f;agent.speed=3.8f;agent.acceleration=16;agent.angularSpeed=540;
            var capsule=root.GetComponent<CapsuleCollider>();capsule.height=1.1f;capsule.radius=.45f;capsule.center=new Vector3(0,.55f,.15f);
            Number(root.GetComponent<EnemyHealth>(),"maxHealth",160);Integer(root.GetComponent<EnemyHealth>(),"experienceReward",180);
            Number(root.GetComponent<EnemyAI>(),"patrolSpeed",1.35f);Number(root.GetComponent<EnemyAI>(),"chaseSpeed",3.8f);Number(root.GetComponent<EnemyAI>(),"eyeHeight",1.05f);
            Number(root.GetComponent<EnemyAI>(),"attackRange",1.95f);Number(root.GetComponent<EnemyAI>(),"patrolRadius",7);
            Integer(root.GetComponent<EnemyAttack>(),"damage",14);Number(root.GetComponent<EnemyAttack>(),"hitDelay",.85f);Number(root.GetComponent<EnemyAttack>(),"attackCooldown",2.7f);
            Number(root.GetComponent<EnemyDeathController>(),"destroyDelay",Mathf.Max(4,AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/WolfBoss/Death.anim").length+.25f));
            root.transform.Find("AimPoint").localPosition=new Vector3(0,.72f,.15f);
            Actor(root,"alpha_wolf","Alpha Wolf","Forest special enemy; reserved for Hunt the Alpha Wolf");
            var drops=root.GetComponent<EnemyLootDropper>()??root.AddComponent<EnemyLootDropper>();Ref(drops,"table",AssetDatabase.LoadAssetAtPath<MonsterDropTable>("Assets/_Project/Items/MonsterLoot/Tables/Bear.asset"));
            PrefabUtility.SaveAsPrefabAsset(root,WolfPrefab);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
#endif
