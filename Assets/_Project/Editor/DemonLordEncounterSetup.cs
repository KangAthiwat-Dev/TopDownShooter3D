#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class DemonLordEncounterSetup
{
    private const string Folder = "Assets/_Project/World/DemonLordEncounter";
    private const string DragonPack = "Assets/FourEvilDragonsPBR";
    private const string DemonPack = "Assets/Character/DemonLord";
    private static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

    [MenuItem("Tools/Enemies/Demon Lord/Inspect Encounter Sources")]
    public static void Inspect()
    {
        var report = new StringBuilder();
        var lord = GameObject.Find("DemonLord");
        report.AppendLine("DemonLord position=" + lord.transform.position + "; bounds=" + BoundsOf(lord));
        foreach (var name in new[] { "Cylinder (1)", "Cylinder (2)" })
        {
            var ground = GameObject.Find("World/Map_WindingForest/DemonLava_LiquidLake_And_ChannelStreams/" + name);
            report.AppendLine(name + " position=" + ground.transform.position + "; bounds=" + BoundsOf(ground) + "; rotation=" + ground.transform.eulerAngles);
        }
        var preview = new PreviewRenderUtility();
        try
        {
            var dragon = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DragonPack + "/Prefab/DragonNightmare/Red.prefab"));
            preview.AddSingleGO(dragon); dragon.transform.position = Vector3.zero;
            report.AppendLine("Dragon bounds=" + BoundsOf(dragon) + "; scale=" + dragon.transform.localScale);
            foreach (var animator in dragon.GetComponentsInChildren<Animator>())
                report.AppendLine("Dragon animator=" + animator.name + "; avatar=" + animator.avatar + "; controller=" + animator.runtimeAnimatorController);
            foreach (var bone in dragon.GetComponentsInChildren<Transform>().Where(t => t.name == "Head" || t.name == "Root" || t.name == "Chest"))
                report.AppendLine("Dragon bone " + bone.name + " position=" + bone.position);
            foreach (var file in new[] { "idle01", "walk", "run", "Basic Attack", "Claw Attack", "Horn Attack", "scream", "die" })
            {
                var clip = Clip(DragonPack + "/Animations/DragonNightMare/" + file + ".fbx");
                report.AppendLine(file + " clip=" + clip.name + "; length=" + clip.length + "; loop=" + clip.isLooping + "; root motion=" + clip.hasRootCurves);
            }
        }
        finally { preview.Cleanup(); }
        if (NavMesh.SamplePosition(lord.transform.position, out var hit, 2, NavMesh.AllAreas))
            report.AppendLine("DemonLord nav=" + hit.position);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/DemonLordEncounterInspection.txt", report.ToString());
        Debug.Log("DemonLord/Dragon source inspection saved.");
    }
}
#endif
