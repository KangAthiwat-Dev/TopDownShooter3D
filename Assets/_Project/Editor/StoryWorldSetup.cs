#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class StoryWorldSetup
{
    public const string Folder = "Assets/_Project/Story";
    public const string Armor = "Assets/Assets_Imported/Blink/Art/Characters/Stylized/Humans/Armors_Humans/Meshes_ArmorsHumans/StarterArmor_HumanMale.fbx";
    [MenuItem("Tools/Story/Inspect Character and Map Sources")]
    public static void Inspect()
    {
        var report = new StringBuilder("Story assets and current world\n");
        foreach (var path in new[] { Armor, "Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab", NpcMerchantSetup.PrefabPath })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); report.AppendLine(path); if(asset==null){report.AppendLine("Asset not found");continue;}
            foreach (var a in asset.GetComponentsInChildren<Animator>(true)) report.AppendLine("Animator " + a.name + " avatar=" + a.avatar?.name + " human=" + a.isHuman);
            foreach (var skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true)) report.AppendLine($"Skin {skin.name} vertices={skin.sharedMesh?.vertexCount} root={skin.rootBone?.name} bones={skin.bones.Length} materials={string.Join(",", skin.sharedMaterials.Select(m => m?.name))}");
            var renderers = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.AppendLine("BONES " + string.Join(",", renderers.SelectMany(r => r.bones).Where(b => b != null).Select(b => b.name).Distinct()));
        }
        var layout = UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        report.AppendLine($"Map {layout.minimum} to {layout.maximum} terrain={layout.terrain.transform.position} size={layout.terrain.terrainData.size}");
        foreach (var t in new[] { layout.city, layout.village, layout.sanctuary, layout.castle }) report.AppendLine("Landmark " + t?.name + " " + t?.position);
        var cliffs = UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>(); report.AppendLine("Castle helper " + cliffs?.castleFootprint?.position);
        foreach (var city in UnityEngine.Object.FindObjectsByType<CityAssemblyRoot>(FindObjectsSortMode.None)) report.AppendLine("City root " + city.transform.position);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/StoryAssetInspection.txt", report.ToString()); Debug.Log("Story character and current-map inspection saved.");
    }
}
#endif
