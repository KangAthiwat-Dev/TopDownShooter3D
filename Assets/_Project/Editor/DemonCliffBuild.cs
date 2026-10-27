#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static partial class DemonCliffSetup
{
    private static Terrain terrain;
    private static StringBuilder buildLog;
    private static readonly HashSet<UnityEngine.Object> allowedChanges=new HashSet<UnityEngine.Object>();
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    private static string Hash(string path)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    private static Material Lit(string name,Color color,bool texture=false)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);
        if(texture)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/T_fwOF_RockSurf_02_BC.png"));
        AssetDatabase.CreateAsset(mat,Folder+"/Materials/"+name+".mat");return mat;
    }
    [MenuItem("Tools/World/Demon Cliffs/Build Three Level Lava Approach")]
    public static void Build()
    {
        RequireScene();if(UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>()!=null||AssetDatabase.IsValidFolder(Folder+"/Meshes"))throw new InvalidOperationException("Demon approach already built. Edit its hierarchy; do not overwrite later edits.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");string backup="Logs/SceneBackups/WorldPrototype_BeforeDemonCliffs_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot back up live scene.");
        buildLog=new StringBuilder("Demon cliffs build "+DateTime.Now.ToString("s")+"\nBACKUP "+backup+"\n");
        var before=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        allowedChanges.Clear();var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();terrain=map.terrain;
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();string oldTerrain=AssetDatabase.GetAssetPath(terrain.terrainData),oldNav=AssetDatabase.GetAssetPath(surface.navMeshData);string terrainHash=Hash(oldTerrain),navHash=Hash(oldNav);
        EnsureFolder(Folder+"/Meshes");EnsureFolder(Folder+"/Materials");EnsureFolder(Folder+"/Terrain");
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Build demon cliff approach");
        buildRoot=Group(RootName,map.transform);Undo.RegisterCreatedObjectUndo(buildRoot.gameObject,"Demon approach");
        walkRoot=Group("01_Walkable_ThreeStages_And_Ramps",buildRoot);guardRoot=Group("02_CliffEdge_Colliders",buildRoot);decorRoot=Group("03_Basalt_Scenery",buildRoot);
        var reference=Group("00_UserSketch_Reference_Inactive",buildRoot);
        foreach(var go in scene.GetRootGameObjects().Where(g=>SketchNames.Contains(g.name)))
        {allowedChanges.Add(go.transform);Undo.SetTransformParent(go.transform,reference,"Preserve original sketch");Undo.RecordObject(go,"Keep original sketch inactive");go.SetActive(false);buildLog.AppendLine("PRESERVED SKETCH "+go.name);}
        foreach(string group in new[]{"02_Forest","03_RocksAndUndergrowth","07_AxisAndFoliageFinish"})
        {
            var parent=map.transform.Find(group);if(parent==null)continue;
            foreach(Transform child in parent)
            {if(child.GetComponentInChildren<Renderer>()==null||!Inside(XZ(BoundsOf(child.gameObject).center),Basin))continue;Undo.RecordObject(child.gameObject,"Clear scoped lava basin foliage");child.gameObject.SetActive(false);buildLog.AppendLine("SCOPED FOLIAGE hidden "+child.name);}
        }
        floorMaterial=Lit("M_Basalt_Walkable",new Color(.39f,.38f,.40f),true);sideMaterial=Lit("M_Basalt_Cliff",new Color(.20f,.19f,.22f),true);rimMaterial=Lit("M_Broken_Cliff_Rims",new Color(.26f,.23f,.26f),true);ashMaterial=Lit("M_Approach_Ash",new Color(.25f,.22f,.23f),true);
        glowMaterial=Lit("M_Ember_Fissure",new Color(.7f,.12f,.01f));glowMaterial.EnableKeyword("_EMISSION");glowMaterial.SetColor("_EmissionColor",new Color(2.8f,.3f,.01f));
        CopyAndSculptTerrain();walkPaths.Clear();rimVertices.Clear();rimTriangles.Clear();
        var entry=new[]{P(8,Ground(8,49)+.03f,49),P(18,Heights[0],49),P(23,Heights[0],49)};
        var ramp2=new[]{P(84,Heights[0],34),P(84,Heights[1],47),P(84,Heights[1],50)};
        var ramp3=new[]{P(60,Heights[1],61),P(60,Heights[2],73),P(60,Heights[2],75)};
        walkPaths.Add(entry);walkPaths.Add(SmoothFlat(LowBridge));walkPaths.Add(ramp2);walkPaths.Add(ramp3);
        for(int i=0;i<3;i++)Deck(i);
        Ribbon("Ramp_01_West_Entrance",entry);Ribbon("Bridge_01_Curved_Southern_Lava",walkPaths[1]);Ribbon("Ramp_02_LowerRight_Ascent",ramp2);Ribbon("Ramp_03_Upper_Forecourt",ramp3);
        for(int i=0;i<3;i++)DeckGuards(i);
        MeshObject("Broken_Basalt_Edge_Parapets",decorRoot,rimVertices,rimTriangles,rimMaterial,false);
        MakeLavaAndScenery();MakeMarkers(map,entry[0]);ConnectForestTrails(map);MakeAtmosphere();
        allowedChanges.Add(map);allowedChanges.Add(map.transform);allowedChanges.Add(surface);allowedChanges.Add(terrain);allowedChanges.Add(terrain.GetComponent<TerrainCollider>());
        Physics.SyncTransforms();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/DemonCliffsNavigation.asset");EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        buildLog.AppendLine((Hash(oldTerrain)==terrainHash?"PASS":"FAIL")+" original terrain asset bytes unchanged: "+oldTerrain);
        buildLog.AppendLine((Hash(oldNav)==navHash?"PASS":"FAIL")+" original navigation asset bytes unchanged: "+oldNav);
        int unexpected=0;
        foreach(var pair in before)if(pair.Key!=null&&!allowedChanges.Contains(pair.Key)&&EditorJsonUtility.ToJson(pair.Key)!=pair.Value){unexpected++;buildLog.AppendLine("CHANGED EXISTING COMPONENT "+pair.Key.GetType().Name+" "+pair.Key.name);}
        buildLog.AppendLine((unexpected==0?"PASS":"FAIL")+" existing component preservation; unexpected="+unexpected);
        File.WriteAllText("Logs/DemonCliffsBuild.txt",buildLog.ToString());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Validate();Focus();Debug.Log("Built editable demon cliffs. See Logs/DemonCliffsBuild.txt and DemonCliffsValidation.txt");
    }
    private static float Ground(float x,float z)=>terrain.SampleHeight(P(x,0,z))+terrain.transform.position.y;
    private static void CopyAndSculptTerrain()
    {
        var source=terrain.terrainData;var data=UnityEngine.Object.Instantiate(source);data.name="DemonCliffsTerrain";AssetDatabase.CreateAsset(data,Folder+"/Terrain/DemonCliffsTerrain.asset");
        int n=data.heightmapResolution;var original=source.GetHeights(0,0,n,n);var heights=(float[,])original.Clone();Vector3 origin=terrain.transform.position,size=data.size;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            var point=V(origin.x+x*size.x/(n-1),origin.z+z*size.z/(n-1));if(!Inside(point,Basin))continue;
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(OutlineDistance(point)/4));float low=3.7f+.10f*Mathf.PerlinNoise(point.x*.15f,point.y*.15f);
            heights[z,x]=Mathf.Lerp(heights[z,x],(low-origin.y)/size.y,blend);
        }
        data.SetHeights(0,0,heights);
        var layer=new TerrainLayer{diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/T_fwOF_RockSurf_02_BC.png"),tileSize=new Vector2(7,7),smoothness=.08f,diffuseRemapMin=Vector4.zero,diffuseRemapMax=new Vector4(.32f,.29f,.31f,1)};
        AssetDatabase.CreateAsset(layer,Folder+"/Terrain/VolcanicAsh.terrainlayer");var layers=source.terrainLayers.ToList();layers.Add(layer);data.terrainLayers=layers.ToArray();
        int aw=data.alphamapWidth,ah=data.alphamapHeight,oldLayers=source.alphamapLayers;var oldAlpha=source.GetAlphamaps(0,0,aw,ah);var alpha=new float[ah,aw,oldLayers+1];
        for(int z=0;z<ah;z++)for(int x=0;x<aw;x++)
        {
            var p=V(origin.x+x*size.x/(aw-1),origin.z+z*size.z/(ah-1));float blend=Inside(p,Basin)?Mathf.SmoothStep(0,1,Mathf.Clamp01(OutlineDistance(p)/3)):0;
            for(int k=0;k<oldLayers;k++)alpha[z,x,k]=oldAlpha[z,x,k]*(1-blend);alpha[z,x,oldLayers]=blend;
        }
        data.SetAlphamaps(0,0,alpha);
        int dw=data.detailWidth,dh=data.detailHeight;
        for(int k=0;k<data.detailPrototypes.Length;k++)
        {
            var details=data.GetDetailLayer(0,0,dw,dh,k);for(int z=0;z<dh;z++)for(int x=0;x<dw;x++)if(Inside(V(origin.x+(x+.5f)*size.x/dw,origin.z+(z+.5f)*size.z/dh),Basin))details[z,x]=0;data.SetDetailLayer(0,0,k,details);
        }
        Undo.RecordObject(terrain,"Assign scoped terrain copy");terrain.terrainData=data;var tc=terrain.GetComponent<TerrainCollider>();Undo.RecordObject(tc,"Assign scoped terrain collision copy");tc.terrainData=data;terrain.Flush();
        int outsideChanges=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(!Inside(V(origin.x+x*size.x/(n-1),origin.z+z*size.z/(n-1)),Basin)&&heights[z,x]!=original[z,x])outsideChanges++;
        buildLog.AppendLine((outsideChanges==0?"PASS":"FAIL")+" heights outside basin preserved samples changed="+outsideChanges);
    }
    private static Transform Marker(string name,Transform parent,Vector3 position)
    {var t=Group(name,parent);t.position=position;return t;}
    private static void MakeMarkers(ForestMapLayout map,Vector3 entry)
    {
        var data=buildRoot.gameObject.AddComponent<DemonCliffZone>();data.outline=Basin.ToArray();var marks=Group("05_Encounter_And_Travel_Anchors_Future",buildRoot);
        data.entrance=Marker("Entrance_From_West",marks,entry);data.castleEntrance=Marker("CastleEntrance_Future_SeparateMap",marks,P(65,20.1f,89));data.castleFootprint=Marker("CastleFootprint_Reserved_NoBuilding",marks,P(64,20.1f,91));data.castleFootprintSize=new Vector2(28,10);
        var centers=new[]{P(33,9.8f,49),P(69,14.8f,54),P(65,20.1f,80)};var gates=new[]{P(46,9.8f,40),P(60,14.8f,62),P(65,20.1f,88)};
        data.stages=new DemonCliffZone.Stage[3];
        for(int i=0;i<3;i++)
        {
            var group=Group(i==2?"Stage03_MiniBoss_Forecourt":"Stage0"+(i+1)+"_CombatArena",marks);
            data.stages[i]=new DemonCliffZone.Stage{name=i==2?"Mini-boss forecourt":"Combat arena "+(i+1),center=Marker("ArenaCenter",group,centers[i]),arenaRadius=i==2?7:8,futureGate=Marker("GateAnchor_InactiveGameplay",group,gates[i])};
            data.stages[i].monsterSpawns=new[]{Marker("Spawn_A_Future",group,centers[i]+P(-5,0,2)),Marker("Spawn_B_Future",group,centers[i]+P(5,0,3)),Marker("Spawn_C_Future",group,centers[i]+P(0,0,5))};
        }
        var route=new List<Vector3>{entry,P(18,9.8f,49),centers[0],LowBridge[0]};route.AddRange(walkPaths[1].Where((p,i)=>i%4==0));route.Add(LowBridge.Last());route.AddRange(new[]{P(84,14.8f,47),P(84,14.8f,50),centers[1],P(60,14.8f,61),P(60,20.1f,73),P(60,20.1f,75),centers[2],data.castleEntrance.position});
        var routeGroup=Group("Route_Entrance_To_FutureCastle",marks);data.travelRoute=route.Select((p,i)=>Marker("Waypoint_"+i.ToString("00"),routeGroup,p)).ToArray();
        Undo.RecordObject(map,"Assign new castle landmark");map.castle=data.castleFootprint;
    }
    private static void ConnectForestTrails(ForestMapLayout map)
    {
        foreach(var route in map.routes)
        {
            if(!route.name.Contains("ToCastle")||route.points==null)continue;
            int first=route.name.Contains("NorthTrail")?5:5;
            var points=route.name.Contains("NorthTrail")?new[]{V(8,49),V(8,49),V(8,49),V(8,49),V(8,49)}:new[]{V(38,22),V(18,28),V(8,49)};
            for(int i=first;i<route.points.Length;i++){var t=route.points[i];allowedChanges.Add(t);Undo.RecordObject(t,"Connect forest trail to west entrance");var p=points[Mathf.Min(i-first,points.Length-1)];t.position=P(p.x,Ground(p.x,p.y),p.y);}
        }
        // Paint a winding continuation from the existing eastern trail, outside the lava basin.
        var nodes=SmoothFlat(new[]{P(54,0,16),P(38,0,22),P(18,0,28),P(8,0,38),P(8,0,49)});
        var data=terrain.terrainData;int aw=data.alphamapWidth,ah=data.alphamapHeight;var alpha=data.GetAlphamaps(0,0,aw,ah);int layer=data.alphamapLayers-1;Vector3 o=terrain.transform.position,s=data.size;
        for(int z=0;z<ah;z++)for(int x=0;x<aw;x++)
        {
            var p=V(o.x+x*s.x/(aw-1),o.z+z*s.z/(ah-1));if(Inside(p,Basin))continue;
            float distance=float.MaxValue;for(int j=1;j<nodes.Length;j++)distance=Mathf.Min(distance,SegmentDistance(p,XZ(nodes[j-1]),XZ(nodes[j])));
            float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,4.1f,distance));if(blend<=0)continue;for(int k=0;k<layer;k++)alpha[z,x,k]*=1-blend;alpha[z,x,layer]=Mathf.Lerp(alpha[z,x,layer],1,blend);
        }
        data.SetAlphamaps(0,0,alpha);
        // Grass and generated trees must not cover the new approach trail.
        int dw=data.detailWidth,dh=data.detailHeight;
        for(int k=0;k<data.detailPrototypes.Length;k++){var detail=data.GetDetailLayer(0,0,dw,dh,k);for(int z=0;z<dh;z++)for(int x=0;x<dw;x++){var p=V(o.x+(x+.5f)*s.x/dw,o.z+(z+.5f)*s.z/dh);if(nodes.Any(n=>Vector2.Distance(XZ(n),p)<3.4f))detail[z,x]=0;}data.SetDetailLayer(0,0,k,detail);}
        foreach(string name in new[]{"02_Forest","03_RocksAndUndergrowth","07_AxisAndFoliageFinish"}){var parent=map.transform.Find(name);if(parent==null)continue;foreach(Transform t in parent){if(t.GetComponentInChildren<Renderer>()==null)continue;var b=BoundsOf(t.gameObject);if(nodes.Any(n=>Vector2.Distance(XZ(n),XZ(b.center))<4.6f)){Undo.RecordObject(t.gameObject,"Clear approach trail");t.gameObject.SetActive(false);}}}
    }
    [MenuItem("Tools/World/Demon Cliffs/Focus Approach")]
    public static void Focus()
    {var zone=UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>();if(zone==null)return;Selection.activeGameObject=zone.gameObject;SceneView.lastActiveSceneView?.LookAt(P(55,9,65),Quaternion.Euler(58,0,0),76);}
}
#endif
