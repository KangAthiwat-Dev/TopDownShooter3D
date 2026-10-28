#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static partial class DemonZoneSetup
{
    private const string EnemyFolder="Assets/_Project/Prefabs/Enemies/";
    private const string ZoneFolder=EnemyFolder+"DemonZone/";
    private static void Set(UnityEngine.Object target,string name,object value)
    {
        var so=new SerializedObject(target);var p=so.FindProperty(name);
        if(p==null)throw new Exception(target.name+" has no property "+name);
        if(value is bool b)p.boolValue=b;else if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;
        else if(value is Vector3 v)p.vector3Value=v;else p.objectReferenceValue=value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    [MenuItem("Tools/Demon Zone/Install Staged Encounters Lava and Health Bars")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var king=UnityEngine.Object.FindFirstObjectByType<KingOfDemonAI>();
        var lord=UnityEngine.Object.FindObjectsByType<EncounterCombatant>(FindObjectsSortMode.None).First(a=>a.name=="DemonLord");
        if(GameObject.Find("Demon_Encounter_Layout")!=null)throw new Exception("Encounter layout already exists; edit its anchors instead of reinstalling.");
        Directory.CreateDirectory(ZoneFolder);
        var goblin=Variant("Enemy_GoblinDemon",280,24,4.1f,180);
        var skeleton=Variant("Enemy_Skeleton",220,20,3.8f,140);
        var chief=Variant("Enemy_GoblinChief",420,32,4.3f,250);
        var dragon=Variant("DragonNightmare_Guardian",1200,0,5.2f,850);
        var dr=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(dragon));
        try
        {
            var combat=dr.GetComponent<EncounterCombatant>();Set(combat,"showBossHud",false);Set(combat,"chaseSpeed",5.2f);
            ScaleSkills(combat,2.3f);PrefabUtility.SaveAsPrefabAsset(dr,AssetDatabase.GetAssetPath(dragon));
        }
        finally{PrefabUtility.UnloadPrefabContents(dr);}
        var layout=new GameObject("Demon_Encounter_Layout");Undo.RegisterCreatedObjectUndo(layout,"Install Demon encounters");
        var map=GameObject.Find("Zone_Demon").transform;layout.transform.SetParent(map,true);
        Set(lord.GetComponent<EnemyHealth>(),"maxHealth",3200f);Set(lord.GetComponent<EnemyHealth>(),"experienceReward",2600);
        Set(lord,"showBossHud",true);Set(lord,"chaseSpeed",4.7f);ScaleSkills(lord,2.7f);
        var lordFx=lord.GetComponent<DemonLordVFX>();lordFx.boltDamage=54;lordFx.novaDamage=67.5f;lordFx.curseDamage=94.5f;lordFx.slashDamage=32.4f;
        Set(king.GetComponent<EnemyHealth>(),"maxHealth",6000f);Set(king.GetComponent<EnemyHealth>(),"experienceReward",6000);
        Set(king,"meleeDamage",65f);Set(king,"chaseSpeed",5f);Set(king,"teleportMelee",true);
        var kingFx=king.GetComponent<BossVFXController>();kingFx.fireballDamage=60;kingFx.shockwaveDamage=70;
        // Keep the original boss models, scale and HUD references intact.
        var lordArena=lord.Arena;var kingArena=king.Arena;
        lordArena.transform.SetParent(layout.transform,true);kingArena.transform.SetParent(layout.transform,true);
        Set(kingArena.GetComponent<BoxCollider>(),"m_Size",new Vector3(27,8,37));
        lord.transform.SetParent(layout.transform,true);king.transform.SetParent(layout.transform,true);
        var sample=GameObject.Find("sample_zone").transform;
        var sampleGo=new GameObject("Sample_Arena_Monsters_Only");sampleGo.transform.SetParent(layout.transform,false);
        sampleGo.transform.position=sample.position+Vector3.up*3;
        sampleGo.AddComponent<BoxCollider>().size=new Vector3(18,8,18);
        sampleGo.GetComponent<BoxCollider>().center=new Vector3(0,3,0);
        var sampleArena=sampleGo.AddComponent<BossArenaZone>();
        var sampleEncounter=CreateEncounter(layout.transform,"01_Sample_Encounter",sampleArena,null,-1);
        var lordEncounter=CreateEncounter(layout.transform,"02_DemonLord_Encounter",lordArena,lord.gameObject,0);
        var kingEncounter=CreateEncounter(layout.transform,"03_King_Encounter",kingArena,king.gameObject,2);
        Set(kingEncounter,"prerequisite",lordEncounter);
        AddWaves(sampleEncounter,sampleArena,new[]{"ลานหน้า: กำจัดมอนสเตอร์"},new[]{new[]{goblin,skeleton,goblin,skeleton,chief}});
        AddWaves(lordEncounter,lordArena,new[]{"รองบอส: Demon Lord และมังกรพิทักษ์"},new[]{new[]{dragon,dragon,goblin,skeleton,goblin,skeleton}});
        AddWaves(kingEncounter,kingArena,new[]{"รอบ 1: กองทัพจอมมาร","รอบ 2: มังกรคู่","รอบสุดท้าย: Malachar"},new[]{
            new[]{goblin,skeleton,goblin,skeleton,chief,goblin,skeleton,goblin},
            new[]{dragon,dragon,chief,skeleton,goblin,skeleton},Array.Empty<GameObject>()});
        BuildGate(layout.transform,kingArena,lordEncounter);
        foreach(var actor in UnityEngine.Object.FindObjectsByType<EncounterCombatant>(FindObjectsSortMode.None).Where(a=>a!=lord).ToArray())
            Undo.DestroyObjectImmediate(actor.gameObject);
        var oldHud=GameObject.Find("Nightmare Dragon HUD");if(oldHud!=null)Undo.DestroyObjectImmediate(oldHud);
        foreach(var mesh in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(m=>m.name=="Lava"||m.name=="Lava_fall"))
            if(mesh.GetComponent<LavaDeathSurface>()==null)Undo.AddComponent<LavaDeathSurface>(mesh.gameObject);
        var bars=new GameObject("Monster Overhead Health Bars",typeof(RectTransform));Undo.RegisterCreatedObjectUndo(bars,"Monster health bars");bars.AddComponent<MonsterOverheadHealthUI>();
        var status=new GameObject("Demon Encounter Status",typeof(RectTransform));status.AddComponent<DemonEncounterStatusUI>();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(king.gameObject.scene);EditorSceneManager.SaveScene(king.gameObject.scene);
        Inspect();Debug.Log("Installed Demon staged encounters, editable rock gate, lethal liquid surfaces and monster health bars.");
    }
    private static void ScaleSkills(EncounterCombatant combat,float scale)
    {
        var so=new SerializedObject(combat);var array=so.FindProperty("skills");
        for(int i=0;i<array.arraySize;i++)array.GetArrayElementAtIndex(i).FindPropertyRelative("damage").floatValue*=scale;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Variant(string source,float hp,int damage,float speed,int exp)
    {
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder+source+".prefab");
        if(original==null)throw new Exception("Missing enemy prefab: "+source);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(original);
        try
        {
            Set(instance.GetComponent<EnemyHealth>(),"maxHealth",hp);Set(instance.GetComponent<EnemyHealth>(),"experienceReward",exp);
            var attack=instance.GetComponent<EnemyAttack>();if(attack!=null)Set(attack,"damage",damage);
            var ai=instance.GetComponent<EnemyAI>();if(ai!=null){Set(ai,"chaseSpeed",speed);Set(ai,"patrolRadius",3f);Set(ai,"huntCivilians",false);}
            return PrefabUtility.SaveAsPrefabAsset(instance,ZoneFolder+source+"_Elite.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(instance);}
    }
    private static DemonArenaEncounter CreateEncounter(Transform parent,string name,BossArenaZone arena,GameObject boss,int bossWave)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(arena.transform.position,arena.transform.rotation);
        var encounter=go.AddComponent<DemonArenaEncounter>();Set(encounter,"arena",arena);Set(encounter,"boss",boss);Set(encounter,"bossWave",bossWave);return encounter;
    }
    private static void AddWaves(DemonArenaEncounter encounter,BossArenaZone area,string[] titles,GameObject[][] prefabs)
    {
        var so=new SerializedObject(encounter);var waves=so.FindProperty("waves");waves.arraySize=prefabs.Length;
        for(int w=0;w<prefabs.Length;w++)
        {
            var wave=waves.GetArrayElementAtIndex(w);wave.FindPropertyRelative("title").stringValue=titles[w];
            var members=wave.FindPropertyRelative("members");members.arraySize=prefabs[w].Length;
            var group=new GameObject("Wave_"+(w+1)+"_Spawn_Anchors");group.transform.SetParent(encounter.transform,false);
            for(int i=0;i<prefabs[w].Length;i++)
            {
                float angle=(i+.5f)*Mathf.PI*2/Mathf.Max(prefabs[w].Length,1);
                float radius=area.name.StartsWith("Sample")?5.5f:8;
                var candidate=area.transform.position+area.transform.rotation*new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(!NavMesh.SamplePosition(candidate,out var hit,1.5f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-candidate.y)>1.5f)throw new Exception("No walkable spawn at "+candidate);
                var anchor=new GameObject((prefabs[w][i].name.Contains("Dragon")?"Dragon_":"Monster_")+(i+1));anchor.transform.SetParent(group.transform,true);
                anchor.transform.position=hit.position;anchor.transform.LookAt(new Vector3(area.transform.position.x,hit.position.y,area.transform.position.z));
                var element=members.GetArrayElementAtIndex(i);element.FindPropertyRelative("prefab").objectReferenceValue=prefabs[w][i];element.FindPropertyRelative("anchor").objectReferenceValue=anchor.transform;
                var health=prefabs[w][i].GetComponent<EnemyHealth>();element.FindPropertyRelative("health").floatValue=health.MaxHealth; // Serialized MaxHealth is valid outside Play mode.
                element.FindPropertyRelative("experience").intValue=health.ExperienceReward;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void BuildGate(Transform parent,BossArenaZone area,DemonArenaEncounter prerequisite)
    {
        var go=new GameObject("King_Access_Rock_Gate_MOVE_THIS");go.transform.SetParent(parent,true);
        go.transform.SetPositionAndRotation(area.transform.position+area.transform.rotation*new Vector3(-14.5f,0,0),area.transform.rotation);
        var box=go.AddComponent<BoxCollider>();box.center=new Vector3(0,4,0);box.size=new Vector3(3,8,44);
        var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=box.center;obstacle.size=box.size;obstacle.carving=true;
        var rocks=new GameObject("Rock_Visuals");rocks.transform.SetParent(go.transform,false);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Inferno_World_Free/Prefabs/Rocks/RockBig_001.prefab");
        for(int i=0;i<12;i++)
        {
            var rock=(GameObject)PrefabUtility.InstantiatePrefab(prefab);rock.name="Barrier_Rock_"+(i+1);rock.transform.SetParent(rocks.transform,false);
            foreach(var c in rock.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            rock.transform.localRotation=Quaternion.Euler(0,i*73%360,0);rock.transform.localScale=Vector3.one;
            var bounds=rock.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            rock.transform.localScale*=7/Mathf.Max(bounds.size.y,.1f);
            bounds=rock.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            rock.transform.localPosition=new Vector3((i%2==0?-.3f:.3f),-(bounds.min.y-rock.transform.position.y),-21+i*3.8f);
        }
        var gate=go.AddComponent<DemonProgressionGate>();Set(gate,"prerequisite",prerequisite);Set(gate,"rocks",rocks.transform);
    }
    [MenuItem("Tools/Demon Zone/Finalize Movable Layout and Validate Scene")]
    public static void FinalizeAndValidate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var layout=GameObject.Find("Demon_Encounter_Layout");
        Undo.SetTransformParent(layout.transform,GameObject.Find("Zone_Demon").transform,"Group Demon encounter layout");
        var report=new System.Text.StringBuilder();int failures=0;
        foreach(var e in UnityEngine.Object.FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None))
        {
            foreach(var wave in e.Waves)foreach(var member in wave.members)
            {
                bool valid=member.prefab!=null&&member.anchor!=null&&member.health>0&&
                    NavMesh.SamplePosition(member.anchor.position,out var hit,1.5f,NavMesh.AllAreas)&&e.Arena.Contains(hit.position);
                report.AppendLine((valid?"PASS ":"FAIL ")+e.name+" / "+member.anchor?.name);if(!valid)failures++;
            }
        }
        foreach(var root in layout.scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            int missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if(missing>0){report.AppendLine("FAIL missing scripts "+t.name+" count="+missing);failures+=missing;}
        }
        report.AppendLine("Layout parent="+layout.transform.parent.name+"; failures="+failures);
        File.WriteAllText("Logs/DemonZoneSceneValidation.txt",report.ToString());
        if(failures>0)throw new Exception("Demon scene validation failed; see Logs/DemonZoneSceneValidation.txt");
        EditorSceneManager.MarkSceneDirty(layout.scene);EditorSceneManager.SaveScene(layout.scene);
        Debug.Log("Demon encounter scene validated and grouped under Zone_Demon.");
    }
}
#endif
