using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>One-shot, source-preserving outdoor map builder. All output remains ordinary editable objects.</summary>
public static class WindingForestMapSetup
{
    public const string Folder = "Assets/_Project/World/WindingForestMap";
    public const string RootName = "Map_WindingForest";
    private const string Village = "Assets/Assets_Imported/villanger/";
    private const string Dungeon = "Assets/Assets_Imported/city/Assets/fbx(unity)/";
    private const string Inferno = "Assets/ithappy/Inferno_World_Free/Prefabs/";
    private const string Nature = "Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Prefabs/";
    private static Terrain terrain;
    private static System.Random random;
    private static Transform root, buildings, forest, rocks, props;
    private static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
    private static readonly List<Bounds> footprints = new List<Bounds>();
    private static readonly List<Vector2[]> curves = new List<Vector2[]>();
    private static readonly List<float> widths = new List<float>();
    private static Material stone, darkStone;
    private static float[,] roadField;
    private static readonly Vector2 VillageCenter = new Vector2(-63, 62), CityCenter = new Vector2(61, -57), CastleCenter = new Vector2(62, 65), CampCenter = new Vector2(-2, -3);

    private static string Building(string name) => Village + "Buildings/FBX/" + name + ".fbx";
    private static string Prop(string name) => Village + "Props/FBX/" + name + ".fbx";
    private static string Kit(string name) => Dungeon + name + ".fbx";

