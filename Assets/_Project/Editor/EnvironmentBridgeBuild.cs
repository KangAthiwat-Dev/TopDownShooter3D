#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static partial class EnvironmentBridgeSetup
{
    private static Transform Group(string name,Transform parent)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=7;return go.transform;}
    private static void FolderReady()
    {if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Project/World","EnvironmentBridges");}
    [MenuItem("Tools/World/Environment Bridges/Align Bridges Add Collision and Gentle Water")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var environment=GameObject.Find("World/Environment").transform;
        if(environment.Find("Aligned_Bridges")!=null)throw new InvalidOperationException("Bridges already aligned; preserve manual edits.");
        Directory.CreateDirectory("Logs/SceneBackups");string backup="Logs/SceneBackups/WorldPrototype_BeforeEnvironmentBridges_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),backup,true);FolderReady();
        var roots=new[]{environment.Find("brige"),environment.Find("brige_wood")};
        var group=Group("Aligned_Bridges",environment);group.localScale=new Vector3(1/environment.lossyScale.x,1/environment.lossyScale.y,1/environment.lossyScale.z);
        var report=new StringBuilder("Aligned Environment bridges\nBackup="+backup+"\n");
        foreach(var bridge in roots)
        {
            bool stone=bridge.name=="brige";
            var pieces=bridge.Cast<Transform>().Where(t=>t.name.StartsWith("SM_VK_Bridge_")).ToArray();
            var centre=pieces.Single(t=>stone?t.name=="SM_VK_Bridge_Stone_Span":t.name=="SM_VK_Bridge_Wood_Mid (1)");
            var position=centre.position;float yaw=stone?45:315.3f;
            Undo.SetTransformParent(bridge,group,"Align bridge without inherited stretch");Undo.RecordObject(bridge,"Centre bridge assembly");
            bridge.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));bridge.localScale=Vector3.one;
            var mids=pieces.Where(t=>t.name.Contains("Wood_Mid")).OrderBy(t=>t.position.x).ToArray();
            var ends=pieces.Where(t=>t.name.Contains(stone?"Stone_Ramp":"Wood_End")).OrderBy(t=>t.position.x).ToArray();
            foreach(var piece in pieces)
            {
                Undo.RecordObject(piece,"Align bridge piece");piece.localScale=Vector3.one;piece.localRotation=Quaternion.identity;
                if(piece==centre&&stone)piece.localPosition=Vector3.zero;
                else if(!stone&&piece.name.Contains("Wood_Mid"))piece.localPosition=new Vector3((Array.IndexOf(mids,piece)-1)*3,0,0);
                else {int i=Array.IndexOf(ends,piece);piece.localPosition=new Vector3((i==0?-1:1)*(stone?4.5f:6),0,0);piece.localRotation=Quaternion.Euler(0,stone?(i==0?0:180):(i==0?180:0),0);}
                foreach(var f in piece.GetComponentsInChildren<MeshFilter>())
                {
                    f.gameObject.layer=7;var c=f.GetComponent<MeshCollider>();if(c==null)c=Undo.AddComponent<MeshCollider>(f.gameObject);c.sharedMesh=f.sharedMesh;c.convex=false;c.enabled=true;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(piece);
            }
            // Editable test/approach anchors, extending just beyond both ends of the bridge.
            foreach(var sign in new[]{-1,1}){var p=Group(sign<0?"Crossing_Start":"Crossing_End",bridge);p.localPosition=new Vector3(sign*(stone?7.6f:7.5f),0,0);}
            Physics.SyncTransforms();
            var terrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;
            for(float x=stone?-7.6f:-7.5f;x<=7.61f;x+=.5f)
            {
                Vector3 p=bridge.TransformPoint(new Vector3(x,0,0));bool hit=Physics.Raycast(p+Vector3.up*10,Vector3.down,out var h,20,1<<7,QueryTriggerInteraction.Ignore);
                report.AppendLine(bridge.name+" x="+x.ToString("F2")+" solid="+hit+" deck="+h.point.y.ToString("F3")+" terrain="+(terrain.SampleHeight(p)+terrain.transform.position.y).ToString("F3"));
            }
        }
        var shader=Shader.Find("Project/Gentle River Water");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Water shader did not compile.");
        var water=new Material(shader){name="Environment_Gentle_River"};water.SetColor("_WaterColor",new Color(.11f,.30f,.33f,.6f));water.SetFloat("_WaveHeight",.018f);water.SetFloat("_WaveSpeed",.45f);water.SetFloat("_NormalStrength",.12f);AssetDatabase.CreateAsset(water,Folder+"/Environment_Gentle_River.mat");
        var waters=environment.Cast<Transform>().Where(t=>t.name.StartsWith("UNS_Water_Detailed")).OrderBy(t=>t.position.x).ToArray();
        if(waters.Length==2){Undo.RecordObject(waters[1],"Join adjacent water tiles");waters[1].position=waters[0].position+Vector3.right*50;PrefabUtility.RecordPrefabInstancePropertyModifications(waters[1]);}
        foreach(var tile in waters)
        {
            foreach(var r in tile.GetComponentsInChildren<Renderer>(true)){Undo.RecordObject(r,"Gentle river water");r.sharedMaterial=water;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            foreach(var c in tile.GetComponentsInChildren<Collider>(true)){Undo.RecordObject(c,"Water is not a solid walking floor");c.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        }
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/EnvironmentBridgeNavigation.asset");EditorUtility.SetDirty(surface);
        File.WriteAllText("Logs/EnvironmentBridgeBuild.txt",report.ToString());AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(environment.gameObject.scene);EditorSceneManager.SaveScene(environment.gameObject.scene);
        Capture("EnvironmentBridges_After",new Vector3(-26,3,-9),new Vector3(25,46,-42),32);Selection.activeGameObject=group.gameObject;
        Debug.Log("Environment bridges aligned with mesh collision; subtle shared world-space river ripples installed.");
    }
}
#endif
