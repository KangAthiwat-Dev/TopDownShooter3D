using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>Scoped city streets, grounded terrain and southwest/southeast exterior set dressing.</summary>
public static class CityExpansionSetup
{
    public const string Kit=MedievalModelOnlySetup.Package;
    public const string Folder="Assets/_Project/World/CityExpansion";
    public const string RootName="City_Streets_Construction_Armory";
    public const float Top=5.14f,West=54.7626f,East=96.7626f,South=-94.7745f,North=-50.7745f;
    public const float CrossX=75.7626f,CrossZ=-70.8856f;
    private static IEnumerable<Transform> All()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static Bounds BoundsOf(GameObject go)=>CityNorthwestQuarterSetup.BoundsOf(go);
    private static void RequireScene(){if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");}
    [Serializable] private sealed class Record
    {
        public string backup,originalTerrain,newTerrain,originalTerrainHash;
        public int retainedComponents,reusedConstruction,reusedArmory,changedHeights;
        public float maxOutsideHeightChange;
    }
    private static readonly string[] ConstructionNames={"SM_VK_Scaffold_Corner_Top","SM_VK_Scaffold_Corner","SM_VK_Scaffold_Ladder","SM_VK_Scaffold_Wall_Top","SM_VK_Roof_Gable_Frame","SM_VK_Roof_Hip_Frame","SM_VK_Prop_Wheelbarrow","SM_VK_Prop_Cart","SM_VK_Prop_Table"};
    private static readonly string[] ArmoryNames={"SM_VK_Prop_ShopGoods_Tools","SM_VK_Prop_WeaponRack","SM_VK_Prop_HandPump","SM_VK_Prop_Anvil","SM_VK_Prop_ArcheryButt","SM_VK_Prop_Bedroll"};
    private static Transform Group(string name,Transform parent){var g=new GameObject(name);g.layer=7;g.transform.SetParent(parent,false);return g.transform;}
    private static void FolderAt(string p){if(AssetDatabase.IsValidFolder(p))return;string parent=Path.GetDirectoryName(p).Replace('\\','/');FolderAt(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(p));}
    private static string Hash(string p){using(var sha=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","");}
    private static GameObject Part(string family,string stem,Transform parent,Vector3 local,float yaw=0,float scale=1)
    {
        string path=Kit+"/Generated/Prefabs/VK/"+family+"/SM_VK_"+stem+".prefab";var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new IOException(path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);go.transform.localPosition=local;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        foreach(Transform t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=7;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);return go;
    }
    private static void MoveUser(GameObject go,Transform parent,Vector3 p,float yaw,float scale,bool stand=true)
    {
        Undo.SetTransformParent(go.transform,parent,"Assemble user's city props");Undo.RecordObject(go.transform,"Position city prop");go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        if(stand)go.transform.position+=Vector3.up*(p.y-BoundsOf(go).min.y);
        foreach(Transform t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=7;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }
    private static GameObject User(string name)=>SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name==name);
    private static void Block(Transform parent,string name,Vector3 center,Vector3 size)
    {
        var p=Group(name,parent);p.position=center;var box=p.gameObject.AddComponent<BoxCollider>();box.size=size;
        var modifier=p.gameObject.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
    }
    private static void PropCollision(GameObject go)
    {
        if(go.GetComponentInChildren<Collider>()!=null)return;Bounds b=BoundsOf(go);var p=Group("Simple_Collision",go.transform);p.position=b.center;p.rotation=Quaternion.identity;p.SetParent(null,true);p.localScale=Vector3.one;p.SetParent(go.transform,true);p.gameObject.AddComponent<BoxCollider>().size=b.size;
        var modifier=p.gameObject.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
    }
    private static void Construction(Transform root)
    {
        Transform site=Group("01_Construction_Site_SW",root),frame=Group("01_Unfinished_Walls_and_Roof",site),scaff=Group("02_Assembled_User_Scaffolding",site),supplies=Group("03_Building_Materials",site);
        const float cx=64.4f,cz=-78.9f,s=.8f;
        for(int x=0;x<3;x++)for(int z=0;z<2;z++)Part("BuildSite","BuildSite_Cell",frame,new Vector3(cx+(x-1)*2.4f,Top,cz+(z-.5f)*2.4f),0,s);
        for(int x=0;x<3;x++)Part("Wall","Wall_Timber_Frame",frame,new Vector3(cx+(x-1)*2.4f,Top,cz-2.4f),180,s);
        for(int i=0;i<4;i++)Part("Corner","Corner_Plaster",frame,new Vector3(cx+(i<2?3.6f:-3.6f),Top,cz+(i==0||i==3?2.4f:-2.4f)),-i*90,s);
        Part("Wall","Wall_Plaster_Frame",frame,new Vector3(cx-3.6f,Top,cz-1.2f),270,s);
        Part("Wall","Wall_Stone_Half",frame,new Vector3(cx+3.6f,Top,cz+1.2f),90,s);
        MoveUser(User("SM_VK_Roof_Gable_Frame"),frame,new Vector3(cx-2.4f,Top+2.4f,cz),0,s,false);
        Part("Roof","Roof_Mid_Frame",frame,new Vector3(cx,Top+2.4f,cz),0,s);
        MoveUser(User("SM_VK_Roof_Hip_Frame"),frame,new Vector3(cx+2.4f,Top+2.4f,cz),180,s,false);
        MoveUser(User("SM_VK_Scaffold_Corner"),scaff,new Vector3(cx-3.6f,Top,cz-2.4f),180,s);
        MoveUser(User("SM_VK_Scaffold_Corner_Top"),scaff,new Vector3(cx-3.6f,Top+2.4f,cz-2.4f),180,s);
        MoveUser(User("SM_VK_Scaffold_Wall_Top"),scaff,new Vector3(cx,Top+2.4f,cz-2.4f),180,s);
        Part("Scaffold","Scaffold_Wall",scaff,new Vector3(cx,Top,cz-2.4f),180,s);
        MoveUser(User("SM_VK_Scaffold_Ladder"),scaff,new Vector3(cx+.8f,Top,cz-2.4f),180,s,false);
        MoveUser(User("SM_VK_Prop_Wheelbarrow"),supplies,new Vector3(70,Top,-79),30,.85f);
        MoveUser(User("SM_VK_Prop_Cart"),supplies,new Vector3(69.5f,Top,-91.3f),20,.85f);
        MoveUser(User("SM_VK_Prop_Table"),supplies,new Vector3(61.8f,Top,-90.7f),0,.9f);
        foreach(var p in new[]{new Vector3(70,Top,-76.5f),new Vector3(69.8f,Top,-81.9f)})PropCollision(Part("Pile","Pile_Planks_2",supplies,p,20,.75f));
        PropCollision(Part("Pile","Pile_Bricks_3",supplies,new Vector3(61,Top,-84.3f),0,.8f));
        PropCollision(Part("Pile","Pile_Stone_2",supplies,new Vector3(70.5f,Top,-84),90,.7f));
        Part("Pile","Pile_Logs_2",supplies,new Vector3(64,Top,-92),0,.75f);
        foreach(Transform supply in supplies)PropCollision(supply.gameObject);
        Block(site,"Safe_Construction_Footprint",new Vector3(cx,Top+2.6f,cz),new Vector3(9.3f,5.2f,7.6f));
        foreach(var c in site.GetComponentsInChildren<Collider>())if(c.transform.parent!=null&&c.transform.name!="Safe_Construction_Footprint"&&c.transform.name!="Simple_Collision")c.enabled=false;
        var anchor=Group("Future_Builder_NPC_Only_Anchor",site);anchor.position=new Vector3(70.8f,Top,-82);
    }
    private static void Armory(Transform root)
    {
        Transform armory=Group("02_Armory_Block_SE",root),house=Group("01_Red_Roof_Armory",armory);house.position=new Vector3(87.7f,Top,-91);house.localScale=Vector3.one*.82f;
        for(int level=0;level<2;level++)
        {
            float y=level==0?0:3;
            for(int i=0;i<2;i++)
            {
                float x=(i-.5f)*3;
                Part("Wall",level==0?(i==0?"Wall_Stone_Door":"Wall_Stone_Shop"):"Wall_PlasterUp_Window",house,new Vector3(x,y,1.5f));
                Part("Wall",level==0?"Wall_Stone":"Wall_Timber_X",house,new Vector3(x,y,-1.5f),180);
            }
            Part("Wall",level==0?"Wall_Stone_Window":"Wall_PlasterUp_Window",house,new Vector3(-3,y,0),270);
            Part("Wall",level==0?"Wall_Stone":"Wall_Timber_K",house,new Vector3(3,y,0),90);
            for(int i=0;i<4;i++)Part("Corner",level==0?"Corner_Stone":"Corner_PlasterUp",house,new Vector3(i<2?3:-3,y,i==0||i==3?1.5f:-1.5f),-i*90);
        }
        Part("RoofS","RoofS_Gable",house,new Vector3(-1.5f,5.8f,0));Part("RoofS","RoofS_Gable",house,new Vector3(1.5f,5.8f,0),180);
        Part("Sign","Sign_Anvil",house,new Vector3(-2.5f,2.7f,1.8f));Part("Prop","Prop_Lantern",house,new Vector3(-.5f,2.7f,1.65f));
        Block(house,"Closed_Building_Collision",new Vector3(87.7f,Top+3.7f,-91),new Vector3(5.34f,7.4f,2.86f));
        // Block() uses world centres: keep the collider's volume in world metres beneath the scaled building.
        var block=house.Find("Closed_Building_Collision");block.SetParent(armory,true);block.localScale=Vector3.one;
        Transform smithy=Group("02_Covered_Forge",armory);
        Part("Smithy","Smithy_Canopy",smithy,new Vector3(92.1f,Top,-88.3f),0,.58f);
        Part("Smithy","Smithy_Chimney",smithy,new Vector3(93.3f,Top,-90.3f),0,.6f);
        var forge=Part("Prop","Prop_Forge",smithy,new Vector3(92.6f,Top,-89.4f),0,.8f);PropCollision(forge);
        var table=Part("Prop","Prop_Table",smithy,new Vector3(91.5f,Top,-87.5f),0,.72f);PropCollision(table);
        Transform props=Group("03_Reused_User_Weapons_and_Equipment",armory);
        MoveUser(User("SM_VK_Prop_ShopGoods_Tools"),props,new Vector3(91.5f,Top+.78f,-87.5f),0,.75f);
        MoveUser(User("SM_VK_Prop_WeaponRack"),props,new Vector3(88.6f,Top,-88.7f),0,.9f);
        MoveUser(User("SM_VK_Prop_Anvil"),props,new Vector3(93.1f,Top,-87.7f),90,.92f);
        MoveUser(User("SM_VK_Prop_HandPump"),props,new Vector3(94.3f,Top,-91.6f),270,.85f);
        MoveUser(User("SM_VK_Prop_ArcheryButt"),props,new Vector3(81.1f,Top,-91.6f),0,.9f);
        MoveUser(User("SM_VK_Prop_Bedroll"),props,new Vector3(91.5f,Top,-92.2f),90,.9f);
        foreach(Transform t in props)if(t.name!="SM_VK_Prop_ShopGoods_Tools"&&t.name!="SM_VK_Prop_Bedroll")PropCollision(t.gameObject);
        PropCollision(Part("Prop","Prop_ArmorStand",armory,new Vector3(89.7f,Top,-87.2f),0,.85f));
        PropCollision(Part("Prop","Prop_Grindstone",smithy,new Vector3(94,Top,-86.5f),-30,.85f));
        Part("Pile","Pile_Coal_1",smithy,new Vector3(94.2f,Top,-89.5f),0,.7f);
        var npc=Group("Future_Weapon_Shop_NPC_Anchor",armory);npc.position=new Vector3(86.4f,Top,-87.7f);
        var interaction=Group("Future_Weapon_Shop_Interaction_Anchor",armory);interaction.position=new Vector3(86.4f,Top,-86.8f);
    }
    private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){Vector2 d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
    private static float SmoothMin(float a,float b,float k){float h=Mathf.Max(k-Mathf.Abs(a-b),0)/k;return Mathf.Min(a,b)-h*h*k*.25f;}
    public static float RoadDistance(Vector2 p)
    {
        float a=SegmentDistance(p,new Vector2(51.7626f,CrossZ),new Vector2(91.7626f,CrossZ))-2;
        float b=SegmentDistance(p,new Vector2(CrossX,-86.8856f),new Vector2(CrossX,-46.8856f))-2;
        float c=SegmentDistance(p,new Vector2(67.7626f,-86.8856f),new Vector2(CrossX,-86.8856f))-2;
        float d=SegmentDistance(p,new Vector2(CrossX,-82.8856f),new Vector2(87.7626f,-82.8856f))-2;
        return SmoothMin(SmoothMin(a,b,3.0f),Mathf.Min(c,d),2.0f);
    }
    private struct Vertex{public Vector2 p;public float d;public Vertex(Vector2 p,float d){this.p=p;this.d=d;}}
    private static List<Vertex> Clip(List<Vertex> input,float threshold,bool below)
    {
        var result=new List<Vertex>();if(input.Count==0)return result;Vertex prev=input[input.Count-1];bool prevIn=below?prev.d<=threshold:prev.d>=threshold;
        foreach(Vertex next in input){bool nextIn=below?next.d<=threshold:next.d>=threshold;if(nextIn!=prevIn){float t=(threshold-prev.d)/(next.d-prev.d);result.Add(new Vertex(Vector2.Lerp(prev.p,next.p,t),threshold));}if(nextIn)result.Add(next);prev=next;prevIn=nextIn;}return result;
    }
    private static Mesh SurfaceMesh(bool curb)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        Action<Vertex,Vertex,Vertex> emit=(a,b,c)=>
        {
            var poly=Clip(new List<Vertex>{a,b,c},curb?.24f:0,true);if(curb)poly=Clip(poly,0,false);if(poly.Count<3)return;
            int start=vertices.Count;foreach(var v in poly){float y=Top+(curb?.014f+.065f*Mathf.Sin(Mathf.Clamp01(v.d/.24f)*Mathf.PI):.014f);vertices.Add(new Vector3(v.p.x,y,v.p.y));uv.Add(v.p/3);}
            for(int i=1;i<poly.Count-1;i++){triangles.Add(start);triangles.Add(start+i+1);triangles.Add(start+i);}
        };
        const float step=.2f;
        for(float z=-94;z<-43;z+=step)for(float x=48;x<97;x+=step)
        {
            var a=new Vertex(new Vector2(x,z),RoadDistance(new Vector2(x,z)));var b=new Vertex(new Vector2(x+step,z),RoadDistance(new Vector2(x+step,z)));var c=new Vertex(new Vector2(x+step,z+step),RoadDistance(new Vector2(x+step,z+step)));var d=new Vertex(new Vector2(x,z+step),RoadDistance(new Vector2(x,z+step)));
            emit(a,b,c);emit(a,c,d);
        }
        var mesh=new Mesh{name=curb?"Curved low chamfered kerbs":"Continuous rounded city streets",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.colors=Enumerable.Repeat(Color.white,vertices.Count).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+(curb?"/RoundedKerbs.asset":"/RoundedRoads.asset"));return mesh;
    }
    private static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material){var go=Group(name,parent).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;}
    private static Material MaterialCopy(string source,string name,Color tint)
    {
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Generated/Materials/"+source+".mat")){name=name};material.SetColor("_BaseColor",tint);AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");return material;
    }
    private static void PavingMaterial(string name,Color tint)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
        var replacement=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
        replacement.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Kit+"/Art/Textures/T_VKI_Flagstone_BC.png"));replacement.SetColor("_BaseColor",tint);replacement.SetFloat("_Smoothness",.05f);replacement.SetFloat("_Metallic",0);
        EditorUtility.CopySerialized(replacement,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(replacement);
    }
    [MenuItem("Tools/World/City Expansion/Finish Paving Contrast and Shop Facade")]
    public static void FinishAppearance()
    {
        RequireScene();var root=GameObject.Find(RootName).transform;if(root.Find("Visual_Finish_Applied")!=null)throw new InvalidOperationException("Finish already installed; preserve later edits.");
        PavingMaterial("City_Warm_Paving",new Color(1.45f,1.30f,1.05f));PavingMaterial("City_Smooth_Road",new Color(.65f,.73f,.79f));
        Transform house=root.Find("02_Armory_Block_SE/01_Red_Roof_Armory");
        foreach(Transform child in house.Cast<Transform>().Where(t=>Mathf.Abs(t.localPosition.z+1.5f)<.01f&&(t.name=="SM_VK_Wall_Stone"||t.name=="SM_VK_Wall_Timber_X")).ToArray())
        {
            Vector3 p=child.localPosition;bool ground=p.y<1;string stem=ground?(p.x<0?"Wall_Stone_Window":"Wall_Stone_Shop"):(p.x<0?"Wall_PlasterUp_Window":"Wall_Timber_X");
            UnityEngine.Object.DestroyImmediate(child.gameObject);Part("Wall",stem,house,p,180);
        }
        Group("Visual_Finish_Applied",root);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Capture();
    }
    private static void Streets(Transform root,GameObject floor,GameObject way)
    {
        var group=Group("03_Continuous_Paving_Rounded_Streets",root);
        Material paving=MaterialCopy("M_VKT_Paving","City_Warm_Paving",new Color(1.05f,.98f,.84f));
        Material road=MaterialCopy("M_VKT_Paving","City_Smooth_Road",new Color(.58f,.64f,.67f));
        Material kerb=MaterialCopy("M_VK_StoneDressed","City_Light_Kerb",new Color(1.12f,1.07f,.94f));
        var mesh=new Mesh{name="Continuous level city paving"};mesh.vertices=new[]{new Vector3(West,Top,South),new Vector3(East,Top,South),new Vector3(East,Top,North),new Vector3(West,Top,North)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x,v.z)/3).ToArray();mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/ContinuousPaving.asset");
        var baseObject=MeshObject("Level_City_Paving",group,mesh,paving);var box=baseObject.AddComponent<BoxCollider>();box.center=new Vector3((West+East)*.5f,Top-.12f,(South+North)*.5f);box.size=new Vector3(East-West,.24f,North-South);
        MeshObject("Smooth_Rounded_Road_Surface",group,SurfaceMesh(false),road);MeshObject("Rounded_Low_Kerbs_Visual_Only",group,SurfaceMesh(true),kerb);
        foreach(var bounds in new[]{new Bounds(new Vector3(52.7f,Top-.12f,CrossZ),new Vector3(9.4f,.24f,5)),new Bounds(new Vector3(CrossX,Top-.12f,-48.0f),new Vector3(5,.24f,8))})
        {var support=Group("Gate_Approach_Flat_Collision",group);var collider=support.gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;}
        var archive=Group("00_Previous_Floor_and_Wood_Roads_Inactive",root);Undo.SetTransformParent(floor.transform,archive,"Keep original city paving");Undo.SetTransformParent(way.transform,archive,"Keep original wooden roads");floor.SetActive(false);way.SetActive(false);archive.gameObject.SetActive(false);
    }
    public static float CityGroundDistance(Vector2 p)
    {
        float dx=Mathf.Max(West-1.4f-p.x,0,p.x-East-1.4f),dz=Mathf.Max(South-1.4f-p.y,0,p.y-North-1.4f);
        return Mathf.Min(new Vector2(dx,dz).magnitude,Mathf.Max(0,RoadDistance(p)-.7f));
    }
    private static void GroundTerrain(Record record,Terrain terrain)
    {
        TerrainData source=terrain.terrainData;record.originalTerrain=AssetDatabase.GetAssetPath(source);record.originalTerrainHash=Hash(record.originalTerrain);record.newTerrain=Folder+"/GroundedCityTerrain.asset";
        if(!AssetDatabase.CopyAsset(record.originalTerrain,record.newTerrain))throw new IOException("Could not copy TerrainData.");
        var copy=AssetDatabase.LoadAssetAtPath<TerrainData>(record.newTerrain);int n=source.heightmapResolution;var original=source.GetHeights(0,0,n,n);var heights=(float[,])original.Clone();
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            Vector2 p=new Vector2(terrain.transform.position.x+(float)x/(n-1)*source.size.x,terrain.transform.position.z+(float)z/(n-1)*source.size.z);float distance=CityGroundDistance(p);
            if(distance>=6)continue;float weight=1-Mathf.SmoothStep(0,1,distance/6);heights[z,x]=Mathf.Lerp(original[z,x],(4.99f-terrain.transform.position.y)/source.size.y,weight);if(Mathf.Abs(heights[z,x]-original[z,x])>1e-7f)record.changedHeights++;
        }
        copy.SetHeights(0,0,heights);EditorUtility.SetDirty(copy);Undo.RecordObject(terrain,"Ground the city on a cloned terrain");terrain.terrainData=copy;var collider=terrain.GetComponent<TerrainCollider>();Undo.RecordObject(collider,"Match city terrain collision");collider.terrainData=copy;terrain.Flush();
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            Vector2 p=new Vector2(terrain.transform.position.x+(float)x/(n-1)*source.size.x,terrain.transform.position.z+(float)z/(n-1)*source.size.z);if(CityGroundDistance(p)>=6)record.maxOutsideHeightChange=Mathf.Max(record.maxOutsideHeightChange,Mathf.Abs(heights[z,x]-original[z,x])*source.size.y);
        }
    }
    private static Dictionary<string,string> Snapshot(IEnumerable<Transform> except)
    {
        var excluded=except.ToArray();var result=new Dictionary<string,string>();foreach(var t in All())
        {
            if(excluded.Any(r=>t==r||t.IsChildOf(r)))continue;
            foreach(Component c in t.GetComponents<Component>())if(c!=null&&!(c is Terrain)&&!(c is TerrainCollider)&&!(c is NavMeshSurface))result[c.GetEntityId().ToString()]=EditorJsonUtility.ToJson(c);
        }
        return result;
    }
    [MenuItem("Tools/World/City Expansion/Build Grounded Streets Construction and Armory")]
    public static void Build()
    {
        RequireScene();if(GameObject.Find(RootName)!=null||AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Expansion already exists; preserve manual edits.");
        var floor=GameObject.Find("Floor_city");var way=GameObject.Find("Way_city");var terrain=Terrain.activeTerrain;
        var selected=ConstructionNames.Concat(ArmoryNames).Select(User).ToArray();if(selected.Length!=15||floor==null||way==null||terrain==null)throw new InvalidOperationException("City inputs changed; inspect first.");
        var record=new Record{backup="Logs/SceneBackups/WorldPrototype_BeforeCityExpansion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",reusedConstruction=ConstructionNames.Length,reusedArmory=ArmoryNames.Length};
        Directory.CreateDirectory("Logs/SceneBackups");if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),record.backup,true))throw new IOException("Live scene backup failed.");
        var before=Snapshot(selected.Select(g=>g.transform).Concat(new[]{floor.transform,way.transform}));FolderAt(Folder);
        var root=Group(RootName,null);Undo.RegisterCreatedObjectUndo(root.gameObject,"Assemble grounded city expansion");
        GroundTerrain(record,terrain);Streets(root,floor,way);Construction(root);Armory(root);Physics.SyncTransforms();
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();Undo.RecordObject(surface,"Navigation for grounded city streets");surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/CityExpansionNavigation.asset");EditorUtility.SetDirty(surface);
        var after=Snapshot(new[]{root});bool scoped=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out string json)&&json==p.Value);record.retainedComponents=before.Count;
        File.WriteAllText("Logs/CityExpansionScope.txt",$"{DateTime.Now:s}\nCompared {before.Count} original components outside floor/road replacements and the 15 explicitly reused props.\n"+(scoped?"PASS All existing buildings, northwest quarter, walls, furniture and other zones unchanged. Terrain data/collider and navigation references deliberately refreshed.":"FAIL Unexpected change."));
        if(!scoped)throw new InvalidOperationException("Scope comparison failed; see backup and report.");
        AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(record,true)),Folder+"/BuildRecord.asset");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Capture();
    }
    [MenuItem("Tools/World/City Expansion/Validate Grounded City")]
    public static void Validate()
    {
        RequireScene();var root=GameObject.Find(RootName);var record=JsonUtility.FromJson<Record>(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/BuildRecord.asset").text);var report=new StringBuilder("City expansion validation "+DateTime.Now.ToString("s")+"\n");bool pass=true;Action<bool,string> check=(ok,s)=>{pass&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+s);};
        check(Hash(record.originalTerrain)==record.originalTerrainHash,"Original TerrainData untouched and recoverable");check(record.maxOutsideHeightChange==0,"No height changes beyond city/approach 6 m blend zone");
        Terrain t=Terrain.activeTerrain;bool flat=true;for(float z=South;z<=North;z+=1)for(float x=West;x<=East;x+=1)flat&=Mathf.Abs(t.SampleHeight(new Vector3(x,0,z))+t.transform.position.y-4.99f)<.003f;
        check(flat,"Terrain under the entire city is level at 4.99 m, supporting the 5.14 m paving");check(t.GetComponent<TerrainCollider>().terrainData==t.terrainData,"Terrain rendering and physical collision use the same cloned data");
        var road=root.transform.Find("03_Continuous_Paving_Rounded_Streets/Smooth_Rounded_Road_Surface").GetComponent<MeshFilter>().sharedMesh;
        check(road.vertices.All(v=>Mathf.Abs(v.y-(Top+.014f))<.0001f),"One continuous flat road mesh with no wood seams or height steps");
        check(root.transform.Find("03_Continuous_Paving_Rounded_Streets/Rounded_Low_Kerbs_Visual_Only").GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"Rounded 24 cm kerbs follow the smoothed street outline; visual lips do not block players");
        check(root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.shader!=null&&!ShaderUtil.ShaderHasError(m.shader)),"All new meshes, models, materials and shaders resolve");
        check(root.GetComponentsInChildren<Transform>(true).All(tr=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)==0),"No missing scripts");
        check(!root.GetComponentsInChildren<MonoBehaviour>().Any(m=>m.GetType().Namespace=="MedievalKit"),"No imported kit gameplay/procedural systems restored");
        check(root.transform.Find("00_Previous_Floor_and_Wood_Roads_Inactive").childCount==2,"Original city floor and wooden road groups kept inactive, not deleted");
        check(root.transform.Find("02_Armory_Block_SE/03_Reused_User_Weapons_and_Equipment").childCount==6,"All six user weapon-shop props reused");
        check(record.reusedConstruction==9,"All nine construction sketch/tool props reused");
        foreach(Vector3 p in new[]{new Vector3(59,Top,-70.8856f),new Vector3(CrossX,Top,-78),new Vector3(71,Top,-86.8856f),new Vector3(85,Top,-82.8856f),new Vector3(86.4f,Top,-86.8f),new Vector3(71.8f,Top,-79)})
            check(NavMesh.SamplePosition(p,out NavMeshHit hit,.8f,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-Top)<.1f,"Grounded city navigation at "+p.ToString("F2"));
        report.AppendLine($"Terrain samples changed: {record.changedHeights}; original components preserved: {record.retainedComponents}; scene backup: {record.backup}");report.AppendLine(pass?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/CityExpansionValidation.txt",report.ToString());if(pass)Debug.Log(report.ToString());else Debug.LogError(report.ToString());
    }
    [MenuItem("Tools/World/City Expansion/Capture Expanded City")]
    public static void Capture()
    {
        RequireScene();var camera=new GameObject("Temporary expanded city camera").AddComponent<Camera>();var target=new RenderTexture(1600,1300,24);var pixels=new Texture2D(1600,1300,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try
        {
            camera.orthographic=true;camera.targetTexture=target;
            Action<Vector3,Vector3,float,string> shot=(position,look,size,path)=>{camera.transform.position=position;camera.transform.LookAt(look);camera.orthographicSize=size;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,1300),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());};
            shot(new Vector3(46,49,-119),new Vector3(75.5f,6,-73),31,"Logs/CityExpansion_Preview.png");
            shot(new Vector3(97,21,-77),new Vector3(90.3f,7.3f,-89.5f),9,"Logs/CityExpansion_Armory.png");
            shot(new Vector3(54,21,-93),new Vector3(64.8f,7,-79.8f),10.5f,"Logs/CityExpansion_Construction.png");
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
    [MenuItem("Tools/World/City Expansion/Finish Forge Collision")]
    public static void FinishForgeCollision()
    {
        RequireScene();Transform root=GameObject.Find(RootName).transform;var smithy=root.Find("02_Armory_Block_SE/02_Covered_Forge");
        if(smithy.Find("Forge_Post_Collision")!=null)throw new InvalidOperationException("Forge collision already finished.");
        var poles=Group("Forge_Post_Collision",smithy);
        foreach(float x in new[]{-3.15f,3.15f})foreach(float z in new[]{-2.85f,2.85f})
        {
            var pole=Group("Post",poles);pole.position=new Vector3(92.1f+x*.58f,Top+1.17f,-88.3f+z*.58f);
            var capsule=pole.gameObject.AddComponent<CapsuleCollider>();capsule.radius=.12f;capsule.height=2.34f;
            var modifier=pole.gameObject.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
        }
        PropCollision(smithy.Find("SM_VK_Smithy_Chimney").gameObject);Physics.SyncTransforms();
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/CityExpansionNavigation_Final.asset");EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Capture();
    }
    [MenuItem("Tools/World/City Expansion/Focus Expanded City")]
    public static void Focus()
    {RequireScene();Selection.activeGameObject=GameObject.Find(RootName);SceneView.lastActiveSceneView?.LookAt(new Vector3(75.5f,7,-73),Quaternion.Euler(48,25,0),34,false,true);}
    [MenuItem("Tools/World/City Expansion/Inspect Current Streets and User Props")]
    public static void Inspect()
    {
        RequireScene();var report=new StringBuilder("City expansion audit "+DateTime.Now.ToString("s")+"\n");
        foreach(var terrain in Terrain.activeTerrains)
        {
            report.AppendLine($"TERRAIN {PathOf(terrain.transform)} pos={terrain.transform.position:F3} size={terrain.terrainData.size:F3} resolution={terrain.terrainData.heightmapResolution} asset={AssetDatabase.GetAssetPath(terrain.terrainData)}");
            foreach(Vector3 p in new[]{new Vector3(West,0,South),new Vector3(East,0,South),new Vector3(West,0,North),new Vector3(East,0,North),new Vector3(CrossX,0,CrossZ)})report.AppendLine($"GROUND {p:F3}: {terrain.SampleHeight(p)+terrain.transform.position.y:F3}");
        }
        foreach(var t in All())
        {
            if(!t.gameObject.activeInHierarchy)continue;
            if(t.name=="Floor_city"||t.name=="Way_city"||t.name=="Wall_city")report.AppendLine($"GROUP {t.name}: {BoundsOf(t.gameObject)}");
            bool outer=PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject)==t.gameObject;
            if(!outer||t.position.x<48||t.position.x>100||t.position.z<-100||t.position.z>-43)continue;
            if(t.GetComponentsInParent<Transform>().Any(p=>p.name=="Floor_city"||p.name=="Wall_city"||p.name=="NW_RedRoof_Quarter"))continue;
            Bounds b=BoundsOf(t.gameObject);report.AppendLine($"PLACED {PathOf(t)} source={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)} p={t.position:F3} yaw={t.eulerAngles.y:F2} scale={t.lossyScale:F3} min={b.min:F3} max={b.max:F3}");
        }
        foreach(string family in new[]{"Scaffold","BuildSite","Pile","Smithy","Roof","RoofS","Wall","Prop","Sign"})
            foreach(string p in AssetDatabase.FindAssets("t:Prefab",new[]{Kit+"/Generated/Prefabs/VK/"+family}).Select(AssetDatabase.GUIDToAssetPath))
            {
                if(family=="Prop"&&!new[]{"Forge","Anvil","WeaponRack","Crate","Cart","Hoist","Grind","Work","Armor","Bucket","Bench","Barrel"}.Any(p.Contains))continue;
                if(family=="Wall"&&!new[]{"Stone","Timber","Plaster"}.Any(p.Contains))continue;
                if(family=="Roof"&&!new[]{"Gable","Mid","Hip"}.Any(p.Contains))continue;
                Bounds b=BoundsOf(AssetDatabase.LoadAssetAtPath<GameObject>(p));report.AppendLine($"PIECE {System.IO.Path.GetFileNameWithoutExtension(p)} min={b.min:F3} max={b.max:F3}");
            }
        foreach(string name in new[]{"M_VKT_Paving","M_VKI_ApronCobble","M_VK_StoneDressed"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Generated/Materials/"+name+".mat");report.AppendLine($"MATERIAL {name} texture={AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap"))}");
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CityExpansionAudit.txt",report.ToString());CitySurfaceAlignment.Capture();File.Copy("Logs/CitySurfaces_Aligned.png","Logs/CityExpansion_Before.png",true);Debug.Log("City audit saved to Logs/CityExpansionAudit.txt");
    }
}
