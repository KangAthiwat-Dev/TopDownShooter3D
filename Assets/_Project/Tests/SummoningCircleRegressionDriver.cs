#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SummoningCircleRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private bool finished, passed = true, oldBackground;
    private int oldFps, oldVsync;
    private string runtimeError;
    private GamePauseController pause;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        oldBackground = Application.runInBackground; oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Summoning circle real-scene regression — " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception failure = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; }
            catch (Exception exception) { failure = exception; }
            if (failure != null) { Finish(failure); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }

    private IEnumerator Checks()
    {
        StopWaves();
        var spawn = FindAnyObjectByType<PlayerSceneSpawnPoint>(); var movement = spawn.Player.GetComponent<PlayerMovement>();
        var effect = FindAnyObjectByType<SummoningCircleVfx>(); pause = FindAnyObjectByType<GamePauseController>();
        Check(effect != null && FindObjectsByType<SummoningCircleVfx>().Length == 1, "Exactly one birth circle starts automatically");
        Check(effect.transform.parent.name == "SummonVfxAnchor" && Vector3.Distance(effect.transform.position, spawn.Position) < 0.2f, "Circle is on the original platform spawn anchor");
        Check(Vector3.Distance(spawn.Player.position, spawn.Position) < 0.2f && !movement.IsInteractionLocked && spawn.Player.GetComponent<CharacterController>().enabled, "Original player spawns normally; circle adds no input lock/teleport state");
        Check(effect.IsPlaying && effect.ElapsedTime < 0.65f && effect.Opacity < 1f, "Birth begins with a gradual fade-in, not an instant full-bright flash");
        var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        Check(particles.Length == 6 && particles.All(p => p.isPlaying && !p.main.useUnscaledTime && !p.main.playOnAwake), "All six pack particle layers are driven by the scaled-time one-shot");
        Check(effect.GetComponentsInChildren<Collider>(true).Length == 0 && particles.All(p => !p.collision.enabled && !p.trigger.enabled), "VFX cannot block movement, bullets, interactions or enemy sight");
        var materials = effect.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
        Check(materials.All(m => m != null && m.shader.name == "TopDownShooter/VFX/Summoning Circle URP" && !ShaderUtil.ShaderHasError(m.shader)), "Every renderer uses a compiled URP material (no built-in pink fallback)");
        Check(materials.Any(m => m.GetTexture("_MainTex") != null && m.GetTexture("_MainTex").name == "MagicCircle"), "Actual pack rune-ring texture is retained");
        yield return Wait(0.9f);
        Check(effect != null && effect.Opacity > 0.98f && particles.Sum(p => p.particleCount) > 10, "Rune circle, rising strands and orbiting sparks become visibly active");
        Check(effect.GetComponentsInChildren<Light>(true).Length == 1 && effect.GetComponentInChildren<Light>().intensity > 0, "One bounded blue ground light follows the effect, no demo light spawning");
        yield return Capture("Gameplay");

        pause.SetPaused(GamePauseReason.Inventory, true); yield return null;
        float elapsed = effect.ElapsedTime, opacity = effect.Opacity; float[] times = particles.Select(p => p.time).ToArray();
        yield return Wait(0.35f);
        Check(Mathf.Abs(effect.ElapsedTime - elapsed) < 0.001f && Mathf.Abs(effect.Opacity - opacity) < 0.001f && particles.Select((p, i) => Mathf.Abs(p.time - times[i]) < 0.002f).All(equal => equal), "Inventory pause freezes particle simulation, fade and cleanup timer together");
        pause.SetPaused(GamePauseReason.Inventory, false);

        // Independent tuning uses MPBs, never material edits shared with the scene.
        var copyObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/World/SummoningSanctum/MagicCircle/PlayerSummonCircle.prefab"), effect.transform.position + Vector3.right * 6, Quaternion.identity);
        var copy = copyObject.GetComponent<SummoningCircleVfx>(); var tuning = new SerializedObject(copy);
        tuning.FindProperty("effectColor").colorValue = new Color(0.15f, 1, 0.3f, 1); tuning.FindProperty("radius").floatValue = 1.2f;
        tuning.FindProperty("fadeInDuration").floatValue = 0.05f; tuning.ApplyModifiedPropertiesWithoutUndo();
        yield return Wait(0.12f);
        var originalProperties = new MaterialPropertyBlock(); var copyProperties = new MaterialPropertyBlock();
        effect.GetComponent<Renderer>().GetPropertyBlock(originalProperties); copy.GetComponent<Renderer>().GetPropertyBlock(copyProperties);
        Check(copyProperties.GetColor("_EffectTint").g > 0.9f && originalProperties.GetColor("_EffectTint").b > originalProperties.GetColor("_EffectTint").g,
            "A second instance can turn green without recoloring the original blue circle");
        Check(copy.transform.localScale.x < effect.transform.localScale.x && Mathf.Approximately(copy.Radius, 1.2f), "Radius setting resizes the entire effect consistently");
        Check(materials.All(m => Mathf.Approximately(m.GetFloat("_Opacity"), 0)), "Animation/tint never mutate shared material assets");
        copyObject.SetActive(false); yield return null; yield return null;
        Check(copyObject == null, "Interrupted one-shot cleans up instead of replaying or leaving a light behind");

        yield return Until(() => effect == null || effect.ElapsedTime >= 2.95f, 3f);
        Check(effect != null && effect.Opacity > 0 && effect.Opacity < 0.95f && particles.All(p => !p.isEmitting), "Ending stops new sparks and smoothly fades all living layers/light");
        yield return Until(() => effect == null, 2f);
        Check(effect == null && FindObjectsByType<SummoningCircleVfx>().Length == 0, "One-shot fully removes itself after its configured duration");
        Check(spawn.transform.parent.Find("SummonVfxAnchor").childCount == 0, "No particles, runtime materials or light objects remain on the anchor");
        spawn.PlacePlayer(); yield return null;
        Check(FindObjectsByType<SummoningCircleVfx>().Length == 0, "Utility PlacePlayer does not accidentally duplicate/replay the birth effect");

        string scenePath = SceneManager.GetActiveScene().path;
        EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return Wait(0.12f); StopWaves();
        var restarted = FindObjectsByType<SummoningCircleVfx>(); spawn = FindAnyObjectByType<PlayerSceneSpawnPoint>();
        Check(restarted.Length == 1 && restarted[0].IsPlaying && restarted[0].ElapsedTime < 0.65f, "Actual scene restart creates exactly one fresh fade-in sequence");
        Check(Vector3.Distance(spawn.Player.position, spawn.Position) < 0.2f && restarted[0].EffectColor.b > restarted[0].EffectColor.g && Mathf.Approximately(restarted[0].Radius, 2.3f), "Restart retains the real spawn and original saved blue/size tuning");
        Check(runtimeError == null, "No runtime errors during playback, pause, independent tuning, cancellation or reload");
    }

    private static void StopWaves()
    {
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
    }
    private static IEnumerator Wait(float duration) { float until = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Until(Func<bool> condition, float timeout) { float until = Time.realtimeSinceStartup + timeout; while (!condition() && Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Capture(string label) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/SummoningCircle_" + label + ".png", 2); yield return null; }
    private void Check(bool condition, string message) { report.AppendLine((condition ? "PASS " : "FAIL ") + message); passed &= condition; }
    private void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        if (failure != null) { passed = false; report.AppendLine("EXCEPTION " + failure); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        Application.logMessageReceived -= OnLog;
        if (pause != null) pause.SetPaused(GamePauseReason.Inventory, false);
        Application.runInBackground = oldBackground; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SummoningCirclePlaytest.txt", report.ToString()); Debug.Log(report.ToString());
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Circle test interrupted")); }
}
#endif
