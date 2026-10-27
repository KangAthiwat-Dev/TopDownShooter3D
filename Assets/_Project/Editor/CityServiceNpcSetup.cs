#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CityServiceNpcSetup
{
    private const string Folder="Assets/_Project/NPCs/CityServices";
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<MeshRenderer>();
        if(renderers.Length==0)return new Bounds(go.transform.position,Vector3.zero);
        var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    private static Transform Building(string name)
    {
        string Clean(string value)=>string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        var matches=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.gameObject.activeInHierarchy&&(Clean(t.name)==Clean(name)||name=="Gild"&&Clean(t.name)=="guild")).ToArray();
        if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one "+name+", found "+matches.Length);
        return matches[0];
    }
    private static void Render(string name,Vector3 target,Vector3 offset,float size)
    {
        var go=new GameObject("Temporary city service NPC camera");var cam=go.AddComponent<Camera>();var rt=RenderTexture.GetTemporary(1400,1100,24);var previous=RenderTexture.active;var image=new Texture2D(1400,1100,TextureFormat.RGB24,false);
        try{cam.transform.position=target+offset;cam.transform.LookAt(target);cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=300;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1100),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/NPC/City Services/Inspect Named Buildings")]
    public static void Inspect()
    {
        var report=new StringBuilder("City service building inspection "+DateTime.Now.ToString("s")+"\n");
        Directory.CreateDirectory("Logs");
        foreach(var t in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>new[]{"hotel","gild","guild","potion"}.Any(s=>t.name.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0)))report.AppendLine("NAME "+PathOf(t));
        File.WriteAllText("Logs/CityServiceBuildings.txt",report.ToString());
        foreach(string name in new[]{"Hotel","Gild","Potion Store"})
        {
            var b=Building(name);var bounds=BoundsOf(b.gameObject);
            report.AppendLine("BUILDING "+PathOf(b)+" position="+b.position.ToString("F3")+" rot="+b.eulerAngles+" scale="+b.lossyScale+" bounds="+bounds);
            foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>new[]{"Door","Entrance","NPC_Stand","Shop_Front"}.Any(s=>t.name.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0)))
                report.AppendLine(" ANCHOR "+PathOf(t)+" position="+t.position.ToString("F3")+" forward="+t.forward+" bounds="+BoundsOf(t.gameObject));
            File.WriteAllText("Logs/CityServiceBuildings.txt",report.ToString());
            Render("CityServices_Before_"+name.Replace(" ",""),new Vector3(bounds.center.x,bounds.min.y+2,bounds.center.z),new Vector3(10,13,-15),Mathf.Max(8,bounds.size.magnitude*.65f));
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CityServiceBuildings.txt",report.ToString());Debug.Log("Named service buildings inspected.");
    }
}
#endif
