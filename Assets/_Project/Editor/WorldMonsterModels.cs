#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static partial class WorldMonsterSetup
{
    private static void Float(Component c,string field,float value){var s=new SerializedObject(c);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Int(Component c,string field,int value){var s=new SerializedObject(c);s.FindProperty(field).intValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Reference(Component c,string field,UnityEngine.Object value){var s=new SerializedObject(c);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static AnimationClip Clip(AnimationClip source,string name,bool loop)
    {
        string path=Folder+"/Animations/"+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old!=null)return old;
        var clip=UnityEngine.Object.Instantiate(source);clip.name=name;AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,path);return clip;
    }
    private static RuntimeAnimatorController Controller(bool goblin)
    {
        string name=goblin?"GoblinDemon":"Skeleton",path=Folder+"/"+name+".controller";var old=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(old!=null)return old;
        AssetDatabase.CopyAsset("Assets/_Project/Animations/Enemies/EnemyAnimator.controller",path);var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var native=AssetDatabase.LoadAllAssetsAtPath(GoblinModel).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Native(string take)=>native.Single(c=>c.name==take);
        AnimationClip Human(string file)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(TownNpcSetup.Folder+"/Animations/"+file+".anim");
        var idle=Clip(goblin?Native("Idle_1_weapon"):Human("Citizen_Idle"),name+"_Idle",true);
        var walk=Clip(goblin?Native("Run_weapon"):Human("Citizen_Walk"),name+"_Walk",true);
        var run=Clip(goblin?Native("Run_weapon"):Human("Citizen_Run"),name+"_Run",true);
        var attack=Clip(goblin?Native("Attack_2"):AssetDatabase.LoadAllAssetsAtPath(TownNpcSetup.Ual).OfType<AnimationClip>().Single(c=>c.name=="Armature|Punch_Cross"),name+"_Attack",false);
        var death=Clip(goblin?Native("Idle_1_weapon"):Human("Citizen_Death"),name+"_Death",false);
        var states=controller.layers[0].stateMachine.states.Select(s=>s.state).ToArray();var blend=(BlendTree)states.Single(s=>s.name=="Locomotion").motion;
        blend.useAutomaticThresholds=false;blend.children=new[]{new ChildMotion{motion=idle,threshold=0,timeScale=1},new ChildMotion{motion=walk,threshold=.5f,timeScale=goblin?.45f:1},new ChildMotion{motion=run,threshold=1,timeScale=1}};
        states.Single(s=>s.name=="Attack").motion=attack;states.Single(s=>s.name=="Death").motion=death;
        foreach(var state in states)EditorUtility.SetDirty(state);EditorUtility.SetDirty(blend);EditorUtility.SetDirty(controller);return controller;
    }
    [MenuItem("Tools/Enemies/World Population/Build Goblin and Skeleton Enemies")]
    public static void BuildModels()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        EnsureFolder(Folder+"/Animations");EnsureFolder(Folder+"/Materials");
        foreach(bool goblin in new[]{true,false})
        {
            string kind=goblin?"GoblinDemon":"Skeleton",path="Assets/_Project/Prefabs/Enemies/Enemy_"+kind+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)continue;
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/EnemyBase.prefab"));root.name="Enemy_"+kind;
            try
            {
                if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(Transform child in root.transform.Find("ModelRoot").Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                var pivot=new GameObject("WorldMonsterVisual").transform;pivot.SetParent(root.transform.Find("ModelRoot"),false);pivot.localRotation=Quaternion.Euler(0,goblin?0:180,0);
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(goblin?GoblinSource:SkeletonSource),pivot);model.transform.localPosition=Vector3.zero;
                if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in model.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in model.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(c);
                var animator=model.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=Controller(goblin);animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                if(!goblin)
                {
                    if(!animator.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("Skeleton avatar is invalid.");
                    var bone=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Skeleton_Old_Ivory"};bone.SetColor("_BaseColor",new Color(.69f,.65f,.51f));bone.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(bone,Folder+"/Materials/Skeleton_Old_Ivory.mat");
                    foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterial=bone;
                }
                animator.Rebind();animator.Play("Locomotion",0,.15f);animator.Update(0);
                var geometry=NpcMerchantSetup.GeometryBounds(model);pivot.localScale=Vector3.one*((goblin?1.95f:1.85f)/geometry.size.y);geometry=NpcMerchantSetup.GeometryBounds(model);pivot.localPosition-=Vector3.up*geometry.min.y;
                var agent=root.GetComponent<NavMeshAgent>();agent.height=goblin?2:1.9f;agent.radius=.36f;agent.speed=goblin?4:3.8f;
                var capsule=root.GetComponent<CapsuleCollider>();capsule.height=goblin?1.9f:1.82f;capsule.radius=.34f;capsule.center=Vector3.up*capsule.height*.5f;
                Float(root.GetComponent<EnemyAI>(),"patrolSpeed",goblin?1.55f:1.45f);Float(root.GetComponent<EnemyAI>(),"chaseSpeed",agent.speed);Float(root.GetComponent<EnemyAI>(),"eyeHeight",1.5f);Float(root.GetComponent<EnemyAI>(),"patrolRadius",goblin?5:6);Float(root.GetComponent<EnemyAI>(),"attackRange",1.8f);
                Float(root.GetComponent<EnemyHealth>(),"maxHealth",goblin?170:150);Int(root.GetComponent<EnemyHealth>(),"experienceReward",goblin?100:75);
                Int(root.GetComponent<EnemyAttack>(),"damage",goblin?16:14);Float(root.GetComponent<EnemyAttack>(),"hitDelay",goblin?.42f:.24f);Float(root.GetComponent<EnemyAttack>(),"attackCooldown",goblin?1.6f:1.3f);
                foreach(var c in new Component[]{root.GetComponent<EnemyAttack>(),root.GetComponent<EnemyAnimationController>(),root.GetComponent<EnemyDeathController>()})Reference(c,"animator",animator);
                if(goblin)
                {
                    var fall=root.AddComponent<EnemyDeathVisualFall>();Reference(fall,"visual",pivot);
                    var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;source.maxDistance=24;source.minDistance=3;source.volume=.4f;
                    var voice=root.AddComponent<EnemyVoice>();Reference(voice,"voice",source);Reference(voice,"alertClip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Mobs/Goblin/speaking.mp3"));Reference(voice,"attackClip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Mobs/Goblin/scream.mp3"));Reference(voice,"deathClip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Mobs/Goblin/scream.mp3"));
                }
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=6;
                PrefabUtility.SaveAsPrefabAsset(root,path);Render("WorldMonster_Ready_"+kind,geometry.center,new Vector3(3,2,5),1.8f);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        string chief="Assets/_Project/Prefabs/Enemies/Enemy_GoblinChief.prefab";
        if(!File.Exists(chief))
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_GoblinDemon.prefab"));
            try{go.name="Enemy_GoblinChief";go.transform.localScale=Vector3.one*1.15f;Float(go.GetComponent<EnemyHealth>(),"maxHealth",320);Int(go.GetComponent<EnemyHealth>(),"experienceReward",220);Int(go.GetComponent<EnemyAttack>(),"damage",23);PrefabUtility.SaveAsPrefabAsset(go,chief);}
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();Debug.Log("Goblin demo and Skeleton enemy prefabs built with combat and EXP.");
    }
}
#endif
