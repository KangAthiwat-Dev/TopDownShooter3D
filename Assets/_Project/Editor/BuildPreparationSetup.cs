#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class BuildPreparationSetup
{
    private const string LogFolder="Logs/BuildPreparation";
    private static string ScenePath(Transform t)=>t.parent==null?t.name:ScenePath(t.parent)+"/"+t.name;
    [MenuItem("Tools/Build Preparation/Inspect Terrain Fountain and Platforms")]
    public static void Inspect()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;Directory.CreateDirectory(LogFolder);var report=new StringBuilder();
        foreach(var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            var data=terrain.terrainData;report.AppendLine($"Terrain={ScenePath(terrain.transform)} data={AssetDatabase.GetAssetPath(data)} pos={terrain.transform.position} size={data.size} details={data.detailWidth}x{data.detailHeight} mode={data.detailScatterMode} distance={terrain.detailObjectDistance} density={terrain.detailObjectDensity}");
            for(int i=0;i<data.terrainLayers.Length;i++){var layer=data.terrainLayers[i];report.AppendLine($"Layer {i}: {AssetDatabase.GetAssetPath(layer)} texture={layer?.diffuseTexture?.name}");}
            for(int i=0;i<data.detailPrototypes.Length;i++){var p=data.detailPrototypes[i];var values=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,i);long count=0;foreach(var v in values)count+=v;report.AppendLine($"Detail {i}: {p.prototype?.name} mesh={p.usePrototypeMesh} instanced={p.useInstancing} width={p.minWidth}-{p.maxWidth} height={p.minHeight}-{p.maxHeight} count={count}");}
        }
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.IndexOf("fountain",StringComparison.OrdinalIgnoreCase)>=0))
        {
            report.AppendLine($"Fountain={ScenePath(t)} pos={t.position} scale={t.lossyScale} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)}");
            foreach(var r in t.GetComponentsInChildren<MeshRenderer>(true))report.AppendLine($"Renderer {r.name} layer={r.gameObject.layer} bounds={r.bounds}");
            foreach(var c in t.GetComponentsInChildren<Collider>(true))report.AppendLine($"Collider {c.name} {c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} bounds={c.bounds}");
            foreach(var o in t.GetComponentsInChildren<NavMeshObstacle>(true))report.AppendLine($"Navigation {o.name} shape={o.shape} size={o.size} radius={o.radius} carving={o.carving}");
        }
        foreach(var target in new[]{BuildTarget.StandaloneWindows64,BuildTarget.StandaloneOSX})report.AppendLine($"Build support {target}={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target)}");
        report.AppendLine("Active target="+EditorUserBuildSettings.activeBuildTarget);File.WriteAllText(LogFolder+"/Inspection.txt",report.ToString());Debug.Log("Terrain, fountain and build platforms inspected.");
    }
}
#endif
