#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class NpcMerchantRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private PlayerMovement movement;
    private PlayerInput input;
    private CharacterController cc;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? previousDevices;
    private bool done, passed = true, previousBackground;
    private int previousFps, previousVsync;
    private float previousTimeScale;
    private string runtimeError;
    private GameObject temporaryWall;
    private GamePauseController pause;

    private IEnumerator Start()
    {
        previousBackground = Application.runInBackground; previousFps = Application.targetFrameRate;
        previousVsync = QualitySettings.vSyncCount; previousTimeScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Actual-player merchant regression " + DateTime.Now.ToString("s"));
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
        foreach (var enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        movement = FindFirstObjectByType<PlayerMovement>(); input = movement.GetComponent<PlayerInput>(); cc = movement.GetComponent<CharacterController>();
        var devices = input.actions.devices;
        if (devices.HasValue) previousDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard = InputSystem.AddDevice<Keyboard>("MerchantTestKeyboard"); input.actions.devices = new InputDevice[] { keyboard };
        var npc = FindObjectsByType<NpcIdleLook>().Single(n => n.NpcId == "armory_merchant_01"); var animator = npc.GetComponentInChildren<Animator>();
        Vector3 homePosition = npc.transform.position; Quaternion homeFacing = npc.FacingRoot.rotation;
        Warp(homePosition + Vector3.forward * 7);
        yield return Wait(.8f);
        Check(!npc.IsWatching, "Merchant ignores a distant player");
        Check(animator.isHuman && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Valid Humanoid Avatar plays Idle, not T-pose");
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Quaternion oldHead = head.rotation; float oldTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        yield return Wait(.7f);
        Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > oldTime && Quaternion.Angle(oldHead, head.rotation) > .02f, "Idle time advances and the head actually animates");
        Check(Vector3.Distance(homePosition, npc.transform.position) < .001f, "Idle/root motion does not move the merchant");

        Warp(homePosition + Vector3.forward * 2.8f);
        yield return Wait(.4f);
        Check(npc.IsWatching, "Nearby actual player attracts the merchant's attention");
        Check(Quaternion.Angle(npc.FacingRoot.rotation, homeFacing) < 4f, "Merchant model faces the same +Z direction as its look pivot");
        var eyes = npc.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "Eyes");
        var bakedEyes = new Mesh(); eyes.BakeMesh(bakedEyes);
        Vector3 eyeCentre = eyes.transform.TransformPoint(bakedEyes.vertices.Aggregate(Vector3.zero, (sum, p) => sum + p) / bakedEyes.vertexCount);
        Destroy(bakedEyes);
        Vector3 faceDirection = Vector3.ProjectOnPlane(eyeCentre - head.position, Vector3.up).normalized;
        Check(Vector3.Dot(faceDirection, npc.FacingRoot.forward) > .35f, "Visible face, not the back of the head, points at the player");

        Warp(homePosition + new Vector3(2.4f, 0, 1.8f));
        Quaternion beforeTurn = npc.FacingRoot.rotation;
        yield return null;
        Check(Quaternion.Angle(beforeTurn, npc.FacingRoot.rotation) < 20f, "Turning is interpolated rather than snapping in one frame");
        yield return Wait(.85f);
        Vector3 desired = Vector3.ProjectOnPlane(movement.transform.position - homePosition, Vector3.up).normalized;
        Check(npc.IsWatching && Vector3.Dot(npc.FacingRoot.forward, desired) > .98f, "Merchant smoothly tracks the player to the side");
        Warp(homePosition + Vector3.forward * 4.5f);
        yield return Wait(.7f);
        Check(npc.IsWatching, "Release-radius hysteresis avoids flicker at the notice boundary");
        Warp(homePosition + Vector3.forward * 6);
        yield return Wait(.9f);
        Check(!npc.IsWatching && Quaternion.Angle(homeFacing, npc.FacingRoot.rotation) < 2f, "Merchant releases the distant player and returns to its original facing");

        Warp(homePosition + Vector3.forward * 2.8f);
        yield return Wait(.3f);
        temporaryWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporaryWall.name = "Temporary sight blocker — Play Mode only"; temporaryWall.layer = 7;
        temporaryWall.transform.position = homePosition + new Vector3(0, 1.25f, 1.4f);
        temporaryWall.transform.localScale = new Vector3(1.5f, 2.5f, .15f); Physics.SyncTransforms();
        yield return Wait(.2f);
        Check(!npc.IsWatching, "An environment wall blocks looking at the player");
        Destroy(temporaryWall); temporaryWall = null;
        yield return Wait(.3f);
        Check(npc.IsWatching, "Attention resumes after the obstruction is removed");

        pause = FindFirstObjectByType<GamePauseController>();
        pause.SetPaused(GamePauseReason.Inventory, true);
        // Finish the already-scheduled animation evaluation before measuring a paused interval.
        yield return null; yield return null;
        float pausedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime; Quaternion pausedFacing = npc.FacingRoot.rotation;
        Warp(homePosition + new Vector3(-2, 0, 2)); yield return Wait(.3f);
        float idleAdvance = Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - pausedTime);
        float facingAdvance = Quaternion.Angle(pausedFacing, npc.FacingRoot.rotation);
        Check(pause.IsPaused && Time.timeScale == 0f && idleAdvance < .001f && facingAdvance < .01f, "Inventory pause freezes Idle and turning; idleDelta=" + idleAdvance + ", yawDelta=" + facingAdvance);
        pause.SetPaused(GamePauseReason.Inventory, false);

        Warp(homePosition + Vector3.forward * 2.2f); yield return Wait(.3f);
        Keys(Key.S); yield return Wait(.8f); Keys(); yield return Wait(.2f);
        Check(Vector2.Distance(new Vector2(movement.transform.position.x, movement.transform.position.z), new Vector2(homePosition.x, homePosition.z)) > .58f, "Actual CharacterController cannot walk through the merchant");
        Check(Mathf.Abs(movement.transform.position.y - homePosition.y) < .2f, "Player remains grounded at the merchant forecourt");
        Check(npc.GetComponent<Targetable>() == null && npc.GetComponent<EnemyAI>() == null && npc.GetComponent<EnemyHealth>() == null && npc.gameObject.layer != 6, "Merchant never joins enemy targeting/combat layers");
        Check(npc.GetComponent<NavMeshObstacle>().carving && !NavMesh.SamplePosition(homePosition, out _, .08f, NavMesh.AllAreas), "Stationary merchant footprint is carved from navigation");
        var path = new NavMeshPath();
        bool route = NavMesh.SamplePosition(homePosition + new Vector3(-1.8f, 0, 1.4f), out var a, .8f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(homePosition + new Vector3(1.8f, 0, 1.4f), out var b, .8f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        Check(route, "Navigation still connects both sides of the shop forecourt");
        Check(npc.GetComponentsInChildren<SkinnedMeshRenderer>().All(s => s.bones.All(bone => bone != null && bone.IsChildOf(npc.transform))), "Head, eyes, hair and clothing remain bound to one rig during animation");
        Warp(homePosition + Vector3.forward * 7); yield return Wait(.8f);
        Capture(npc, "Logs/NpcMerchant_Playmode.png");
        FindFirstObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
        Check(runtimeError == null, "No runtime errors during the NPC/player test");
    }

    private void Warp(Vector3 p) { Keys(); cc.enabled = false; movement.transform.position = new Vector3(p.x, 5.22f, p.z); cc.enabled = true; Physics.SyncTransforms(); }
    private void Keys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private void Check(bool value, string label) { passed &= value; report.AppendLine((value ? "PASS " : "FAIL ") + label); }
    private void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition; }
    private static void Capture(NpcIdleLook npc, string path)
    {
        var go = new GameObject("Temporary runtime merchant camera"); var camera = go.AddComponent<Camera>();
        var rt = RenderTexture.GetTemporary(1100, 1300, 24); var previous = RenderTexture.active; var image = new Texture2D(1100, 1300, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = npc.transform.position + new Vector3(-2.1f, 2.1f, 4.3f); camera.transform.LookAt(npc.transform.position + Vector3.up * .95f);
            camera.fieldOfView = 35; camera.nearClipPlane = .05f; camera.farClipPlane = 200;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1100, 1300), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(image); Destroy(go); }
    }

    private void Finish(Exception error)
    {
        if (done) return; done = true;
        if (error != null) { passed = false; report.AppendLine("EXCEPTION " + error); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        if (pause != null) pause.SetPaused(GamePauseReason.Inventory, false);
        Time.timeScale = previousTimeScale; Keys();
        if (temporaryWall != null) Destroy(temporaryWall);
        if (input != null) input.actions.devices = previousDevices;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived -= OnLog; Application.runInBackground = previousBackground; Application.targetFrameRate = previousFps; QualitySettings.vSyncCount = previousVsync;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/NpcMerchantPlaytest.txt", report.ToString());
        if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Playtest interrupted")); }
}
#endif