    [MenuItem("Tools/World/Full Map/Inspect Building Sources")]
    public static void Inspect()
    {
        var report = new StringBuilder("Outdoor map source audit " + DateTime.Now.ToString("s") + "\n");
        foreach (string path in new[] { Building("House_1"), Building("House_4"), Building("Inn"), Building("Mill"), Building("Blacksmith"), Prop("Fence"), Prop("Well"), Prop("Bonfire_Lit"), Kit("wall"), Kit("wall_doorway"), Kit("floor_tile_large"), Kit("stairs_wide"), Inferno + "Buildings/TowerBig_001.prefab" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) { report.AppendLine("MISSING " + path); continue; }
            Bounds b = BoundsOf(source);
            report.AppendLine($"MODEL {path} size={b.size:F3} center={b.center:F3}, meshes={source.GetComponentsInChildren<MeshFilter>(true).Length}, colliders={source.GetComponentsInChildren<Collider>(true).Length}");
            foreach (Material mat in source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                report.AppendLine($" MATERIAL {mat.name} shader={mat.shader?.name} texture={mat.mainTexture?.name} color={mat.color}");
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/WindingMapSources.txt", report.ToString()); Debug.Log(report.ToString());
    }

    [MenuItem("Tools/World/Full Map/Build Winding Forest Map")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype first.");
        if (GameObject.Find(RootName) != null || AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Map already exists; refusing to overwrite later manual edits.");
        terrain = Terrain.activeTerrains.Single();
        if (Vector2.Distance(new Vector2(terrain.terrainData.size.x, terrain.terrainData.size.z), new Vector2(200, 200)) > 0.1f) throw new InvalidOperationException("Expected the audited 200m terrain.");
        foreach (string path in new[] { Building("House_1"), Kit("wall"), Inferno + "Buildings/TowerBig_001.prefab", Nature + "P_fwOF_Tree_M_2.prefab" })
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing " + path);
        Directory.CreateDirectory("Logs/SceneBackups");
        string backup = "Logs/SceneBackups/WorldPrototype_BeforeFullMap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Live scene backup failed.");
        EnsureFolder(Folder); EnsureFolder(Folder + "/Materials"); EnsureFolder(Folder + "/Prefabs");
        random = new System.Random(26102026); materials.Clear(); footprints.Clear(); curves.Clear(); widths.Clear();
        root = Group(RootName, GameObject.Find("World")?.transform); var layout = root.gameObject.AddComponent<ForestMapLayout>();
        Transform routes = Group("00_WindingRoutes_EditableWaypoints", root);
        buildings = Group("01_Settlements", root); forest = Group("02_Forest", root); rocks = Group("03_RocksAndUndergrowth", root); props = Group("04_TrailLandmarks", root);
        MakeRoutes(routes, layout);
        CloneAndShapeTerrain(); layout.terrain = terrain;
        stone = FlatMaterial("M_WarmStone", new Color(0.47f, 0.44f, 0.36f)); darkStone = FlatMaterial("M_DemonStone", new Color(0.22f, 0.25f, 0.3f));
        BuildVillage(layout); BuildCity(layout); BuildCastle(layout); BuildCamp(layout);
        AddForest(); AddBoundary(layout); PaintGrassAndGround();
        layout.sanctuary = GameObject.Find("Zone_SummoningSanctum")?.transform;
        foreach (var route in layout.routes) foreach (Transform point in route.points) point.position = Ground(XZ(point.position)) + Vector3.up * 0.12f;
        Physics.SyncTransforms();
        var surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        Undo.RecordObject(surface, "New full-map navigation");
        surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("Full-map NavMesh build failed; scene backup retained.");
        AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/WindingForestNavigation.asset"); EditorUtility.SetDirty(surface);
        EditorUtility.SetDirty(terrain.terrainData); EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/WindingMapBuild.txt", $"Built {DateTime.Now:s}\nBackup {backup}\nNW village / NE castle / SW original sanctuary / SE city\nNew terrain/nav assets: {Folder}\nSource packs, old terrain/navigation, player, waves and markers retained.\n");
        Validate(); Capture(); Focus();
    }

    private static void MakeRoutes(Transform parent, ForestMapLayout layout)
    {
        var list = new List<ForestMapLayout.Route>();
        Action<string, float, Vector2[]> add = (name, width, nodes) =>
        {
            Transform group = Group(name, parent); var points = new List<Transform>();
            for (int i = 0; i < nodes.Length; i++) { Transform p = Group("Waypoint_" + i.ToString("00"), group); p.position = new Vector3(nodes[i].x, 0, nodes[i].y); points.Add(p); }
            list.Add(new ForestMapLayout.Route { name = name, width = width, points = points.ToArray() }); curves.Add(Smooth(nodes)); widths.Add(width);
        };
        add("01_SanctuaryTrail", 6, new[] { V(-74,-74), V(-65,-67), V(-60,-56), V(-57,-42), V(-57,-29), V(-52,-22) });
        add("02_WestTrail_ToVillage", 7, new[] { V(-52,-22), V(-65,-8), V(-57,12), V(-67,30), V(-61,44), V(-63,62) });
        add("03_SouthTrail_ToCity", 7, new[] { V(-52,-22), V(-35,-34), V(-22,-27), V(-4,-43), V(13,-39), V(27,-52), V(43,-50), V(61,-57) });
        add("04_NorthTrail_ToCastle", 7, new[] { V(-63,62), V(-44,57), V(-29,47), V(-11,62), V(7,48), V(23,62), V(41,53), V(50,38), V(62,43), V(62,53) });
        add("05_EastTrail_CityToCastle", 7, new[] { V(61,-57), V(61,-34), V(52,-18), V(68,-1), V(54,16), V(66,29), V(62,43), V(62,53) });
        add("06_CentralWoodlandShortcut", 4.5f, new[] { V(-52,-22), V(-34,-7), V(-17,-13), V(-2,-3), V(16,-9), V(33,0), V(52,-18) });
        add("07_VillageToCamp", 4.5f, new[] { V(-63,44), V(-45,31), V(-41,14), V(-22,9), V(-2,-3) });
        layout.routes = list.ToArray();
        // Rasterize once instead of scanning every curve for each of a million grass cells.
        roadField = new float[420,420];
        for(int z=0;z<420;z++)for(int x=0;x<420;x++)roadField[z,x]=1000;
        for(int r=0;r<curves.Count;r++)foreach(Vector2 p in curves[r])
        {
            int cx=Mathf.RoundToInt((p.x+104)*2),cz=Mathf.RoundToInt((p.y+104)*2);
            for(int z=Mathf.Max(0,cz-28);z<=Mathf.Min(419,cz+28);z++)for(int x=Mathf.Max(0,cx-28);x<=Mathf.Min(419,cx+28);x++)
                roadField[z,x]=Mathf.Min(roadField[z,x],Vector2.Distance(p,V(x*0.5f-104,z*0.5f-104))-widths[r]*0.5f);
        }
    }
    private static Vector2[] Smooth(Vector2[] nodes)
    {
        var points = new List<Vector2>();
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            Vector2 a = nodes[Mathf.Max(0, i-1)], b = nodes[i], c = nodes[i+1], d = nodes[Mathf.Min(nodes.Length-1, i+2)];
            int steps = Mathf.CeilToInt(Vector2.Distance(b,c));
            for (int j = 0; j < steps; j++) { float t = (float)j / steps; points.Add(0.5f * (2*b + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t)); }
        }
        points.Add(nodes.Last()); return points.ToArray();
    }
    private static float RoadDistance(Vector2 p)
    {
        return roadField[Mathf.Clamp(Mathf.RoundToInt((p.y+104)*2),0,419),Mathf.Clamp(Mathf.RoundToInt((p.x+104)*2),0,419)];
    }
    private static float Preserve(Vector2 p) => 1 - Mathf.SmoothStep(0,1, Mathf.Min(Mathf.InverseLerp(31,39,Vector2.Distance(p,V(-60,-60))), Mathf.InverseLerp(20,27,Vector2.Distance(p,V(-82.63f,-82.18f)))));
    private static float MeadowDistance(Vector2 p) => Mathf.Min(Vector2.Distance(p,VillageCenter)-28, Mathf.Min(Vector2.Distance(p,CityCenter)-29, Mathf.Min(Vector2.Distance(p,CastleCenter)-30,Vector2.Distance(p,CampCenter)-11)));
    private static void CloneAndShapeTerrain()
    {
        TerrainData source = terrain.terrainData;
        float[,,] paint = source.GetAlphamaps(0,0,source.alphamapWidth,source.alphamapHeight);
        var copy = UnityEngine.Object.Instantiate(source); copy.name = "WindingForestTerrain";
        AssetDatabase.CreateAsset(copy,Folder + "/WindingForestTerrain.asset"); copy.SetAlphamaps(0,0,paint);
        var heights = source.GetHeights(0,0,source.heightmapResolution,source.heightmapResolution);
        for (int z=0; z<source.heightmapResolution; z++) for (int x=0; x<source.heightmapResolution; x++)
        {
            Vector2 p = XZ(terrain.transform.position) + new Vector2((float)x/(source.heightmapResolution-1)*source.size.x,(float)z/(source.heightmapResolution-1)*source.size.z);
            float y = 4.4f + Mathf.Sin(p.x*0.045f)*0.55f + Mathf.Cos(p.y*0.052f)*0.55f;
            y = Mathf.Lerp(y,5.7f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(27,35,Vector2.Distance(p,VillageCenter))));
            y = Mathf.Lerp(y,5,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(29,36,Vector2.Distance(p,CityCenter))));
            y = Mathf.Lerp(y,8.3f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(29,41,Vector2.Distance(p,CastleCenter))));
            heights[z,x] = Mathf.Lerp(y/source.size.y, heights[z,x], Preserve(p));
        }
        copy.SetHeights(0,0,heights);
        Undo.RecordObject(terrain,"Use full-map terrain copy"); Undo.RecordObject(terrain.GetComponent<TerrainCollider>(),"Use full-map terrain collider");
        terrain.terrainData=copy; terrain.GetComponent<TerrainCollider>().terrainData=copy;
    }

    private static void BuildVillage(ForestMapLayout layout)
    {
        Transform zone=Group("01_Village_NW",buildings); zone.position=Ground(VillageCenter); layout.village=zone;
        Place(Building("House_1"),zone,V(-79,75),7,145,true); Place(Building("House_2"),zone,V(-60,79),8,180,true);
        Place(Building("House_3"),zone,V(-43,73),7,215,true); Place(Building("House_4"),zone,V(-80,55),7,80,true);
        Place(Building("Inn"),zone,V(-44,51),9,-80,true); Place(Building("Blacksmith"),zone,V(-78,38),7,40,true);
        Place(Building("Mill"),zone,V(-39,84),10,220,true); Place(Building("Stable"),zone,V(-44,37),6,145,true);
        Place(Prop("Well"),zone,V(-63,62),2.8f,0,false); Place(Prop("Cart"),zone,V(-47,61),2.8f,60,false);
        Place(Prop("MarketStand_1"),zone,V(-56,54),2.8f,10,false); Place(Prop("MarketStand_2"),zone,V(-69,52),2.8f,-15,false);
        Place(Prop("Bench_1"),zone,V(-67,67),1,30,false); Place(Prop("Bonfire_Lit"),zone,V(-52,69),1.1f,0,false);
        for(int i=0;i<8;i++) Place(Prop("Fence"),zone,V(-86+i*5,88),1.3f,0,false);
        Arrival(zone,"VillageArrival",V(-63,45)); Marker(zone,"FutureNPC_Merchant",V(-56,57)); Marker(zone,"FutureNPC_Blacksmith",V(-72,42));
    }
    private static void BuildCity(ForestMapLayout layout)
    {
        Transform zone=Group("02_Town_SE",buildings); zone.position=Ground(CityCenter); layout.city=zone;
        var plots=new[]{V(44,-76),V(60,-77),V(77,-76),V(78,-57),V(78,-40),V(44,-38),V(45,-61),V(56,-40)};
        string[] names={"House_2","Inn","House_1","Blacksmith","House_3","House_4","Stable","House_1"};
        for(int i=0;i<plots.Length;i++) Place(Building(names[i]),zone,plots[i],i==1?11:7.5f,i<3?0:i<5?-90:90,true);
        Place(Building("Bell_Tower"),zone,V(69,-48),11,0,true);
        Place(Prop("Well"),zone,V(61,-57),3,0,false);
        for(int i=0;i<3;i++){Place(Prop("MarketStand_1"),zone,V(55+i*6,-64),3,0,false);Place(Prop("Crate"),zone,V(55+i*5,-68),0.8f,15*i,false);}
        // Low town wall and two wide gateways keep the city distinct from the village.
        WallLine(zone,V(36,-85),V(86,-85),2.4f,false); WallLine(zone,V(86,-85),V(86,-31),2.4f,false);
        WallLine(zone,V(36,-85),V(36,-56),2.4f,false); WallLine(zone,V(36,-44),V(36,-31),2.4f,false);
        WallLine(zone,V(36,-31),V(55,-31),2.4f,false); WallLine(zone,V(68,-31),V(86,-31),2.4f,false);
        Arrival(zone,"CityWestArrival",V(40,-50)); Arrival(zone,"CityNorthArrival",V(61,-31)); Marker(zone,"FutureNPC_TownSquare",V(65,-59));
    }
    private static void BuildCastle(ForestMapLayout layout)
    {
        Transform zone=Group("03_DemonCastle_NE",buildings);zone.position=Ground(CastleCenter);layout.castle=zone;
        WallLine(zone,V(39,44),V(56,44),5,true);WallLine(zone,V(68,44),V(85,44),5,true);
        WallLine(zone,V(39,44),V(39,86),5,true);WallLine(zone,V(85,44),V(85,86),5,true);WallLine(zone,V(39,86),V(85,86),5,true);
        foreach(Vector2 p in new[]{V(40,45),V(84,45),V(40,85),V(84,85)})Place(Inferno+"Buildings/TowerBig_001.prefab",zone,p,12,0,true,"Castle");
        // Rear keep: courtyard remains open and readable from the top-down game camera.
        foreach(Vector2 p in new[]{V(53,76),V(71,76)})Place(Inferno+"Buildings/TowerBig_001.prefab",zone,p,16,0,true,"Castle");
        WallLine(zone,V(53,76),V(71,76),9,true);
        var dais=Cube("BossDais",zone,new Vector3(62,Ground(V(62,70)).y+0.15f,69),new Vector3(14,0.3f,12),darkStone,true);
        for(int i=0;i<4;i++) Place(Kit("pillar"),zone,V(49+i*8.5f,81),4,0,false,"Castle");
        foreach(Vector2 p in new[]{V(55,48),V(69,48)}){Place(Inferno+"Props/Brazier_002.prefab",zone,p,2,0,false,"Castle");AddLight(zone,p,new Color(1,0.32f,0.08f),2.2f);}
        Place(Kit("banner_red"),zone,V(43,46),2.3f,0,false,"Castle");Place(Kit("banner_red"),zone,V(81,46),2.3f,0,false,"Castle");
        Arrival(zone,"CastleGateArrival",V(62,46));Marker(zone,"FutureBoss_ArenaCenter",V(62,63));
    }
    private static void BuildCamp(ForestMapLayout layout)
    {
        Transform camp=Group("05_CentralForestCamp",props);camp.position=Ground(CampCenter);layout.camp=camp;
        Place(Prop("Bonfire_Lit"),camp,V(-4,0),1.2f,0,false);
        Place(Prop("Bench_1"),camp,V(-7,3),1.1f,-30,false);Place(Prop("Bench_2"),camp,V(0,2),1.1f,25,false);
        Place(Prop("Cart"),camp,V(-8,-8),2.8f,70,false);Place(Prop("Barrel"),camp,V(3,4),1,0,false);
        AddLight(camp,V(-4,0),new Color(1,0.62f,0.25f),1.3f);Arrival(camp,"CampArrival",CampCenter);
        foreach(Vector2 p in new[]{V(-52,-22),V(-57,39),V(30,-49),V(42,44)}){var sign=Place(Prop("Fence"),props,p+V(3,3),1.2f,40,false);sign.name="TrailLandmark_Fence";}
    }

    private static void AddForest()
    {
        // One cleaned project prefab; branch capsules from the source are not copied into every instance.
        var tree=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Nature+"P_fwOF_Tree_M_2.prefab"));
        foreach(Collider c in tree.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        var trunk=tree.AddComponent<CapsuleCollider>();trunk.radius=1.15f;trunk.height=5;trunk.center=Vector3.up*2.3f;
        SetLayer(tree,7);var treePrefab=PrefabUtility.SaveAsPrefabAsset(tree,Folder+"/Prefabs/ForestOak.prefab");UnityEngine.Object.DestroyImmediate(tree);
        for(float z=-89;z<92;z+=8.8f)for(float x=-91;x<92;x+=8.8f)
        {
            Vector2 p=V(x+Rand(-2.8f,2.8f),z+Rand(-2.8f,2.8f));
            if(Preserve(p)>0.01f||RoadDistance(p)<4.7f||MeadowDistance(p)<3||NearBuilding(p,5))continue;
            Place(AssetDatabase.GetAssetPath(treePrefab),forest,p,Rand(0.49f,0.7f),Rand(0,360),false);
        }
        for(int i=0;i<480;i++)
        {
            Vector2 p=V(Rand(-93,93),Rand(-91,93));
            if(Preserve(p)>0.01f||RoadDistance(p)<1.4f||MeadowDistance(p)<1||NearBuilding(p,3))continue;
            bool rock=i%3==0;
            Place(Nature+(rock?"P_fwOF_Rock_01":"P_fwOF_TreeSapling_02B")+".prefab",rocks,p,rock?Rand(1.4f,3.5f):Rand(1.0f,2.8f),Rand(0,360),false);
        }
        // Vegetated road shoulders add depth without putting solid trunks in the route.
        foreach(var curve in curves)for(int i=5;i<curve.Length-4;i+=12)
        {
            Vector2 direction=(curve[i+2]-curve[i-2]).normalized, side=V(-direction.y,direction.x);
            Vector2 p=curve[i]+side*Rand(5.6f,7.5f)*(i%2==0?1:-1);
            if(Preserve(p)>0.01f||MeadowDistance(p)<0||RoadDistance(p)<1)continue;
            Place(Nature+"P_fwOF_ForestPlant_B_02.prefab",rocks,p,Rand(2,4),Rand(0,360),false);
        }
    }
    private static void AddBoundary(ForestMapLayout layout)
    {
        Transform border=Group("06_MapBoundary_VisualCliffsAndSafetyColliders",root);
        Vector2 min=XZ(terrain.transform.position)+Vector2.one*2,max=XZ(terrain.transform.position+terrain.terrainData.size)-Vector2.one*2;
        layout.minimum=min;layout.maximum=max;
        Cube("WestSafetyWall",border,new Vector3(min.x,15,(min.y+max.y)/2),new Vector3(2,50,max.y-min.y),null,true);
        Cube("EastSafetyWall",border,new Vector3(max.x,15,(min.y+max.y)/2),new Vector3(2,50,max.y-min.y),null,true);
        Cube("SouthSafetyWall",border,new Vector3((min.x+max.x)/2,15,min.y),new Vector3(max.x-min.x,50,2),null,true);
        Cube("NorthSafetyWall",border,new Vector3((min.x+max.x)/2,15,max.y),new Vector3(max.x-min.x,50,2),null,true);
        for(float u=0;u<196;u+=9)
        {
            foreach(Vector2 p in new[]{V(min.x+2,min.y+u),V(max.x-2,min.y+u),V(min.x+u,min.y+2),V(min.x+u,max.y-2)})
            {
                if(Vector2.Distance(p,V(-82.63f,-82.18f))<13)continue;
                var cliff=Place(Inferno+"Mountains/Cliff_003.prefab",border,p,Rand(7,10),Rand(0,360),false,"Boundary");
                // Cliff visuals sit behind the playing area; the explicit boxes provide reliable continuous containment.
                foreach(Collider c in cliff.GetComponentsInChildren<Collider>())c.enabled=false;
            }
        }
    }
    private static void PaintGrassAndGround()
    {
        TerrainData data=terrain.terrainData;float[,,] old=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight), paint=(float[,,])old.Clone();
        for(int z=0;z<data.alphamapHeight;z++)for(int x=0;x<data.alphamapWidth;x++)
        {
            Vector2 p=XZ(terrain.transform.position)+V((float)x/(data.alphamapWidth-1)*data.size.x,(float)z/(data.alphamapHeight-1)*data.size.z);
            float keep=Preserve(p);if(keep>=0.9999f)continue;
            float road=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-0.8f,1.5f,RoadDistance(p)));
            float plaza=0;foreach(Vector2 c in new[]{VillageCenter,CityCenter,CampCenter})plaza=Mathf.Max(plaza,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,13,Vector2.Distance(p,c))));
            float castle=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(23,31,Vector2.Distance(p,CastleCenter)));
            float dirt=Mathf.Max(road*0.94f,plaza*0.88f)*(1-castle*0.85f),rock=castle*0.84f,moss=(1-dirt-rock)*0.07f;
            float grass=Mathf.Max(0,1-dirt-rock-moss);float[] values={0,grass,dirt,moss,rock};
            for(int l=0;l<data.alphamapLayers;l++)paint[z,x,l]=Mathf.Lerp(l<values.Length?values[l]:0,old[z,x,l],keep);
        }
        data.SetAlphamaps(0,0,paint);
        // Keep existing brush edits in the protected start area, paint new grass elsewhere.
        for(int layer=0;layer<data.detailPrototypes.Length;layer++)
        {
            int[,] detail=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer);
            for(int z=0;z<data.detailHeight;z++)for(int x=0;x<data.detailWidth;x++)
            {
                Vector2 p=XZ(terrain.transform.position)+V((x+0.5f)/data.detailWidth*data.size.x,(z+0.5f)/data.detailHeight*data.size.z);
                if(Preserve(p)>0.5f)continue;
                bool clear=RoadDistance(p)<1.1f||MeadowDistance(p)<-2||NearBuilding(p,1.5f)||p.x<-95||p.x>94||p.y<-93||p.y>95;
                detail[z,x]=!clear&&random.NextDouble()<0.055?1:0;
            }
            data.SetDetailLayer(0,0,layer,detail);
        }
    }

    private static GameObject Place(string path,Transform parent,Vector2 p,float size,float yaw,bool solid,string palette="")
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new InvalidOperationException("Missing model "+path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);go.transform.position=Vector3.zero;go.transform.rotation=source.transform.rotation;
        Bounds b=BoundsOf(go);bool natural=path.StartsWith(Nature)||path.EndsWith("ForestOak.prefab");
        if(!natural)go.transform.localScale*=size/Mathf.Max(0.01f,b.size.y);else go.transform.localScale*=size;
        go.transform.rotation=Quaternion.Euler(0,yaw,0)*source.transform.rotation;b=BoundsOf(go);
        go.transform.position+=Ground(p)-new Vector3(b.center.x,b.min.y,b.center.z);
        SetLayer(go,7);
        if(!natural)foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>ConvertMaterial(m,palette)).ToArray();
        foreach(var renderer in go.GetComponentsInChildren<Renderer>(true)){renderer.lightProbeUsage=LightProbeUsage.Off;if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        if(solid)
        {
            foreach(Collider c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
            b=BoundsOf(go);var blocker=new GameObject("BuildingFootprintCollider");blocker.transform.position=new Vector3(b.center.x,b.min.y+b.size.y*0.5f,b.center.z);blocker.transform.SetParent(go.transform,true);blocker.layer=7;
            var box=blocker.AddComponent<BoxCollider>();box.size=new Vector3(b.size.x*0.84f,b.size.y,b.size.z*0.84f);
            footprints.Add(new Bounds(b.center,new Vector3(b.size.x*0.84f,b.size.y,b.size.z*0.84f)));
        }
        else if(!natural)foreach(Collider c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
        return go;
    }
    private static Material ConvertMaterial(Material source,string palette)
    {
        if(source==null)return stone;
        // Per palette caches preserve source texture/color variation while unifying roughness and URP shaders.
        string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string key=Folder+"/Materials/M_"+Safe(source.name)+"_"+guid.Substring(0,Mathf.Min(6,guid.Length))+"_"+palette+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(key);if(existing!=null)return existing;
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=Path.GetFileNameWithoutExtension(key),enableInstancing=true};
        string map=source.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
        if(source.HasProperty(map))mat.SetTexture("_BaseMap",source.GetTexture(map));
        Color tint=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.color:Color.white;
        if(palette=="Castle")tint*=new Color(0.63f,0.65f,0.72f,1);else if(palette=="Boundary")tint*=new Color(0.7f,0.75f,0.62f,1);
        mat.SetColor("_BaseColor",tint);mat.SetFloat("_Smoothness",0.12f);mat.SetFloat("_Metallic",0);
        AssetDatabase.CreateAsset(mat,key);return mat;
    }
    private static void WallLine(Transform parent,Vector2 a,Vector2 b,float height,bool dark)
    {
        Vector2 direction=(b-a).normalized;float length=Vector2.Distance(a,b);int count=Mathf.CeilToInt(length/3.2f);float width=length/count;
        for(int i=0;i<count;i++)
        {
            Vector2 p=Vector2.Lerp(a,b,(i+0.5f)/count);var wall=Place(Kit("wall"),parent,p,height,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg-90,false,dark?"Castle":"Town");
            Bounds bounds=BoundsOf(wall);Vector3 s=wall.transform.localScale;float current=length>0?Mathf.Max(bounds.size.x,bounds.size.z):1;
            // Simple backing collider is exact and keeps decorative seams navigationally closed.
            var backing=Cube("WallBacking",parent,Ground(p)+Vector3.up*height*0.5f,new Vector3(width+0.1f,height,0.55f),dark?darkStone:stone,true);
            backing.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,0);
        }
    }
    private static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collision)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.layer=7;
        if(mat==null)go.GetComponent<Renderer>().enabled=false;else go.GetComponent<Renderer>().sharedMaterial=mat;
        go.GetComponent<Collider>().enabled=collision;return go;
    }
    private static Material FlatMaterial(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.color=color;m.SetFloat("_Smoothness",0.08f);AssetDatabase.CreateAsset(m,Folder+"/Materials/"+name+".mat");return m;}
    private static void AddLight(Transform parent,Vector2 p,Color color,float intensity){var go=Group("LocalWarmLight_NoShadows",parent);go.position=Ground(p)+Vector3.up*2;var light=go.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=7;light.shadows=LightShadows.None;}
    private static void Marker(Transform parent,string name,Vector2 p){var t=Group(name,parent);t.position=Ground(p);}
    private static void Arrival(Transform parent,string name,Vector2 p){Marker(parent,name,p);}
    private static bool NearBuilding(Vector2 p,float margin){foreach(Bounds b in footprints)if(Mathf.Abs(p.x-b.center.x)<b.extents.x+margin&&Mathf.Abs(p.y-b.center.z)<b.extents.z+margin)return true;return false;}
    private static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return new Bounds(go.transform.position,Vector3.one);Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    private static string Safe(string s)=>new string(s.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_').ToArray());
    private static void SetLayer(GameObject go,int layer){foreach(Transform t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
    private static Transform Group(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    private static Vector2 V(float x,float z)=>new Vector2(x,z);
    private static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    private static Vector3 Ground(Vector2 p)=>new Vector3(p.x,terrain.SampleHeight(new Vector3(p.x,0,p.y))+terrain.transform.position.y,p.y);
    private static float Rand(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}

    [MenuItem("Tools/World/Full Map/Finalize Imported Model Axes")]
    public static void FinalizeAxes()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();if(layout==null)throw new InvalidOperationException("Build the map first.");
        if(layout.transform.Find("07_AxisAndFoliageFinish")!=null)throw new InvalidOperationException("Finishing already applied. Later edits will not be overwritten.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Logs/SceneBackups/WorldPrototype_BeforeMapFinish_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        terrain=layout.terrain;var audit=new StringBuilder("Model-axis correction "+DateTime.Now.ToString("s")+"\n");
        foreach(Transform t in layout.GetComponentsInChildren<Transform>(true))
        {
            if(!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))continue;
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            if(!path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);Bounds before=BoundsOf(t.gameObject);Vector2 anchor=XZ(before.center);float height=before.size.y,yaw=t.eulerAngles.y;
            t.position=Vector3.zero;t.rotation=source.transform.rotation;t.localScale=source.transform.localScale;
            Bounds upright=BoundsOf(t.gameObject);t.localScale*=height/Mathf.Max(0.01f,upright.size.y);t.rotation=Quaternion.Euler(0,yaw,0)*source.transform.rotation;
            Bounds after=BoundsOf(t.gameObject);t.position+=Ground(anchor)-new Vector3(after.center.x,after.min.y,after.center.z);after=BoundsOf(t.gameObject);
            Transform blocker=t.Find("BuildingFootprintCollider");
            if(blocker!=null){blocker.SetParent(null,true);blocker.localScale=Vector3.one;blocker.rotation=Quaternion.identity;blocker.position=after.center;blocker.GetComponent<BoxCollider>().size=new Vector3(after.size.x*0.84f,after.size.y,after.size.z*0.84f);blocker.SetParent(t,true);}
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);audit.AppendLine($"{t.name}: {before.size:F2} -> {after.size:F2}, source axis {source.transform.eulerAngles:F1}");
        }
        Transform finish=Group("07_AxisAndFoliageFinish",layout.transform);
        random=new System.Random(26102027);footprints.Clear();
        foreach(BoxCollider box in layout.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="BuildingFootprintCollider"))footprints.Add(box.bounds);
        curves.Clear();widths.Clear();foreach(var route in layout.routes){curves.Add(Smooth(route.points.Select(t=>XZ(t.position)).ToArray()));widths.Add(route.width);}
        // Recreate the distance lookup without adding or moving the saved waypoint groups.
        RasterCurrentRoads();
        var existingTrees=layout.transform.Find("02_Forest").GetComponentsInChildren<Transform>().Where(t=>t.GetComponent<LODGroup>()!=null).Select(t=>XZ(t.position)).ToList();
        int count=0;for(int i=0;i<1600;i++)
        {
            Vector2 p=V(Rand(-91,92),Rand(-90,92));float d=RoadDistance(p);
            if(d<2.2f||d>6.5f||Preserve(p)>0.01f||MeadowDistance(p)<1||NearBuilding(p,4)||existingTrees.Any(t=>Vector2.Distance(t,p)<5.2f))continue;
            Place(Folder+"/Prefabs/ForestOak.prefab",finish,p,Rand(0.52f,0.72f),Rand(0,360),false);existingTrees.Add(p);count++;
        }
        audit.AppendLine("Added road-shoulder trees: "+count);File.WriteAllText("Logs/WindingMapAxisFix.txt",audit.ToString());
        // Expand the existing forest's local grading over the new land without changing its profile or global lighting.
        var look=Group("ForestLook_Extension",finish);look.position=new Vector3(0,12,0);var trigger=look.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(202,80,202);
        var volume=look.gameObject.AddComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=false;volume.priority=9;volume.blendDistance=3;volume.sharedProfile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Project/World/ForestDemo/ForestLook.asset");
        Physics.SyncTransforms();var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Folder+"/WindingForestNavigation_Final.asset"));EditorUtility.SetDirty(surface);
        Validate();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Capture();Focus();
    }
    private static void RasterCurrentRoads()
    {
        roadField=new float[420,420];for(int z=0;z<420;z++)for(int x=0;x<420;x++)roadField[z,x]=1000;
        for(int r=0;r<curves.Count;r++)foreach(Vector2 p in curves[r]){int cx=Mathf.RoundToInt((p.x+104)*2),cz=Mathf.RoundToInt((p.y+104)*2);for(int z=Mathf.Max(0,cz-28);z<=Mathf.Min(419,cz+28);z++)for(int x=Mathf.Max(0,cx-28);x<=Mathf.Min(419,cx+28);x++)roadField[z,x]=Mathf.Min(roadField[z,x],Vector2.Distance(p,V(x*0.5f-104,z*0.5f-104))-widths[r]*0.5f);}
    }

    [MenuItem("Tools/World/Full Map/Add Castle Keep Silhouette")]
    public static void FinishCastle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();if(layout==null||layout.castle.Find("Keep_ExteriorOnly")!=null)throw new InvalidOperationException("Map missing or keep already finished.");
        terrain=layout.terrain;darkStone=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/M_DemonStone.mat");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Logs/SceneBackups/WorldPrototype_BeforeCastleKeep_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        Transform keep=Group("Keep_ExteriorOnly",layout.castle);float ground=Ground(V(62,79)).y;
        Cube("KeepHall",keep,new Vector3(62,ground+4.5f,79),new Vector3(16,9,10),darkStone,true);
        for(int i=0;i<9;i++)foreach(float z in new[]{73.8f,84.2f})Cube("RoofBattlement",keep,new Vector3(54+i*2,ground+9.55f,z),new Vector3(1.15f,1.1f,1),darkStone,true);
        for(int i=1;i<5;i++)foreach(float x in new[]{54f,70f})Cube("RoofBattlement",keep,new Vector3(x,ground+9.55f,74+i*2),new Vector3(1,1.1f,1.15f),darkStone,true);
        var door=Place(Kit("wall_doorway"),keep,V(62,73.85f),5.6f,0,false,"Castle");door.name="KeepSealedDoorway_FutureInterior";
        foreach(float x in new[]{57f,67f}){var banner=Place(Kit("banner_red"),keep,V(x,73.7f),3.4f,0,false,"Castle");banner.transform.position+=Vector3.up*3;}
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Folder+"/Materials"}))
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(!mat.name.StartsWith("M_Fire_"))continue;
            mat.SetColor("_BaseColor",new Color(1,0.38f,0.06f));mat.SetColor("_EmissionColor",new Color(2,0.5f,0.04f));mat.EnableKeyword("_EMISSION");EditorUtility.SetDirty(mat);
        }
        Physics.SyncTransforms();var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Folder+"/WindingForestNavigation_Keep.asset"));EditorUtility.SetDirty(surface);
        Validate();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Capture();Focus();
    }

    [MenuItem("Tools/World/Full Map/Validate Full Map")]
    public static void Validate()
    {
        var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();var report=new StringBuilder("Full map validation "+DateTime.Now.ToString("s")+"\n");bool passed=true;
        Action<bool,string> check=(ok,text)=>{passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text);};check(layout!=null,"Map layout exists");
        if(layout!=null)
        {
            layout.arrivals=layout.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Arrival")).ToArray();
            check(layout.village.position.x<0&&layout.village.position.z>0&&layout.city.position.x>0&&layout.city.position.z<0&&layout.castle.position.x>0&&layout.castle.position.z>0,"NW village / SE city / NE castle layout");
            check(layout.sanctuary!=null&&Vector3.Distance(UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>().Position,new Vector3(-82.63f,5.778f,-82.18f))<0.1f,"Original southwest sanctuary and player spawn retained");
            check(layout.terrain.terrainData==layout.terrain.GetComponent<TerrainCollider>().terrainData&&AssetDatabase.GetAssetPath(layout.terrain.terrainData).StartsWith(Folder),"New terrain copy is shared with physics; original asset retained");
            check(layout.routes.Length==7&&layout.routes.All(r=>r.points.Length>=5),"Seven winding main/shortcut routes with editable waypoints");
            check(layout.transform.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
            foreach(Material m in layout.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())check(m.shader!=null&&m.shader.isSupported&&!ShaderUtil.ShaderHasError(m.shader),"Shader works: "+m.name);
            bool sample=NavMesh.SamplePosition(UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>().Position,out NavMeshHit start,2,NavMesh.AllAreas);check(sample,"Sanctuary remains navigable");
            foreach(Transform arrival in layout.arrivals)
            {
                var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(arrival.position,out NavMeshHit end,3,NavMesh.AllAreas)&&sample&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                check(ok,"Complete path from sanctuary to "+arrival.name);
            }
            foreach(var route in layout.routes)
            {
                int blocked=0;foreach(Vector2 p in Smooth(route.points.Select(t=>XZ(t.position)).ToArray()).Where((p,i)=>i%3==0))
                {
                    Vector3 world=new Vector3(p.x,layout.terrain.SampleHeight(new Vector3(p.x,0,p.y))+layout.terrain.transform.position.y,p.y);
                    if(!NavMesh.SamplePosition(world,out _,2.5f,NavMesh.AllAreas))blocked++;
                }
                check(blocked==0,route.name+" entire smoothed curve is navigable (blocked="+blocked+")");
            }
            check(layout.GetComponentsInChildren<BoxCollider>().Count(c=>c.name.EndsWith("SafetyWall")&&c.enabled)==4,"Four continuous solid boundary safety walls");
            EditorUtility.SetDirty(layout);
        }
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WindingMapValidation.txt",report.ToString());Debug.Log(report.ToString());
    }
    [MenuItem("Tools/World/Full Map/Focus Full Map")]
    public static void Focus(){var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();if(layout==null)return;Selection.activeGameObject=layout.gameObject;SceneView.lastActiveSceneView?.LookAt(new Vector3(0,4,0),Quaternion.Euler(70,0,0),115,false,true);}
    [MenuItem("Tools/World/Full Map/Rebake Full Map Navigation")]
    public static void Rebake()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();if(layout==null)throw new InvalidOperationException("Open the completed WorldPrototype map first.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");
        if(!EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/WorldPrototype_BeforeMapRebake_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity",true))throw new IOException("Scene backup failed.");
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();Undo.RecordObject(surface,"Rebake edited full map");surface.BuildNavMesh();
        if(surface.navMeshData==null)throw new InvalidOperationException("Navigation build failed.");
        AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Folder+"/WindingForestNavigation_Rebake.asset"));EditorUtility.SetDirty(surface);
        Validate();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    [MenuItem("Tools/World/Full Map/Capture Full Map")]
    public static void Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var cam=new GameObject("Temporary map capture").AddComponent<Camera>();var rt=new RenderTexture(1600,1600,24);var tex=new Texture2D(1600,1600,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{cam.transform.position=new Vector3(0,240,-75);cam.transform.rotation=Quaternion.Euler(72,0,0);cam.orthographic=true;cam.orthographicSize=111;cam.farClipPlane=500;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1600),0,0);tex.Apply();File.WriteAllBytes("Logs/WindingForestMap_Overview.png",tex.EncodeToPNG());}
        finally{RenderTexture.active=old;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(cam.gameObject);}
    }
}
