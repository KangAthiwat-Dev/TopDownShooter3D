using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;

/// <summary>Hand-authored, static modular red-roof quarter. Never runs in the player.</summary>
public static class CityNorthwestQuarterSetup
{
    public const string Kit = MedievalModelOnlySetup.Package;
    public const string Folder = "Assets/_Project/World/CityNorthwestQuarter";
    public const string RootName = "NW_RedRoof_Quarter";
    public const float Ground = 5.14f;
    [Serializable] private sealed class Record
    {
        public string backup;
        public int archivedSketchPieces;
        public int preservedComponents;
        public int modularPieces;
        public string scopeAudit;
    }
    private sealed class House
    {
        public string name, ground, plaster, shutter, front;
        public float x, z, scale;
        public int bays, floors;
        public House(string name, float x, float z, float scale, int bays, int floors, string ground, string plaster, string shutter, string front)
        { this.name=name;this.x=x;this.z=z;this.scale=scale;this.bays=bays;this.floors=floors;this.ground=ground;this.plaster=plaster;this.shutter=shutter;this.front=front; }
    }
    private static readonly House[] Houses =
    {
        new House("01_Merchant_Corner",60.2f,-55.6f,.82f,2,2,"Plaster","Cream","Teal","DW"),
        new House("02_Rose_Townhouse",69.4f,-55.6f,.80f,2,2,"Stone","Rose","Natural","WD"),
        new House("03_Bakery_Counter",60.1f,-61.4f,.84f,2,1,"Plaster","White","Teal","DS"),
        new House("04_Apothecary",69.7f,-61.0f,.88f,1,2,"Plaster","Sage","Green","D"),
        new House("05_Craft_Workshop",60.3f,-66.3f,.84f,2,1,"Stone","Cream","Natural","DW"),
        new House("06_Cobblers_House",69.4f,-66.3f,.82f,2,2,"Plaster","Ochre","Teal","WD")
    };
    public static Vector3[] AlleyRoute => new[]
    {
        new Vector3(65.0f,Ground,-69.8f),new Vector3(65.0f,Ground,-64.0f),
        new Vector3(65.0f,Ground,-58.5f),new Vector3(65.0f,Ground,-52.7f)
    };
    public static Vector3[] CrossRoute => new[]
    {
        new Vector3(75.7f,Ground,-58.5f),new Vector3(70f,Ground,-58.5f),
        new Vector3(65f,Ground,-58.5f),new Vector3(57.3f,Ground,-58.5f)
    };
    private static GameObject Piece(string family, string stem, Transform parent, Vector3 position, float yaw=0, Vector3? scale=null)
    {
        string path=Kit+"/Generated/Prefabs/VK/"+family+"/SM_VK_"+stem+".prefab";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(source==null)throw new IOException("Missing modular piece: "+path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
        go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale??Vector3.one;
        foreach(Transform t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=7;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }
    private static void Style(GameObject go, House house)
    {
        Material plaster=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Generated/Materials/"+(house.plaster=="Cream"?"M_VK_Plaster":"M_VK_Plaster_"+house.plaster)+".mat");
        Material shutter=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Generated/Materials/M_VK_Shutter_"+house.shutter+".mat");
        foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                if(mats[i]!=null&&mats[i].name=="M_VK_Plaster"&&plaster!=null)mats[i]=plaster;
                if(mats[i]!=null&&mats[i].name.StartsWith("M_VK_Shutter_",StringComparison.Ordinal)&&shutter!=null)mats[i]=shutter;
            }
            renderer.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
    private static Transform Group(string name,Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=7;return go.transform;
    }
    private static void Wall(House h,Transform parent,Vector3 position,float yaw,char kind,bool upper)
    {
        string stem=upper?(kind=='W'||kind=='D'||kind=='S'?"Wall_PlasterUp_Window":"Wall_Timber_K"):
            "Wall_"+h.ground+(kind=='D'?"_Door":kind=='W'?"_Window":kind=='S'?"_Shop":"");
        Style(Piece("Wall",stem,parent,position,yaw),h);
    }
    private static Transform MakeHouse(House h, Transform parent)
    {
        Transform building=Group(h.name,parent);building.position=new Vector3(h.x,Ground,h.z);building.localScale=Vector3.one*h.scale;
        Transform shell=Group("01_Walls_and_Corners",building),roof=Group("02_Red_Tile_Roof",building),details=Group("03_Facade_Details",building);
        float width=h.bays*3,depth=3;
        for(int level=0;level<h.floors;level++)
        {
            float y=level==0?0:3+(level-1)*2.8f;
            for(int i=0;i<h.bays;i++)
            {
                float x=-width*.5f+1.5f+i*3;
                Wall(h,shell,new Vector3(x,y,depth*.5f),0,h.front[i],level>0);
                Wall(h,shell,new Vector3(x,y,-depth*.5f),180,i%2==0?'W':'.',level>0);
            }
            Wall(h,shell,new Vector3(-width*.5f,y,0),270,'W',level>0);
            Wall(h,shell,new Vector3(width*.5f,y,0),90,'.',level>0);
            string corner=level==0?"Corner_"+h.ground:"Corner_PlasterUp";
            for(int cornerIndex=0;cornerIndex<4;cornerIndex++)
            {
                float x=cornerIndex<2?width*.5f:-width*.5f;
                float z=cornerIndex==0||cornerIndex==3?depth*.5f:-depth*.5f;
                Piece("Corner",corner,shell,new Vector3(x,y,z),-90*cornerIndex);
            }
        }
        float top=3+(h.floors-1)*2.8f;
        if(h.bays==1)Style(Piece("RoofS","RoofS_Single",roof,new Vector3(0,top,0)),h);
        else for(int i=0;i<h.bays;i++)Style(Piece("RoofS",i==0||i==h.bays-1?"RoofS_Gable":"RoofS_Mid",roof,new Vector3(-width*.5f+1.5f+i*3,top,0),i==0?0:180),h);
        if(h.name.StartsWith("01_")||h.name.StartsWith("05_"))
            Piece("Chimney","Chimney_Party",roof,new Vector3(width*.5f-1.0f,top+2.05f,-.45f),0,Vector3.one*.65f);
        int door=h.front.IndexOf('D');float doorX=-width*.5f+1.5f+door*3;
        Piece("Prop","Prop_Lantern",details,new Vector3(doorX+.9f,2.65f,1.62f));
        // Facades face the camera/south. Prefabs use +Z as their exterior side.
        building.rotation=Quaternion.Euler(0,h.name.StartsWith("04_")?90:180,0);
        if(h.name.StartsWith("03_"))Piece("Awning","Awning",details,new Vector3(width*.5f-1.5f,1f,1.5f));
        if(h.name.StartsWith("04_"))Piece("Sign","Sign_Potion",details,new Vector3(-1.12f,2.65f,1.65f));
        if(h.name.StartsWith("05_"))Piece("Sign","Sign_Anvil",details,new Vector3(doorX+1.1f,2.65f,1.65f));
        if(h.name.StartsWith("06_"))Piece("Sign","Sign_Boot",details,new Vector3(doorX-1.05f,2.65f,1.65f));
        if(h.name.StartsWith("01_")||h.name.StartsWith("02_"))
            Piece("Balcony","Balcony",details,new Vector3(doorX,3.3f,1.5f),0,Vector3.one*.86f);
        for(int i=0;i<h.bays;i++)if(h.front[i]=='W')
            Piece("Prop","Prop_FlowerBox_Daisy",details,new Vector3(-width*.5f+1.5f+i*3,.97f,2.02f),0,Vector3.one*.82f);
        if(h.name.StartsWith("03_")||h.name.StartsWith("04_"))
            Piece("Prop","Prop_Planter",details,new Vector3(-width*.5f+.3f,0,2.15f),0,Vector3.one*.78f);
        Transform anchors=Group("04_Future_Shop_Anchors_No_Gameplay",building);
        var npc=Group("NPC_Stand_Point",anchors);npc.localPosition=new Vector3(doorX,0,3.05f);
        var interaction=Group("Shop_Front_Point",anchors);interaction.localPosition=new Vector3(doorX,0,2.45f);
        Transform physics=Group("05_Solid_Exterior_Collision",building);
        var box=physics.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,(top+3.3f)*.5f,0);box.size=new Vector3(width+.48f,top+3.3f,depth+.48f);
        // Exclude the closed, non-enterable roof mass from enemy walkable navigation.
        var modifier=physics.gameObject.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
        foreach(var c in building.GetComponentsInChildren<Collider>(true))if(c!=box)c.enabled=false;
        return building;
    }
    private static Dictionary<string,string> Snapshot(IEnumerable<Transform> excluded)
    {
        var roots=excluded.ToArray();var result=new Dictionary<string,string>();
        foreach(Transform t in SceneTransforms())
        {
            if(roots.Any(r=>t==r||t.IsChildOf(r)))continue;
            foreach(Component c in t.GetComponents<Component>())
                if(c!=null&&!(c is NavMeshSurface)&&!(c is Transform&&t.name=="Building_city"))result[c.GetEntityId().ToString()]=EditorJsonUtility.ToJson(c);
        }
        return result;
    }
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
    }
    private static void Courtyard(Transform root)
    {
        Transform garden=Group("03_Courtyard_Details",root);
        var bench=Piece("Prop","Prop_Bench",garden,new Vector3(63.25f,Ground,-61.4f),90,Vector3.one*.85f);
        Bounds b=BoundsOf(bench);var box=bench.AddComponent<BoxCollider>();box.center=bench.transform.InverseTransformPoint(b.center);box.size=new Vector3(b.size.z,b.size.y,b.size.x)/.85f;
        foreach(float z in new[]{-60.15f,-62.65f})
        {
            var planter=Piece("Prop","Prop_Planter",garden,new Vector3(63.25f,Ground,z),0,Vector3.one*.75f);
            var collider=planter.AddComponent<CapsuleCollider>();collider.radius=.34f;collider.height=1.05f;collider.center=Vector3.up*.525f;
        }
    }
    [MenuItem("Tools/World/Northwest Quarter/Finish Facades and Courtyard")]
    public static void FinishDetails()
    {
        RequireScene();Transform root=GameObject.Find(RootName)?.transform??throw new InvalidOperationException("Build first.");
        if(root.Find("03_Courtyard_Details")!=null)throw new InvalidOperationException("Details already installed; preserve subsequent edits.");
        Transform tower=root.Find("01_Modular_Buildings/04_Apothecary");tower.rotation=Quaternion.Euler(0,90,0);
        Courtyard(root);Physics.SyncTransforms();var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();
        AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/NorthwestNavigation_Final.asset");EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Capture();Focus();
    }
    [MenuItem("Tools/World/Northwest Quarter/Build Red Roof Quarter")]
    public static void Build()
    {
        RequireScene();
        if(GameObject.Find(RootName)!=null||AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Quarter already exists; refusing to replace later hand edits.");
        var sketch=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>
            PrefabUtility.IsAnyPrefabInstanceRoot(g)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g).StartsWith(Kit+"/Generated/Prefabs/VK/",StringComparison.Ordinal)&&
            g.transform.position.x>55&&g.transform.position.x<74&&g.transform.position.z>-60&&g.transform.position.z<-49).ToArray();
        if(sketch.Length!=10)throw new InvalidOperationException("User sketch changed; inspect before building (expected 10, got "+sketch.Length+").");
        string backup="Logs/SceneBackups/WorldPrototype_BeforeNorthwestQuarter_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        Directory.CreateDirectory("Logs/SceneBackups");if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),backup,true))throw new IOException("Scene backup failed.");
        var before=Snapshot(sketch.Select(g=>g.transform));
        Transform root=Group(RootName,GameObject.Find("Building_city").transform);Undo.RegisterCreatedObjectUndo(root.gameObject,"Build northwest red-roof quarter");
        Transform archive=Group("00_User_Sketch_Preserved_Inactive",root);
        foreach(var piece in sketch){Undo.SetTransformParent(piece.transform,archive,"Preserve original building sketch");}archive.gameObject.SetActive(false);
        Transform buildings=Group("01_Modular_Buildings",root);foreach(var house in Houses)MakeHouse(house,buildings);
        Courtyard(root);
        Transform lane=Group("02_Alley_Layout_Reference",root);
        for(int i=0;i<AlleyRoute.Length;i++){var p=Group("Main_Alley_"+i,lane);p.position=AlleyRoute[i];}
        for(int i=0;i<CrossRoute.Length;i++){var p=Group("Cross_Alley_"+i,lane);p.position=CrossRoute[i];}
        EnsureFolder(Folder);Physics.SyncTransforms();
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();Undo.RecordObject(surface,"Bake paths around northwest quarter");surface.BuildNavMesh();
        if(surface.navMeshData==null)throw new InvalidOperationException("Navigation bake failed.");
        AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/NorthwestNavigation.asset");EditorUtility.SetDirty(surface);
        var after=Snapshot(new[]{root});bool scoped=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out string value)&&value==p.Value);
        string scope=$"{DateTime.Now:s}\nCompared {before.Count} pre-existing components outside the ten preserved sketch pieces.\n"+(scoped?"PASS Existing city roads, walls, furniture, other zones and player unchanged. Only navigation data reference refreshed.":"FAIL Unexpected scene change.");
        File.WriteAllText("Logs/CityNorthwestScope.txt",scope);if(!scoped)throw new InvalidOperationException("Out-of-scope changes detected; see scope report and backup.");
        var record=new Record{backup=backup,archivedSketchPieces=sketch.Length,preservedComponents=before.Count,scopeAudit=scope,modularPieces=root.GetComponentsInChildren<Transform>(true).Count(t=>t.gameObject.activeInHierarchy&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))};
        AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(record,true)),Folder+"/BuildRecord.asset");AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Capture();Focus();
    }
    [MenuItem("Tools/World/Northwest Quarter/Validate Red Roof Quarter")]
    public static void Validate()
    {
        RequireScene();var root=GameObject.Find(RootName);if(root==null)throw new InvalidOperationException("Build the quarter first.");
        var report=new StringBuilder("Northwest red-roof quarter validation "+DateTime.Now.ToString("s")+"\n");bool passed=true;
        Action<bool,string> check=(ok,message)=>{passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+message);};
        Transform buildings=root.transform.Find("01_Modular_Buildings");check(buildings.childCount==6,"Six individually editable modular buildings");
        var renderers=buildings.GetComponentsInChildren<Renderer>();
        check(renderers.All(r=>r.bounds.min.x>=56.6f&&r.bounds.max.x<=73.75f&&r.bounds.min.z>=-68.88f&&r.bounds.max.z<=-52.6f),"Entire quarter stays inside northwest stone parcel, clear of wooden roads and walls");
        check(renderers.SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.shader!=null&&!ShaderUtil.ShaderHasError(m.shader)),"Materials and shaders valid");
        var roofs=buildings.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("SM_VK_Roof",StringComparison.Ordinal)).SelectMany(t=>t.GetComponents<Renderer>()).ToArray();
        check(roofs.Length>=11&&roofs.SelectMany(r=>r.sharedMaterials).Where(m=>m.name.Contains("Roof")).All(m=>m.name=="M_VK_RoofRed"),"Only red tile roof materials, no blue/green/thatch roofs");
        check(buildings.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
        check(!buildings.GetComponentsInChildren<MonoBehaviour>().Any(m=>m.GetType().Namespace=="MedievalKit"),"No imported kit gameplay or procedural scripts reintroduced");
        check(Houses.All(h=>h.scale>=.8f&&h.scale<=.88f),"Reduced 0.80–0.88 uniform scales; ground storeys 2.40–2.64 m for a 2 m player");
        check(buildings.GetComponentsInChildren<BoxCollider>().Count(c=>c.enabled)==6,"Six solid collision volumes; no passage through building shells");
        check(buildings.GetComponentsInChildren<Transform>().Count(t=>t.name=="NPC_Stand_Point")==6,"Six empty future shop/NPC anchors, with no shop or NPC gameplay");
        check(!root.transform.Find("00_User_Sketch_Preserved_Inactive").gameObject.activeSelf,"Original ten sketch pieces preserved, inactive and recoverable");
        var routes=new[]{AlleyRoute,CrossRoute};
        for(int routeIndex=0;routeIndex<routes.Length;routeIndex++)
        {
            var route=routes[routeIndex];
            bool clear=true,nav=true;
            for(int i=0;i<route.Length-1;i++)
            {
                var a=route[i];var b=route[i+1];int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.2f);
                for(int j=0;j<=steps;j++)
                {
                    Vector3 p=Vector3.Lerp(a,b,(float)j/steps);clear&=!Physics.CheckCapsule(p+Vector3.up*.55f,p+Vector3.up*1.55f,.49f,1<<7,QueryTriggerInteraction.Ignore);
                    nav&=NavMesh.SamplePosition(p,out NavMeshHit hit,.4f,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-Ground)<.12f;
                }
                var path=new NavMeshPath();nav&=NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            }
            check(clear,(routeIndex==0?"Main":"Cross")+" alley has full player capsule clearance");check(nav,"Complete grounded navigation along alley route");
        }
        foreach(Transform building in buildings)
        {
            Bounds b=BoundsOf(building.gameObject);report.AppendLine($"BUILDING {building.name} scale={building.localScale.x:F2} world size={b.size:F2}");
        }
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/CityNorthwestValidation.txt",report.ToString());if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());
    }
    [MenuItem("Tools/World/Northwest Quarter/Capture Red Roof Quarter")]
    public static void Capture()
    {
        RequireScene();var cam=new GameObject("Temporary northwest capture").AddComponent<Camera>();var rt=new RenderTexture(1440,1100,24);var pixels=new Texture2D(1440,1100,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try
        {
            cam.transform.position=new Vector3(48.5f,26,-83);cam.transform.LookAt(new Vector3(65,8,-60.5f));cam.orthographic=true;cam.orthographicSize=14.5f;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,1100),0,0);pixels.Apply();File.WriteAllBytes("Logs/CityNorthwest_Preview.png",pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=old;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(cam.gameObject);}
        CitySurfaceAlignment.Capture();File.Copy("Logs/CitySurfaces_Aligned.png","Logs/CityNorthwest_Overview.png",true);
    }
    [MenuItem("Tools/World/Northwest Quarter/Focus Red Roof Quarter")]
    public static void Focus()
    {RequireScene();Selection.activeGameObject=GameObject.Find(RootName);SceneView.lastActiveSceneView?.LookAt(new Vector3(65,8,-60.5f),Quaternion.Euler(42,0,0),22,false,true);}
    public static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds result = renderers[0].bounds;
        foreach (var renderer in renderers) result.Encapsulate(renderer.bounds);
        return result;
    }
    private static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
    private static IEnumerable<Transform> SceneTransforms() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));
    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode first.");
    }
    [MenuItem("Tools/World/Northwest Quarter/Inspect Site and Modular Pieces")]
    public static void Inspect()
    {
        RequireScene();
        var text = new StringBuilder("Northwest city audit " + DateTime.Now.ToString("s") + "\n");
        foreach (var t in SceneTransforms())
        {
            if (t.name == "Player")
            {
                var cc = t.GetComponent<CharacterController>();
                text.AppendLine($"PLAYER {t.position:F3} scale={t.lossyScale:F3} controller height={cc.height} radius={cc.radius} center={cc.center}");
            }
            if (t.name == "Floor_city" || t.name == "Way_city" || t.name == "Wall_city")
                text.AppendLine($"CITY {Hierarchy(t)} {BoundsOf(t.gameObject)}");
            bool prefab = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) == t.gameObject;
            if (!prefab || t.position.x < 52 || t.position.x > 99 || t.position.z < -98 || t.position.z > -47) continue;
            if (t.IsChildOf(GameObject.Find("Floor_city").transform) || t.IsChildOf(GameObject.Find("Way_city").transform) || t.IsChildOf(GameObject.Find("Wall_city").transform)) continue;
            Bounds b = BoundsOf(t.gameObject);
            text.AppendLine($"EXISTING {Hierarchy(t)} source={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)} pos={t.position:F3} yaw={t.eulerAngles.y:F2} scale={t.lossyScale:F3} min={b.min:F3} max={b.max:F3}");
        }
        string[] families = { "Wall", "Roof", "RoofS", "Corner", "Foundation", "Chimney", "Awning", "Balcony", "Porch", "Sign", "Passage", "Stoop", "Prop" };
        foreach (string family in families)
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Kit + "/Generated/Prefabs/VK/" + family }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                if (family == "Prop" && !new[] { "Flower", "Planter", "Lantern", "Barrel", "Crate", "Bench", "Basket", "Sack" }.Any(n => path.Contains(n))) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Bounds b = BoundsOf(prefab);
                text.AppendLine($"PIECE {Path.GetFileNameWithoutExtension(path)} pos={prefab.transform.position:F3} scale={prefab.transform.localScale:F3} min={b.min:F3} max={b.max:F3} mats={string.Join(",", prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.name).Distinct())}");
            }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CityNorthwestAudit.txt", text.ToString());
        CitySurfaceAlignment.Capture(); File.Copy("Logs/CitySurfaces_Aligned.png", "Logs/CityNorthwest_Before.png", true);
        Debug.Log("Northwest site and module audit written: Logs/CityNorthwestAudit.txt");
    }
}
