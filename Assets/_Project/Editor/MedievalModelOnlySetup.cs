#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-shot, backed-up removal of the imported kit's optional C# tooling.</summary>
public static class MedievalModelOnlySetup
{
    public const string Package = "Assets/Assets_Imported/MedievalVillageKitUnity/com.danielgruginski.medievalkit";
    private const string StatePath = "Logs/MedievalModelOnlyState.json";
    private const string ReportPath = "Logs/MedievalModelOnlyValidation.txt";

    [Serializable] private sealed class FileHash { public string path; public string sha256; }
    [Serializable] private sealed class State
    {
        public string backup;
        public string scene;
        public int prefabCount;
        public int removedComponents;
        public int codeFiles;
        public int sceneComponents;
        public int renderers;
        public int colliders;
        public List<FileHash> retainedFiles = new List<FileHash>();
        public List<string> archivedGuids = new List<string>();
        public List<string> movedPaths = new List<string>();
    }

    private static bool KitScript(MonoBehaviour component)
    {
        if (component == null) return false;
        MonoScript script = MonoScript.FromMonoBehaviour(component);
        return script != null && AssetDatabase.GetAssetPath(script).StartsWith(Package + "/", StringComparison.Ordinal);
    }

    private static IEnumerable<Component> SceneComponents(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true));

    private static Dictionary<string, string> SceneSnapshot(Scene scene) => SceneComponents(scene)
        .Where(c => c != null && !(c is MonoBehaviour mb && KitScript(mb)))
        .ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));

    private static string NativeSnapshot(GameObject root) => string.Join("\n", root.GetComponentsInChildren<Component>(true)
        .Where(c => c != null && !(c is MonoBehaviour))
        .Select(c => c.GetType().FullName + ":" + EditorJsonUtility.ToJson(c)));

    private static string HashFile(string path)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        foreach (string folder in Directory.GetDirectories(source)) CopyTree(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }

    private static List<string> ArchiveTargets()
    {
        var targets = new List<string> { "Editor", "Runtime", "Walker", "Data", "Documentation~", "Generated/Levels", "package.json", "README.md" }
            .Select(p => Package + "/" + p).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        targets.AddRange(Directory.GetFiles(Package + "/Generated", "*.asset"));
        string instructions = "Assets/Assets_Imported/MedievalVillageKitUnity/INSTALL.txt";
        if (File.Exists(instructions)) targets.Add(instructions);
        return targets;
    }

    private static void MoveOutsideAssets(string source, string archive)
    {
        string destination = archive + "/RemovedFromAssets/" + source;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination);
        if (File.Exists(source + ".meta")) File.Move(source + ".meta", destination + ".meta");
    }

    [MenuItem("Tools/Asset Cleanup/Medieval Kit/Convert to Model Only")]
    private static void Convert()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (File.Exists(StatePath)) throw new InvalidOperationException("Conversion already recorded; run Validate instead.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("/WorldPrototype.unity", StringComparison.Ordinal)) throw new InvalidOperationException("Open WorldPrototype first.");
        if (!Directory.Exists(Package + "/Runtime")) throw new InvalidOperationException("Expected unmodified kit source is missing.");
        if (SceneComponents(scene).Any(c => c == null)) throw new InvalidOperationException("Scene already contains a missing script; resolve it before converting.");

        var state = new State { backup = "Backups/MedievalVillageKit_ModelOnly_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), scene = scene.path };
        Directory.CreateDirectory(state.backup);
        if (!EditorSceneManager.SaveScene(scene, state.backup + "/WorldPrototype_BeforeModelOnly.unity", true)) throw new IOException("Live scene backup failed.");
        CopyTree("Assets/Assets_Imported/MedievalVillageKitUnity", state.backup + "/OriginalPackage");
        foreach (string file in Directory.GetFiles("Assets/Assets_Imported/MedievalVillageKitUnity", "*", SearchOption.AllDirectories))
        {
            string copy = state.backup + "/OriginalPackage/" + file.Substring("Assets/Assets_Imported/MedievalVillageKitUnity/".Length);
            if (HashFile(file) != HashFile(copy)) throw new IOException("Backup hash mismatch: " + file);
        }
        File.Copy("Packages/manifest.json", state.backup + "/manifest.before.json");
        File.Copy("Packages/packages-lock.json", state.backup + "/packages-lock.before.json");
        CitySurfaceAlignment.Capture();
        File.Copy("Logs/CitySurfaces_Aligned.png", state.backup + "/City_BeforeModelOnly.png");

        var targets = ArchiveTargets();
        foreach (string target in targets)
        {
            var metas = Directory.Exists(target) ? Directory.GetFiles(target, "*.meta", SearchOption.AllDirectories).ToList() : new List<string>();
            if (File.Exists(target + ".meta")) metas.Add(target + ".meta");
            foreach (string meta in metas)
            {
                string guid = AssetDatabase.AssetPathToGUID(meta.Substring(0, meta.Length - 5));
                if (!string.IsNullOrEmpty(guid)) state.archivedGuids.Add(guid);
            }
        }
        state.archivedGuids = state.archivedGuids.Distinct().ToList();
        state.codeFiles = Directory.GetFiles(Package, "*.cs", SearchOption.AllDirectories).Length;
        foreach (string folder in new[] { "Art", "Shaders", "Generated/Materials" })
            foreach (string file in Directory.GetFiles(Package + "/" + folder, "*", SearchOption.AllDirectories))
                state.retainedFiles.Add(new FileHash { path = file.Replace('\\', '/'), sha256 = HashFile(file) });

        var beforeScene = SceneSnapshot(scene);
        state.sceneComponents = beforeScene.Count;
        state.renderers = SceneComponents(scene).OfType<Renderer>().Count();
        state.colliders = SceneComponents(scene).OfType<Collider>().Count();
        string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Package + "/Generated/Prefabs", Package + "/Generated/Structures" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        state.prefabCount = prefabs.Length;
        if (prefabs.Length == 0) throw new InvalidOperationException("No kit prefabs found.");

        try
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (i % 25 == 0)
                {
                    EditorUtility.DisplayProgressBar("Medieval Kit model-only", "Preserving meshes, materials and colliders: " + i + "/" + prefabs.Length, (float)i / prefabs.Length);
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/MedievalModelOnlyProgress.txt", DateTime.Now.ToString("s") + "\nCleaning prefab " + i + "/" + prefabs.Length);
                }
                GameObject root = PrefabUtility.LoadPrefabContents(prefabs[i]);
                try
                {
                    if (root.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Existing missing script: " + prefabs[i]);
                    string native = NativeSnapshot(root);
                    var components = root.GetComponentsInChildren<MonoBehaviour>(true).Where(KitScript).ToArray();
                    foreach (var component in components) Object.DestroyImmediate(component);
                    if (native != NativeSnapshot(root)) throw new InvalidOperationException("Native geometry changed: " + prefabs[i]);
                    if (components.Length > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabs[i], out bool saved);
                        if (!saved) throw new IOException("Cannot save cleaned prefab: " + prefabs[i]);
                        state.removedComponents += components.Length;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // Also detach explicitly added/overridden kit components, without unpacking scene instances.
            foreach (var component in SceneComponents(scene).OfType<MonoBehaviour>().Where(KitScript).ToArray()) Object.DestroyImmediate(component);
            var afterScene = SceneSnapshot(scene);
            if (beforeScene.Count != afterScene.Count || beforeScene.Any(p => !afterScene.TryGetValue(p.Key, out string value) || value != p.Value))
                throw new InvalidOperationException("A non-kit scene component changed; original scene and package are backed up in " + state.backup);
            foreach (var file in state.retainedFiles)
                if (HashFile(file.path) != file.sha256) throw new IOException("Retained art/material/shader changed: " + file.path);

            // Refuse to archive anything still required by the retained models or project scenes/settings.
            var assetRoots = prefabs.Concat(AssetDatabase.FindAssets("", new[] { "Assets/_Project", "Assets/Settings", "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => new[] { ".unity", ".asset", ".prefab", ".mat" }.Contains(Path.GetExtension(p)))).Distinct();
            foreach (string dependency in AssetDatabase.GetDependencies(assetRoots.ToArray(), true))
                if (state.archivedGuids.Contains(AssetDatabase.AssetPathToGUID(dependency)))
                    throw new InvalidOperationException("An asset still requires code/demo data: " + dependency + ". Nothing has been archived yet.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save cleaned scene.");
            AssetDatabase.SaveAssets();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string target in targets) { MoveOutsideAssets(target, state.backup); state.movedPaths.Add(target); }
                Directory.CreateDirectory("Logs");
                File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
            }
            finally { AssetDatabase.StopAssetEditing(); }
            File.WriteAllText("Logs/MedievalModelOnlyProgress.txt", "Conversion complete. Backup: " + state.backup + "\nAwaiting script reload and validation.");
            AssetDatabase.Refresh();
            Debug.Log("Medieval Kit converted to model-only. Backup: " + state.backup + ". Run Tools > Asset Cleanup > Medieval Kit > Validate Model Only after reload.");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    [MenuItem("Tools/Asset Cleanup/Medieval Kit/Validate Model Only")]
    public static void Validate()
    {
        var report = new StringBuilder("Medieval Kit model-only validation " + DateTime.Now.ToString("s") + "\n");
        bool pass = true;
        Action<bool, string> check = (ok, message) => { pass &= ok; report.AppendLine((ok ? "PASS " : "FAIL ") + message); };
        State state = JsonUtility.FromJson<State>(File.ReadAllText(StatePath));
        check(Directory.Exists(state.backup + "/OriginalPackage"), "Original package and scene backup available: " + state.backup);
        check(!Directory.GetFiles(Package, "*.cs", SearchOption.AllDirectories).Any() && !Directory.GetFiles(Package, "*.asmdef", SearchOption.AllDirectories).Any(), "No C# scripts or assembly definitions remain in model-only package");
        check(!Directory.Exists(Package + "/Generated/Levels") && !Directory.Exists(Package + "/Data"), "Demo scenes, generation data and optional tools are outside Assets");
        check(state.retainedFiles.All(f => File.Exists(f.path) && HashFile(f.path) == f.sha256), "All original models, textures, shaders, material files and their GUIDs are unchanged");
        string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Package + "/Generated/Prefabs", Package + "/Generated/Structures" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        check(prefabs.Length == state.prefabCount, "All " + state.prefabCount + " reusable model/structure prefabs retained");
        int missing = 0, scripts = 0, nullMeshes = 0, nullMaterials = 0, shaderErrors = 0;
        foreach (string prefab in prefabs)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            scripts += root.GetComponentsInChildren<MonoBehaviour>(true).Length;
            nullMeshes += root.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh == null);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) nullMaterials++;
                    else if (material.shader == null || ShaderUtil.ShaderHasError(material.shader)) shaderErrors++;
                }
        }
        check(missing == 0 && scripts == 0, "All retained prefabs are script-free with no Missing Script (removed " + state.removedComponents + " kit components / " + state.codeFiles + " C# files)");
        check(nullMeshes == 0 && nullMaterials == 0 && shaderErrors == 0, "Prefab meshes, materials and shaders resolve without errors");
        Scene scene = SceneManager.GetActiveScene();
        var components = SceneComponents(scene).ToArray();
        check(scene.path == state.scene && components.All(c => c != null) && !components.OfType<MonoBehaviour>().Any(KitScript), "Current WorldPrototype has no missing or kit script components");
        check(components.Length == state.sceneComponents && components.OfType<Renderer>().Count() == state.renderers && components.OfType<Collider>().Count() == state.colliders, "Scene component, renderer and collider counts preserved");
        check(!AssetDatabase.GetDependencies(state.scene, true).Any(p => state.archivedGuids.Contains(AssetDatabase.AssetPathToGUID(p))), "World scene has no dependency on archived scripts or demo assets");
        CitySurfaceAlignment.Capture();
        File.Copy("Logs/CitySurfaces_Aligned.png", "Logs/MedievalModelOnly_City.png", true);
        report.AppendLine(pass ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        File.WriteAllText(ReportPath, report.ToString());
        if (pass) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
    }
}
#endif
