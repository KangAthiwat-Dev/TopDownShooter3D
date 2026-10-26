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
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

// Real scene coverage. Fixtures exist only in Play Mode, including the restart test.
public sealed class SummoningPlatformRegressionDriver : MonoBehaviour
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
        DontDestroyOnLoad(gameObject);
        oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount; oldBackground = Application.runInBackground;
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Summoning sanctuary real-scene playtest — " + DateTime.Now.ToString("s"));
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
        var spawn = FindAnyObjectByType<PlayerSceneSpawnPoint>();
        var spawner = FindAnyObjectByType<EnemyWaveSpawner>(); spawner.StopAllCoroutines(); spawner.enabled = false;
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>()) ai.gameObject.SetActive(false);
        keyboard = InputSystem.AddDevice<Keyboard>("SummoningPlaytestKeyboard");
        var devices = input.actions.devices; if (devices.HasValue) oldDevices = new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        input.actions.devices = new InputDevice[] { keyboard }; weapon.ProjectileFired += OnShot;
        yield return Wait(0.4f);
        Vector3 initial = movement.transform.position;
        Check(Vector3.Distance(initial, spawn.Position) < 0.2f, "Player starts at the platform marker");
        Check(Grounded(initial, out RaycastHit support) && support.collider.name == "CirclePlatformSmall_001", "Feet stand on the original platform, not inside or below it");
        Check(!movement.IsInteractionLocked && movement.GetComponent<CharacterController>().enabled, "Movement/controller active immediately; no summoning cutscene or input lock added");
        Check(Vector3.Distance(Camera.main.transform.parent.position, initial) < 3, "Follow camera starts near the new player location");
        Check(!Physics.Linecast(Camera.main.transform.position, initial + Vector3.up, 1 << Terrain.activeTerrain.gameObject.layer, QueryTriggerInteraction.Ignore), "Initial camera line to player is clear");
        yield return Capture("Gameplay");
        Keys(Key.W, Key.D); yield return Wait(1.95f); Keys(); yield return Wait(0.3f);
        Check(Vector3.Distance(initial, movement.transform.position) > 8f, "Real input walks off the platform through the open northeast exit");
        Check(Grounded(movement.transform.position, out support), "Player stays supported on the stone path/terrain after leaving");
        report.AppendLine("WALK_OUT position=" + movement.transform.position.ToString("F3") + " distance=" + Vector3.Distance(initial, movement.transform.position).ToString("F3"));
        Keys(Key.S, Key.A); yield return Wait(1.95f); Keys(); yield return Wait(0.3f);
        Check(Vector3.Distance(initial, movement.transform.position) < 2f, "Player can walk back up the existing slope without getting stuck on the rim");
        Check(Grounded(movement.transform.position, out support) && support.collider.name == "CirclePlatformSmall_001", "Walking back returns to the top of the platform");
        Check(spawn.PlacePlayer(), "Spawn marker can place the existing scene player"); yield return Wait(0.2f);
        Vector3 before = movement.transform.position;
        Keys(Key.W, Key.D, Key.Space); yield return Wait(0.25f); Keys(); yield return Wait(0.4f);
        Check(Vector3.Distance(before, movement.transform.position) > 3f, "Short dash still works on the platform");
        spawn.PlacePlayer(); yield return Wait(0.2f);
        Keys(Key.W, Key.D, Key.M); yield return Wait(0.5f);
        Check(movement.IsShadowDashing, "Shadow form still activates at the new spawn");
        Keys(); yield return Wait(0.95f);
        Check(!movement.IsShadowDashing && Grounded(movement.transform.position, out support), "Shadow dash crosses the rim and lands back on a supported surface");
        spawn.PlacePlayer(); yield return Wait(0.2f);

        var settings = new SerializedObject(spawner);
        Transform marker = settings.FindProperty("spawnPoints").GetArrayElementAtIndex(0).objectReferenceValue as Transform;
        GameObject prefab = settings.FindProperty("waves").GetArrayElementAtIndex(0).FindPropertyRelative("enemies").GetArrayElementAtIndex(0).FindPropertyRelative("enemyPrefab").objectReferenceValue as GameObject;
        Check(NavMesh.SamplePosition(marker.position, out NavMeshHit point, 1, NavMesh.AllAreas), "Real wave marker is on rebuilt navigation");
        Vector3 enemyFacing = movement.transform.position - point.position; enemyFacing.y = 0;
        GameObject enemy = Instantiate(prefab, point.position, Quaternion.LookRotation(enemyFacing)); var health = enemy.GetComponent<EnemyHealth>();
        float distance = Vector3.Distance(enemy.transform.position, movement.transform.position);
        Check(distance >= 7 && distance < 12, "Enemy marker respects spawn distance and starts inside detection range");
        yield return Wait(1.4f);
        var agent = enemy.GetComponent<NavMeshAgent>();
        Check(agent.isOnNavMesh && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete && Vector3.Distance(enemy.transform.position, movement.transform.position) < distance - 0.7f, "Actual enemy approaches the platform along a complete path");
        Keys(Key.J); yield return Wait(1.15f); Keys(); yield return Wait(0.35f);
        Check(shots > 0, "Rifle still fires actual projectiles from the sanctuary");
        Check(health == null || health.CurrentHealth < health.MaxHealth, "Actual projectile combat can damage the approaching enemy");
        yield return Capture("Combat");
        var panel = FindAnyObjectByType<InventoryPanelUI>(); Check(panel.TryOpen(), "Inventory still opens on the platform");
        before = movement.transform.position; yield return Wait(0.25f);
        Check(Time.timeScale == 0 && movement.transform.position == before, "Inventory pause still freezes gameplay");
        panel.Close(); yield return Wait(0.15f);

        // Restore/remove the test device before destroying its owning PlayerInput.
        ReleaseInput();
        string scene = SceneManager.GetActiveScene().path;
        EditorSceneManager.LoadSceneInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
        yield return Wait(0.45f);
        movement = FindAnyObjectByType<PlayerMovement>(); spawn = FindAnyObjectByType<PlayerSceneSpawnPoint>();
        spawner = FindAnyObjectByType<EnemyWaveSpawner>(); spawner.StopAllCoroutines(); spawner.enabled = false;
        Check(Vector3.Distance(movement.transform.position, spawn.Position) < 0.2f && Grounded(movement.transform.position, out support) && support.collider.name == "CirclePlatformSmall_001", "Reloading the actual scene starts the player on the same platform again");
        Check(FindObjectsByType<PlayerSceneSpawnPoint>().Length == 1, "Reload leaves one spawn marker, no duplicated player/spawn setup");
        Check(FindObjectsByType<PlayerMovement>().Length == 1, "Exactly one player after scene reload");
        yield return Capture("SpawnAfterReload");
        Check(runtimeError == null, "No runtime errors during spawn, movement, dash, combat, pause or reload");
    }
    private static bool Grounded(Vector3 p, out RaycastHit hit) => Physics.Raycast(p + Vector3.up, Vector3.down, out hit, 3, 1 << Terrain.activeTerrain.gameObject.layer, QueryTriggerInteraction.Ignore) && Mathf.Abs(p.y - hit.point.y) < 0.25f;
    private void OnShot(Projectile projectile) => shots++;
    private void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition; }
    private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static IEnumerator Wait(float duration) { float until = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Capture(string label) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/SummoningPlatform_" + label + ".png"); yield return null; }
    private void Check(bool condition, string message) { report.AppendLine((condition ? "PASS " : "FAIL ") + message); passed &= condition; }
    private void ReleaseInput()
    {
        if (weapon != null) weapon.ProjectileFired -= OnShot;
        if (input != null) input.actions.devices = oldDevices;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        keyboard = null; input = null; weapon = null; oldDevices = null;
    }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        if (failure != null) { passed = false; report.AppendLine("EXCEPTION " + failure); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        try { ReleaseInput(); }
        finally { Application.logMessageReceived -= OnLog; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync; Application.runInBackground = oldBackground; }
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SummoningPlaytest.txt", report.ToString());
        Debug.Log(report.ToString()); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Summoning playtest interrupted")); }
}
#endif
