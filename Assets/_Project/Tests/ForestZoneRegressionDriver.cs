#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

// Uses the real plateau, scene player, input, NavMesh and enemy prefab; saves no fixtures.
public class ForestZoneRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private PlayerMovement movement;
    private PlayerInput input;
    private PlayerRangedWeapon weapon;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? oldDevices;
    private bool finished, passed = true, oldBackground;
    private int oldFps, oldVsync, shots;
    private string runtimeError;
    private IEnumerator Start()
    {
        oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount; oldBackground = Application.runInBackground;
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Forest plateau real-scene playtest — " + DateTime.Now.ToString("s"));
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
        movement = FindAnyObjectByType<PlayerMovement>(); input = movement.GetComponent<PlayerInput>(); weapon = movement.GetComponent<PlayerRangedWeapon>();
        var spawner = FindAnyObjectByType<EnemyWaveSpawner>(); spawner.StopAllCoroutines(); spawner.enabled = false;
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>()) ai.gameObject.SetActive(false);
        keyboard = InputSystem.AddDevice<Keyboard>("ForestPlaytestKeyboard");
        var devices = input.actions.devices; if (devices.HasValue) oldDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        input.actions.devices = new InputDevice[] { keyboard }; weapon.ProjectileFired += OnShot;
        yield return Wait(0.4f);
        Terrain t = Terrain.activeTerrains.Single(); Vector3 initial = movement.transform.position;
        Check(IsGrounded(initial, t), "Player settles onto walkable terrain or the summoning platform");
        Check(!Physics.Linecast(Camera.main.transform.position, initial + Vector3.up, 1 << t.gameObject.layer, QueryTriggerInteraction.Ignore), "Starting camera line to player is not blocked by tree/rock colliders");
        yield return Capture("Gameplay");
        Keys(Key.W); yield return Wait(0.5f); Keys(); yield return Wait(0.2f);
        Check(Vector3.Distance(initial, movement.transform.position) > 1.5f, "Real movement input traverses the plateau");
        Check(IsGrounded(movement.transform.position, t), "Player remains grounded while moving");
        Vector3 before = movement.transform.position;
        Keys(Key.W, Key.Space); yield return Wait(0.22f); Keys(); yield return Wait(0.35f);
        Check(Vector3.Distance(before, movement.transform.position) > 3f, "Short dash travels across the clearing");
        before = movement.transform.position;
        Keys(Key.M, Key.S); yield return Wait(0.5f);
        Check(movement.IsShadowDashing, "Shadow trail form still activates on the new terrain");
        Keys(); yield return Wait(0.9f);
        Check(!movement.IsShadowDashing && Vector3.Distance(before, movement.transform.position) > 2f, "Shadow form moves and returns to the player on the plateau");
        // Spawn the existing first-wave enemy on the new NavMesh, within detection range.
        var waves = new SerializedObject(spawner).FindProperty("waves");
        GameObject prefab = waves.GetArrayElementAtIndex(0).FindPropertyRelative("enemies").GetArrayElementAtIndex(0).FindPropertyRelative("enemyPrefab").objectReferenceValue as GameObject;
        Check(NavMesh.SamplePosition(movement.transform.position + Vector3.right * 7f, out NavMeshHit spawn, 1f, NavMesh.AllAreas), "Test enemy spawn resolves on the raised NavMesh");
        Vector3 enemyFacing = movement.transform.position - spawn.position; enemyFacing.y = 0;
        GameObject enemy = Instantiate(prefab, spawn.position, Quaternion.LookRotation(enemyFacing));
        var health = enemy.GetComponent<EnemyHealth>();
        float distance = Vector3.Distance(enemy.transform.position, movement.transform.position);
        yield return Wait(0.7f);
        var agent = enemy.GetComponent<NavMeshAgent>();
        Check(agent.isOnNavMesh && Vector3.Distance(enemy.transform.position, movement.transform.position) < distance - 0.7f, "Real enemy follows the player across the plateau");
        Keys(Key.J); yield return Wait(0.75f); Keys();
        Check(shots > 0, "Rifle still commits real shots in the forest scene");
        yield return Wait(0.25f);
        Check(health == null || health.CurrentHealth < health.MaxHealth, "Combat actually damages the enemy in the forest clearing");
        yield return Capture("Combat"); yield return Wait(0.2f);
        foreach (Shader shader in FindObjectsByType<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.shader).Where(s => s != null).Distinct())
            Check(!ShaderUtil.ShaderHasError(shader), "Rendered shader compiles: " + shader.name);
        Check(runtimeError == null, "No runtime errors/exceptions during movement, dash, enemy chase and shooting");
    }
    private void OnShot(Projectile bullet) => shots++;
    private static bool IsGrounded(Vector3 position, Terrain terrain) => Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit support, 3, 1 << terrain.gameObject.layer, QueryTriggerInteraction.Ignore) && Mathf.Abs(position.y - support.point.y) < 0.25f;
    private void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition; }
    private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static IEnumerator Wait(float duration) { float until = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Capture(string label) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/ForestDemo_" + label + ".png", 2); yield return null; }
    private void Check(bool condition, string message) { report.AppendLine((condition ? "PASS " : "FAIL ") + message); passed &= condition; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        if (failure != null) { passed = false; report.AppendLine("EXCEPTION " + failure); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        try { if (weapon != null) weapon.ProjectileFired -= OnShot; if (input != null) input.actions.devices = oldDevices; if (keyboard != null) InputSystem.RemoveDevice(keyboard); }
        finally { Application.logMessageReceived -= OnLog; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync; Application.runInBackground = oldBackground; }
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ForestZonePlaytest.txt", report.ToString());
        Debug.Log(report.ToString()); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Forest playtest interrupted")); }
}
#endif
