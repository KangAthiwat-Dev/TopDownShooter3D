#if UNITY_EDITOR
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

public static partial class CityAssemblySetup
{
    private static readonly string[] UrbanNames = { "Floor_city", "Way_city", "Wall_city", "Building_city", "Prop_city", "Store_city",
        "City_Streets_Construction_Armory", "NW_RedRoof_Quarter", "Town_NPC_Population", "City_Music_Zone", "NPC_WeaponMerchant" };

    [MenuItem("Tools/World/City Assembly/Group Entire City and Ground It")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
        if (UnityEngine.Object.FindAnyObjectByType<CityAssemblyRoot>() != null) throw new InvalidOperationException("City_All already exists; move that parent or use its grounding command.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype first.");
        var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
        var map = UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        var candidates = all.Where(t => UrbanNames.Contains(t.name)).Append(map.city).Distinct().ToArray();
        var roots = candidates.Where(t => !candidates.Any(other => other != t && t.IsChildOf(other))).ToArray();
        if (!roots.Any(t => t.name == "Wall_city") || !roots.Any(t => t.name == "City_Streets_Construction_Armory"))
            throw new InvalidOperationException("The authored city groups are missing.");
        bool Urban(Transform t) => roots.Any(root => t == root || t.IsChildOf(root));
        var urbanTransforms = all.Where(Urban).ToArray();
        var oldParents = roots.Select(t => t.parent).Where(t => t != null).Append(map.transform).ToHashSet();
        var outside = all.Where(t => !Urban(t)).SelectMany(t => t.GetComponents<Component>())
            .Where(c => c != null && !(c is NavMeshSurface) && !(c is Transform tr && oldParents.Contains(tr)))
            .ToDictionary(c => c, EditorJsonUtility.ToJson);
        var before = all.ToDictionary(t => t, t => t.localToWorldMatrix);
        string terrainPath = AssetDatabase.GetAssetPath(map.terrain.terrainData), terrainHash = Hash(terrainPath);
        Directory.CreateDirectory("Logs/SceneBackups");
        string backup = "Logs/SceneBackups/WorldPrototype_BeforeCityParent_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Cannot back up the scene.");
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Group and ground entire city");
        var rootObject = new GameObject("City_All");
        rootObject.transform.SetParent(map.transform, false);
        rootObject.transform.SetPositionAndRotation(new Vector3(75.7626f, 5f, -72.7745f), Quaternion.identity);
        rootObject.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(rootObject, "Create movable city parent");
        var rootTransform = rootObject.transform;
        foreach (var group in roots) Undo.SetTransformParent(group, rootTransform, "Group city objects");
        var assembly = rootObject.AddComponent<CityAssemblyRoot>();
        Ref(assembly, "terrain", map.terrain);
        foreach (var music in rootObject.GetComponentsInChildren<TownMusicZone>(true))
        {
            Undo.RecordObject(music, "Make city music follow parent");
            var fields = new SerializedObject(music);
            fields.FindProperty("center").vector3Value = rootTransform.InverseTransformPoint(fields.FindProperty("center").vector3Value);
            fields.FindProperty("boundsFrame").objectReferenceValue = rootTransform; fields.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var population in rootObject.GetComponentsInChildren<TownNpcPopulation>(true))
        {
            Undo.RecordObject(population, "Make NPC bounds follow city");
            var fields = new SerializedObject(population);
            fields.FindProperty("cityCenter").vector3Value = rootTransform.InverseTransformPoint(fields.FindProperty("cityCenter").vector3Value);
            fields.FindProperty("cityFrame").objectReferenceValue = rootTransform; fields.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var station in rootObject.GetComponentsInChildren<TownNpcWorkStation>(true))
        {
            Undo.RecordObject(station, "Make construction work bounds follow city");
            var fields = new SerializedObject(station); var center = fields.FindProperty("workAreaCenter").vector2Value;
            var local = rootTransform.InverseTransformPoint(new Vector3(center.x, station.transform.position.y, center.y));
            fields.FindProperty("workAreaCenter").vector2Value = new Vector2(local.x, local.z);
            fields.FindProperty("boundsFrame").objectReferenceValue = rootTransform; fields.ApplyModifiedPropertiesWithoutUndo();
        }
        var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube); foundation.name = "City_Foundation";
        foundation.transform.SetParent(rootTransform, false); foundation.layer = LayerMask.NameToLayer("Environment");
        UnityEngine.Object.DestroyImmediate(foundation.GetComponent<Collider>());
        foundation.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/World/CityExpansion/City_Light_Kerb.mat");
        Ref(assembly, "foundation", foundation.transform);
        if (!assembly.GroundToTerrain()) throw new InvalidOperationException("City footprint is not over its terrain.");
        var delta = rootTransform.position - new Vector3(75.7626f, 5f, -72.7745f);
        assembly.GroundAndRebuildNavigation();

        var report = new StringBuilder("Movable city assembly " + DateTime.Now.ToString("s") + "\nBackup=" + backup + "\n");
        bool passed = true;
        void Check(bool valid, string message) { passed &= valid; report.AppendLine((valid ? "PASS " : "FAIL ") + message); }
        bool SameMatrix(Matrix4x4 a, Matrix4x4 b) => Enumerable.Range(0, 16).All(i => Mathf.Abs(a[i] - b[i]) < .002f);
        var shift = Matrix4x4.Translate(delta);
        Check(urbanTransforms.All(t => t != null && t.IsChildOf(rootTransform) && SameMatrix(t.localToWorldMatrix, shift * before[t])),
            urbanTransforms.Length + " original city transforms keep their relative layout, rotation and size");
        Check(all.Where(t => !Urban(t)).All(t => SameMatrix(t.localToWorldMatrix, before[t])), "all non-city world placements preserved");
        Check(outside.All(pair => pair.Key != null && EditorJsonUtility.ToJson(pair.Key) == pair.Value), "non-city components preserved (navigation reference deliberately refreshed)");
        Check(Hash(terrainPath) == terrainHash, "terrain heights, textures, trees and details unchanged");
        Check(UrbanNames.Where(name => all.Any(t => t.name == name)).All(name => all.Where(t => t.name == name).All(t => t.IsChildOf(rootTransform))),
            "walls, floors, roads, buildings, props, shops, merchant, citizens and music are inside one city parent");
        report.AppendLine("Parent=" + PathOf(rootTransform) + "\nCity delta=" + delta.ToString("F3") + "\nFloor=" + assembly.FloorHeight.ToString("F3"));
        foreach (var group in roots) report.AppendLine("GROUP " + group.name);
        foreach (var item in rootObject.GetComponentsInChildren<Component>(true))
            if (item != null) { EditorUtility.SetDirty(item); if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item); }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Undo.CollapseUndoOperations(undo);
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/CityAssemblyBuild.txt", report.ToString());
        Debug.Log("City_All assembled and grounded. Passed=" + passed + "; floor=" + assembly.FloorHeight.ToString("F3"));
    }

    private static string Hash(string path)
    { using (var sha = System.Security.Cryptography.SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)); }
    private static void Ref(UnityEngine.Object target, string property, UnityEngine.Object value)
    { var fields = new SerializedObject(target); fields.FindProperty(property).objectReferenceValue = value; fields.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
