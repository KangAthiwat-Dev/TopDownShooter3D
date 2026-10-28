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

public static partial class DemonBarrierSetup
{
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);}
    private static void Array(UnityEngine.Object owner,string field,UnityEngine.Object[] values)
    {var so=new SerializedObject(owner);var p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static Transform Group(string name,Transform parent,Vector3 position)
    {var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Organize Demon area");go.transform.SetParent(parent,true);go.transform.position=position;return go.transform;}
    private static void Parent(Transform item,Transform parent)=>Undo.SetTransformParent(item,parent,"Organize Demon area");
    private static void Layer(GameObject root)
    {foreach(var t in root.GetComponentsInChildren<Transform>(true)){Undo.RecordObject(t.gameObject,"Demon barrier collision layer");t.gameObject.layer=7;}}
    private static void EnsureCollision(GameObject root)
    {
        Layer(root);
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(mesh.sharedMesh==null)continue;
            var colliders=mesh.GetComponents<Collider>();
            if(colliders.Length==0){var collider=Undo.AddComponent<BoxCollider>(mesh.gameObject);collider.center=mesh.sharedMesh.bounds.center;collider.size=mesh.sharedMesh.bounds.size;}
            foreach(var collider in mesh.GetComponents<Collider>()){Undo.RecordObject(collider,"Enable barrier collision");collider.enabled=true;collider.isTrigger=false;}
        }
    }
    private static Bounds LocalBounds(Transform frame,IEnumerable<Renderer> renderers)
    {
        bool first=true;Bounds bounds=default;
        foreach(var renderer in renderers)
        {
            var b=renderer.localBounds;var matrix=frame.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
    private static NavMeshObstacle Obstacle(Transform frame,Bounds bounds)
    {
        var obstacle=frame.GetComponent<NavMeshObstacle>();if(obstacle==null)obstacle=Undo.AddComponent<NavMeshObstacle>(frame.gameObject);
        obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=bounds.center;obstacle.size=new Vector3(Mathf.Max(.15f,bounds.size.x),Mathf.Max(2,bounds.size.y),Mathf.Max(.15f,bounds.size.z));
        obstacle.carving=true;obstacle.carveOnlyStationary=true;return obstacle;
    }
    [MenuItem("Tools/Demon Barriers/Install User Gate Sample Exit and Organized Areas")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.name!=GameMenuUI.WorldScene)throw new Exception("Open WorldPrototype first.");
        if(UnityEngine.Object.FindObjectsByType<EncounterBarrier>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)throw new Exception("User barriers are already installed; edit the named groups instead.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeDemonUserBarriers_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var zone=GameObject.Find("Zone_Demon").transform;
        var encounters=UnityEngine.Object.FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None);
        var sample=encounters.Single(e=>e.name.StartsWith("01"));var lord=encounters.Single(e=>e.name.StartsWith("02"));var king=encounters.Single(e=>e.name.StartsWith("03"));
        var gates=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("SM_VKI_Leaf_BarsGate")&&t.position.x>30&&t.position.z>5).ToArray();
        if(gates.Length!=1)throw new Exception("Expected exactly one user gate in Demon zone, found "+gates.Length);
        var door=gates[0];
        var spikes=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("SM_VKI_Prop_Stalagmites")&&Vector3.Distance(g.transform.position,sample.Arena.transform.position)<13).Select(g=>g.transform).ToArray();
        if(spikes.Length==0)throw new Exception("No sample exit stalagmites found.");
        var rootProps=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("SM_")&&g.transform.position.x>-10&&g.transform.position.x<105&&g.transform.position.z>8&&g.transform.position.z<105).Select(g=>g.transform).ToArray();

        var sampleGroup=Group("01_Sample_Area",zone,sample.Arena.transform.position);var lordGroup=Group("02_DemonLord_Area",zone,lord.Arena.transform.position);var kingGroup=Group("03_King_Area",zone,king.Arena.transform.position);
        var groups=new[]{sampleGroup,lordGroup,kingGroup};var regions=new[]{sample,lord,king};var names=new[]{"sample_zone","Lord_zone","King_zone"};
        var scenery=new Transform[3];
        for(int i=0;i<groups.Length;i++)
        {
            var geometry=Group("Geometry",groups[i],groups[i].position);scenery[i]=Group("Scenery",groups[i],groups[i].position);
            Parent(GameObject.Find(names[i]).transform,geometry);Parent(regions[i].Arena.transform,groups[i]);Parent(regions[i].transform,groups[i]);
        }
        var lordBoss=GameObject.Find("DemonLord");if(lordBoss!=null)Parent(lordBoss.transform,lordGroup);
        var kingBoss=GameObject.Find("KingOfDemon");if(kingBoss!=null)Parent(kingBoss.transform,kingGroup);
        var shared=Group("00_Shared_Lava_And_Route",zone,zone.position);
        foreach(string name in new[]{"Lava","Lava_fall","Props"}){var item=zone.Find(name);if(item!=null)Parent(item,shared);}

        var old=UnityEngine.Object.FindFirstObjectByType<DemonProgressionGate>();
        var entrance=old.transform;Undo.RecordObject(entrance.gameObject,"Name castle entrance");entrance.name="King_Entrance_Walls_And_Door";Parent(entrance,kingGroup);
        // The broad old rock-wall box crossed the entire platform. Individual user
        // wall colliders now own the boundary; only the real door can open.
        foreach(var collider in entrance.GetComponents<Collider>())Undo.DestroyObjectImmediate(collider);
        foreach(var obstacle in entrance.GetComponents<NavMeshObstacle>())Undo.DestroyObjectImmediate(obstacle);
        Undo.DestroyObjectImmediate(old);
        var staticWalls=Group("Permanent_Walls",entrance,entrance.position);
        var oldVisuals=entrance.Find("Rock_Visuals");if(oldVisuals!=null)
        {foreach(var child in oldVisuals.Cast<Transform>().ToArray())Parent(child,staticWalls);if(oldVisuals.childCount==0)Undo.DestroyObjectImmediate(oldVisuals.gameObject);}
        foreach(var prop in rootProps)
        {
            if(prop==door||spikes.Contains(prop))continue;
            bool boundary=prop.name.StartsWith("SM_VKI_Wall")||prop.name.StartsWith("SM_VKI_Post");
            if(boundary&&Vector3.Distance(prop.position,door.position)<25)Parent(prop,staticWalls);
            else
            {
                int nearest=Enumerable.Range(0,3).OrderBy(i=>Vector3.ProjectOnPlane(prop.position-regions[i].Arena.transform.position,Vector3.up).sqrMagnitude).First();
                Parent(prop,scenery[nearest]);
            }
        }
        foreach(var child in staticWalls.Cast<Transform>().ToArray())
        {
            EnsureCollision(child.gameObject);
            foreach(var collider in child.GetComponentsInChildren<BoxCollider>(true))Obstacle(collider.transform,new Bounds(collider.center,collider.size));
        }
        var doorRoot=Group("Door_Hinge_MOVE_THIS",entrance,door.position);doorRoot.rotation=door.rotation;Parent(door,doorRoot);EnsureCollision(door.gameObject);
        foreach(var t in door.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
        var closedBounds=LocalBounds(doorRoot,door.GetComponentsInChildren<Renderer>(true));var closedBlocker=Group("Closed_Door_Navigation",entrance,doorRoot.position);closedBlocker.rotation=doorRoot.rotation;
        var doorObstacle=Obstacle(closedBlocker,closedBounds);var doorBody=Undo.AddComponent<Rigidbody>(doorRoot.gameObject);doorBody.isKinematic=true;doorBody.useGravity=false;
        var doorBarrier=Undo.AddComponent<EncounterBarrier>(entrance.gameObject);Ref(doorBarrier,"prerequisite",lord);Ref(doorBarrier,"movingPart",doorRoot);
        Array(doorBarrier,"blockingColliders",door.GetComponentsInChildren<Collider>(true));Array(doorBarrier,"navigationObstacles",new UnityEngine.Object[]{doorObstacle});
        var doorSo=new SerializedObject(doorBarrier);doorSo.FindProperty("motion").enumValueIndex=(int)EncounterBarrierMotion.Swing;doorSo.FindProperty("swingEuler").vector3Value=new Vector3(0,105,0);doorSo.FindProperty("hideWhenOpen").boolValue=false;doorSo.ApplyModifiedPropertiesWithoutUndo();

        Vector3 center=Vector3.zero;foreach(var spike in spikes)center+=spike.position;center/=spikes.Length;
        var sampleExit=Group("Sample_Exit_Stalagmites",sampleGroup,center);var visuals=Group("Sinking_Stalagmites_MOVE_THESE",sampleExit,center);
        foreach(var spike in spikes){Parent(spike,visuals);EnsureCollision(spike.gameObject);}
        var spikeBounds=LocalBounds(sampleExit,visuals.GetComponentsInChildren<Renderer>(true));
        var sampleObstacle=Obstacle(sampleExit,spikeBounds);var body=Undo.AddComponent<Rigidbody>(visuals.gameObject);body.isKinematic=true;body.useGravity=false;
        var sampleBarrier=Undo.AddComponent<EncounterBarrier>(sampleExit.gameObject);Ref(sampleBarrier,"prerequisite",sample);Ref(sampleBarrier,"movingPart",visuals);
        Array(sampleBarrier,"blockingColliders",visuals.GetComponentsInChildren<Collider>(true));Array(sampleBarrier,"navigationObstacles",new UnityEngine.Object[]{sampleObstacle});
        var spikeSo=new SerializedObject(sampleBarrier);spikeSo.FindProperty("slideOffset").vector3Value=new Vector3(0,-Mathf.Max(7,spikeBounds.size.y+1),0);spikeSo.FindProperty("hideWhenOpen").boolValue=true;spikeSo.ApplyModifiedPropertiesWithoutUndo();

        var layout=zone.Find("Demon_Encounter_Layout");if(layout!=null&&layout.childCount==0)Undo.DestroyObjectImmediate(layout.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Inspect();
        File.WriteAllText("Logs/DemonBarrierInstallation.txt","User door opens 105 degrees only after DemonLord and every guardian die. Permanent walls keep colliders and carved navigation. Sample exit: "+spikes.Length+" stalagmite groups sink after the entire sample encounter. Broad legacy invisible wall removed. World poses and user prefab references preserved.\n");
        Debug.Log("User Demon gate, sinking sample exit and organized area groups installed.");
    }
}
#endif
