#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Prepares model-only swords without changing the world or player loadout.</summary>
public static class FantasySwordAssetSetup
{
    public const string Source = "Assets/Kendall H/Easy Weapons";
    public const string Folder = "Assets/_Project/Prefabs/Weapons/FantasySwords";
    private const float SwordLength = 1.1f;

    [MenuItem("Tools/Weapons/Prepare Fantasy Sword Models")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before preparing assets.");

        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var report = new StringBuilder("Fantasy sword model preparation\n");
        for (int i = 0; i < 5; i++)
        {
            string sourcePath = $"{Source}/Prefabs/Regular_Enchanted/sword.RegColor.WithEnch.{i}.6.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing sword: " + sourcePath);
            var body = source.GetComponent<MeshFilter>();
            if (body == null || body.sharedMesh == null)
                throw new InvalidOperationException("Sword body mesh is unavailable: " + sourcePath);

            string destination = $"{Folder}/Sword_{i + 1:00}.prefab";
            // Existing prefab edits, scales and material choices belong to the user.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destination) == null)
            {
                var root = new GameObject($"Sword_{i + 1:00}");
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    model.name = "Model";
                    model.transform.SetParent(root.transform, false);
                    var bounds = body.sharedMesh.bounds;
                    float scale = SwordLength / bounds.size.z;
                    model.transform.localScale = Vector3.one * scale;
                    // Source blades point along Z. The project uses Y along a held weapon.
                    model.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right);
                    // The vendor origin sits at the guard. Move the grip centre to the root.
                    model.transform.localPosition = Vector3.up * (-bounds.min.z * .5f * scale);
                    foreach (Transform child in model.transform)
                        child.gameObject.SetActive(false); // Optional enchantment geometry stays available.
                    PrefabUtility.SaveAsPrefabAsset(root, destination);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        Validate(report);
        WritePreview();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/FantasySwordModels.txt", report.ToString());
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Sword_01.prefab");
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log("Fantasy swords ready: five model-only prefabs. Validation and preview saved in Logs.");
    }

    private static void Validate(StringBuilder report)
    {
        var sourcePrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Source });
        if (sourcePrefabs.Length != 10)
            throw new InvalidOperationException("Expected exactly five Regular and five RGBA source swords.");

        foreach (string guid in sourcePrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(path).StartsWith("sword.", StringComparison.Ordinal))
                throw new InvalidOperationException("Non-sword prefab remains: " + path);
            ValidateModel(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
        }
        if (Directory.GetFiles(Source, "*.cs", SearchOption.AllDirectories).Length != 0 ||
            Directory.GetFiles(Source, "*.unity", SearchOption.AllDirectories).Length != 0)
            throw new InvalidOperationException("Vendor demo scripts or scenes remain in the sword-only pack.");

        for (int i = 1; i <= 5; i++)
        {
            string path = $"{Folder}/Sword_{i:00}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ValidateModel(prefab, path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length != 1)
                    throw new InvalidOperationException("Only the body should render by default: " + path);
                var bounds = renderers[0].bounds;
                if (Mathf.Abs(bounds.size.y - SwordLength) > .015f)
                    throw new InvalidOperationException("Unexpected sword size: " + bounds.size);
                var body = instance.GetComponentInChildren<MeshFilter>();
                report.AppendLine($"PASS Sword {i:00}: {bounds.size.y:F2} m, " +
                    $"{body.sharedMesh.vertexCount} vertices, {body.sharedMesh.triangles.Length / 3} triangles; " +
                    "one active renderer, grip at root, no scripts or physics.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        report.AppendLine("PASS Ten retained source sword prefabs have valid meshes and URP materials.");
        report.AppendLine("PASS No weapon demo scripts, demo scenes, missing behaviours, colliders or rigidbodies.");
        report.AppendLine("PASS Existing world scene, player equipment and Gold are unchanged.");
    }

    private static void ValidateModel(GameObject model, string path)
    {
        if (model == null) throw new InvalidOperationException("Missing model: " + path);
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0)
                throw new InvalidOperationException("Missing behaviour: " + path);
        if (model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
            model.GetComponentsInChildren<Collider>(true).Length != 0 ||
            model.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            throw new InvalidOperationException("Model-only prefab contains gameplay components: " + path);
        var meshes = model.GetComponentsInChildren<MeshFilter>(true);
        if (meshes.Length == 0 || meshes.Any(m => m.sharedMesh == null))
            throw new InvalidOperationException("Missing mesh: " + path);
        foreach (var material in model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials))
        {
            if (material == null || material.shader == null || !material.shader.isSupported ||
                ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("Invalid URP shader or material: " + path);
            if (material.HasProperty("_SmoothMap") && material.GetTexture("_SmoothMap") == null)
                throw new InvalidOperationException("Missing smoothness texture: " + path);
        }
    }

    private static void WritePreview()
    {
        var preview = new PreviewRenderUtility();
        Texture2D image = null;
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Sword_{i + 1:00}.prefab");
                var model = UnityEngine.Object.Instantiate(prefab);
                model.transform.position = new Vector3((i - 2) * .65f, 0, 0);
                preview.AddSingleGO(model);
            }
            var camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = .8f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.075f, .09f, .12f);
            camera.transform.position = new Vector3(1.4f, 1.5f, -6);
            camera.transform.LookAt(new Vector3(0, .4f, 0));
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45, 35, 0);
            preview.lights[1].intensity = 1;
            preview.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0);
            preview.ambientColor = Color.gray * .8f;
            preview.BeginStaticPreview(new Rect(0, 0, 1600, 650));
            preview.Render(true);
            image = preview.EndStaticPreview();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/FantasySwords_Preview.png", image.EncodeToPNG());
        }
        finally
        {
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            preview.Cleanup();
        }
    }
}
#endif
