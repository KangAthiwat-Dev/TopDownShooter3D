using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>Scoped tools for the user's Floor_city / Wall_city / Way_city groups.</summary>
public static class CitySurfaceAlignment
{
    public const string Folder="Assets/_Project/World/CitySurfaceAlignment";
    private static readonly string[] Names={"Floor_city","Wall_city","Way_city"};
    private static Transform[] Roots()=>Names.Select(n=>GameObject.Find(n)?.transform??throw new InvalidOperationException("Missing user city group "+n)).ToArray();
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static Bounds BoundsOf(GameObject go){Renderer[] rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return new Bounds(go.transform.position,Vector3.zero);Bounds b=rs[0].bounds;foreach(Renderer r in rs)b.Encapsulate(r.bounds);return b;}
    private static Transform[] Models(Transform root)=>root.GetComponentsInChildren<Transform>(true).Where(t=>t!=root&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&!t.GetComponentsInParent<Transform>(true).Skip(1).TakeWhile(p=>p!=root).Any(p=>PrefabUtility.IsAnyPrefabInstanceRoot(p.gameObject))).ToArray();

    [Serializable] private sealed class Plan
    {
        public float west,east,south,north,top,wallBase,westGateZ,northGateX;
        public Vector4[] stone,wood;
        public int reusedStone,reusedWood,addedStone,addedWood;
        public string backup;
    }
    private static float Median(IEnumerable<float> source){float[] a=source.OrderBy(v=>v).ToArray();return a[a.Length/2];}
    private static string Source(Transform t)=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
    private static string Stem(Transform t)=>System.IO.Path.GetFileNameWithoutExtension(Source(t));
    private static float Yaw(Transform t)=>Mathf.Round(t.eulerAngles.y/90)*90;
    private static void Canonical(Transform t)
    {
        Undo.RecordObject(t,"Align user city module");float yaw=Yaw(t);
        t.rotation=Quaternion.Euler(0,yaw,0)*Quaternion.Euler(-90,0,0);
        t.localScale=AssetDatabase.LoadAssetAtPath<GameObject>(Source(t)).transform.localScale;
    }
    private static void SetBoundsCenter(Transform t,Vector2 p,float bottom)
    {
        Bounds b=BoundsOf(t.gameObject);t.position+=new Vector3(p.x-b.center.x,bottom-b.min.y,p.y-b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));}
    private static void SaveBackup(string path){Directory.CreateDirectory("Logs/SceneBackups");if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),path,true))throw new IOException("Live scene backup failed.");}
    private static void SetLayer(Transform t){foreach(Transform child in t.GetComponentsInChildren<Transform>(true)){Undo.RecordObject(child.gameObject,"City environment layer");child.gameObject.layer=7;}}
    private static bool InScope(Transform t,Transform[] roots)=>roots.Any(r=>t==r||t.IsChildOf(r));
    private static Dictionary<string,string> OutsideSnapshot(Transform[] roots)
    {
        var result=new Dictionary<string,string>();
        foreach(var go in SceneManager.GetActiveScene().GetRootGameObjects())foreach(Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            if(InScope(t,roots))continue;
            foreach(Component c in t.GetComponents<Component>())if(c!=null&&!(c is NavMeshSurface))result[c.GetEntityId().ToString()]=EditorJsonUtility.ToJson(c);
        }
        return result;
    }

    [MenuItem("Tools/World/City Surfaces/Align Walls and Fill Existing City")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        if(SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype.");
        if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("City already aligned; refusing to overwrite later manual edits.");
        Transform[] roots=Roots();Transform floor=roots[0],walls=roots[1],way=roots[2];
        Transform[] stones=Models(floor),wood=Models(way),allWalls=Models(walls),direct=allWalls.Where(t=>t.parent==walls).ToArray();
        if(stones.Any(t=>Stem(t)!="floor_tile_large")||wood.Any(t=>Stem(t)!="floor_wood_large_dark"))throw new InvalidOperationException("Unexpected floor type; inspect before changing it.");
        Transform[] corners=direct.Where(t=>Stem(t)=="wall_corner").ToArray();if(corners.Length!=4)throw new InvalidOperationException("Expected the four user wall corners.");
        var gates=walls.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_door_city")).ToArray();if(gates.Length!=2)throw new InvalidOperationException("Expected the user's two gate assemblies.");
        float oldWest=corners.Min(t=>t.position.x),oldEast=corners.Max(t=>t.position.x),oldSouth=corners.Min(t=>t.position.z),oldNorth=corners.Max(t=>t.position.z);
        Transform westGate=gates.OrderBy(t=>BoundsOf(t.gameObject).center.x).First(),northGate=gates.Single(t=>t!=westGate);
        float oldGateZ=Models(westGate).Where(t=>Stem(t)=="wall_corner_small").Average(t=>t.position.z);
        float oldGateX=Models(northGate).Where(t=>Stem(t)=="wall_corner_small").Average(t=>t.position.x);
        var plain=direct.Except(corners).ToArray();
        Func<Transform,int> side=t=>{Vector3 p=BoundsOf(t.gameObject).center;float[] distances={Mathf.Abs(p.x-oldWest),Mathf.Abs(p.x-oldEast),Mathf.Abs(p.z-oldSouth),Mathf.Abs(p.z-oldNorth)};return Array.IndexOf(distances,distances.Min());};
        var west=plain.Where(t=>side(t)==0).ToArray();var east=plain.Where(t=>side(t)==1).ToArray();var south=plain.Where(t=>side(t)==2).ToArray();var north=plain.Where(t=>side(t)==3).ToArray();
        Transform[] westLower=west.Where(t=>BoundsOf(t.gameObject).center.z<oldGateZ).ToArray(),westUpper=west.Except(westLower).ToArray();
        Transform[] northLeft=north.Where(t=>BoundsOf(t.gameObject).center.x<oldGateX).ToArray(),northRight=north.Except(northLeft).ToArray();
        if(new[]{westLower,westUpper,east,south,northLeft,northRight}.Any(a=>a.Length==0))throw new InvalidOperationException("Wall grouping did not match audited rectangle.");
        var plan=new Plan{west=oldWest,east=oldWest+42,south=oldNorth-44,north=oldNorth,top=Median(stones.Select(t=>BoundsOf(t.gameObject).max.y)),wallBase=5};
        float lowerLength=westLower.Sum(t=>BoundsOf(t.gameObject).size.z),upperLength=westUpper.Sum(t=>BoundsOf(t.gameObject).size.z);
        plan.westGateZ=plan.south+2+34*lowerLength/(lowerLength+upperLength)+3;
        plan.northGateX=plan.west+21;
        var road=new HashSet<Vector2Int>();
        for(int x=-6;x<=4;x++)road.Add(new Vector2Int(x,0));
        for(int z=1;z<=6;z++)road.Add(new Vector2Int(0,z));
        for(int z=-4;z<=-1;z++)road.Add(new Vector2Int(0,z));
        road.Add(new Vector2Int(-1,-4));road.Add(new Vector2Int(-2,-4));
        for(int x=1;x<=3;x++)road.Add(new Vector2Int(x,-3));
        plan.wood=road.OrderBy(p=>p.y).ThenBy(p=>p.x).Select(p=>new Vector4(plan.northGateX+p.x*4,plan.westGateZ+p.y*4,4,4)).ToArray();
        var cells=new List<Vector4>();
        for(int z=-10;z<=10;z++)for(int x=-10;x<=10;x++)
        {
            if(road.Contains(new Vector2Int(x,z)))continue;
            float cx=plan.northGateX+x*4,cz=plan.westGateZ+z*4;
            float minX=Mathf.Max(cx-2,plan.west),maxX=Mathf.Min(cx+2,plan.east),minZ=Mathf.Max(cz-2,plan.south),maxZ=Mathf.Min(cz+2,plan.north);
            if(maxX-minX<0.01f||maxZ-minZ<0.01f)continue;
            cells.Add(new Vector4((minX+maxX)*0.5f,(minZ+maxZ)*0.5f,maxX-minX,maxZ-minZ));
        }
        plan.stone=cells.ToArray();
        if(stones.Length>plan.stone.Length||wood.Length>plan.wood.Length)throw new InvalidOperationException("More authored modules than target slots; refusing to discard any.");
        plan.backup="Logs/SceneBackups/WorldPrototype_BeforeCityAlignment_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";SaveBackup(plan.backup);
        var outside=OutsideSnapshot(roots);EnsureFolder(Folder);Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Align only user city walls and surfaces");
        foreach(Transform t in corners)
        {
            bool isWest=t.position.x<(oldWest+oldEast)*0.5f,isSouth=t.position.z<(oldSouth+oldNorth)*0.5f;
            Undo.RecordObject(t,"Square city wall corner");t.rotation=Quaternion.Euler(0,isWest?(isSouth?0:90):(isSouth?270:180),0)*Quaternion.Euler(-90,0,0);
            t.localScale=AssetDatabase.LoadAssetAtPath<GameObject>(Source(t)).transform.localScale;t.position=new Vector3(isWest?plan.west:plan.east,plan.wallBase,isSouth?plan.south:plan.north);PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        FitRun(south,plan.west+2,plan.east-2,plan.south,true,plan.wallBase);
        FitRun(east,plan.south+2,plan.north-2,plan.east,false,plan.wallBase);
        FitRun(westLower,plan.south+2,plan.westGateZ-3,plan.west,false,plan.wallBase);
        FitRun(westUpper,plan.westGateZ+3,plan.north-2,plan.west,false,plan.wallBase);
        FitRun(northLeft,plan.west+2,plan.northGateX-3,plan.north,true,plan.wallBase);
        FitRun(northRight,plan.northGateX+3,plan.east-2,plan.north,true,plan.wallBase);
        FitGate(westGate,new Vector2(plan.west,plan.westGateZ),false,plan.wallBase);
        FitGate(northGate,new Vector2(plan.northGateX,plan.north),true,plan.wallBase);
        plan.reusedStone=stones.Length;plan.reusedWood=wood.Length;
        plan.addedStone=FitFloor(stones,floor,plan.stone,plan.top);plan.addedWood=FitFloor(wood,way,plan.wood,plan.top);
        foreach(Transform r in roots)SetLayer(r);
        foreach(Transform t in allWalls)foreach(MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))if(mf.sharedMesh!=null&&mf.GetComponent<Collider>()==null){var col=Undo.AddComponent<MeshCollider>(mf.gameObject);col.sharedMesh=mf.sharedMesh;}
        Physics.SyncTransforms();
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();Undo.RecordObject(surface,"Navigation for aligned city floor");surface.BuildNavMesh();
        if(surface.navMeshData==null)throw new InvalidOperationException("City navigation bake failed; backups retained.");
        AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/CityAlignedNavigation.asset");EditorUtility.SetDirty(surface);
        var after=OutsideSnapshot(roots);bool untouched=outside.Count==after.Count&&outside.All(p=>after.TryGetValue(p.Key,out string value)&&value==p.Value);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CityAlignmentScope.txt",$"{DateTime.Now:s}\nOutside-city components compared: {outside.Count}\n"+(untouched?"PASS All outside-city objects and components unchanged (only navigation data reference refreshed).":"FAIL Outside-city scene changes detected.")+"\n");
        if(!untouched)throw new InvalidOperationException("Unexpected out-of-scope scene change; see scope audit.");
        AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(plan,true)),Folder+"/AlignmentPlan.asset");Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Capture();Focus();
    }
    private static void FitRun(Transform[] modules,float start,float end,float plane,bool horizontal,float bottom)
    {
        var sorted=modules.OrderBy(t=>horizontal?BoundsOf(t.gameObject).center.x:BoundsOf(t.gameObject).center.z).ToArray();
        var sizes=sorted.Select(t=>horizontal?BoundsOf(t.gameObject).size.x:BoundsOf(t.gameObject).size.z).ToArray();float factor=(end-start)/sizes.Sum(),cursor=start;
        for(int i=0;i<sorted.Length;i++)
        {
            Transform t=sorted[i];Canonical(t);Bounds b=BoundsOf(t.gameObject);float length=sizes[i]*factor;
            Vector3 scale=t.localScale;scale.x*=length/(horizontal?b.size.x:b.size.z);t.localScale=scale;
            SetBoundsCenter(t,horizontal?new Vector2(cursor+length*0.5f,plane):new Vector2(plane,cursor+length*0.5f),bottom);cursor+=length;
        }
    }
    private static void FitGate(Transform group,Vector2 center,bool north,float bottom)
    {
        var modules=Models(group);Vector2 outward=north?Vector2.up:Vector2.left,along=north?Vector2.right:Vector2.up;
        Transform[] corners=modules.Where(t=>Stem(t)=="wall_corner_small").OrderBy(t=>north?t.position.x:t.position.z).ToArray();
        Transform[] caps=modules.Where(t=>Stem(t)=="wall_endcap").OrderBy(t=>north?t.position.x:t.position.z).ToArray();
        if(corners.Length!=2||caps.Length!=2)throw new InvalidOperationException("Unexpected gate parts.");
        Vector3 oldAnchor=(corners[0].position+corners[1].position)*0.5f;Undo.RecordObject(group,"Align gate assembly");group.position+=new Vector3(center.x-oldAnchor.x,0,center.y-oldAnchor.z);
        for(int i=0;i<2;i++)
        {
            float sign=i==0?-1:1;Transform c=corners[i];Canonical(c);c.rotation=Quaternion.Euler(0,(north?90:0)+(i==0?180:270),0)*Quaternion.Euler(-90,0,0);
            Vector2 p=center+along*(sign*2);c.position=new Vector3(p.x,bottom,p.y);PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            Transform cap=caps[i];Canonical(cap);cap.rotation=Quaternion.Euler(0,north?90:0,0)*Quaternion.Euler(-90,0,0);
            p=center+outward*5.5f+along*(sign*1.75f);cap.position=new Vector3(p.x,bottom,p.y);PrefabUtility.RecordPrefabInstancePropertyModifications(cap);
        }
        Transform arch=modules.Single(t=>Stem(t)=="wall_doorway_Tsplit");Canonical(arch);arch.rotation=Quaternion.Euler(0,north?180:90,0)*Quaternion.Euler(-90,0,0);Vector2 a=center+outward*3;arch.position=new Vector3(a.x,bottom,a.y);PrefabUtility.RecordPrefabInstancePropertyModifications(arch);
    }
    private static int FitFloor(Transform[] existing,Transform parent,Vector4[] slots,float top)
    {
        string path=Source(existing[0]);var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);var available=existing.ToList();int added=0;
        foreach(Vector4 slot in slots)
        {
            Vector3 center=new Vector3(slot.x,top,slot.y);Transform t;
            if(available.Count>0){t=available.OrderBy(p=>Vector3.SqrMagnitude(BoundsOf(p.gameObject).center-center)).First();available.Remove(t);}
            else{t=((GameObject)PrefabUtility.InstantiatePrefab(source,parent)).transform;t.name=source.name+"_Fill_"+(++added).ToString("000");Undo.RegisterCreatedObjectUndo(t.gameObject,"Fill city surface gap");}
            Canonical(t);t.rotation=Quaternion.Euler(-90,0,0);Vector3 s=source.transform.localScale;s.x*=slot.z/4;s.y*=slot.w/4;t.localScale=s;
            Bounds b=BoundsOf(t.gameObject);t.position+=new Vector3(slot.x-b.center.x,top-b.max.y,slot.y-b.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            if(t.GetComponentInChildren<Collider>()==null)
            {
                b=BoundsOf(t.gameObject);var support=new GameObject("SurfaceCollision");support.transform.position=b.center;support.transform.SetParent(t,true);support.layer=7;Undo.RegisterCreatedObjectUndo(support,"City floor collision");var box=support.AddComponent<BoxCollider>();box.size=b.size;
            }
        }
        return added;
    }
    private static Plan LoadPlan()=>JsonUtility.FromJson<Plan>(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/AlignmentPlan.asset").text);

    [MenuItem("Tools/World/City Surfaces/Validate Aligned City")]
    public static void Validate()
    {
        Plan p=LoadPlan();Transform[] roots=Roots();var report=new StringBuilder("City surface validation "+DateTime.Now.ToString("s")+"\n");bool passed=true;Action<bool,string> check=(ok,text)=>{passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text);};
        var floor=Models(roots[0]);var road=Models(roots[2]);check(floor.Length==p.stone.Length&&road.Length==p.wood.Length,"Expected stone/wood cell count; all original modules reused");
        check(floor.Concat(road).All(t=>Mathf.Abs(BoundsOf(t.gameObject).max.y-p.top)<0.002f),"Every stone and wood tile has the same top level");
        var rectangles=floor.Concat(road).Select(t=>BoundsOf(t.gameObject)).ToArray();int gaps=0,overlaps=0;
        for(float z=p.south+0.13f;z<p.north;z+=0.25f)for(float x=p.west+0.13f;x<p.east;x+=0.25f)
        {
            int count=rectangles.Count(b=>x>b.min.x+0.0001f&&x<b.max.x-0.0001f&&z>b.min.z+0.0001f&&z<b.max.z-0.0001f);if(count==0)gaps++;if(count>1)overlaps++;
        }
        check(gaps==0,"Entire interior covered without holes (25 cm samples; holes="+gaps+")");check(overlaps==0,"No overlapping coplanar floor tiles (overlaps="+overlaps+")");
        check(Models(roots[1]).All(t=>Mathf.Abs(BoundsOf(t.gameObject).min.y-p.wallBase)<0.002f),"All wall pieces and gate parts share one baseline");
        check(roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
        bool physical=true;for(float z=p.south+1;z<p.north;z+=2)for(float x=p.west+1;x<p.east;x+=2)
            physical&=Physics.Raycast(new Vector3(x,p.top+0.5f,z),Vector3.down,out RaycastHit hit,1,1<<7,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-p.top)<0.003f;
        check(physical,"Continuous flat collision support across the city floor");
        foreach(Vector3 point in new[]{new Vector3(p.west-5,p.top,p.westGateZ),new Vector3(p.northGateX,p.top,p.north+5),new Vector3(p.northGateX,p.top,p.westGateZ)})check(NavMesh.SamplePosition(point,out _,1,NavMesh.AllAreas),"Walkable navigation at city/gate "+point.ToString("F2"));
        report.AppendLine($"Stone {floor.Length} (reused {p.reusedStone}, added {p.addedStone}); wood {road.Length} (reused {p.reusedWood}, added {p.addedWood}); level {p.top:F3}");report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CitySurfaceValidation.txt",report.ToString());Debug.Log(report.ToString());
    }
    [MenuItem("Tools/World/City Surfaces/Focus User City")]
    public static void Focus(){var p=LoadPlan();Selection.activeGameObject=Roots()[0].gameObject;SceneView.lastActiveSceneView?.LookAt(new Vector3((p.west+p.east)*0.5f,p.top,(p.south+p.north)*0.5f),Quaternion.Euler(58,0,0),36,false,true);}
    [MenuItem("Tools/World/City Surfaces/Capture User City")]
    public static void Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;var p=LoadPlan();var cam=new GameObject("Temporary city surface camera").AddComponent<Camera>();var rt=new RenderTexture(1200,1100,24);var pixels=new Texture2D(1200,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{cam.transform.position=new Vector3((p.west+p.east)*0.5f,78,(p.south+p.north)*0.5f-27);cam.transform.rotation=Quaternion.Euler(70,0,0);cam.orthographic=true;cam.orthographicSize=29;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1200,1100),0,0);pixels.Apply();File.WriteAllBytes("Logs/CitySurfaces_Aligned.png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=previous;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(cam.gameObject);}
    }

    [MenuItem("Tools/World/City Surfaces/Inspect User City")]
    public static void Inspect()
    {
        var report=new StringBuilder("User city audit "+DateTime.Now.ToString("s")+"\n");
        foreach(Transform root in Roots())
        {
            report.AppendLine($"GROUP {PathOf(root)} world={root.position:F4} rotation={root.eulerAngles:F4} scale={root.lossyScale:F4} bounds={BoundsOf(root.gameObject)}");
            foreach(Transform t in Models(root))
            {
                Bounds b=BoundsOf(t.gameObject);string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                report.AppendLine($"MODEL {PathOf(t)} source={path} position={t.position:F4} rotation={t.eulerAngles:F4} scale={t.lossyScale:F4} center={b.center:F4} size={b.size:F4} bottom={b.min.y:F4} top={b.max.y:F4} colliders={t.GetComponentsInChildren<Collider>(true).Length}");
            }
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true).Where(t=>t!=root&&t.GetComponent<MeshRenderer>()==null&&t.childCount>0&&!PrefabUtility.IsPartOfPrefabInstance(t)))report.AppendLine($" SUBGROUP {PathOf(t)} pos={t.position:F4} rotation={t.eulerAngles:F4} scale={t.lossyScale:F4}");
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CitySurfaceAudit.txt",report.ToString());Debug.Log("City audit: Logs/CitySurfaceAudit.txt");
    }
}
