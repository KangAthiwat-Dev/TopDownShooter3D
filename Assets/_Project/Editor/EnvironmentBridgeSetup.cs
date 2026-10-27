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
    private const string Folder="Assets/_Project/World/EnvironmentBridges";
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static Bounds BoundsOf(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)return new Bounds(go.transform.position,Vector3.zero);
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    private static void Capture(string name,Vector3 target,Vector3 offset,float size)
    {
        var go=new GameObject("Temporary bridge inspection camera");var cam=go.AddComponent<Camera>();var rt=RenderTexture.GetTemporary(1500,1100,24);var old=RenderTexture.active;var image=new Texture2D(1500,1100,TextureFormat.RGB24,false);
        try{cam.transform.position=target+offset;cam.transform.LookAt(target);cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=500;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1500,1100),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/World/Environment Bridges/Inspect Bridges and Water")]
    public static void Inspect()
    {
        var report=new StringBuilder("Live Environment bridges and water "+DateTime.Now.ToString("s")+"\n");
        var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach(var t in all.Where(t=>t.name=="Environment"||t.name=="brige"||t.name=="brige_wood"))
        {
            report.AppendLine("GROUP "+PathOf(t)+" pos="+t.position+" rot="+t.eulerAngles+" scale="+t.lossyScale+" bounds="+BoundsOf(t.gameObject));
            foreach(Transform child in t)report.AppendLine(" CHILD "+PathOf(child)+" pos="+child.position.ToString("F3")+" rot="+child.eulerAngles.ToString("F2")+" scale="+child.lossyScale.ToString("F3")+" bounds="+BoundsOf(child.gameObject)+" source="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)+" colliders="+child.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled));
        }
        foreach(var family in new[]{"Stone_Span","Stone_Ramp","Wood_Mid","Wood_End"})
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(MedievalModelOnlySetup.Package+"/Generated/Prefabs/VK/Bridge/SM_VK_Bridge_"+family+".prefab");
            foreach(var f in model.GetComponentsInChildren<MeshFilter>())
                report.AppendLine("MODEL "+family+" mesh="+f.sharedMesh.name+" bounds="+f.sharedMesh.bounds+" pos="+f.transform.localPosition+" rot="+f.transform.localEulerAngles+" scale="+f.transform.localScale);
        }
        var terrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;
        foreach(var t in all.Where(t=>t.gameObject.activeInHierarchy&&(t.name.Contains("Water")||t.name.Contains("water"))))
        {
            report.AppendLine("WATER "+PathOf(t)+" pos="+t.position+" rot="+t.eulerAngles+" scale="+t.lossyScale+" bounds="+BoundsOf(t.gameObject));
            foreach(var r in t.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials.Where(m=>m!=null))report.AppendLine(" MATERIAL "+AssetDatabase.GetAssetPath(m)+" shader="+m.shader.name+" json="+EditorJsonUtility.ToJson(m));
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EnvironmentBridgeInspection.txt",report.ToString());
        Capture("EnvironmentBridges_Before",new Vector3(-26,3,-9),new Vector3(25,46,-42),32);
        Debug.Log("Live Environment bridge / water inspection saved.");
    }
}
#endif
