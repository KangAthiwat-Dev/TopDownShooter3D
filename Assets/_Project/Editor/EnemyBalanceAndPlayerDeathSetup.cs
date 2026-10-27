#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class EnemyBalanceAndPlayerDeathSetup
{
    public const string DeathPath="Assets/_Project/Animations/Player/Player_Death.anim";
    private const string Controller="Assets/_Project/Animations/Player/PlayerAnimator.controller";
    private sealed class Profile
    {
        public string name; public float health,chase,patrol,cooldown;public int damage;
        public Profile(string name,float health,float chase,float patrol,int damage,float cooldown)
        {this.name=name;this.health=health;this.chase=chase;this.patrol=patrol;this.damage=damage;this.cooldown=cooldown;}
    }
    private static readonly Profile[] Profiles={
        new Profile("Enemy_Spider",32,3.6f,1.3f,6,1.8f),
        new Profile("Enemy_Imp_Green",40,3.2f,1.2f,6,1.5f),
        new Profile("Enemy_Imp_Blue",56,3.8f,1.4f,8,1.6f),
        new Profile("Enemy_Imp_Red",72,3.3f,1.25f,10,1.75f),
        new Profile("Enemy_Puglin",96,2.8f,1.1f,12,2f),
        new Profile("Enemy_Bear",120,3.2f,1.1f,14,2.2f),
        new Profile("Enemy_Skeleton",48,2.9f,1.1f,8,1.7f),
        new Profile("Enemy_GoblinDemon",64,3.4f,1.3f,8,1.8f),
        new Profile("Enemy_GoblinChief",112,3.2f,1.2f,12,2f)
    };
    private static string Prefab(string name)=>"Assets/_Project/Prefabs/Enemies/"+name+".prefab";
    private static float Number(Component owner,string field)=>new SerializedObject(owner).FindProperty(field).floatValue;
    private static void Float(Component owner,string field,float value)
    {var s=new SerializedObject(owner);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Apply(GameObject actor,Profile p)
    {
        var health=actor.GetComponent<EnemyHealth>();Float(health,"maxHealth",p.health);
        var ai=actor.GetComponent<EnemyAI>();Float(ai,"chaseSpeed",p.chase);Float(ai,"patrolSpeed",p.patrol);
        var attack=actor.GetComponent<EnemyAttack>();var data=new SerializedObject(attack);data.FindProperty("damage").intValue=p.damage;
        data.FindProperty("attackCooldown").floatValue=p.cooldown;data.ApplyModifiedPropertiesWithoutUndo();
        var nav=actor.GetComponent<NavMeshAgent>();nav.speed=p.chase;
        foreach(var c in new Component[]{health,ai,attack,nav}){EditorUtility.SetDirty(c);if(PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
    }

    [MenuItem("Tools/Combat Balance/Inspect Current Enemies")]
    public static void Inspect()
    {
        var report=new StringBuilder("Enemy balance audit "+DateTime.Now.ToString("s")+"\n");
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/Prefabs/Enemies"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);var hp=go.GetComponent<EnemyHealth>();var nav=go.GetComponent<NavMeshAgent>();var ai=go.GetComponent<EnemyAI>();
            float chase=ai!=null?Number(ai,"chaseSpeed"):0;if(chase<=0&&nav!=null)chase=nav.speed;
            report.AppendLine(go.name+" hp="+(hp!=null?hp.MaxHealth:0)+" chase="+chase+" agent="+(nav!=null?nav.speed:0)+" exp="+(hp!=null?hp.ExperienceReward:0));
        }
        foreach(var hp in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            report.AppendLine("SCENE "+hp.name+" hp="+hp.MaxHealth+" source="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hp.gameObject));
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EnemyBalanceAudit.txt",report.ToString());Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Combat Balance/Apply Fair Monster Stats and Player Death")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(UnityEngine.Object.FindAnyObjectByType<PlayerDeathPresentation>()!=null)throw new InvalidOperationException("Death and balance pass already installed. Edit the resulting prefab values normally.");
        Inspect();string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");string backup="Logs/SceneBackups/EnemyBalance_"+stamp;Directory.CreateDirectory(backup);
        var scene=SceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene,backup+"/Before.unity",true);File.Copy(Controller,backup+"/PlayerAnimator.controller");
        File.Copy("Logs/EnemyBalanceAudit.txt",backup+"/OriginalStats.txt");
        var report=new StringBuilder("Monster rebalance and player death "+DateTime.Now.ToString("s")+"\n");
        foreach(var p in Profiles)
        {
            string path=Prefab(p.name);File.Copy(path,backup+"/"+p.name+".prefab");
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                float oldHp=root.GetComponent<EnemyHealth>().MaxHealth;
                float oldChase=Number(root.GetComponent<EnemyAI>(),"chaseSpeed");if(oldChase<=0)oldChase=root.GetComponent<NavMeshAgent>().speed;
                Apply(root,p);PrefabUtility.SaveAsPrefabAsset(root,path);
                int shots=Mathf.CeilToInt(p.health/8f);float killTime=((shots-1)/3)*1.2f+((shots-1)%3)*.2f;
                report.AppendLine($"{p.name}: HP {oldHp} -> {p.health}; chase {oldChase} -> {p.chase}; damage={p.damage}, cooldown={p.cooldown}; level-1 shots={shots}, burst time={killTime:0.0}s");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        // Prefab instance overrides in the active map receive the same gameplay values.
        foreach(var hp in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hp.gameObject);
            var profile=Profiles.FirstOrDefault(p=>source==Prefab(p.name));if(profile!=null)Apply(hp.gameObject,profile);
        }
        var death=AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathPath);
        if(death==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/NPCs/Townsfolk/Animations/Citizen_Death.anim");
            if(source==null||!source.isHumanMotion)throw new InvalidOperationException("Humanoid death take is missing.");
            death=UnityEngine.Object.Instantiate(source);death.name="Player_Death";AnimationUtility.SetAnimationEvents(death,Array.Empty<AnimationEvent>());
            var settings=AnimationUtility.GetAnimationClipSettings(death);settings.loopTime=false;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(death,settings);
            AssetDatabase.CreateAsset(death,DeathPath);
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);var machine=controller.layers[0].stateMachine;
        var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Death")??machine.AddState("Death");
        state.motion=death;state.speed=1;state.speedParameterActive=false;state.iKOnFeet=false;
        EditorUtility.SetDirty(state);EditorUtility.SetDirty(controller);
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();var presentation=Undo.AddComponent<PlayerDeathPresentation>(player.gameObject);
        var data=new SerializedObject(presentation);data.FindProperty("deathClip").objectReferenceValue=death;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(presentation);
        report.AppendLine("Bosses retain their authored HP, skills, arena, scale and rewards; all EXP and loot tables unchanged.");
        report.AppendLine("Player death: UAL1 Death01 Humanoid, length="+death.length+" seconds; one-shot at normal speed in unscaled time; Game Over after final pose.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/EnemyBalanceAndDeathInstallation.txt",report.ToString());Debug.Log(report.ToString());Selection.activeGameObject=player.gameObject;
    }
}
#endif
