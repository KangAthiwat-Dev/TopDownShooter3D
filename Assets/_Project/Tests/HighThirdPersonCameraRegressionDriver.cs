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

[DefaultExecutionOrder(10000)]
public sealed class HighThirdPersonCameraRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly Vector3 arena = new Vector3(1000, 10, 1000);
    private PlayerMovement movement;
    private PlayerInput input;
    private CharacterController cc;
    private PlayerRangedWeapon weapon;
    private PlayerCombatFacing facing;
    private CameraFollow rig;
    private Camera view;
    private CameraBoomCollision boom;
    private CameraOcclusionFade fade;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? previousDevices;
    private GamePauseController pause;
    private GameObject floor, enemy, obstacle;
    private bool passed = true, done, previousBackground, keepEnemyOffset, hasYawSample;
    private int previousFps, previousVsync, shots;
    private float previousTimeScale, previousYaw, peakYawSpeed;
    private Vector3 enemyDirection;
    private string runtimeError;

    private IEnumerator Start()
    {
        previousBackground = Application.runInBackground; previousFps = Application.targetFrameRate;
        previousVsync = QualitySettings.vSyncCount; previousTimeScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Actual-player high third-person camera " + DateTime.Now.ToString("s"));
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
        movement = FindFirstObjectByType<PlayerMovement>(); input = movement.GetComponent<PlayerInput>(); cc = movement.GetComponent<CharacterController>();
        weapon = movement.GetComponent<PlayerRangedWeapon>(); facing = movement.GetComponent<PlayerCombatFacing>();
        rig = FindFirstObjectByType<CameraFollow>(); view = rig.FollowCamera; boom = rig.GetComponent<CameraBoomCollision>(); fade = rig.GetComponent<CameraOcclusionFade>();
        var views = rig.GetComponent<CameraViewSettings>();
        if(views != null) views.ApplyView(CameraViewMode.HighThirdPerson,false);
        var devices = input.actions.devices; if (devices.HasValue) previousDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard = InputSystem.AddDevice<Keyboard>("HighThirdPersonTestKeyboard"); input.actions.devices = new InputDevice[] { keyboard };
        weapon.ProjectileFired += Shot;
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Temporary third person arena";
        floor.transform.position = arena + Vector3.down * .6f; floor.transform.localScale = new Vector3(140, 1, 140); floor.layer = 7;
        yield return Wait(.4f); Warp(arena); yield return Wait(1.5f);
        Check(movement.UsesCameraRelativeMovement && rig.FollowsMovementHeading, "View-relative input and automatic travel-facing camera enabled");
        Check(fade == null || (!fade.enabled && fade.ActiveFadeCount == 0), "Transparency disabled, no active faded objects");
        Check(Vector3.Distance(view.transform.localPosition, new Vector3(0, 6, -8)) < .01f && Mathf.Abs(view.fieldOfView - 55f) < .01f, "High third-person boom and wider field of view configured");

        yield return Travel("W starts forward in the view", new Vector2(0, 1), 3.2f, Key.W);
        // User's exact sequence: D turns right; once the camera catches up W continues straight.
        yield return Travel("D turns right relative to the current view", new Vector2(1, 0), 4f, Key.D);
        Vector3 afterRight = Flat(view.transform.forward);
        yield return Travel("W after D continues in the newly aligned direction", new Vector2(0, 1), 2.2f, Key.W);
        Check(Vector3.Dot(afterRight, Flat(cc.velocity)) > .99f, "D -> W preserves the new forward direction rather than reverting to world north");
        Keys(); yield return Wait(.7f);
        yield return Travel("A turns left relative to the view", new Vector2(-1, 0), 3.5f, Key.A);
        yield return Travel("S chooses the back direction without spinning indefinitely", new Vector2(0, -1), 4.8f, Key.S);
        yield return Travel("W+D diagonal remains normalised and straight", new Vector2(1, 1), 3.5f, Key.W, Key.D);
        Keys(); yield return Wait(.8f);
        yield return Travel("Holding D for six seconds does not create camera/movement feedback circles", new Vector2(1, 0), 6f, Key.D);
        Keys(); yield return Wait(1f);

        Warp(arena); yield return Wait(.6f);
        enemyDirection = Flat(view.transform.forward); CreateEnemy(); keepEnemyOffset = true;
        int beforeShots = shots;
        yield return Travel("Move right while shooting at a different direction", new Vector2(1, 0), 3.5f, Key.D, Key.J);
        Check(shots > beforeShots && facing.IsAiming && Vector3.Angle(movement.transform.forward, enemyDirection) < 12f, "Actual weapon fires and body faces the enemy independently of travel");
        Check(Vector3.Dot(Flat(view.transform.forward), enemyDirection) < .2f, "Camera follows travel, not the enemy or gun-facing rotation");
        Keys(Key.J); yield return Wait(1f); keepEnemyOffset = false;
        float idleYaw = rig.transform.eulerAngles.y, bodyYaw = movement.transform.eulerAngles.y; beforeShots = shots;
        float until = Time.realtimeSinceStartup + 1.4f;
        while (Time.realtimeSinceStartup < until)
        {
            float angle = (1.4f - (until - Time.realtimeSinceStartup)) * 70f;
            enemy.transform.position = movement.transform.position + Quaternion.Euler(0, angle, 0) * enemyDirection * 7f;
            yield return null;
        }
        Check(shots > beforeShots && Mathf.Abs(Mathf.DeltaAngle(bodyYaw, movement.transform.eulerAngles.y)) > 40f, "Idle body tracks a moving enemy and continues shooting");
        Check(Mathf.Abs(Mathf.DeltaAngle(idleYaw, rig.transform.eulerAngles.y)) < .5f, "Changing aim does not pull the idle camera around");
        Keys(); enemy.SetActive(false); yield return Wait(.8f);

        Warp(arena); yield return Wait(.6f);
        Vector3 sprintForward = Flat(view.transform.forward); Keys(Key.W, Key.LeftShift); yield return Wait(1f);
        Check(movement.IsRunning && Vector3.Dot(Flat(cc.velocity), sprintForward) > .99f && Vector3.Distance(view.transform.localPosition, new Vector3(0, 6, -8)) > .001f, "Sprint uses the new input reference and retains subtle bob");
        Keys(); yield return Wait(1f);
        Check(Vector3.Distance(view.transform.localPosition, new Vector3(0, 6, -8)) < .01f, "Sprint bob recovers without accumulating boom drift");
        Vector3 dashForward = Flat(view.transform.forward); float dashYaw = rig.transform.eulerAngles.y;
        Keys(Key.W, Key.Space); yield return Wait(.09f);
        Check(movement.IsDashing && Vector3.Dot(Flat(cc.velocity), dashForward) > .99f && view.fieldOfView > 55.5f, "Short dash follows view-relative W and retains FOV punch");
        Check(Mathf.Abs(Mathf.DeltaAngle(dashYaw, rig.transform.eulerAngles.y)) < .5f, "Dash does not whip the camera to another heading");
        Keys(); yield return Wait(1f);
        Vector3 shadowForward = Flat(view.transform.forward); Keys(Key.W, Key.M); yield return Wait(.35f);
        Check(movement.IsShadowDashing && Vector3.Dot(Flat(cc.velocity), shadowForward) > .99f, "Shadow Dash also uses the captured camera-relative movement direction");
        Keys(); yield return Wait(1f);

        Warp(arena); yield return Wait(1f);
        obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "Temporary third person camera obstacle"; obstacle.layer = 7;
        obstacle.transform.position = Vector3.Lerp(rig.transform.position, view.transform.position, .55f);
        obstacle.transform.rotation = Quaternion.LookRotation(Flat(view.transform.position - rig.transform.position)); obstacle.transform.localScale = new Vector3(5, 5, .5f);
        Material originalMaterial = obstacle.GetComponent<Renderer>().sharedMaterial;
        Physics.SyncTransforms(); yield return Wait(.15f);
        float blockedDistance = boom.CurrentDistance;
        Check(boom.IsObstructed && blockedDistance < 6f, "Solid wall shortens the boom without making scenery transparent");
        Check(Vector3.Distance(obstacle.GetComponent<Collider>().ClosestPoint(view.transform.position), view.transform.position) > .1f && obstacle.GetComponent<Renderer>().sharedMaterial == originalMaterial, "Camera stays outside the wall and the original material is unchanged");
        pause = FindFirstObjectByType<GamePauseController>(); pause.SetPaused(GamePauseReason.Inventory, true);
        Vector3 pausedCamera = view.transform.position; float pausedYaw = rig.transform.eulerAngles.y;
        obstacle.SetActive(false); Keys(Key.D); yield return Wait(.3f);
        Check(Time.timeScale == 0 && Vector3.Distance(pausedCamera, view.transform.position) < .001f && Mathf.Abs(Mathf.DeltaAngle(pausedYaw, rig.transform.eulerAngles.y)) < .001f, "Inventory pause freezes follow, orbit, collision recovery and movement");
        Keys(); pause.SetPaused(GamePauseReason.Inventory, false); yield return Wait(.1f);
        Check(boom.CurrentDistance > blockedDistance && boom.CurrentDistance < 9.8f, "Boom smoothly extends after the view clears");
        yield return Wait(1.2f);
        Check(Mathf.Abs(boom.CurrentDistance - 10f) < .02f, "Boom recovers the authored 10 m distance");
        obstacle.GetComponent<BoxCollider>().isTrigger = true; obstacle.SetActive(true); Physics.SyncTransforms(); yield return Wait(.2f);
        Check(!boom.IsObstructed && boom.CurrentDistance > 9.9f, "Trigger volumes do not push the camera in"); obstacle.SetActive(false);

        Application.targetFrameRate = 30; yield return Travel("W at requested 30 FPS", Vector2.up, 2.4f, Key.W); Keys(); yield return Wait(.6f);
        Application.targetFrameRate = 120; yield return Travel("D at requested 120 FPS", Vector2.right, 3.5f, Key.D); Keys(); yield return Wait(.6f);
        Application.targetFrameRate = 60;
        Check(peakYawSpeed <= 122f, "Smooth camera yaw respects 120 degree/second cap; measured peak=" + peakYawSpeed.ToString("F2"));
        Warp(new Vector3(75.76f, 5.22f, -75.8f)); yield return Wait(1.6f);
        Capture("Logs/HighThirdPerson_City.png");
        Check(Mathf.Abs(movement.transform.position.y - 5.14f) < .2f && (fade == null || fade.ActiveFadeCount == 0), "Real city view uses solid scenery and remains grounded");
        FindFirstObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
        Check(runtimeError == null, "No runtime errors across movement, aiming, dash, collision or pause");
    }
    private IEnumerator Travel(string label, Vector2 axes, float duration, params Key[] keys)
    {
        Keys(); yield return Wait(.5f);
        Vector3 forward = Flat(view.transform.forward), right = Vector3.Cross(Vector3.up, forward);
        Vector3 expected = (right * axes.x + forward * axes.y).normalized;
        Vector3 start = movement.transform.position; float maxLateral = 0f, maxSpeed = 0f;
        Keys(keys); float until = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < until)
        {
            Vector3 displacement = Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up);
            maxLateral = Mathf.Max(maxLateral, Vector3.Cross(displacement, expected).magnitude);
            maxSpeed = Mathf.Max(maxSpeed, Vector3.ProjectOnPlane(cc.velocity, Vector3.up).magnitude);
            yield return null;
        }
        Vector3 travel = Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up);
        Check(travel.magnitude > 5f && Vector3.Dot(travel.normalized, expected) > .995f && maxLateral < .18f, label + ": straight path in captured view direction; lateral=" + maxLateral.ToString("F3"));
        Check(Vector3.Dot(Flat(view.transform.forward), expected) > .99f, label + ": camera settles behind travel");
        Check(maxSpeed <= movement.RunSpeed + .15f, label + ": no diagonal speed boost");
        // Leave held attack/movement available to checks immediately following this segment.
    }
    private static Vector3 Flat(Vector3 value) => Vector3.ProjectOnPlane(value, Vector3.up).normalized;
    private void Update() { if (keepEnemyOffset && enemy != null) enemy.transform.position = movement.transform.position + enemyDirection * 7f; }
    private void LateUpdate()
    {
        if (rig == null) return;
        float yaw = rig.transform.eulerAngles.y;
        if (hasYawSample && Time.timeScale > 0 && Time.deltaTime > 0) peakYawSpeed = Mathf.Max(peakYawSpeed, Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw)) / Time.deltaTime);
        previousYaw = yaw; hasYawSample = true;
    }
    private void CreateEnemy()
    {
        enemy = new GameObject("Temporary third person aiming target"); enemy.SetActive(false); enemy.layer = 6;
        var collider = enemy.AddComponent<CapsuleCollider>(); collider.height = 2; collider.radius = .4f; collider.center = Vector3.up;
        var health = enemy.AddComponent<EnemyHealth>(); Set(health, "maxHealth", 100000f);
        var target = enemy.AddComponent<Targetable>(); var aim = new GameObject("Aim Point").transform;
        aim.SetParent(enemy.transform, false); aim.localPosition = Vector3.up * 1.4f; Set(target, "aimPoint", aim);
        enemy.transform.position = movement.transform.position + enemyDirection * 7f; enemy.SetActive(true);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private void Warp(Vector3 position) { Keys(); cc.enabled = false; movement.transform.position = position; cc.enabled = true; Physics.SyncTransforms(); }
    private void Keys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private void Shot(Projectile _) => shots++;
    private void Check(bool value, string label) { passed &= value; report.AppendLine((value ? "PASS " : "FAIL ") + label); }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
    private void Capture(string path)
    {
        var rt = RenderTexture.GetTemporary(1500, 1000, 24); var oldTarget = view.targetTexture; var oldActive = RenderTexture.active; var image = new Texture2D(1500, 1000, TextureFormat.RGB24, false);
        try { view.targetTexture = rt; view.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1500, 1000), 0, 0); image.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { view.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); Destroy(image); }
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
        Application.logMessageReceived -= OnLog; Application.runInBackground = previousBackground;
        Application.targetFrameRate = previousFps; QualitySettings.vSyncCount = previousVsync;
        if (floor != null) Destroy(floor); if (enemy != null) Destroy(enemy); if (obstacle != null) Destroy(obstacle);
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/HighThirdPersonPlaytest.txt", report.ToString());
        if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString()); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Regression interrupted")); }
}
#endif
