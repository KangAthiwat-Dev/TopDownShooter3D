#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scoped Built-in Standard to URP repair. Material GUIDs and prefab/model references stay intact.</summary>
public static class StylizedDarkCastleMaterialRepair
{
    private const string Pack="Assets/StylizedDarkCastle";
    private static string Hash(string path)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    [MenuItem("Tools/Assets/Stylized Dark Castle/Repair Materials for URP")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before repairing asset materials.");
        var materials=AssetDatabase.FindAssets("t:Material",new[]{Pack+"/Materials"}).Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).Where(m=>m!=null).ToArray();
        var targets=materials.Where(m=>m.shader.name=="Standard"||m.shader.name=="Standard (Specular setup)").ToArray();
        if(Shader.Find("Universal Render Pipeline/Lit")==null)throw new InvalidOperationException("URP Lit shader is unavailable.");
        string backup="Logs/AssetBackups/StylizedDarkCastle_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");Directory.CreateDirectory(backup);var log=new StringBuilder("Stylized Dark Castle URP repair "+DateTime.Now.ToString("s")+"\nBACKUP "+backup+"\n");
        var changedFiles=new HashSet<string>(targets.Select(AssetDatabase.GetAssetPath));
        foreach(var mat in targets){AssetDatabase.SaveAssetIfDirty(mat);string file=AssetDatabase.GetAssetPath(mat);File.Copy(file,backup+"/"+Path.GetFileName(file));File.Copy(file+".meta",backup+"/"+Path.GetFileName(file)+".meta");}
        var untouched=Directory.GetFiles(Pack,"*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!changedFiles.Contains(p)).ToDictionary(p=>p,Hash);
        var scene=SceneManager.GetActiveScene();var oldComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        int passed=0,failed=0;
        void Check(bool ok,string text){if(ok)passed++;else failed++;log.AppendLine((ok?"PASS ":"FAIL ")+text);}
        foreach(var mat in targets)
        {
            string path=AssetDatabase.GetAssetPath(mat),guid=AssetDatabase.AssetPathToGUID(path),oldShader=mat.shader.name;var texture=mat.GetTexture("_MainTex");var scale=mat.GetTextureScale("_MainTex");var offset=mat.GetTextureOffset("_MainTex");var color=mat.GetColor("_Color");var normal=mat.GetTexture("_BumpMap");float bump=mat.GetFloat("_BumpScale"),smooth=mat.GetFloat("_Glossiness"),metal=mat.GetFloat("_Metallic");
            Undo.RecordObject(mat,"Repair castle material for URP");new StandardUpgrader(oldShader).Upgrade(mat,MaterialUpgrader.UpgradeFlags.None);BaseShaderGUI.SetMaterialKeywords(mat);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
            Check(mat.shader.name=="Universal Render Pipeline/Lit"&&mat.shader.isSupported&&!ShaderUtil.ShaderHasError(mat.shader),mat.name+" shader now supports current URP");
            Check(texture!=null&&mat.GetTexture("_BaseMap")==texture,mat.name+" original albedo preserved: "+AssetDatabase.GetAssetPath(texture));
            Check(mat.GetTextureScale("_BaseMap")==scale&&mat.GetTextureOffset("_BaseMap")==offset&&mat.GetColor("_BaseColor")==color,mat.name+" original tiling, offset, tint preserved");
            Check(mat.GetTexture("_BumpMap")==normal&&Mathf.Approximately(mat.GetFloat("_BumpScale"),bump)&&mat.IsKeywordEnabled("_NORMALMAP")== (normal!=null),mat.name+" normal map and strength preserved");
            Check(Mathf.Approximately(mat.GetFloat("_Smoothness"),smooth)&&Mathf.Approximately(mat.GetFloat("_Metallic"),metal),mat.name+" original surface response preserved");
            Check(AssetDatabase.AssetPathToGUID(path)==guid,mat.name+" GUID preserved for existing prefab and scene references");
        }
        int prefabs=0,renderers=0,slots=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Pack+"/Prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);prefabs++;
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;foreach(var material in renderer.sharedMaterials){slots++;bool valid=material!=null&&material.shader!=null&&material.shader.name=="Universal Render Pipeline/Lit"&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader)&&material.GetTexture("_BaseMap")!=null;if(!valid)Check(false,"prefab material invalid: "+path+" / "+renderer.name);}
            }
        }
        Check(prefabs>0&&slots>0,"audited "+prefabs+" prefabs / "+renderers+" renderers / "+slots+" material slots");
        foreach(var material in materials.Where(m=>m.shader.name.StartsWith("Skybox/")))Check(material.shader.isSupported&&material.HasProperty("_Tex")&&material.GetTexture("_Tex")!=null,"existing skybox remains compatible and textured");
        int sceneSlots=0;
        foreach(var renderer in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)))
        {
            var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null||!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Pack+"/Models/"))continue;
            foreach(var material in renderer.sharedMaterials){sceneSlots++;Check(material!=null&&material.HasProperty("_BaseMap")&&material.GetTexture("_BaseMap")!=null,"existing scene castle slot "+renderer.name+" material="+(material!=null?material.name:"missing"));}
        }
        log.AppendLine("Existing castle scene material slots="+sceneSlots);
        Check(oldComponents.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value),"active scene components, transforms and material assignments unchanged");
        Check(untouched.All(p=>File.Exists(p.Key)&&Hash(p.Key)==p.Value),"all model, prefab, texture, importer, skybox, and material GUID files unchanged");
        log.AppendLine("Converted "+targets.Length+" materials. TOTAL "+passed+" PASS / "+failed+" FAIL");File.WriteAllText("Logs/StylizedDarkCastleMaterialRepair.txt",log.ToString());CaptureSamples();SceneView.RepaintAll();Debug.Log("Castle material repair: "+targets.Length+" converted; "+failed+" validation failures. See Logs/StylizedDarkCastleMaterialRepair.txt");
    }
    [MenuItem("Tools/Assets/Stylized Dark Castle/Capture Repaired Prefab Samples")]
    public static void CaptureSamples()
    {
        Directory.CreateDirectory("Logs");
        foreach(string file in new[]{"CastlePartBase/Wall_Window.prefab","CastlePartBase/Roof_Center.prefab","CastleInterior/Bed_Double.prefab"})
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"/Prefabs/"+file);var instance=UnityEngine.Object.Instantiate(asset);preview.AddSingleGO(instance);var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                float radius=bounds.extents.magnitude;var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.16f,.20f);camera.fieldOfView=35;camera.nearClipPlane=.01f;camera.farClipPlane=Mathf.Max(100,radius*10);camera.transform.position=bounds.center+new Vector3(1,.7f,-1.5f).normalized*radius*3.7f;camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(40,35,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(20,210,0);preview.ambientColor=new Color(.4f,.4f,.4f);
                preview.BeginStaticPreview(new Rect(0,0,800,650));preview.Render(true);image=preview.EndStaticPreview();File.WriteAllBytes("Logs/StylizedDarkCastle_"+Path.GetFileNameWithoutExtension(file)+".png",image.EncodeToPNG());
            }
            finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
#endif
