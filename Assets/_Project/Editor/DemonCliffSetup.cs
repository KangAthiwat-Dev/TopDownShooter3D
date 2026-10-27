#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static partial class DemonCliffSetup
{
    public const string Folder="Assets/_Project/World/DemonCliffs";
    public const string RootName="DemonCliffs_CastleApproach";
    private const string Inferno="Assets/ithappy/Inferno_World_Free/Prefabs/";
    private static readonly string[] SketchNames={"PlatformStair_001","Platform_003","PlatformSmall_001","PlatformSmall_002","Crag_001","Crag_003","Stone_006","RockBig_001"};
    private static void RequireScene()
    {if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");}
    private static Bounds BoundsOf(GameObject source)
    {
        var renderers=source.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return new Bounds(source.transform.position,Vector3.zero);
        var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
    }
    [MenuItem("Tools/World/Demon Cliffs/Inspect Live Sketch and Sources")]
    public static void Inspect()
    {
        RequireScene();var log=new StringBuilder("Demon cliff live audit "+DateTime.Now.ToString("s")+"\n");
        foreach(var go in SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>SketchNames.Any(n=>g.name.StartsWith(n))))
        {
            log.AppendLine("SKETCH "+go.name+" id="+go.GetEntityId()+" position="+go.transform.position.ToString("F3")+" rotation="+go.transform.eulerAngles.ToString("F2")+" scale="+go.transform.localScale.ToString("F3")+" bounds="+BoundsOf(go));
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))log.AppendLine("  mesh "+renderer.name+" bounds="+renderer.bounds);
            foreach(var collider in go.GetComponentsInChildren<Collider>(true))log.AppendLine("  collider "+collider.GetType().Name+" enabled="+collider.enabled+" bounds="+collider.bounds+" layer="+collider.gameObject.layer);
        }
        var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();var terrain=map.terrain;
        log.AppendLine("TERRAIN "+AssetDatabase.GetAssetPath(terrain.terrainData)+" transform="+terrain.transform.position+" size="+terrain.terrainData.size+" trees="+terrain.terrainData.treeInstanceCount);
        for(int z=30;z<=95;z+=13){for(int x=25;x<=95;x+=14)log.Append("("+x+","+z+")="+(terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y).ToString("F2")+" ");log.AppendLine();}
        foreach(var surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>())log.AppendLine("NAV "+surface.name+" data="+AssetDatabase.GetAssetPath(surface.navMeshData)+" layerMask="+surface.layerMask+" geometry="+surface.useGeometry+" agent="+surface.agentTypeID+" center="+surface.center+" size="+surface.size+" collect="+surface.collectObjects);
        foreach(string file in new[]{"Platforms/Platform_003.prefab","Platforms/PlatformSmall_001.prefab","Platforms/PlatformSmall_002.prefab","Decorations/PlatformStair_001.prefab","Decorations/StairsMiddle_001.prefab","Mountains/Cliff_003.prefab","Mountains/Cliff_004.prefab","Mountains/Cliff_005.prefab","Rocks/Crag_001.prefab","Props/Brazier_002.prefab","Props/Bone_003.prefab"})
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Inferno+file);if(asset==null){log.AppendLine("MISSING "+file);continue;}
            log.AppendLine("SOURCE "+file+" rootRot="+asset.transform.localEulerAngles+" bounds="+BoundsOf(asset)+" colliders="+asset.GetComponentsInChildren<Collider>(true).Length);
            foreach(var material in asset.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())log.AppendLine("  material="+material.name+" shader="+material.shader.name+" valid="+material.shader.isSupported);
        }
        var lava=AssetDatabase.LoadAssetAtPath<Material>("Assets/ithappy/Inferno_World_Free/Materials/Lava.mat");log.AppendLine("LAVA shader="+lava.shader.name+" supported="+lava.shader.isSupported+" errors="+ShaderUtil.ShaderHasError(lava.shader));
        log.AppendLine("Shadow vs Environment ignore="+Physics.GetIgnoreLayerCollision(9,7));
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DemonCliffsInspection.txt",log.ToString());Debug.Log("Demon cliff inspection saved to Logs/DemonCliffsInspection.txt");
    }
}
#endif
