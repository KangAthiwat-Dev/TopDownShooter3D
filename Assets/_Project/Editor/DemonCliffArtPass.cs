#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static partial class DemonCliffSetup
{
    [MenuItem("Tools/World/Demon Cliffs/Finalize Landmark and Save")]
    public static void FinalizeLandmark()
    {
        RequireScene();var zone=UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>();var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        Undo.RecordObject(map,"Castle footprint landmark");map.castle=zone.castleFootprint;EditorUtility.SetDirty(map);EditorSceneManager.MarkSceneDirty(map.gameObject.scene);EditorSceneManager.SaveScene(map.gameObject.scene);
        Validate();CaptureOverview();Focus();
    }
    [MenuItem("Tools/World/Demon Cliffs/Refine Ember Fissures")]
    public static void RefineFissures()
    {
        RequireScene();var zone=UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>();var source=zone.transform.Find("03_Basalt_Scenery/Subtle_Ember_Stone_Cracks");var mesh=source.GetComponent<MeshFilter>().sharedMesh;
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        foreach(var stage in zone.stages)
        {
            var center=stage.center.position+P(0,.035f,4);
            for(int i=0;i<3;i++)
            {
                var a=center+P(-6+i*4,0,i%2*.5f);var points=new[]{a,a+P(.55f,0,.3f),a+P(.92f,0,.08f),a+P(1.15f,0,.7f),a+P(1.8f,0,1.15f)};
                for(int j=1;j<points.Length;j++){var side=Vector3.Cross(Vector3.up,points[j]-points[j-1]).normalized*.027f;Quad(vertices,triangles,source.InverseTransformPoint(points[j-1]-side),source.InverseTransformPoint(points[j]-side),source.InverseTransformPoint(points[j]+side),source.InverseTransformPoint(points[j-1]+side));}
            }
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();CaptureOverview();
    }
    [MenuItem("Tools/World/Demon Cliffs/Finish Jagged Rock Silhouette and Capture")]
    public static void ArtPass()
    {
        RequireScene();var zone=UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>();if(zone==null||zone.transform.Find("07_Jagged_Rock_Details")!=null)throw new InvalidOperationException("Approach absent or detailing already applied.");
        buildRoot=zone.transform;decorRoot=Group("07_Jagged_Rock_Details",buildRoot);
        var rocks=new[]{
            new[]{19f,8.8f,41f,3f,4f,3f},new[]{22f,8.8f,58f,3f,3.5f,3f},new[]{35f,8.8f,62f,4f,2.2f,2.4f},
            new[]{49f,8.8f,53f,3f,4f,3f},new[]{53f,12.8f,46f,3f,5f,4f},new[]{56f,12.8f,58f,3f,3.5f,3f},
            new[]{89f,12.8f,57f,3f,5f,4f},new[]{78f,12.8f,65f,4f,3.5f,3f},new[]{43f,18.1f,79f,4f,6f,4f},
            new[]{45f,18.1f,92f,4f,9f,4f},new[]{84f,18.1f,84f,3f,6f,5f},new[]{83f,17.1f,95f,5f,12f,5f},
            new[]{47f,18.1f,98f,5f,11f,4f},new[]{91f,5f,85f,4f,13f,4f},new[]{92f,5f,70f,4f,12f,5f},
            new[]{30f,4f,69f,5f,9f,4f},new[]{34f,4f,84f,4f,12f,4f},new[]{49f,4f,29f,3f,3f,3f},new[]{73f,4f,28f,3f,3f,3f}};
        for(int i=0;i<rocks.Length;i++){var r=rocks[i];Prop("Rocks/Crag_00"+(i%2==0?1:3)+".prefab","Jagged_Obsidian_"+i,P(r[0],r[1],r[2]),P(r[3],r[4],r[5]),i*71);}
        foreach(string file in new[]{"Broken_Basalt_Edge_Parapets","Shoreline_Basalt_Retaining_Edge"})
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+file+".asset");if(mesh==null)continue;
            var renderer=zone.transform.Find("03_Basalt_Scenery/"+file);if(renderer==null)continue;var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++){int quadVertex=i%12;if(quadVertex==2||quadVertex==3||quadVertex==6||quadVertex==7||quadVertex>=8){var world=renderer.TransformPoint(vertices[i]);vertices[i].y+=Mathf.PerlinNoise(world.x*.43f,world.z*.43f)*.7f-.42f;}}
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());CaptureOverview();
    }
    [MenuItem("Tools/World/Demon Cliffs/Capture Clean Overview")]
    public static void CaptureOverview()
    {
        var go=new GameObject("Temporary demon overview camera");var camera=go.AddComponent<Camera>();var rt=RenderTexture.GetTemporary(1600,1200,24);var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);var oldActive=RenderTexture.active;
        try
        {
            camera.transform.position=P(55,109,-9);camera.transform.rotation=Quaternion.LookRotation(P(55,10,65)-camera.transform.position);camera.orthographic=true;camera.orthographicSize=48;camera.farClipPlane=250;camera.clearFlags=CameraClearFlags.Skybox;camera.cullingMask=~(1<<5);camera.targetTexture=rt;
            var extra=go.AddComponent<UniversalAdditionalCameraData>();extra.renderPostProcessing=true;extra.volumeLayerMask=~0;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/DemonCliffs_Overview.png",image.EncodeToPNG());
        }
        finally{RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
        Debug.Log("Captured Logs/DemonCliffs_Overview.png");
    }
}
#endif
