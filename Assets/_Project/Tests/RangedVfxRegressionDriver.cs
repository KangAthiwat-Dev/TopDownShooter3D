#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Rendering.Universal;

// Menu-only real-scene coverage: no fixture is serialized into user scenes.
[DefaultExecutionOrder(1000)]
public class RangedVfxRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<Projectile> bullets = new List<Projectile>();
    private PlayerRangedWeapon weapon;
    private PlayerAttackController attack;
    private PlayerMovement movement;
    private PlayerInput input;
    private PlayerRangedFeedback feedback;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? oldDevices;
    private int shots, oldFps, oldVsync;
    private bool oldBackground, finished;
    private string runtimeError;
    private float lastShot = -10;
    private readonly Vector3 origin = new Vector3(1000, 10, 1000);
    private Camera camera;
    private EnemyHealth enemyHealth;

    private IEnumerator Start()
    {
        oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount; oldBackground = Application.runInBackground;
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Thin tracer / Spiky Hit / refraction regression — " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception failure = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; }
            catch (Exception exception) { failure = exception; }
            if (failure != null) { Finish(failure); yield break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach (string name in new[] { "TopDownShooter/VFX/Energy Tracer URP", "TopDownShooter/VFX/Ranged Refraction URP", "Vefects/Lite_SH_Vefects_URP_Unlit_Combat_Flipbook_Advanced_01" })
        {
            Shader shader = Shader.Find(name); Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader compiles: " + name);
        }
        weapon = FindAnyObjectByType<PlayerRangedWeapon>(); movement = weapon.GetComponent<PlayerMovement>();
        attack = weapon.GetComponent<PlayerAttackController>(); input = weapon.GetComponent<PlayerInput>(); feedback = weapon.GetComponent<PlayerRangedFeedback>();
        Check(Mathf.Approximately(attack.RangedAttackInterval, 0.18f) && Mathf.Approximately(Get<float>(attack, "attackInterval"), 0.35f),
            "Rifle interval is 0.18s; melee interval remains 0.35s");
        Check(Get<float>(weapon, "damage") == 20f && Get<float>(weapon, "projectileSpeed") == 18f,
            "Damage and projectile speed remain unchanged");
        Projectile prefab = Get<Projectile>(weapon, "projectilePrefab");
        Check(prefab.GetComponentsInChildren<LineRenderer>(true).Length == 2 && !prefab.GetComponent<MeshRenderer>().enabled &&
            prefab.GetComponentsInChildren<LineRenderer>(true).All(line => line.widthMultiplier <= 0.075f),
            "Bullet is a narrow core/glow line, not the original visible ball");
        Check(Mathf.Abs(prefab.GetComponent<SphereCollider>().radius * prefab.transform.localScale.x - 0.1f) < 0.001f &&
            prefab.GetComponentsInChildren<Collider>(true).Length == 1, "Collision radius stays 0.1m; refraction has no extra gameplay colliders");
        RangedBurstVfx muzzlePrefab = Get<RangedBurstVfx>(feedback, "muzzleVfx"), impactPrefab = Get<RangedBurstVfx>(prefab, "impactVfx");
        Check(muzzlePrefab != null && impactPrefab != null && new[] { muzzlePrefab, impactPrefab }.All(fx =>
            fx.GetComponentsInChildren<ParticleSystem>(true).All(ps => !ps.main.loop && !ps.main.playOnAwake)),
            "Muzzle and impact copies are one-shot systems, not looping demos");
        Check(muzzlePrefab.GetComponentsInChildren<Collider>(true).Length == 0 && impactPrefab.GetComponentsInChildren<Collider>(true).Length == 0,
            "Spiky effects cannot damage, push or block characters");
        AudioSource audio = Get<AudioSource>(feedback, "shotAudio"); AudioClip clip = Get<AudioClip>(feedback, "shotClip");
        Check(audio != null && clip != null && clip.length > 0f && AssetDatabase.GetAssetPath(clip) == "Assets/_Project/Audio/Gun/pistol-sound-effect.mp3" &&
            !audio.playOnAwake && !audio.loop && !audio.ignoreListenerPause,
            "Requested pistol-sound-effect is wired, does not autoplay/loop and obeys pause");
        Material spiky = muzzlePrefab.GetComponentsInChildren<ParticleSystemRenderer>(true).First(r => r.sharedMaterial != null).sharedMaterial;
        Check(spiky.GetTexture("_AlphaTexture") != null && spiky.GetTexture("_MaskTexture") != null && spiky.GetFloat("_Emissive") > 0,
            "Actual Spiky Hit flipbook alpha/mask textures are installed");

        foreach (EnemyWaveSpawner wave in FindObjectsByType<EnemyWaveSpawner>()) { wave.StopAllCoroutines(); wave.enabled = false; }
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>()) ai.gameObject.SetActive(false);
        MakeGridFloor();
        var controller = movement.GetComponent<CharacterController>(); controller.enabled = false;
        movement.transform.SetPositionAndRotation(origin, Quaternion.identity); controller.enabled = true;
        MakeTarget(); camera = Camera.main;
        CameraFollow follow = camera.GetComponentInParent<CameraFollow>(); if (follow != null) follow.enabled = false;
        camera.transform.position = origin + new Vector3(4f, 4f, -4.5f); camera.transform.LookAt(origin + Vector3.up + Vector3.forward * 2.4f);
        keyboard = InputSystem.AddDevice<Keyboard>("TracerVfxRegressionKeyboard");
        var devices = input.actions.devices;
        if (devices.HasValue) oldDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        input.actions.devices = new InputDevice[] { keyboard }; weapon.ProjectileFired += OnShot;
        yield return Wait(0.2f);
        Check(FindObjectsByType<RangedBurstVfx>().Length == 0 && !audio.isPlaying, "No muzzle/impact burst or shot sound on spawn");
        Keys(Key.J); float until = Time.realtimeSinceStartup + 2f;
        while (shots < 1 && Time.realtimeSinceStartup < until) yield return null;
        Check(shots > 0 && audio.isPlaying, "A committed shot produces actual projectile and shot audio together");
        yield return Capture("FirstShot");
        int before = shots; float began = Time.time;
        yield return Wait(1.25f);
        float elapsed = Time.time - began; int emitted = shots - before;
        Check(emitted >= 5 && emitted <= 8, "Held rifle fires about 5.5 shots/second with real input/Animator/IK");
        report.AppendLine("CADENCE shots=" + emitted + " elapsed=" + elapsed.ToString("F3") + " configured=" + attack.RangedAttackInterval);
        Check(enemyHealth.CurrentHealth < enemyHealth.MaxHealth && FindObjectsByType<RangedBurstVfx>().Any(fx => fx.name.Contains("Impact")),
            "Real collisions deal damage and spawn detached Spiky impact effects");
        yield return Capture("AutomaticFire");
        // Include travel time of the last in-flight bullet, then its impact lifetime.
        Keys(); yield return Wait(Mathf.Max(1.25f, clip.length / Mathf.Max(0.1f, Get<Vector2>(feedback, "pitchRange").x) + 0.15f));
        Check(Mathf.Approximately(enemyHealth.MaxHealth - enemyHealth.CurrentHealth, shots * 20f), "Each straight projectile hits only once for the original 20 damage");
        Check(FindObjectsByType<RangedBurstVfx>(FindObjectsInactive.Include).Length == 0 && !audio.isPlaying,
            "All one-shot bursts clean up and shot sound stops after releasing attack");
        Check(bullets.All(bullet => bullet == null), "Hit projectiles are destroyed without leaving tracer objects behind");

        var oldFlash = Instantiate(muzzlePrefab, weapon.MuzzlePoint.position, weapon.MuzzlePoint.rotation);
        yield return Wait(0.11f); oldFlash.gameObject.SetActive(false); yield return null;
        oldFlash.gameObject.SetActive(true); yield return Wait(0.03f);
        Check(oldFlash.GetComponentsInChildren<ParticleSystem>().All(ps => ps.particleCount == 0),
            "Hiding/re-enabling a gun cannot replay an old one-shot flash");
        yield return Wait(0.2f);

        yield return RefractionChecks(impactPrefab);

        Keys(Key.J); yield return Wait(0.24f);
        InventoryPanelUI panel = FindAnyObjectByType<InventoryPanelUI>();
        Check(panel.TryOpen(), "Inventory opens while automatic fire is active");
        int pausedShots = shots;
        Projectile live = bullets.LastOrDefault(bullet => bullet != null);
        Vector3 position = live != null ? live.transform.position : Vector3.zero;
        yield return Wait(0.24f);
        Check(shots == pausedShots && AudioListener.pause && (live == null || live.transform.position == position),
            "Inventory freezes in-flight bullets, effects and audio without firing more shots");
        panel.Close(); yield return Wait(0.4f);
        Check(shots == pausedShots, "Held fire does not leak through closing inventory; release is required");
        Keys(); yield return Wait(0.08f); Keys(Key.J); yield return Wait(0.3f);
        Check(shots > pausedShots, "Fresh fire after release restores automatic shooting");
        Keys(); yield return Wait(1.25f);
        Check(FindObjectsByType<RangedBurstVfx>(FindObjectsInactive.Include).Length == 0, "No effect objects accumulate after repeated fire/pause/resume");
        Check(runtimeError == null, "No runtime errors or exceptions");
    }
    private IEnumerator RefractionChecks(RangedBurstVfx impactPrefab)
    {
        Check(RangedRefraction.HasOpaqueTexture(camera), "Gameplay camera supplies opaque scene color for refraction");
        var effect = Instantiate(impactPrefab, origin + new Vector3(1f, 0.12f, 2.4f), Quaternion.identity);
        foreach (ParticleSystemRenderer renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>(true)) renderer.enabled = false;
        foreach (AudioSource sound in effect.GetComponentsInChildren<AudioSource>()) sound.enabled = false;
        yield return Wait(0.08f);
        GamePauseController pause = FindAnyObjectByType<GamePauseController>(); pause.SetPaused(GamePauseReason.Inventory, true);
        RangedRefraction refraction = effect.GetComponentInChildren<RangedRefraction>(); MeshRenderer mesh = refraction.GetComponent<MeshRenderer>();
        yield return new WaitForEndOfFrame();
        Texture2D with = ScreenCapture.CaptureScreenshotAsTexture();
        Check(mesh.enabled, "Transparent refraction renders without visible Spiky particles");
        refraction.enabled = false; mesh.enabled = false;
        yield return null; yield return new WaitForEndOfFrame();
        Texture2D without = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] a = with.GetPixels32(), b = without.GetPixels32(); int changed = 0;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 8) changed++;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/RifleVfx_RefractionOn.png", with.EncodeToPNG()); File.WriteAllBytes("Logs/RifleVfx_RefractionOff.png", without.EncodeToPNG());
        Destroy(with); Destroy(without);
        Check(changed > 8, "Pixel comparison confirms genuine background distortion, not just additive light");
        report.AppendLine("REFRACTION changedPixels=" + changed);
        Destroy(effect.gameObject); pause.SetPaused(GamePauseReason.Inventory, false); yield return null;
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData(); CameraOverrideOption previous = data.requiresColorOption;
        data.requiresColorOption = CameraOverrideOption.Off;
        effect = Instantiate(impactPrefab, origin + Vector3.up + Vector3.forward * 2, Quaternion.identity);
        yield return Wait(0.04f);
        Check(!effect.GetComponentInChildren<RangedRefraction>().GetComponent<MeshRenderer>().enabled &&
            effect.GetComponentsInChildren<ParticleSystem>().Any(ps => ps.particleCount > 0),
            "Without Opaque Texture refraction hides safely while Spiky Hit still plays");
        data.requiresColorOption = previous; yield return Wait(0.04f);
        Check(effect.GetComponentInChildren<RangedRefraction>().GetComponent<MeshRenderer>().enabled, "Refraction returns when opaque scene color is restored");
        yield return Wait(0.8f);
    }
    private void OnShot(Projectile projectile)
    {
        float spacing = Time.time - lastShot; lastShot = Time.time;
        if (spacing < attack.RangedAttackInterval - 0.005f) runtimeError = "Shot cadence violated";
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (Vector3.Angle(body.linearVelocity, weapon.MuzzlePoint.forward) > 0.05f || Vector3.Distance(projectile.transform.position, weapon.MuzzlePoint.position) > 0.001f)
            runtimeError = "Tracer did not start on the final barrel pose";
        shots++; bullets.Add(projectile);
    }
    private void MakeTarget()
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule); target.SetActive(false); target.name = "Tracer VFX Target (Temporary)";
        target.layer = LayerMask.NameToLayer("Enemy"); target.transform.position = origin + Vector3.forward * 6 + Vector3.up;
        enemyHealth = target.AddComponent<EnemyHealth>(); Set(enemyHealth, "maxHealth", 100000f);
        Targetable targetable = target.AddComponent<Targetable>();
        Transform aim = new GameObject("Aim Point").transform; aim.SetParent(target.transform, false); aim.localPosition = Vector3.up * 0.4f;
        Set(targetable, "aimPoint", aim); target.SetActive(true); Physics.SyncTransforms();
    }
    private void MakeGridFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "VFX Regression Grid (Temporary)";
        floor.transform.position = origin + Vector3.down * 0.6f; floor.transform.localScale = new Vector3(80, 1, 80);
        var texture = new Texture2D(64, 64); var colors = new Color[64 * 64];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) colors[y * 64 + x] = ((x / 8 + y / 8) % 2 == 0) ? new Color(0.55f, 0.56f, 0.58f) : new Color(0.3f, 0.31f, 0.34f);
        texture.SetPixels(colors); texture.Apply(); texture.wrapMode = TextureWrapMode.Repeat;
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", Vector2.one * 40);
        floor.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    private IEnumerator Capture(string label)
    {
        yield return new WaitForEndOfFrame(); Directory.CreateDirectory("Logs"); ScreenCapture.CaptureScreenshot("Logs/RifleVfx_" + label + ".png", 2); yield return null;
    }
    private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(seconds);
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private void Check(bool condition, string message)
    {
        if (runtimeError != null) throw new InvalidOperationException(runtimeError);
        if (!condition) throw new InvalidOperationException(message); report.AppendLine("PASS " + message);
    }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        try { if (weapon != null) weapon.ProjectileFired -= OnShot; if (input != null) input.actions.devices = oldDevices; if (keyboard != null) InputSystem.RemoveDevice(keyboard); }
        catch (Exception exception) { failure = failure ?? exception; }
        FindAnyObjectByType<InventoryPanelUI>()?.Close(); FindAnyObjectByType<GamePauseController>()?.ResetForRestart(); Time.timeScale = 1;
        Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync; Application.runInBackground = oldBackground;
        Application.logMessageReceived -= OnLog; report.AppendLine(failure == null ? "ALL CHECKS PASSED" : "FAIL " + failure);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/RangedVfxRegression.txt", report.ToString());
        if (failure == null) Debug.Log("Thin tracer / Spiky VFX regression: ALL CHECKS PASSED. Logs/RangedVfxRegression.txt"); else Debug.LogError("Ranged VFX regression failed: " + failure.Message);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Regression interrupted")); }
}
#endif
