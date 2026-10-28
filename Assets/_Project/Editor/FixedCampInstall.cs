#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class FixedCampSetup
{
    [MenuItem("Tools/Fixed Camps/Place Camps on Four Painted Clearings")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.name!=GameMenuUI.WorldScene)throw new Exception("Open WorldPrototype first.");
        var director=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>();
        if(director.useFixedCamps||GameObject.Find("Fixed_Bandit_Camps")!=null)throw new Exception("Fixed camps already installed; edit their scene roots instead of placing duplicates.");
        var patches=Patches(director.terrain).Where(p=>p.layer==4&&p.center.y>3.9f&&p.center.y<4.2f&&p.center.z<20&&p.size.x<14&&p.size.y<14&&p.size.x>6&&p.size.y>6).OrderBy(p=>p.center.z).ToArray();
        if(patches.Length!=4)throw new Exception("Expected four flat, painted rock clearings; found "+patches.Length+". Inspect before placing camps.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeFixedCamps_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var root=new GameObject("Fixed_Bandit_Camps");Undo.RegisterCreatedObjectUndo(root,"Place permanent monster camps");var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();root.transform.SetParent(map.transform,true);
        var camps=new WorldMonsterCamp[4];var report=new StringBuilder();
        for(int i=0;i<camps.Length;i++)
        {
            var point=patches[i].center;
            if(director.IsProtected(point))throw new Exception("Painted clearing lies in a protected zone: "+point);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(director.campTemplates[i%director.campTemplates.Length].gameObject,root.transform);
            camps[i]=instance.GetComponent<WorldMonsterCamp>();instance.name="Camp_0"+(i+1)+"_"+(point.x<0?"West":"East");instance.transform.position=point;
            // Keep the actor parent at unit scale. Shrink only authored props and
            // spawn offsets, so Goblin/Chief size remains the same as in the world.
            const float footprint=.7f;instance.transform.localScale=Vector3.one;
            Vector3 road=NearestRoutePoint(map,point);var entrance=Vector3.ProjectOnPlane(road-point,Vector3.up);
            if(entrance.sqrMagnitude>.1f)instance.transform.rotation=Quaternion.LookRotation(-entrance.normalized);
            foreach(var prop in camps[i].groundedProps)
            {
                Undo.RecordObject(prop,"Fit camp to painted clearing");prop.localPosition*=footprint;prop.localScale*=footprint;
                PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
            }
            foreach(var anchor in camps[i].memberPoints)
            {
                Undo.RecordObject(anchor,"Fit monster spawn to clearing");anchor.localPosition*=footprint;
                var outward=Vector3.ProjectOnPlane(anchor.localPosition,Vector3.up);if(outward.sqrMagnitude>.1f)anchor.localRotation=Quaternion.LookRotation(outward.normalized);
                PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            }
            camps[i].radius=7;camps[i].GroundProps(director.terrain);
            foreach(var prop in camps[i].groundedProps)PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camps[i]);PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            report.AppendLine(instance.name+" center="+point+" rotation="+instance.transform.eulerAngles.y+" props="+camps[i].groundedProps.Length+" members="+camps[i].memberPoints.Length);
            foreach(var anchor in camps[i].memberPoints)
            {
                bool valid=NavMesh.SamplePosition(anchor.position,out var hit,.9f,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-anchor.position.y)<.6f;
                report.AppendLine((valid?"PASS ":"FAIL ")+anchor.name+" at="+anchor.position+" nav="+hit.position);
                if(!valid)throw new Exception("Fixed camp spawn is not on navigation: "+instance.name+" / "+anchor.name);
            }
        }
        Undo.RecordObject(director,"Use four permanent monster camps");director.fixedCamps=camps;director.useFixedCamps=true;director.campPopulation=4;
        // Preserve the existing 150-second respawn tuning and all roaming species.
        var oldSites=director.transform.Find("Camp Clearing Sites");if(oldSites!=null){Undo.RecordObject(oldSites.gameObject,"Disable unused random camp candidates");oldSites.gameObject.SetActive(false);}
        EditorUtility.SetDirty(director);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/FixedCampInstallation.txt",report+"Respawn delay="+director.campRespawnDelay+"s after the last real death. Layout persists; only monsters respawn after player leaves view and proximity.\n");
        Debug.Log("Placed four permanent camps on painted clearings; monster-only respawn enabled.");
    }
    private static Vector3 NearestRoutePoint(ForestMapLayout map,Vector3 point)
    {
        Vector3 best=point+Vector3.back*10;float nearest=float.MaxValue;
        foreach(var route in map.routes)for(int i=1;i<route.points.Length;i++)
        {
            if(route.points[i-1]==null||route.points[i]==null)continue;
            Vector3 a=route.points[i-1].position,b=route.points[i].position,d=Vector3.ProjectOnPlane(b-a,Vector3.up);
            float t=Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(point-a,Vector3.up),d)/Mathf.Max(.001f,d.sqrMagnitude));
            var candidate=a+(b-a)*t;float distance=Vector3.ProjectOnPlane(candidate-point,Vector3.up).sqrMagnitude;
            if(distance<nearest){nearest=distance;best=candidate;}
        }
        return best;
    }
}
#endif
