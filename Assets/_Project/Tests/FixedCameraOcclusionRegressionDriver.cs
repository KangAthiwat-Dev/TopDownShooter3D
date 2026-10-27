#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class FixedCameraOcclusionRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<GameObject> temporary = new List<GameObject>();
    private readonly Vector3 arena = new Vector3(1000, 10, 1000);
    private PlayerMovement movement;
    private CharacterController controller;
    private PlayerInput input;
    private CameraFollow rig;
    private Camera view;
    private CameraOcclusionFade fade;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? previousDevices;
    private GamePauseController pause;
    private Material source;
    private bool passed = true, done, previousBackground;
    private int previousFps, previousVsync;
    private float previousTimeScale;
    private string runtimeError;

    private IEnumerator Start()
    {
        previousBackground = Application.runInBackground; previousFps = Application.targetFrameRate;
        previousVsync = QualitySettings.vSyncCount; previousTimeScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Actual player / fixed camera / scenery fading " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception error = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; }
            catch (Exception e) { error = e; }
            if (error != null) { Finish(error); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var ai in FindObjectsByType<EnemyAI>()) ai.gameObject.SetActive(false);
        movement = FindFirstObjectByType<PlayerMovement>(); controller = movement.GetComponent<CharacterController>(); input = movement.GetComponent<PlayerInput>();
        rig = FindFirstObjectByType<CameraFollow>(); view = rig.FollowCamera; fade = rig.GetComponent<CameraOcclusionFade>();
        var devices = input.actions.devices; if (devices.HasValue) previousDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard = InputSystem.AddDevice<Keyboard>("FixedCameraTestKeyboard"); input.actions.devices = new InputDevice[] { keyboard };
        source = new Material(Shader.Find("MedievalKit/KitLit")); source.SetColor("_BaseColor", new Color(.8f, .45f, .25f, 1f));
        string sourceJson = EditorJsonUtility.ToJson(source);
        var floor = Cube("Temporary low paving", arena + Vector3.down * .6f, new Vector3(120, 1, 120));
        yield return Wait(.4f); Warp(arena); yield return Wait(.7f);
        Quaternion originalRotation = rig.transform.rotation;
        Check(!rig.FollowsMovementHeading && Quaternion.Angle(originalRotation, Quaternion.identity) < .01f, "Original fixed camera restored, movement orbit off");
        Check(Vector3.Distance(view.transform.localPosition, new Vector3(0, 12, -10)) < .001f && Mathf.Abs(view.fieldOfView - 45) < .01f, "Original height, distance, pitch and field of view");
        foreach (var key in new[] { Key.W, Key.D, Key.S, Key.A })
        {
            Warp(arena); yield return Wait(.3f); Vector3 start = movement.transform.position;
            Keys(key); yield return Wait(.65f); Keys(); yield return Wait(.3f);
            Check(Vector3.Distance(start, movement.transform.position) > 1f && Quaternion.Angle(originalRotation, rig.transform.rotation) < .01f, "Actual keyboard " + key + " moves the player without rotating the camera");
        }
        Warp(arena); yield return Wait(.7f);
        movement.transform.rotation = Quaternion.Euler(0, 135, 0); yield return Wait(.2f);
        Check(Quaternion.Angle(originalRotation, rig.transform.rotation) < .01f, "Body / aiming direction cannot rotate the fixed camera");
        Vector3 line = Vector3.Lerp(view.transform.position, movement.transform.position + Vector3.up, .7f);
        var wall = Cube("Temporary obstructing wall", line, new Vector3(3, 4, .5f));
        var wall2 = Cube("Temporary colliderless roof", line + Vector3.forward, new Vector3(3, 3, .3f)); Destroy(wall2.GetComponent<Collider>());
        var offRay = Cube("Temporary non-obstructing shared material", line + Vector3.right * 10, new Vector3(3, 4, .5f));
        var behind = Cube("Temporary wall behind player", arena + new Vector3(0, 2, 4), new Vector3(3, 4, .5f));
        var enemyLayer = Cube("Temporary enemy layer exclusion", line, Vector3.one); enemyLayer.gameObject.layer = 6;
        var child = Cube("Temporary held player mesh exclusion", movement.transform.position + Vector3.up, Vector3.one * .2f); child.transform.SetParent(movement.transform, true);
        var block = new MaterialPropertyBlock(); block.SetFloat("_BumpScale", .7f); wall.SetPropertyBlock(block, 0);
        fade.RefreshCandidates();
        yield return Wait(.09f);
        Check(fade.GetOpacity(wall) < 1 && fade.GetOpacity(wall) > .15f, "Occluder fades progressively, not an instant hide");
        yield return Wait(.6f);
        Check(Mathf.Abs(fade.GetOpacity(wall) - .15f) < .01f && Mathf.Abs(fade.GetOpacity(wall2) - .15f) < .01f, "Both stacked wall and colliderless roof fade to 15 percent");
        Check(wall.sharedMaterial != source && wall.sharedMaterial.shader == source.shader && wall.sharedMaterial.GetFloat("_ZWrite") == 0 && wall.sharedMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "Kit materials retain their shader and use real transparent blending");
        Check(offRay.sharedMaterial == source && behind.sharedMaterial == source && fade.GetOpacity(floor) == 1, "Off-ray, behind-player scenery and low paving remain opaque");
        Check(enemyLayer.sharedMaterial == source && child.sharedMaterial == source, "Enemy and player-held meshes excluded");
        Check(wall.GetComponent<Collider>().enabled && wall.gameObject.activeInHierarchy, "Fading does not disable colliders or objects");
        Check(EditorJsonUtility.ToJson(source) == sourceJson, "Shared original material remains unchanged");
        var savedBlock = new MaterialPropertyBlock(); wall.GetPropertyBlock(savedBlock, 0);
        Check(Mathf.Abs(savedBlock.GetFloat("_BumpScale") - .7f) < .001f, "Existing material property overrides survive fading");
        pause = FindFirstObjectByType<GamePauseController>(); pause.SetPaused(GamePauseReason.Inventory, true);
        float opacity = fade.GetOpacity(wall); Vector3 cameraPosition = rig.transform.position;
        wall.transform.position += Vector3.right * 20; yield return Wait(.3f);
        Check(Time.timeScale == 0 && Mathf.Abs(fade.GetOpacity(wall) - opacity) < .001f && Vector3.Distance(cameraPosition, rig.transform.position) < .001f, "Inventory pause freezes camera and fades");
        pause.SetPaused(GamePauseReason.Inventory, false); yield return Wait(.25f);
        Check(fade.GetOpacity(wall) > .15f && fade.GetOpacity(wall) < 1f, "Clearing the view smoothly restores opacity");
        yield return Wait(.6f); wall.GetPropertyBlock(savedBlock, 0);
        Check(wall.sharedMaterial == source && savedBlock.HasProperty("_BumpScale") && !savedBlock.HasProperty("_BaseColor"), "Fade-in restores the exact original material and per-slot property block");
        fade.enabled = false;
        Check(wall2.sharedMaterial == source && fade.ActiveFadeCount == 0, "Disabling the service restores remaining faded objects");
        fade.enabled = true; yield return Wait(.5f);
        Check(fade.GetOpacity(wall2) < .2f, "Re-enabling rebuilds detection and material copies");
        foreach (var g in temporary.Where(g => g != null && g.name != "Temporary low paving")) g.SetActive(false);
        Warp(arena); yield return Wait(.7f);
        var collisionWall = Cube("Temporary solid ghost wall", arena + new Vector3(0, 2, -1.5f), new Vector3(4, 4, .4f));
        fade.RefreshCandidates(); yield return Wait(.5f);
        Check(fade.GetOpacity(collisionWall) < .2f, "Near-ground solid wall is transparent before collision test");
        Keys(Key.S); yield return Wait(.8f); Keys();
        Check(movement.transform.position.z > arena.z - 1.2f && collisionWall.GetComponent<Collider>().enabled, "Actual CharacterController cannot walk through the transparent wall");
        collisionWall.gameObject.SetActive(false);
        Warp(arena); yield return Wait(.7f); Keys(Key.D, Key.LeftShift); yield return Wait(.6f);
        Check(movement.IsRunning && Vector3.Distance(view.transform.localPosition, new Vector3(0, 12, -10)) > .001f && Quaternion.Angle(originalRotation, rig.transform.rotation) < .01f, "Existing subtle sprint bob works with fixed camera yaw");
        Keys(); yield return Wait(.6f);
        Check(Vector3.Distance(view.transform.localPosition, new Vector3(0, 12, -10)) < .001f, "Bob returns to authored camera pose");
        Keys(Key.W, Key.Space); yield return Wait(.08f);
        Check(movement.IsDashing && view.fieldOfView > 45.5f, "Existing dash field-of-view effect retained");
        Keys(); yield return Wait(1f);

        // Real, authored city pieces, not only test primitives. Stand just behind the armory.
        Warp(new Vector3(85.2f, 5.22f, -76.3f)); yield return Wait(.9f); fade.RefreshCandidates(); yield return Wait(.5f);
        var realFaded = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => fade.GetOpacity(r) < .3f).ToArray();
        Check(realFaded.Any(r => r.sharedMaterials.Any(m => m != null && m.shader.name == "MedievalKit/KitLit")), "Real city roof / wall pieces fade through the main camera sight line");
        report.AppendLine("City occluders: " + string.Join(", ", realFaded.Select(r => r.name)));
        Capture("Logs/FixedCamera_City_Faded.png"); fade.enabled = false; yield return null; Capture("Logs/FixedCamera_City_Opaque.png"); fade.enabled = true;
        FindFirstObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
        Check(runtimeError == null, "No runtime errors during actual player, fade, pause, sprint or dash checks");
    }
    private Renderer Cube(string name, Vector3 position, Vector3 size)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.layer = 7;
        item.transform.position = position; item.transform.localScale = size; temporary.Add(item);
        var renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = source; return renderer;
    }
    private void Warp(Vector3 position) { Keys(); controller.enabled = false; movement.transform.position = position; controller.enabled = true; Physics.SyncTransforms(); }
    private void Keys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float duration) { float until = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < until) yield return null; }
    private void Check(bool condition, string label) { passed &= condition; report.AppendLine((condition ? "PASS " : "FAIL ") + label); }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
    private void Capture(string path)
    {
        var rt = RenderTexture.GetTemporary(1500, 1000, 24); var oldTarget = view.targetTexture; var oldActive = RenderTexture.active; var image = new Texture2D(1500, 1000, TextureFormat.RGB24, false);
        try { view.targetTexture = rt; view.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1500, 1000), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { view.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); Destroy(image); }
    }
    private void Finish(Exception error)
    {
        if (done) return; done = true;
        if (error != null) { passed = false; report.AppendLine("EXCEPTION " + error); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        Keys(); if (pause != null) pause.SetPaused(GamePauseReason.Inventory, false); Time.timeScale = previousTimeScale;
        if (input != null) input.actions.devices = previousDevices;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (fade != null) { fade.enabled = false; fade.enabled = true; }
        foreach (var g in temporary) if (g != null) Destroy(g); if (source != null) Destroy(source);
        Application.logMessageReceived -= OnLog; Application.runInBackground = previousBackground; Application.targetFrameRate = previousFps; QualitySettings.vSyncCount = previousVsync;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/FixedCameraPlaytest.txt", report.ToString());
        if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString()); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Regression interrupted")); }
}
#endif
