#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static partial class VillageFarmSetup
{
    [MenuItem("Tools/World/Yellow Roof Village/Keep Villagers off Vegetable Beds")]
    public static void ProtectCrops()
    {
        RequireScene();var root=GameObject.Find(RootName).transform;
        var plots=root.Find("03_Vegetable_and_Herb_Plots");
        if(plots.Find("Tilled_Ground_NPC_Exclusion")==null)
        {
            var exclusion=Group("Tilled_Ground_NPC_Exclusion",plots);exclusion.position=new Vector3(-75.5f,5,59.25f);
            var volume=exclusion.gameObject.AddComponent<NavMeshModifierVolume>();volume.center=Vector3.zero;volume.size=new Vector3(14.5f,3,11);volume.area=1;
            var population=root.GetComponentInChildren<TownNpcPopulation>();
            population.CityPoints[12].transform.position=new Vector3(-66.9f,4.08f,65.8f);
            Physics.SyncTransforms();var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/VillageNavigation_CropProtection.asset");EditorUtility.SetDirty(surface);
        }
        Validate();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);Capture();
    }
    [MenuItem("Tools/World/Yellow Roof Village/Soften Lane Ends and Save")]
    public static void SoftenLanes()
    {
        RequireScene(); var root=GameObject.Find(RootName).transform;
        foreach(var filter in root.Find("08_Soft_Winding_Dirt_Lanes").GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;var colors=mesh.colors;int rows=colors.Length/4;
            for(int i=0;i<colors.Length;i++)
            {
                int row=i/4;float end=Mathf.Clamp01(Mathf.Min(row,rows-1-row)/3f);
                colors[i]=new Color(1,1,1,(i%4==0||i%4==3?0:1)*end);
            }
            mesh.colors=colors;EditorUtility.SetDirty(mesh);
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Village_Worn_Earth.mat");material.SetColor("_Tint",new Color(.30f,.22f,.14f,.45f));EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.gameObject.scene);Capture();
    }
    private static void Lane(Transform parent, string name, Vector2[] controls, float width, Material material)
    {
        var points = new List<Vector2>();
        for (int segment = 0; segment < controls.Length - 1; segment++)
        {
            Vector2 a=controls[Mathf.Max(0,segment-1)], b=controls[segment], c=controls[segment+1], d=controls[Mathf.Min(controls.Length-1,segment+2)];
            int steps=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(b,c)/.55f));
            for (int j=0;j<steps;j++)
            {
                float t=j/(float)steps;
                points.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
        }
        points.Add(controls[controls.Length-1]);
        var go=Group(name,parent).gameObject; var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        for(int i=0;i<points.Count;i++)
        {
            Vector2 tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized; var side=new Vector2(-tangent.y,tangent.x);
            for(int edge=0;edge<4;edge++)
            {
                float offset=edge==0?-width*.5f-.45f:edge==1?-width*.33f:edge==2?width*.33f:width*.5f+.45f;
                var p=points[i]+side*offset; vertices.Add(go.transform.InverseTransformPoint(new Vector3(p.x,Height(p.x,p.y)+.018f,p.y)));
                colors.Add(new Color(1,1,1,edge==0||edge==3?0:1));
            }
            if(i==0)continue;
            for(int edge=0;edge<3;edge++) {int k=i*4+edge;triangles.AddRange(new[]{k-4,k,k+1,k-4,k+1,k-3});}
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=true;
    }
    [MenuItem("Tools/World/Yellow Roof Village/Finish Lanes Nature and Validate")]
    public static void Finish()
    {
        RequireScene();var root=GameObject.Find(RootName)?.transform??throw new InvalidOperationException("Build the village first.");
        siteTerrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;
        if(root.Find("08_Soft_Winding_Dirt_Lanes")==null)
        {
            var lanes=Group("08_Soft_Winding_Dirt_Lanes",root);
            var shader=Shader.Find("Project/Village Ground Overlay");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Village lane shader has errors.");
            var material=new Material(shader){name="Village_Worn_Earth"};material.SetColor("_Tint",new Color(.41f,.31f,.20f,.6f));AssetDatabase.CreateAsset(material,Folder+"/Materials/Village_Worn_Earth.mat");
            Lane(lanes,"Main_Winding_Village_Lane",new[]{new Vector2(-59,37.5f),new Vector2(-60.4f,44),new Vector2(-62.2f,50.5f),new Vector2(-62,57),new Vector2(-60.5f,65),new Vector2(-62.4f,73.7f),new Vector2(-65,81.4f),new Vector2(-66.2f,84)},2.3f,material);
            Lane(lanes,"Orchard_Homes_Lane",new[]{new Vector2(-65,81.4f),new Vector2(-72.3f,81.9f),new Vector2(-78,82.7f),new Vector2(-81.5f,82.6f)},1.5f,material);
            Lane(lanes,"Eastern_Homes_Lane",new[]{new Vector2(-62.3f,76.7f),new Vector2(-57.1f,80.4f),new Vector2(-51,80.2f)},1.6f,material);
            Lane(lanes,"Pasture_and_Crop_Lane",new[]{new Vector2(-61,66.9f),new Vector2(-67.6f,67),new Vector2(-75.7f,67.9f),new Vector2(-81.1f,67.7f)},1.7f,material);
            Lane(lanes,"Garden_and_Mill_Lane",new[]{new Vector2(-62,51.7f),new Vector2(-68.5f,50),new Vector2(-75.2f,49.5f),new Vector2(-78.8f,47.6f)},1.4f,material);
            Lane(lanes,"Well_and_East_Lane",new[]{new Vector2(-62,56),new Vector2(-59.6f,54.7f),new Vector2(-54,54),new Vector2(-49,53)},1.5f,material);
            var nature=root.Find("06_Orchard_Shrubs_and_Wild_Growth");
            foreach(Transform t in nature)
            {
                if(t.name.StartsWith("SM_VK_Tree_")) {t.localScale*=1.5f;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            }
            for(int i=0;i<34;i++)
            {
                float x=-83.5f+(i%12)*1.32f, z=i<12?66.2f:i<24?52.1f:80.2f;
                At("Plant",i%3==0?"Plant_WildGarlic":"Plant_TallGrass",nature,x,z+(i%3)*.19f,i*39,.8f);
            }
            foreach(var animal in root.Find("04_Livestock_Pens_and_Shelters").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("SM_VK_Animal_")).ToArray())
                if(!animal.GetComponentsInChildren<Collider>().Any(c=>c.enabled))SolidBounds(animal.gameObject,true,.15f);
            FenceLine(root.Find("02_Well_Lane_and_Household_Props"),new Vector2(-72.5f,88.7f),new Vector2(-72.5f,84.7f));
            Physics.SyncTransforms();var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/VillageNavigation_Final.asset");EditorUtility.SetDirty(surface);
        }
        Validate();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);Capture();Selection.activeGameObject=root.gameObject;
    }
    [MenuItem("Tools/World/Yellow Roof Village/Validate Village")]
    public static void Validate()
    {
        RequireScene();var root=GameObject.Find(RootName)?.transform??throw new InvalidOperationException("No village.");
        var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();siteTerrain=map.terrain;
        var report=new StringBuilder("Yellow-roof rural village validation "+DateTime.Now.ToString("s")+"\n");bool passed=true;
        void Check(bool valid,string label){passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+label);}
        var homes=root.Find("01_Yellow_Roof_Cottages");Check(homes.childCount==7,"Seven separately editable assembled cottages");
        Check(root.Find("00_Original_Sketch_Preserved_Inactive")!=null&&!root.Find("00_Original_Sketch_Preserved_Inactive").gameObject.activeSelf,"Original sketch retained inactive");
        Check(homes.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m.name.Contains("Roof")).All(m=>m.name.Contains("Thatch")),"All house roofs use the existing yellow thatch assets");
        foreach(Transform house in homes)
        {
            var b=BoundsOf(house.gameObject);
            Check(b.min.x>-90&&b.max.x<-40&&b.min.z>37&&b.max.z<94,house.name+" stays on village side of the FRONT stream, bounds="+b);
            Check(Mathf.Abs(house.position.y-Height(house.position.x,house.position.z))<.05f,house.name+" grounded on existing terrain");
        }
        var rs=root.GetComponentsInChildren<Renderer>();Check(rs.SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.shader!=null&&!ShaderUtil.ShaderHasError(m.shader)),"All active village materials valid for URP");
        Check(root.GetComponentsInChildren<Transform>().All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
        Check(!root.GetComponentsInChildren<MonoBehaviour>().Any(c=>c.GetType().Namespace=="MedievalKit"),"Only model assets, no imported generation/gameplay scripts");
        var population=root.GetComponentInChildren<TownNpcPopulation>();Check(population.MaxPopulation==10&&population.ConfiguredFemales==5&&population.ConfiguredWorkers==0,"Ten villagers, five women, own population separate from city");
        if(root.Find("03_Vegetable_and_Herb_Plots/Tilled_Ground_NPC_Exclusion")!=null)
            Check(!NavMesh.SamplePosition(new Vector3(-75.5f,4,59.25f),out _, .3f,NavMesh.AllAreas),"Planted beds are excluded from villager navigation");
        var points=population.CityPoints;var origin=points[0].transform.position;
        foreach(var point in points)
        {
            var path=new NavMeshPath();bool reachable=NavMesh.SamplePosition(origin,out var a,1.2f,NavMesh.AllAreas)&&NavMesh.SamplePosition(point.transform.position,out var b,1.2f,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            Check(reachable&&population.Contains(point.transform.position),point.name+" connected to village arrival");
        }
        Check(map.village==root,"Map village landmark linked to the new parent");
        var oldCity=UnityEngine.Object.FindObjectsByType<TownNpcPopulation>(FindObjectsSortMode.None).Single(p=>p!=population);
        Check(oldCity.MaxPopulation==12&&oldCity.transform.GetComponentInParent<CityAssemblyRoot>()!=null,"Existing movable city and population retained");
        report.AppendLine("Parent="+PathOf(root));report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/VillageValidation.txt",report.ToString());
        if(!passed)throw new InvalidOperationException("Village validation failed. Read Logs/VillageValidation.txt");
    }
}
#endif
