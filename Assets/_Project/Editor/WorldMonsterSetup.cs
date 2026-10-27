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

public static partial class WorldMonsterSetup
{
    public const string Folder="Assets/_Project/Enemies/WorldPopulation";
    public const string GoblinSource="Assets/Charttyp/Goblin demon creature modular unit Demo/Prefabs/Creature.prefab";
    public const string GoblinModel="Assets/Charttyp/Goblin demon creature modular unit Demo/Meshes/Creature.fbx";
    public const string SkeletonSource="Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Skeleton/PT_Skeleton_Male_Modular.prefab";
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static Bounds BoundsOf(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();if(rs.Length==0)return new Bounds(go.transform.position,Vector3.zero);
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private static void Render(string name,Vector3 target,Vector3 offset,float size)
    {
        var go=new GameObject("Temporary world-monster camera");var cam=go.AddComponent<Camera>();var rt=RenderTexture.GetTemporary(1500,1200,24);var old=RenderTexture.active;var pixels=new Texture2D(1500,1200,TextureFormat.RGB24,false);
        try{cam.transform.position=target+offset;cam.transform.LookAt(target);cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=600;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1500,1200),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/Enemies/World Population/Inspect Models Camp Props and Map")]
    public static void Inspect()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var report=new StringBuilder("World monster source audit "+DateTime.Now.ToString("s")+"\n");
        foreach(string source in new[]{GoblinSource,SkeletonSource})
        {
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));model.transform.position=new Vector3(0,80,0);
            try
            {
                report.AppendLine("MODEL "+source+" bounds="+BoundsOf(model));
                foreach(var a in model.GetComponentsInChildren<Animator>(true))report.AppendLine(" ANIMATOR "+PathOf(a.transform)+" human="+a.isHuman+" avatar="+a.avatar?.name+" valid="+(a.avatar!=null&&a.avatar.isValid)+" controller="+a.runtimeAnimatorController?.name);
                foreach(var r in model.GetComponentsInChildren<Renderer>(true))report.AppendLine(" RENDERER "+r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" mats="+string.Join(",",r.sharedMaterials.Where(m=>m!=null).Select(m=>m.name+":"+m.shader.name)));
                foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("spine")||t.name.StartsWith("PT_Hips")||t.name.Contains("Head")||t.name.Contains("DEF-pelvis")))report.AppendLine(" BONE "+t.name+" pos="+(t.position-model.transform.position).ToString("F3"));
                var b=BoundsOf(model);Render("WorldMonster_Source_"+(source==GoblinSource?"Goblin":"Skeleton"),b.center,new Vector3(3,2,-5)*Mathf.Max(1,b.size.y/2),Mathf.Max(1.8f,b.size.y*.7f));
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
        }
        foreach(var path in new[]{GoblinModel,"Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_NPC/Meshes/Skeleton/PT_Skeleton_Male_Modular.fbx"})
            foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))report.AppendLine("CLIP "+path+" name="+clip.name+" human="+clip.isHumanMotion+" length="+clip.length);
        foreach(string prop in new[]{"Goblin_Tent","Goblin_ChiefTent","Goblin_Bonfire","Goblin_Watchtower","Totem_Goblin","Goblin_Gate_Broken","Cage"})
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MedievalModelOnlySetup.Package+"/Generated/Prefabs/VKI/Prop/SM_VKI_Prop_"+prop+".prefab"));
            report.AppendLine("CAMP PROP "+prop+" bounds="+BoundsOf(go));UnityEngine.Object.DestroyImmediate(go);
        }
        foreach(var t in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>new[]{"Zone_City","Zone_Villager","Zone_SummoningRuins","Zone_Demon"}.Contains(t.name)))report.AppendLine("ZONE "+PathOf(t)+" bounds="+BoundsOf(t.gameObject));
        foreach(var spawn in UnityEngine.Object.FindObjectsByType<PlayerSceneSpawnPoint>(FindObjectsSortMode.None))report.AppendLine("PLAYER SPAWN "+spawn.Position);
        var terrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;
        report.AppendLine("NAV GRID (Nav point within 0.8m of terrain)");
        for(int z=-85;z<=85;z+=10){report.Append(z+":");for(int x=-85;x<=85;x+=10){var p=new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y,z);report.Append(NavMesh.SamplePosition(p,out var hit,.8f,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-p.y)<.4f?" "+x:" .");}report.AppendLine();}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WorldMonsterSources.txt",report.ToString());Render("WorldMonster_Map_Before",new Vector3(0,4,0),new Vector3(0,210,.001f),105);Debug.Log("World monster source and map audit saved.");
    }
}
#endif
