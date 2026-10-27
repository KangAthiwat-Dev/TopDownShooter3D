#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static partial class TownNpcDiversitySetup
{
    public const string Folder = "Assets/_Project/NPCs/TownDiversity";
    public const string FemaleBody = NpcMerchantSetup.Outfits + "/Exports/FBX (Unity)/Outfits/Female_Peasant.fbx";
    public const string FemaleHead = NpcMerchantSetup.Base + "/Base Characters/Unity/Superhero_Female_FullBody.fbx";
    public const string HairRoot = NpcMerchantSetup.Base + "/Hairstyles/Rigged to Head Bone/FBX (Unity)/";
    public const string Ual2 = "Assets/_Project/Art/ThirdParty/Quaternius/UAL2/UAL2_Standard.fbx";
    public static readonly Vector3[][] Stations = {
        new[] { new Vector3(70.8f,5.14f,-78.5f), new Vector3(72f,5.14f,-82), new Vector3(70.8f,5.14f,-76) },
        new[] { new Vector3(62.2f,5.14f,-85.4f), new Vector3(59,5.14f,-89), new Vector3(64.5f,5.14f,-85) },
        new[] { new Vector3(68,5.14f,-90), new Vector3(63.5f,5.14f,-88.5f), new Vector3(70,5.14f,-87) }
    };
    [MenuItem("Tools/NPC/Women and Builders/Inspect Sources and Work Routes")]
    public static void Inspect()
    {
        var report = new StringBuilder("Women / construction source audit " + DateTime.Now.ToString("s") + "\n");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var path in new[] { FemaleBody, FemaleHead, HairRoot + "Hair_Long.fbx", HairRoot + "Hair_Buns.fbx" })
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                report.AppendLine("SOURCE " + path);
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    report.AppendLine("  " + skin.name + " bounds=" + skin.bounds + " bones=" + skin.bones.Length);
                    foreach (var material in skin.sharedMaterials) report.AppendLine("    " + material.name + " map=" + AssetDatabase.GetAssetPath(material.mainTexture));
                }
                foreach (var bone in model.GetComponentsInChildren<Transform>().Where(t => t.name == "Head" || t.name == "neck_01")) report.AppendLine("  BONE " + bone.name + " " + bone.position);
                UnityEngine.Object.DestroyImmediate(model);
            }
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(Ual2).OfType<AnimationClip>().Where(c => c.name == "Armature|TreeChopping_Loop" || c.name == "Armature|Walk_Carry_Loop"))
                report.AppendLine(clip.name + " human=" + clip.isHumanMotion + " length=" + clip.length);
            foreach (var p in Stations.SelectMany(s => s)) report.AppendLine("WORK " + p + " sample=" + NavMesh.SamplePosition(p, out var nav, 1.2f, NavMesh.AllAreas) + " nav=" + nav.position);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TownDiversityInspection.txt", report.ToString()); Debug.Log("Women / builder source audit saved.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif
