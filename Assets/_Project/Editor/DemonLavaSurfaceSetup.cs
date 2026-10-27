#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Edits only the new lava visuals; the user's terrain and painted channels are the source.</summary>
public static partial class DemonLavaSurfaceSetup
{
    private const string Source="Assets/Shaders/Lava/";
    private const string Folder="Assets/_Project/World/DemonLavaSurfaces";
    private const string RootName="DemonLava_AnimatedPools_And_MountainFlows";
    private static Terrain CurrentTerrain()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
        var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();return map!=null?map.terrain:Terrain.activeTerrain;
    }
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    [MenuItem("Tools/World/Lava Surfaces/Inspect Current Terrain and New Asset")]
    public static void Inspect()
    {
        var terrain=CurrentTerrain();var data=terrain.terrainData;var log=new StringBuilder("Lava source and current scene "+DateTime.Now.ToString("s")+"\n");
        log.AppendLine("TERRAIN "+PathOf(terrain.transform)+" asset="+AssetDatabase.GetAssetPath(data)+" position="+terrain.transform.position+" size="+data.size+" alpha="+data.alphamapWidth+"x"+data.alphamapHeight);
        for(int i=0;i<data.terrainLayers.Length;i++)
        {var layer=data.terrainLayers[i];log.AppendLine("LAYER "+i+" "+AssetDatabase.GetAssetPath(layer)+" diffuse="+AssetDatabase.GetAssetPath(layer.diffuseTexture));}
        var alpha=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);var layers=data.terrainLayers;var lavaLayers=Enumerable.Range(0,layers.Length).Where(i=>(AssetDatabase.GetAssetPath(layers[i])+AssetDatabase.GetAssetPath(layers[i].diffuseTexture)).ToLowerInvariant().Contains("magma")||(AssetDatabase.GetAssetPath(layers[i])+AssetDatabase.GetAssetPath(layers[i].diffuseTexture)).ToLowerInvariant().Contains("lava")).ToArray();
        log.AppendLine("LAVA LAYERS "+string.Join(",",lavaLayers));int samples=0;float min=float.MaxValue,max=float.MinValue;Vector2 low=Vector2.zero,high=Vector2.zero;
        for(int z=0;z<data.alphamapHeight;z++)for(int x=0;x<data.alphamapWidth;x++)
        {
            if(lavaLayers.Sum(k=>alpha[z,x,k])<.45f)continue;float wx=terrain.transform.position.x+x*data.size.x/(data.alphamapWidth-1),wz=terrain.transform.position.z+z*data.size.z/(data.alphamapHeight-1),y=terrain.SampleHeight(new Vector3(wx,0,wz))+terrain.transform.position.y;
            samples++;if(y<min){min=y;low=new Vector2(wx,wz);}if(y>max){max=y;high=new Vector2(wx,wz);}
        }
        log.AppendLine("PAINTED LAVA samples="+samples+" min="+min+" @"+low+" max="+max+" @"+high);
        for(int z=20;z<=103;z+=4)
        {for(int x=0;x<=100;x+=4){float u=(x-terrain.transform.position.x)/data.size.x,v=(z-terrain.transform.position.z)/data.size.z;int ax=Mathf.Clamp(Mathf.RoundToInt(u*(data.alphamapWidth-1)),0,data.alphamapWidth-1),az=Mathf.Clamp(Mathf.RoundToInt(v*(data.alphamapHeight-1)),0,data.alphamapHeight-1);float mask=lavaLayers.Sum(k=>alpha[az,ax,k]);log.Append(mask>.45f?((int)(terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y)).ToString("00")+" ":" . ");}log.AppendLine(" z="+z);}
        foreach(string file in new[]{"Lava.mat","Lavafall.mat"})
        {var material=AssetDatabase.LoadAssetAtPath<Material>(Source+file);log.AppendLine("MATERIAL "+file+" shader="+material.shader.name+" supported="+material.shader.isSupported+" errors="+ShaderUtil.ShaderHasError(material.shader));foreach(string property in new[]{"_LavaTex","_LavaNormal"})log.AppendLine("  "+property+" "+AssetDatabase.GetAssetPath(material.GetTexture(property)));}
        var zone=UnityEngine.Object.FindObjectsByType<DemonCliffZone>(FindObjectsInactive.Include).FirstOrDefault();log.AppendLine("OLD ZONE active="+(zone!=null&&zone.gameObject.activeInHierarchy));
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())if(renderer.name.Contains("Lava")||renderer.name.Contains("Crag")||renderer.name.Contains("Mountain"))log.AppendLine("RENDERER "+PathOf(renderer.transform)+" bounds="+renderer.bounds);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DemonLavaInspection.txt",log.ToString());Debug.Log("Lava inspection: Logs/DemonLavaInspection.txt");
    }
}
#endif
