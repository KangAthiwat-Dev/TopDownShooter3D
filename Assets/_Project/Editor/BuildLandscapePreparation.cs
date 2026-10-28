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

public static partial class BuildPreparationSetup
{
    private static bool Actor(Transform t)=>t.GetComponentInParent<PlayerMovement>()!=null||t.GetComponentInParent<EnemyHealth>()!=null||t.GetComponentInParent<TownNpcBrain>()!=null||t.GetComponentInParent<CivilianHealth>()!=null;
    private static bool TreeRenderer(MeshRenderer renderer)
    {
        for(var t=renderer.transform;t!=null;t=t.parent)
            if(t.name.IndexOf("ForestOak",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("Tree_",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("TreeSapling",StringComparison.OrdinalIgnoreCase)>=0)return true;
        return false;
    }
    private static bool[,] GrassBlockers(Terrain terrain,out int shapes)
    {
        var data=terrain.terrainData;int w=data.detailWidth,h=data.detailHeight;var origin=terrain.transform.position;var size=data.size;var mask=new bool[h,w];int count=0;
        void Paint(Bounds bounds,float padding,bool water=false)
        {
            bounds.Expand(new Vector3(padding*2,0,padding*2));
            int x0=Mathf.Clamp(Mathf.FloorToInt((bounds.min.x-origin.x)/size.x*w),0,w-1),x1=Mathf.Clamp(Mathf.CeilToInt((bounds.max.x-origin.x)/size.x*w),0,w-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((bounds.min.z-origin.z)/size.z*h),0,h-1),z1=Mathf.Clamp(Mathf.CeilToInt((bounds.max.z-origin.z)/size.z*h),0,h-1);
            if(bounds.max.x<origin.x||bounds.min.x>origin.x+size.x||bounds.max.z<origin.z||bounds.min.z>origin.z+size.z)return;
            count++;
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                if(mask[z,x])continue;var point=new Vector3(origin.x+(x+.5f)/w*size.x,0,origin.z+(z+.5f)/h*size.z);float ground=terrain.SampleHeight(point)+origin.y;
                if(water?ground<=bounds.max.y+.35f:bounds.min.y<=ground+.65f&&bounds.max.y>=ground-.15f)mask[z,x]=true;
            }
        }
        foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if(collider.enabled&&!collider.isTrigger&&!(collider is TerrainCollider)&&!Actor(collider.transform))Paint(collider.bounds,.55f);
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(!renderer.enabled||Actor(renderer.transform)||TreeRenderer(renderer))continue;
            bool water=renderer.sharedMaterials.Any(m=>m!=null&&m.shader!=null&&m.shader.name.IndexOf("water",StringComparison.OrdinalIgnoreCase)>=0);
            Paint(renderer.bounds,water?.75f:.55f,water);
        }
        // Trees represented as Terrain instances have no scene MeshRenderer.
        foreach(var tree in data.treeInstances)
        {
            var p=Vector3.Scale(tree.position,size)+origin;Paint(new Bounds(p+Vector3.up,new Vector3(1.4f*tree.widthScale,3,1.4f*tree.widthScale)),.45f);
        }
        shapes=count;return mask;
    }
    [MenuItem("Tools/Build Preparation/Fill Green Ground Grass and Fix City Fountain")]
    public static void Landscape()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;var scene=SceneManager.GetActiveScene();if(scene.name!=GameMenuUI.WorldScene)throw new InvalidOperationException("Open WorldPrototype first.");
        var terrain=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>().terrain;var data=terrain.terrainData;
        int grass=Array.FindIndex(data.detailPrototypes,p=>p.prototype!=null&&p.prototype.name=="ForestGrass");
        var green=Enumerable.Range(0,data.terrainLayers.Length).Where(i=>data.terrainLayers[i]!=null&&data.terrainLayers[i].diffuseTexture!=null&&data.terrainLayers[i].diffuseTexture.name.IndexOf("Grass",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
        if(grass<0||green.Length==0)throw new InvalidOperationException("Expected the existing ForestGrass detail and green terrain layer.");
        Directory.CreateDirectory(LogFolder);string backup=LogFolder+"/LandscapeBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene,backup+"/WorldPrototype.unity",true);string path=AssetDatabase.GetAssetPath(data);File.Copy(path,backup+"/Terrain.asset");File.Copy(path+".meta",backup+"/Terrain.asset.meta");
        var mask=GrassBlockers(terrain,out int shapes);var paint=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);var random=new System.Random(28102026);
        int w=data.detailWidth,h=data.detailHeight;var detail=new int[h,w];float cell=data.size.x*data.size.z/(w*h);long before=0,after=0;var old=data.GetDetailLayer(0,0,w,h,grass);foreach(int value in old)before+=value;
        Undo.RegisterCompleteObjectUndo(data,"Fill green ground with grass excluding scenery");
        for(int z=0;z<h;z++)for(int x=0;x<w;x++)
        {
            if(mask[z,x])continue;float nx=(x+.5f)/w,nz=(z+.5f)/h;int ax=Mathf.Min(data.alphamapWidth-1,(int)(nx*data.alphamapWidth)),az=Mathf.Min(data.alphamapHeight-1,(int)(nz*data.alphamapHeight));
            float greenWeight=0;foreach(int layer in green)greenWeight+=paint[az,ax,layer];if(greenWeight<.6f)continue;
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,.9f,greenWeight));float patch=Mathf.Lerp(.7f,1.2f,Mathf.PerlinNoise(nx*32+11,nz*32+7));
            if(random.NextDouble()>cell*2.6f*blend*patch||data.GetInterpolatedHeight(nx,nz)+terrain.transform.position.y<3||data.GetSteepness(nx,nz)>38)continue;
            detail[z,x]=data.detailScatterMode==DetailScatterMode.InstanceCountMode?1:data.maxDetailScatterPerRes;after+=detail[z,x];
        }
        data.SetDetailLayer(0,0,grass,detail);
        for(int i=0;i<data.detailPrototypes.Length;i++)if(i!=grass)
        {var values=data.GetDetailLayer(0,0,w,h,i);for(int z=0;z<h;z++)for(int x=0;x<w;x++)if(mask[z,x])values[z,x]=0;data.SetDetailLayer(0,0,i,values);}
        var fountain=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="SM_VK_Prop_Fountain");
        var filter=fountain.GetComponentInChildren<MeshFilter>();if(filter==null||filter.sharedMesh==null)throw new InvalidOperationException("City fountain mesh is missing.");
        var collision=filter.GetComponent<MeshCollider>();if(collision==null)collision=Undo.AddComponent<MeshCollider>(filter.gameObject);else Undo.RecordObject(collision,"Solid fountain mesh");
        collision.sharedMesh=filter.sharedMesh;collision.convex=false;collision.isTrigger=false;collision.enabled=true;filter.gameObject.layer=7;
        var obstacle=fountain.GetComponent<NavMeshObstacle>();if(obstacle==null)obstacle=Undo.AddComponent<NavMeshObstacle>(fountain.gameObject);else Undo.RecordObject(obstacle,"Avoid city fountain");
        var renderer=filter.GetComponent<MeshRenderer>();var local=filter.sharedMesh.bounds;obstacle.shape=NavMeshObstacleShape.Capsule;obstacle.center=local.center;obstacle.radius=Mathf.Max(local.extents.x,local.extents.z);obstacle.height=local.size.y;obstacle.carving=true;obstacle.carveOnlyStationary=true;obstacle.carvingTimeToStationary=.1f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(collision);PrefabUtility.RecordPrefabInstancePropertyModifications(obstacle);
        EditorUtility.SetDirty(data);EditorUtility.SetDirty(terrain);Physics.SyncTransforms();terrain.Flush();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var mesh=data.detailPrototypes[grass].prototype.GetComponent<MeshFilter>()?.sharedMesh;
        File.WriteAllText(LogFolder+"/LandscapeResult.txt",$"Grass before={before}; after={after}; detail layer={grass}; green layers={string.Join(",",green)}; exclusion shapes={shapes}\nInstanced grass clumps, 2.6/m2 base, noise variation, slope <=38 degrees; water/roads/soil/rock and grounded scenery excluded, tree trunks cleared. Terrain height and texture layers unchanged.\nGrass mesh={mesh?.name} vertices={mesh?.vertexCount} bounds={mesh?.bounds}\nFountain={ScenePath(fountain)} mesh collider enabled on Environment, obstacle carves navigation. Renderer bounds={renderer.bounds}\nBackup={backup}\n");
        Debug.Log("Green ground grass filled and city fountain collision installed: "+after+" detail instances.");
    }
    [MenuItem("Tools/Build Preparation/Validate Grass Exclusions and Fountain")]
    public static void ValidateLandscape()
    {
        var terrain=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>().terrain;var data=terrain.terrainData;var mask=GrassBlockers(terrain,out _);int count=0,underProps=0,offGreen=0;
        var paint=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);var green=Enumerable.Range(0,data.terrainLayers.Length).Where(i=>data.terrainLayers[i]?.diffuseTexture?.name.IndexOf("Grass",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
        for(int layer=0;layer<data.detailPrototypes.Length;layer++)
        {
            var detail=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer);
            for(int z=0;z<data.detailHeight;z++)for(int x=0;x<data.detailWidth;x++)if(detail[z,x]>0)
            {
                count+=detail[z,x];if(mask[z,x])underProps++;
                int ax=Mathf.Min(data.alphamapWidth-1,(int)((x+.5f)/data.detailWidth*data.alphamapWidth)),az=Mathf.Min(data.alphamapHeight-1,(int)((z+.5f)/data.detailHeight*data.alphamapHeight));float weight=0;foreach(int i in green)weight+=paint[az,ax,i];if(weight<.6f)offGreen++;
            }
        }
        var fountain=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="SM_VK_Prop_Fountain");var collider=fountain.GetComponentInChildren<MeshCollider>();var obstacle=fountain.GetComponent<NavMeshObstacle>();
        bool valid=count>1000&&underProps==0&&offGreen==0&&collider!=null&&collider.enabled&&!collider.isTrigger&&obstacle!=null&&obstacle.carving;
        File.WriteAllText(LogFolder+"/LandscapeValidation.txt",$"{(valid?"PASS":"FAIL")} grass={count}; under scenery={underProps}; non-green ground={offGreen}; fountain collision={collider!=null&&collider.enabled}; navigation carving={obstacle!=null&&obstacle.carving}\n");
        if(!valid)throw new InvalidOperationException("Landscape validation failed. See Logs/BuildPreparation/LandscapeValidation.txt");Debug.Log("Grass exclusions and fountain collision validated.");
    }
}
#endif
