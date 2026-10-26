#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CreatureEnemySetup
{
    public const string Folder = "Assets/_Project/Enemies/Creatures";
    public const string BearSource = "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab";
    public const string SpiderSource = "Assets/Spiders/Prefabs/Black Widow.prefab";
    private const string BearAnimations = "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Animations/";
    private const string BasePrefab = "Assets/_Project/Prefabs/Enemies/EnemyBase.prefab";

    [MenuItem("Tools/Enemies/Inspect Spider and Bear Sources")]
    public static void Inspect()
    {
        var report = new StringBuilder("Spider/Bear source audit — " + DateTime.Now.ToString("s") + "\n");
        foreach (string source in new[] { SpiderSource, BearSource })
        {
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source)); go.transform.position = Vector3.zero;
            try
            {
                SourceClip(source == SpiderSource, "Idle").SampleAnimation(go, 0.1f);
                Bounds b = BoundsOf(go);
                report.AppendLine($"SOURCE {source}: bounds center={b.center:F3}, size={b.size:F3}; audio sources={go.GetComponentsInChildren<AudioSource>(true).Length}");
                foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh, true); mesh.RecalculateBounds();
                    report.AppendLine($"  mesh {skin.name}: scale={skin.transform.lossyScale:F3}, baked local={mesh.bounds.size:F3}, authored world={skin.bounds.size:F3}");
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                foreach (Animator a in go.GetComponentsInChildren<Animator>(true)) report.AppendLine($"  Animator {a.name}: avatar={a.avatar?.name}, human={a.isHuman}, controller={a.runtimeAnimatorController?.name}");
                var paths = go.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t, go.transform)).ToHashSet();
                foreach (string name in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
                {
                    AnimationClip c = SourceClip(source == SpiderSource, name);
                    var bindings = AnimationUtility.GetCurveBindings(c); int matching = bindings.Count(binding => paths.Contains(binding.path));
                    report.AppendLine($"  {name}: clip={c.name}, length={c.length:F3}, loop={c.isLooping}, legacy={c.legacy}, events={c.events.Length}, matching transform paths={matching}/{bindings.Length}");
                    report.AppendLine("    paths: " + string.Join(" | ", bindings.Select(x => x.path).Distinct().Take(4)));
                }
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)) report.AppendLine($"  head reference {t.name} world={t.position:F3}");
                var eyes = go.GetComponentsInChildren<Renderer>(true).Where(r => r.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (eyes.Length > 0) report.AppendLine($"  eye centroid world={eyes.Aggregate(Vector3.zero, (sum, r) => sum + r.bounds.center) / eyes.Length:F3}");
                foreach (Material m in go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct()) report.AppendLine($"  material {m.name}: {m.shader.name}, texture={m.mainTexture?.name}");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio/Mobs" }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            report.AppendLine($"AUDIO {AssetDatabase.GetAssetPath(clip)} length={clip.length:F2}s, channels={clip.channels}, load={clip.loadState}");
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CreatureSources.txt", report.ToString()); Debug.Log(report.ToString());
    }

    private static AnimationClip SourceClip(bool spider, string role)
    {
        if (spider)
        {
            string file = role == "Run" ? "Walk" : role == "Attack" ? "Attack1" : role;
            return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Spiders/Animations/" + file + ".anim");
        }
        string name = "Bear_" + (role == "Walk" ? "WalkForward" : role == "Run" ? "RunForward" : role == "Attack" ? "Attack1" : role);
        var clips = AssetDatabase.LoadAllAssetsAtPath(BearAnimations + name + ".fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        return clips.SingleOrDefault(c => c.name == name) ?? throw new InvalidOperationException("Expected " + name + "; available: " + string.Join(", ", clips.Select(c => c.name)));
    }

    [MenuItem("Tools/Enemies/Install Spider and Bear")]
    public static void Install() => InstallPrepared(false);

    [MenuItem("Tools/Enemies/Resume Prepared Creature Installation")]
    public static void ResumePrepared() => InstallPrepared(true);

    private static void InstallPrepared(bool resume)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if ((!resume && AssetDatabase.IsValidFolder(Folder)) || AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Spider")) != null || AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Bear")) != null)
            throw new InvalidOperationException("Creature copies already exist. Installer will not overwrite them.");
        var spawner = UnityEngine.Object.FindAnyObjectByType<EnemyWaveSpawner>();
        if (spawner == null) throw new InvalidOperationException("Open the WorldPrototype scene with its wave spawner.");
        Directory.CreateDirectory("Logs/SceneBackups"); var scene = SceneManager.GetActiveScene();
        if (!EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/" + scene.name + "_BeforeCreatures_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true)) throw new IOException("Scene backup failed.");
        EnsureFolder(Folder); EnsureFolder(Folder + "/Materials");
        foreach (bool spider in new[] { true, false }) Build(spider);
        // Append only: existing wave entries, counts, spawn points and timings remain intact.
        var settings = new SerializedObject(spawner); var waves = settings.FindProperty("waves");
        int index = waves.arraySize; waves.InsertArrayElementAtIndex(index);
        var wave = waves.GetArrayElementAtIndex(index); wave.FindPropertyRelative("delayAfterWave").floatValue = 3;
        var entries = wave.FindPropertyRelative("enemies"); entries.arraySize = 2;
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Spider"));
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 3;
        entries.GetArrayElementAtIndex(1).FindPropertyRelative("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Bear"));
        entries.GetArrayElementAtIndex(1).FindPropertyRelative("count").intValue = 1;
        Undo.RecordObject(spawner, "Append creature encounter"); settings.ApplyModifiedProperties(); EditorUtility.SetDirty(spawner);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Validate(); Capture();
    }

    private static void Build(bool spider)
    {
        string kind = spider ? "Spider" : "Bear"; EnsureFolder(Folder + "/" + kind);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string role in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
        {
            string clipPath = Folder + "/" + kind + "/" + kind + "_" + role + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool create = clip == null;
            if (create) clip = UnityEngine.Object.Instantiate(SourceClip(spider, role));
            clip.name = kind + "_" + role;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = role == "Idle" || role == "Walk" || role == "Run";
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            if (create) AssetDatabase.CreateAsset(clip, clipPath); else EditorUtility.SetDirty(clip);
            clips.Add(role, clip);
        }
        string controllerPath = Folder + "/" + kind + "/" + kind + "Animator.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null && !AssetDatabase.CopyAsset("Assets/_Project/Animations/Enemies/EnemyAnimator.controller", controllerPath)) throw new IOException("Cannot copy enemy controller.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var locomotion = states.Single(s => s.name == "Locomotion").motion as BlendTree;
        locomotion.useAutomaticThresholds = false;
        locomotion.children = new[] {
            new ChildMotion { motion = clips["Idle"], threshold = 0, timeScale = 1, directBlendParameter = "Speed" },
            new ChildMotion { motion = clips["Walk"], threshold = 0.5f, timeScale = 1, directBlendParameter = "Speed" },
            new ChildMotion { motion = clips["Run"], threshold = 1, timeScale = spider ? 2.2f : 1, directBlendParameter = "Speed" }
        };
        float attackPlayback = spider ? 1.8f : 1f;
        states.Single(s => s.name == "Attack").motion = clips["Attack"];
        states.Single(s => s.name == "Attack").speed = attackPlayback;
        states.Single(s => s.name == "Death").motion = clips["Death"];
        EditorUtility.SetDirty(locomotion); foreach (var state in states) EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);

        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab)); root.name = "Enemy_" + kind; root.transform.position = Vector3.zero;
        try
        {
            var wrapper = new GameObject("CreatureVisual").transform; wrapper.SetParent(root.transform.Find("ModelRoot"), false);
            // Both source rigs face -Z; gameplay sight/attack/navigation use +Z.
            wrapper.localRotation = Quaternion.Euler(0, 180, 0);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(spider ? SpiderSource : BearSource), wrapper);
            model.transform.localPosition = Vector3.zero;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clips["Idle"].SampleAnimation(model, 0.05f);
            Bounds bounds = BoundsOf(model);
            float factor = spider ? 2.1f / Mathf.Max(bounds.size.x, bounds.size.z) : 1.9f / bounds.size.y;
            wrapper.localScale = Vector3.one * factor;
            bounds = BoundsOf(model); wrapper.localPosition -= Vector3.up * bounds.min.y;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 6;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => ConvertMaterial(m, kind)).ToArray();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            var agent = root.GetComponent<NavMeshAgent>(); agent.speed = spider ? 4.6f : 4f; agent.height = spider ? 1.1f : 2.1f; agent.radius = spider ? 0.55f : 0.8f;
            var capsule = root.GetComponent<CapsuleCollider>(); capsule.height = spider ? 0.9f : 1.9f; capsule.radius = spider ? 0.4f : 0.7f; capsule.center = Vector3.up * capsule.height * 0.5f;
            root.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            Set(root.GetComponent<EnemyAI>(), "patrolSpeed", spider ? 1.5f : 1.4f); Set(root.GetComponent<EnemyAI>(), "chaseSpeed", agent.speed);
            Set(root.GetComponent<EnemyAI>(), "eyeHeight", spider ? 0.65f : 1.5f); Set(root.GetComponent<EnemyAI>(), "attackRange", spider ? 1.6f : 2.2f);
            Set(root.GetComponent<EnemyHealth>(), "maxHealth", spider ? 80f : 280f);
            root.transform.Find("AimPoint").localPosition = new Vector3(0, spider ? 0.5f : 1.1f, 0);
            root.transform.Find("AttackPoint").localPosition = new Vector3(0, spider ? 0.5f : 1.1f, spider ? 0.8f : 1.2f);
            root.transform.Find("TargetHighlight").localScale = Vector3.one * (spider ? 1.25f : 1.6f);
            foreach (var component in new Component[] { root.GetComponent<EnemyAttack>(), root.GetComponent<EnemyAnimationController>(), root.GetComponent<EnemyDeathController>() }) SetObject(component, "animator", animator);
            var attackSettings = new SerializedObject(root.GetComponent<EnemyAttack>());
            attackSettings.FindProperty("damage").intValue = spider ? 10 : 20;
            attackSettings.FindProperty("hitDelay").floatValue = clips["Attack"].length * (spider ? 0.4f : 0.45f) / attackPlayback;
            attackSettings.FindProperty("attackCooldown").floatValue = Mathf.Max(spider ? 1.15f : 1.8f, clips["Attack"].length / attackPlayback + 0.12f);
            attackSettings.ApplyModifiedPropertiesWithoutUndo();
            AudioClip attackClip = Audio(spider ? "Spider/spider-attack.mp3" : "Bear/sound.mp3");
            AudioClip voice1 = Audio(spider ? "Spider/spider-1.mp3" : "Bear/sound.mp3"); AudioClip voice2 = Audio(spider ? "Spider/spider-2.mp3" : "Bear/sound.mp3");
            var audioObject = new GameObject("CreatureVoice"); audioObject.layer = 6; audioObject.transform.SetParent(root.transform, false); audioObject.transform.localPosition = root.transform.Find("AimPoint").localPosition;
            var voiceSource = audioObject.AddComponent<AudioSource>(); voiceSource.playOnAwake = false; voiceSource.loop = false; voiceSource.spatialBlend = 1; voiceSource.dopplerLevel = 0;
            voiceSource.rolloffMode = AudioRolloffMode.Linear; voiceSource.minDistance = 4; voiceSource.maxDistance = 25; voiceSource.ignoreListenerPause = false; voiceSource.priority = 160;
            var voice = root.AddComponent<EnemyVoice>(); var voiceSettings = new SerializedObject(voice);
            voiceSettings.FindProperty("voice").objectReferenceValue = voiceSource;
            var ambient = voiceSettings.FindProperty("ambientClips"); ambient.arraySize = spider ? 2 : 1;
            ambient.GetArrayElementAtIndex(0).objectReferenceValue = voice1; if (spider) ambient.GetArrayElementAtIndex(1).objectReferenceValue = voice2;
            voiceSettings.FindProperty("alertClip").objectReferenceValue = voice1; voiceSettings.FindProperty("attackClip").objectReferenceValue = attackClip;
            voiceSettings.FindProperty("hurtClip").objectReferenceValue = voice2; voiceSettings.FindProperty("deathClip").objectReferenceValue = voice1;
            voiceSettings.FindProperty("volume").floatValue = spider ? 0.45f : 0.55f; voiceSettings.ApplyModifiedPropertiesWithoutUndo();
            Set(root.GetComponent<EnemyDeathController>(), "destroyDelay", Mathf.Max(3, clips["Death"].length + 0.2f, voice1.length / 0.96f + 0.2f));
            PrefabUtility.SaveAsPrefabAsset(root, Prefab(kind));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Material ConvertMaterial(Material original, string kind)
    {
        string path = Folder + "/Materials/M_" + kind + "_" + original.name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + kind + "_" + original.name };
        material.SetTexture("_BaseMap", original.mainTexture); material.SetColor("_BaseColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", kind == "Bear" ? 0.18f : 0.28f);
        if (original.HasProperty("_BumpMap") && original.GetTexture("_BumpMap") != null) { material.SetTexture("_BumpMap", original.GetTexture("_BumpMap")); material.EnableKeyword("_NORMALMAP"); }
        if (original.HasProperty("_OcclusionMap") && original.GetTexture("_OcclusionMap") != null) material.SetTexture("_OcclusionMap", original.GetTexture("_OcclusionMap"));
        if (original.IsKeywordEnabled("_EMISSION")) { material.SetTexture("_EmissionMap", original.GetTexture("_EmissionMap")); material.SetColor("_EmissionColor", original.GetColor("_EmissionColor")); material.EnableKeyword("_EMISSION"); }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    public static string Prefab(string kind) => "Assets/_Project/Prefabs/Enemies/Enemy_" + kind + ".prefab";
    private static AudioClip Audio(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Mobs/" + file) ?? throw new InvalidOperationException("Missing creature voice " + file);
    private static void Set(Component component, string name, float value) { var s = new SerializedObject(component); s.FindProperty(name).floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetObject(Component component, string name, UnityEngine.Object value) { var s = new SerializedObject(component); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    private static Bounds BoundsOf(GameObject model)
    {
        // Authored SkinnedMeshRenderer.localBounds can be much larger than the
        // creature's actual pose (especially the spider's separate eye meshes).
        bool initialized = false; Bounds result = default;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SkinnedMeshRenderer skin)
            {
                // Compensate the renderer scale before TransformPoint applies it.
                // Spider mesh nodes are authored at scale 100.
                var mesh = new Mesh(); skin.BakeMesh(mesh, true); mesh.RecalculateBounds(); Bounds b = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = skin.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; } else result.Encapsulate(point);
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            else if (!initialized) { result = renderer.bounds; initialized = true; }
            else result.Encapsulate(renderer.bounds);
        }
        return result;
    }

    [MenuItem("Tools/Enemies/Validate Spider and Bear")]
    public static void Validate()
    {
        var report = new StringBuilder("Creature validation — " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        Action<bool, string> check = (ok, message) => { passed &= ok; report.AppendLine((ok ? "PASS " : "FAIL ") + message); };
        foreach (string kind in new[] { "Spider", "Bear" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(kind)); check(prefab != null, kind + " prefab exists"); if (prefab == null) continue;
            check(prefab.GetComponent<EnemyAI>() != null && prefab.GetComponent<Targetable>() != null && prefab.GetComponent<EnemyAttack>() != null, kind + " uses existing AI/targeting/combat contracts");
            check(prefab.GetComponent<Rigidbody>().isKinematic && prefab.GetComponent<Rigidbody>().interpolation == RigidbodyInterpolation.None, kind + " movement has no Rigidbody interpolation conflict");
            check(prefab.GetComponentsInChildren<Animator>(true).Length == 1 && !prefab.GetComponentInChildren<Animator>().applyRootMotion, kind + " has one rig-specific in-place Animator");
            var voice = prefab.GetComponent<EnemyVoice>(); check(voice != null && voice.AttackClip != null && voice.DeathClip != null && !voice.Source.playOnAwake && !voice.Source.loop && !voice.Source.ignoreListenerPause && voice.Source.spatialBlend == 1, kind + " has non-looping 3D voices that obey pause");
            foreach (Material material in prefab.transform.Find("ModelRoot").GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                check(material.shader.name == "Universal Render Pipeline/Lit" && !ShaderUtil.ShaderHasError(material.shader), kind + " working project-owned URP material: " + material.name);
            check(prefab.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), kind + " has no missing scripts");
        }
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CreatureValidation.txt", report.ToString()); Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Enemies/Capture Spider and Bear")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var holder = new GameObject("Temporary creature preview"); var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(0.3f, 0.36f, 0.27f);
        var camera = new GameObject("Temporary creature camera").AddComponent<Camera>(); var rt = new RenderTexture(1200, 800, 24); var old = RenderTexture.active;
        var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        try
        {
            Vector3 origin = new Vector3(2000, 10, 2000);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(holder.transform); floor.transform.position = origin + Vector3.down * 0.12f; floor.transform.localScale = new Vector3(12, 0.2f, 10); floor.GetComponent<Renderer>().sharedMaterial = material;
            int i = 0;
            foreach (string kind in new[] { "Spider", "Bear" })
            {
                var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(kind)), origin + Vector3.right * (i++ == 0 ? -2 : 2), Quaternion.Euler(0, 160, 0), holder.transform);
                actor.GetComponent<NavMeshAgent>().enabled = false;
                var model = actor.transform.Find("ModelRoot/CreatureVisual").GetChild(0).gameObject;
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + kind + "/" + kind + "_Idle.anim").SampleAnimation(model, 0.2f);
            }
            camera.transform.position = origin + new Vector3(6, 5.8f, -8.5f); camera.transform.LookAt(origin + Vector3.up * 0.65f); camera.fieldOfView = 42;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/SpiderBear_Preview.png", pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = old; camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(holder); UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(material); }
    }
}
#endif
