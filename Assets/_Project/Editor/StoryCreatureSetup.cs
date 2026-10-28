#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class StoryCreatureSetup
{
    public const string WolfPack = "Assets/Hatogame_new/BossMonsterPack1/Wolfboss";
    public const string WolfSource = WolfPack + "/Mesh/Wolfboss_skin.FBX";
    public const string SpiderSource = "Assets/Assets_Imported/Giant_Spider_Lite/Giant_Spider_Lite.fbx";
    public const string Folder = "Assets/_Project/Enemies/StoryCreatures";

    [MenuItem("Tools/Story Creatures/Inspect Imported Wolf and Spider")]
    public static void Inspect()
    {
        var report = new StringBuilder("Imported story creatures " + DateTime.Now.ToString("s") + "\n");
        foreach (var path in new[] { WolfSource, SpiderSource })
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report.AppendLine("SOURCE " + path);
            if (model == null) { report.AppendLine("MODEL NOT IMPORTED"); continue; }
            var instance = UnityEngine.Object.Instantiate(model);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                if (clips.Length > 0) clips[0].SampleAnimation(instance, 0);
                report.AppendLine("BOUNDS " + NpcMerchantSetup.GeometryBounds(instance));
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    report.AppendLine($"ANIMATOR {animator.name} avatar={animator.avatar?.name} valid={animator.avatar?.isValid} human={animator.isHuman}");
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    report.AppendLine($"MESH {skin.name} vertices={skin.sharedMesh?.vertexCount} bones={skin.bones.Length} bounds={skin.bounds} root={skin.rootBone?.name}");
                var paths = instance.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t,instance.transform)).ToHashSet();
                foreach (var clip in clips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    report.AppendLine($"CLIP {clip.name} length={clip.length:F3} loop={clip.isLooping} legacy={clip.legacy} events={clip.events.Length} bindings={bindings.Length} matching={bindings.Count(b=>paths.Contains(b.path))}");
                    report.AppendLine("  " + string.Join(" | ",bindings.Select(b=>b.path).Distinct().Take(5)));
                }
                foreach (var material in instance.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct())
                {
                    if (material == null) { report.AppendLine("NULL MATERIAL"); continue; }
                    report.AppendLine($"MATERIAL {material.name} shader={material.shader?.name} path={AssetDatabase.GetAssetPath(material)}");
                    foreach (var property in material.GetTexturePropertyNames())
                        if (material.GetTexture(property) is Texture texture) report.AppendLine($"  {property}={texture.name} {texture.width}x{texture.height} {AssetDatabase.GetAssetPath(texture)}");
                }
                report.AppendLine("NODES " + string.Join(",",instance.GetComponentsInChildren<Transform>(true).Select(t=>t.name).Take(100)));
                foreach(var node in instance.GetComponentsInChildren<Transform>(true).Where(t=>t.name.IndexOf("head",StringComparison.OrdinalIgnoreCase)>=0||t.parent==instance.transform))
                    report.AppendLine("POSITION "+node.name+" "+node.position.ToString("F3"));
                if(path==WolfSource)
                {
                    foreach(string file in Directory.GetFiles(WolfPack+"/Animation","*.FBX"))foreach(var c in AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")))
                    {
                        var bindings=AnimationUtility.GetCurveBindings(c);
                        report.AppendLine($"WOLF CLIP {file} take={c.name} length={c.length:F3} loop={c.isLooping} legacy={c.legacy} events={c.events.Length} match={bindings.Count(b=>paths.Contains(b.path))}/{bindings.Length}");
                        report.AppendLine("  "+string.Join(" | ",bindings.Select(b=>b.path).Distinct().Take(3)));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        foreach(var dependency in AssetDatabase.GetDependencies("Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab",true).Where(p=>p.Contains("SpaceRifle")))report.AppendLine("PLAYER DEPENDENCY "+dependency);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/StoryCreatureSources.txt",report.ToString());
        Debug.Log("Wolf and Giant Spider source inspection saved to Logs/StoryCreatureSources.txt");
    }


}
#endif
