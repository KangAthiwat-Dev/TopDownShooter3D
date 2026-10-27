#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

[DefaultExecutionOrder(10000)]
public sealed class MovementCameraRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly Vector3 arena = new Vector3(1000, 10, 1000);
    private PlayerMovement movement;
    private PlayerInput input;
    private PlayerRangedWeapon weapon;
    private PlayerCombatFacing facing;
    private CharacterController cc;
    private CameraFollow rig;
    private Camera cameraView;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? previousDevices;
    private GamePauseController pause;
    private GameObject floor, enemy;
    private bool done, passed = true, previousBackground, keepEnemyNorth;
    private int previousFps, previousVsync, shots;
    private float previousTimeScale, initialFov, peakYawSpeed;
    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;
    private string runtimeError;
    private bool hasYawSample;
    private float previousYawSample;

    private IEnumerator Start()
    {
        previousBackground = Application.runInBackground; previousFps = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount; previousTimeScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog; report.AppendLine("Actual-player movement camera " + DateTime.Now.ToString("s"));
        var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(Checks());
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
        foreach (var enemyAI in FindObjectsByType<EnemyAI>()) enemyAI.gameObject.SetActive(false);
        movement = FindFirstObjectByType<PlayerMovement>(); cc = movement.GetComponent<CharacterController>(); input = movement.GetComponent<PlayerInput>();
        weapon = movement.GetComponent<PlayerRangedWeapon>(); facing = movement.GetComponent<PlayerCombatFacing>();
        rig = FindFirstObjectByType<CameraFollow>(); cameraView = rig.FollowCamera;
        cameraLocalPosition = cameraView.transform.localPosition; cameraLocalRotation = cameraView.transform.localRotation; initialFov = cameraView.fieldOfView;
        var devices = input.actions.devices; if (devices.HasValue) previousDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard = InputSystem.AddDevice<Keyboard>("MovementCameraKeyboard"); input.actions.devices = new InputDevice[] { keyboard };
        weapon.ProjectileFired += Shot;
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Temporary camera test floor";
        floor.transform.position = arena + Vector3.down * .6f; floor.transform.localScale = new Vector3(120, 1, 120);
        yield return Wait(.4f); Warp(arena); yield return Wait(.4f);
        Check(rig.FollowsMovementHeading, "Movement camera enabled");

        yield return Move("east", Vector3.right, 3f, Key.D);
        yield return Move("south", Vector3.back, 3f, Key.S);
        yield return Move("west", Vector3.left, 3f, Key.A);
        yield return Move("north", Vector3.forward, 3f, Key.W);
        yield return Move("northeast", (Vector3.right + Vector3.forward).normalized, 2.8f, Key.D, Key.W);
        Check(peakYawSpeed <= 112f, "Orbit respects 110 degree/second cap; peak=" + peakYawSpeed.ToString("F2"));
        Check(Vector3.Distance(cameraView.transform.localPosition, cameraLocalPosition) < .001f && Quaternion.Angle(cameraView.transform.localRotation, cameraLocalRotation) < .001f, "Walking orbit preserves child-camera height, pitch and distance");

        yield return Wait(.8f);
        float yawAtRest = rig.transform.eulerAngles.y;
        movement.transform.rotation = Quaternion.Euler(0, 240, 0); yield return Wait(.6f);
        Check(Mathf.Abs(Mathf.DeltaAngle(yawAtRest, rig.transform.eulerAngles.y)) < .25f, "Turning the character while idle does not turn the camera");
        Keys(Key.A); yield return Wait(.025f); Keys(); yield return Wait(.7f);
        Check(Mathf.Abs(Mathf.DeltaAngle(yawAtRest, rig.transform.eulerAngles.y)) < .25f, "A tiny direction tap does not flip the camera");

        Warp(arena); CreateEnemy(); keepEnemyNorth = true;
        int beforeShots = shots;
        yield return Move("east while shooting north", Vector3.right, 3.2f, Key.D, Key.J);
        Check(shots > beforeShots && facing.IsAiming && Vector3.Angle(movement.transform.forward, Vector3.forward) < 12f, "Real weapon fires north while player travels east");
        Check(BehindDot(Vector3.right) > .98f, "Camera stays behind eastward travel, not northward aim");
        Keys(Key.J); yield return Wait(1f); keepEnemyNorth = false;
        float shootingIdleYaw = rig.transform.eulerAngles.y; float beforeBody = movement.transform.eulerAngles.y; beforeShots = shots;
        float until = Time.realtimeSinceStartup + 1.6f;
        while (Time.realtimeSinceStartup < until)
        {
            float angle = (1.6f - (until - Time.realtimeSinceStartup)) * 65f;
            enemy.transform.position = movement.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * 7f;
            yield return null;
        }
        Check(shots > beforeShots && Mathf.Abs(Mathf.DeltaAngle(beforeBody, movement.transform.eulerAngles.y)) > 45f, "Stationary player turns and fires at a changing target");
        Check(Mathf.Abs(Mathf.DeltaAngle(shootingIdleYaw, rig.transform.eulerAngles.y)) < .25f, "Camera holds its last movement heading through target changes");
        Keys(); enemy.SetActive(false); yield return Wait(.8f);

        Warp(arena); Keys(Key.D, Key.LeftShift); yield return Wait(2.3f);
        Check(movement.IsRunning && Vector3.Distance(cameraView.transform.localPosition, cameraLocalPosition) > .001f && Vector3.Distance(cameraView.transform.localPosition, cameraLocalPosition) < .08f, "Sprint retains subtle local camera bob without orbit-distance drift");
        Keys(); yield return Wait(.8f);
        Check(Vector3.Distance(cameraView.transform.localPosition, cameraLocalPosition) < .001f && Quaternion.Angle(cameraView.transform.localRotation, cameraLocalRotation) < .001f, "Stopping sprint restores the original child-camera pose");
        float dashYaw = rig.transform.eulerAngles.y;
        Keys(Key.W, Key.Space); yield return Wait(.09f);
        Check(movement.IsDashing && cameraView.fieldOfView > initialFov + .5f, "Existing dash FOV effect still works");
        Check(Mathf.Abs(Mathf.DeltaAngle(dashYaw, rig.transform.eulerAngles.y)) < .5f, "Short dash direction does not whip the camera");
        Keys(); yield return Wait(1f);
        Check(Mathf.Abs(cameraView.fieldOfView - initialFov) < .1f, "Dash FOV recovers normally");

        pause = FindFirstObjectByType<GamePauseController>(); pause.SetPaused(GamePauseReason.Inventory, true); yield return null; yield return null;
        Vector3 pausedPosition = rig.transform.position; Quaternion pausedRotation = rig.transform.rotation;
        Warp(arena + Vector3.left * 4); yield return Wait(.3f);
        Check(Time.timeScale == 0 && Vector3.Distance(pausedPosition, rig.transform.position) < .001f && Quaternion.Angle(pausedRotation, rig.transform.rotation) < .001f, "Production inventory pause freezes camera position and orbit");
        pause.SetPaused(GamePauseReason.Inventory, false); yield return Wait(.6f);

        Application.targetFrameRate = 30; yield return Move("north at requested 30 FPS", Vector3.forward, 3f, Key.W);
        Application.targetFrameRate = 120; yield return Move("east at requested 120 FPS", Vector3.right, 3f, Key.D);
        Application.targetFrameRate = 60;
        Check(peakYawSpeed <= 112f && Finite(rig.transform.position), "Frame-rate changes cause no yaw snap or invalid position");

        // Show the actual orbit in the city, using the existing clear southeast street.
        Warp(new Vector3(75.76f, 5.22f, -82.8856f)); Keys(Key.D); yield return Wait(2.05f); Keys(); yield return Wait(1.2f);
        Capture("Logs/MovementCamera_City.png");
        Check(Finite(rig.transform.position) && Mathf.Abs(movement.transform.position.y - 5.14f) < .2f, "Camera and player remain valid on the real city street");
        FindFirstObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
        Check(runtimeError == null, "No runtime errors in movement, aiming, bob, dash or pause");
    }

    private IEnumerator Move(string label, Vector3 direction, float duration, params Key[] keys)
    {
        Warp(arena); yield return Wait(.15f);
        Vector3 start = movement.transform.position;
        Keys(keys); float until = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
        }
        Vector3 displacement = Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up);
        Check(displacement.magnitude > 5 && Vector3.Dot(displacement.normalized, direction) > .98f, label + ": world-axis movement stays straight while camera rotates");
        Check(BehindDot(direction) > .98f, label + ": camera settles behind travel; dot=" + BehindDot(direction).ToString("F3"));
        // Shooting checks immediately after this phase still observe the real held attack.
        if (!keys.Contains(Key.J)) { Keys(); yield return Wait(.25f); }
    }
    private float BehindDot(Vector3 direction)
    {
        Vector3 cameraOffset = Vector3.ProjectOnPlane(cameraView.transform.position - rig.transform.position, Vector3.up).normalized;
        return Vector3.Dot(cameraOffset, -direction);
    }
    private void Update()
    {
        if (keepEnemyNorth && enemy != null && movement != null) enemy.transform.position = movement.transform.position + Vector3.forward * 7f;
    }
    private void LateUpdate()
    {
        if (rig == null) return;
        // Observe after CameraFollow.LateUpdate, using the SAME frame's deltaTime.
        // Coroutine resumes occur before LateUpdate and pair the previous rotation step with a new dt.
        float yaw = rig.transform.eulerAngles.y;
        if (hasYawSample && Time.timeScale > 0 && Time.deltaTime > 0)
            peakYawSpeed = Mathf.Max(peakYawSpeed, Mathf.Abs(Mathf.DeltaAngle(previousYawSample, yaw)) / Time.deltaTime);
        previousYawSample = yaw; hasYawSample = true;
    }
    private void CreateEnemy()
    {
        enemy = new GameObject("Temporary camera aiming target"); enemy.SetActive(false); enemy.layer = 6;
        var capsule = enemy.AddComponent<CapsuleCollider>(); capsule.height = 2; capsule.radius = .4f; capsule.center = Vector3.up;
        var health = enemy.AddComponent<EnemyHealth>(); Set(health, "maxHealth", 100000f);
        var target = enemy.AddComponent<Targetable>(); var point = new GameObject("Aim Point").transform; point.SetParent(enemy.transform, false); point.localPosition = Vector3.up * 1.4f; Set(target, "aimPoint", point);
        enemy.transform.position = arena + Vector3.forward * 7; enemy.SetActive(true);
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private void Shot(Projectile _) => shots++;
    private void Warp(Vector3 position) { Keys(); cc.enabled = false; movement.transform.position = position; cc.enabled = true; Physics.SyncTransforms(); }
    private void Keys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private void Check(bool value, string label) { passed &= value; report.AppendLine((value ? "PASS " : "FAIL ") + label); }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
    private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    private void Capture(string path)
    {
        var rt = RenderTexture.GetTemporary(1500, 1000, 24); var oldTarget = cameraView.targetTexture; var oldActive = RenderTexture.active; var image = new Texture2D(1500, 1000, TextureFormat.RGB24, false);
        try { cameraView.targetTexture = rt; cameraView.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1500, 1000), 0, 0); image.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { cameraView.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); Destroy(image); }
    }
    private void Finish(Exception error)
    {
        if (done) return; done = true;
        if (error != null) { passed = false; report.AppendLine("EXCEPTION " + error); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        Keys(); if (pause != null) pause.SetPaused(GamePauseReason.Inventory, false); Time.timeScale = previousTimeScale;
        if (weapon != null) weapon.ProjectileFired -= Shot;
        if (input != null) input.actions.devices = previousDevices;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived -= OnLog; Application.runInBackground = previousBackground; Application.targetFrameRate = previousFps; QualitySettings.vSyncCount = previousVsync;
        if (floor != null) Destroy(floor); if (enemy != null) Destroy(enemy);
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MovementCameraPlaytest.txt", report.ToString());
        if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString()); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Camera regression interrupted")); }
}
#endif
