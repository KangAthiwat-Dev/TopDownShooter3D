#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class WorldMonsterSetup
{
    public const string PopulationRoot = "World_Monster_Population";
    private const string CampFolder = Folder + "/Camps";

    private static Bounds LocalMeshBounds(GameObject root)
    {
        bool first=true; Bounds bounds=default;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.sharedMesh==null) continue;
            Bounds b=filter.sharedMesh.bounds;
            for(int corner=0;corner<8;corner++)
            {
                var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(p));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        return bounds;
    }

    private static WorldMonsterCamp BuildCamp(int variant)
    {
        string path=CampFolder+"/Goblin_Camp_"+(variant+1)+".prefab";
        var existing=AssetDatabase.LoadAssetAtPath<WorldMonsterCamp>(path);if(existing!=null)return existing;
        var root=new GameObject("Goblin_Camp_"+(variant+1));
        try
        {
            var camp=root.AddComponent<WorldMonsterCamp>();camp.radius=10;
            var props=new List<Transform>();
            void Prop(string prop,float x,float z,float yaw,float scale)
            {
                string source=MedievalModelOnlySetup.Package+"/Generated/Prefabs/VKI/Prop/SM_VKI_Prop_"+prop+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if(prefab==null)throw new InvalidOperationException("Missing camp prop "+source);
                var go=UnityEngine.Object.Instantiate(prefab,root.transform);go.name=prop;
                if(PrefabUtility.IsPartOfPrefabInstance(go))PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in go.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(c);
                var bounds=LocalMeshBounds(go);
                go.transform.localScale=Vector3.one*scale;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
                go.transform.localPosition=new Vector3(x,-bounds.min.y*scale,z);
                // Small model-only prefabs use simple collision and stationary carving.
                var box=go.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
                var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
                obstacle.center=bounds.center;obstacle.size=bounds.size;obstacle.carving=true;obstacle.carveOnlyStationary=true;
                obstacle.carvingTimeToStationary=.1f;
                foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=7;
                props.Add(go.transform);
            }
            float flip=variant==1?-1:1;
            Prop("Goblin_ChiefTent",0,5.7f,180,.68f);
            Prop("Goblin_Tent",-5.2f,1.8f,95+variant*8,.76f);
            Prop("Goblin_Tent",5.2f,2.2f,-95-variant*8,.72f);
            Prop("Goblin_Bonfire",0,-.3f,variant*40,.56f);
            Prop("Goblin_Watchtower",6*flip,-5,variant==1?20:160,.66f);
            Prop("Totem_Goblin",-5.7f*flip,-4.6f,180,.85f);
            Prop("Cage",-6.8f*flip,-1.9f,20,.85f);
            if(variant==2)Prop("Totem_Goblin",-3.5f,-6.7f,145,.7f);
            var glow=new GameObject("Bonfire Warm Glow");glow.transform.SetParent(props[3],false);glow.transform.localPosition=Vector3.up*1.4f;
            var light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.43f,.12f);light.intensity=1.3f;light.range=5;light.shadows=LightShadows.None;
            AddCampFire(props[3]);
            camp.groundedProps=props.ToArray();
            Vector3[] spots={new Vector3(0,0,2.6f),new Vector3(-2.5f,0,-3.1f),new Vector3(2.5f,0,-3.1f),new Vector3(-2.5f,0,1.7f),new Vector3(2.5f,0,1.7f)};
            camp.memberPoints=spots.Select((p,i)=>{var t=new GameObject(i==0?"Chief Spawn":"Guard Spawn "+i).transform;t.SetParent(root.transform,false);t.localPosition=p;return t;}).ToArray();
            return PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<WorldMonsterCamp>();
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    private static void AddCampFire(Transform bonfire)
    {
        var existing=bonfire.Find("Campfire Flame");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Flipbook VFX Bundle Lite URP/Flipbook VFX URP/_ Lite/Prefabs/Lite_VFX_Fire_Loop_01.prefab");
        var fire=existing!=null?existing.gameObject:UnityEngine.Object.Instantiate(source,bonfire);fire.name="Campfire Flame";
        if(PrefabUtility.IsPartOfPrefabInstance(fire))PrefabUtility.UnpackPrefabInstance(fire,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        fire.transform.localPosition=new Vector3(-.37f,1.8f,-.01f);fire.transform.localScale=Vector3.one*.55f;
        foreach(var ps in fire.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.prewarm=true;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.maxParticles=24;
        }
        foreach(var audio in fire.GetComponentsInChildren<AudioSource>())
        {audio.spatialBlend=1;audio.volume=.16f;audio.minDistance=1.5f;audio.maxDistance=14;audio.dopplerLevel=0;audio.loop=true;}
        foreach(var t in fire.GetComponentsInChildren<Transform>(true))t.gameObject.layer=7;
    }

    [MenuItem("Tools/Enemies/World Population/Finish Campfires and Validate Assets")]
    public static void FinishCamps()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var report=new StringBuilder("World population asset validation "+DateTime.Now.ToString("s")+"\n");
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{CampFolder}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var camp=root.GetComponent<WorldMonsterCamp>();AddCampFire(camp.groundedProps.Single(p=>p.name=="Goblin_Bonfire"));
                if(camp.memberPoints.Length!=5)throw new InvalidOperationException("Expected chief and four guards.");
                foreach(var p in camp.groundedProps)
                {
                    if(p.GetComponent<NavMeshObstacle>()==null||p.GetComponent<BoxCollider>()==null)throw new InvalidOperationException("Missing prop navigation.");
                    foreach(var r in p.GetComponentsInChildren<Renderer>())foreach(var mat in r.sharedMaterials)
                        if(mat==null||!mat.shader.isSupported)throw new InvalidOperationException("Unsupported camp material: "+p.name);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);report.AppendLine("PASS "+path+" members=5; collision, navigation and supported materials.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/WorldMonsterValidation.txt",report.ToString());Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Enemies/World Population/Render Camp Preview")]
    public static void PreviewCamp()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var director=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>();
        var point=director.campSites.OrderBy(s=>s.position.x).Skip(3).First().position;
        var camp=UnityEngine.Object.Instantiate(director.campTemplates[0],point,Quaternion.identity);
        var members=new List<GameObject>();bool fog=RenderSettings.fog;RenderSettings.fog=false;
        try
        {
            camp.GroundProps(director.terrain);
            for(int i=0;i<camp.memberPoints.Length;i++)
            {
                Vector3 pos=camp.memberPoints[i].position;pos.y=director.terrain.SampleHeight(pos)+director.terrain.transform.position.y;
                var go=UnityEngine.Object.Instantiate(i==0?director.chiefPrefab:director.goblinPrefab,pos,Quaternion.Euler(0,160-i*50,0));members.Add(go);
                var animator=go.GetComponentInChildren<Animator>();animator.Rebind();animator.Play("Locomotion",0,.3f);animator.Update(0);
            }
            foreach(var ps in camp.GetComponentsInChildren<ParticleSystem>())ps.Simulate(.65f,true,true,true);
            Render("WorldMonster_Camp_Preview",point+Vector3.up*1.3f,new Vector3(14,18,-22),11.5f);
        }
        finally{foreach(var go in members)UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(camp.gameObject);RenderSettings.fog=fog;}
        Debug.Log("Camp preview saved in Logs/WorldMonster_Camp_Preview.png");
    }

    private static WorldMonsterSafeZone SafeZone(Transform parent,string name,Bounds bounds)
    {
        var previous=parent.Find(name);if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var go=new GameObject(name);go.transform.position=bounds.center;go.transform.SetParent(parent,true);
        // An identity world rotation makes bounds predictable; subsequent parent movement still follows.
        go.transform.rotation=Quaternion.identity;
        var area=go.AddComponent<WorldMonsterSafeZone>();area.size=new Vector2(bounds.size.x,bounds.size.z);return area;
    }

    [MenuItem("Tools/Enemies/World Population/Install Map Population and Goblin Tribes")]
    public static void InstallPopulation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(GameObject.Find(PopulationRoot)!=null)throw new InvalidOperationException("World population is already installed; edit its settings in the Inspector.");
        BuildModels();EnsureFolder(CampFolder);
        var templates=Enumerable.Range(0,3).Select(BuildCamp).ToArray();
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");
        EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeWorldMonsterPopulation_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        var root=new GameObject(PopulationRoot);root.transform.SetParent(GameObject.Find("World/Map").transform,false);
        var director=root.AddComponent<WorldMonsterDirector>();director.terrain=map.terrain;
        var spawn=UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>();director.player=spawn.Player;
        director.campTemplates=templates;
        GameObject Enemy(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_"+name+".prefab");
        director.goblinPrefab=Enemy("GoblinDemon");director.chiefPrefab=Enemy("GoblinChief");
        director.species=new[]{
            new WorldMonsterSpecies{id="spider",prefab=Enemy("Spider"),population=5,habitat=WorldMonsterHabitat.Forest},
            new WorldMonsterSpecies{id="bear",prefab=Enemy("Bear"),population=3,habitat=WorldMonsterHabitat.Forest},
            new WorldMonsterSpecies{id="imp_green",prefab=Enemy("Imp_Green"),population=3},
            new WorldMonsterSpecies{id="imp_blue",prefab=Enemy("Imp_Blue"),population=3},
            new WorldMonsterSpecies{id="imp_red",prefab=Enemy("Imp_Red"),population=2,habitat=WorldMonsterHabitat.Ruins},
            new WorldMonsterSpecies{id="puglin",prefab=Enemy("Puglin"),population=2},
            new WorldMonsterSpecies{id="skeleton",prefab=Enemy("Skeleton"),population=4,habitat=WorldMonsterHabitat.Ruins}
        };
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
        var safe=new List<WorldMonsterSafeZone>();
        foreach(string name in new[]{"Zone_City","Zone_Villager"})
        {
            var zone=all.Single(t=>t.name==name);
            var meshes=zone.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            Bounds b=meshes[0].bounds;foreach(var r in meshes)b.Encapsulate(r.bounds);b.Expand(new Vector3(6,0,6));
            safe.Add(SafeZone(zone,"Monster Spawn Exclusion",b));
        }
        safe.Add(SafeZone(spawn.transform,"Sanctuary Spawn Exclusion",new Bounds(spawn.Position,new Vector3(44,4,44))));
        foreach(var arena in UnityEngine.Object.FindObjectsByType<BossArenaZone>(FindObjectsSortMode.None))
        {
            Bounds b=arena.GetComponent<BoxCollider>().bounds;b.Expand(new Vector3(8,0,8));
            safe.Add(SafeZone(arena.transform,"Encounter Spawn Exclusion",b));
        }
        director.safeZones=safe.ToArray();Physics.SyncTransforms();
        var roamParent=new GameObject("Roaming Sites").transform;roamParent.SetParent(root.transform,false);
        var campParent=new GameObject("Camp Clearing Sites").transform;campParent.SetParent(root.transform,false);
        var roam=new List<WorldMonsterSite>();var campSites=new List<Transform>();var path=new NavMeshPath();
        NavMesh.SamplePosition(spawn.Position,out var origin,8,NavMesh.AllAreas);
        bool Connected(Vector3 point)=>NavMesh.CalculatePath(origin.position,point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
        for(int z=-88;z<=88;z+=8)for(int x=-88;x<=88;x+=8)
        {
            if(!director.TryGround(new Vector3(x,0,z),1,out var point)||!Connected(point))continue;
            var t=new GameObject("Roam "+x+" "+z).transform;t.position=point;t.SetParent(roamParent,true);
            roam.Add(new WorldMonsterSite{point=t,habitat=z>=16&&x>=-28?WorldMonsterHabitat.Ruins:WorldMonsterHabitat.Forest});
        }
        for(int z=-82;z<=82;z+=10)for(int x=-82;x<=82;x+=10)
        {
            if(!director.TryGround(new Vector3(x,0,z),10,out var point)||!Connected(point))continue;
            bool valid=true;
            for(int ring=1;ring<=2&&valid;ring++)for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8;var p=point+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*5*ring;
                if(!director.TryGround(p,0,out var grounded)||Mathf.Abs(grounded.y-point.y)>.5f){valid=false;break;}
            }
            if(!valid)continue;
            var t=new GameObject("Clearing "+x+" "+z).transform;t.position=point;t.SetParent(campParent,true);campSites.Add(t);
        }
        if(roam.Count<40||campSites.Count<4)throw new InvalidOperationException("Too few safe population sites: roam="+roam.Count+" camps="+campSites.Count);
        director.roamingSites=roam.ToArray();director.campSites=campSites.ToArray();
        foreach(var waves in UnityEngine.Object.FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None))
        {Undo.RecordObject(waves,"Use persistent world population");waves.enabled=false;EditorUtility.SetDirty(waves);}
        EditorUtility.SetDirty(director);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Selection.activeGameObject=root;
        var report=new StringBuilder("World population installed "+DateTime.Now.ToString("s")+"\n");
        report.AppendLine("Roaming sites="+roam.Count+" (forest="+roam.Count(s=>s.habitat==WorldMonsterHabitat.Forest)+", ruins="+roam.Count(s=>s.habitat==WorldMonsterHabitat.Ruins)+") camps="+campSites.Count);
        report.AppendLine("22 roaming + 3 complete camps of 5 = 37 ordinary monsters; existing bosses retained.");
        foreach(var s in director.species)report.AppendLine(s.id+" x"+s.population+" EXP="+s.prefab.GetComponent<EnemyHealth>().ExperienceReward);
        foreach(var s in safe)report.AppendLine("SAFE "+PathOf(s.transform)+" center="+s.transform.position+" size="+s.size);
        File.WriteAllText("Logs/WorldMonsterInstallation.txt",report.ToString());Debug.Log(report.ToString());
    }
}
#endif
