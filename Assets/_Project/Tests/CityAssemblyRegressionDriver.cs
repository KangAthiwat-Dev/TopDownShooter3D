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

public sealed class CityAssemblyRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private CityAssemblyRoot city;
    private TownMusicZone music;
    private TownNpcPopulation population;
    private PlayerMovement movement;
    private CharacterController cc;
    private PlayerInput input;
    private ReadOnlyArray<InputDevice>? oldDevices;
    private Gamepad pad;
    private Keyboard keyboard;
    private bool passed = true, finished, oldBackground;
    private int oldFps, oldVsync;
    private float oldTimeScale;
    private string runtimeError;

    private IEnumerator Start()
    {
        oldBackground = Application.runInBackground; oldFps = Application.targetFrameRate;
        oldVsync = QualitySettings.vSyncCount; oldTimeScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.timeScale = 1;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Grounded city actual-player / NPC / music test " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception error = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; }
            catch (Exception failure) { error = failure; }
            if (error != null) { Finish(error); yield break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }

    private IEnumerator Checks()
    {
        city = FindAnyObjectByType<CityAssemblyRoot>(); music = city.GetComponentInChildren<TownMusicZone>();
        population = city.GetComponentInChildren<TownNpcPopulation>(); movement = FindAnyObjectByType<PlayerMovement>();
        cc = movement.GetComponent<CharacterController>(); input = movement.GetComponent<PlayerInput>();
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        if (input.actions.devices.HasValue) oldDevices = new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        pad = InputSystem.AddDevice<Gamepad>("CityAssemblyTestPad"); keyboard = InputSystem.AddDevice<Keyboard>("CityAssemblyTestKeyboard");
        input.actions.devices = new InputDevice[] { pad, keyboard };
        FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown, false);
        yield return Wait(2f);
        Check(!city.NavigationNeedsRebuild, "city navigation is current before gameplay");
        Check(!music.IsInside && music.MusicSource.volume < .01f, "city music remains silent at original player spawn");
        float until = Time.realtimeSinceStartup + 6f;
        while (population.AliveCount < population.MaxPopulation && Time.realtimeSinceStartup < until) yield return null;
        Check(population.AliveCount == population.MaxPopulation, "citizens spawn at grounded city height: " + population.AliveCount + "/" + population.MaxPopulation);
        var citizens = city.GetComponentsInChildren<TownNpcBrain>();
        Check(citizens.Length >= population.MaxPopulation && citizens.All(npc => npc.Agent.isOnNavMesh), "all spawned citizens stand on navigation after lowering city");
        Check(citizens.Count(npc => npc.IsConstructionWorker) == population.ConfiguredWorkers, "construction workers retain assigned city stations");
        var positions = citizens.ToDictionary(npc => npc, npc => npc.transform.position);
        yield return Wait(3f);
        Check(citizens.Any(npc => Vector3.Distance(npc.transform.position, positions[npc]) > .25f), "citizens walk around the grounded city");

        Vector3 Point(float x, float z) => city.transform.TransformPoint(new Vector3(x, .14f, z));
        // The fountain occupies the exact centre of this intersection; use the street beside it.
        var west = Point(-26, 1.8889f); var crossing = Point(-3.5f, 1.8889f); var north = Point(0, 28);
        foreach (var pair in new[] { new[] { west, crossing }, new[] { crossing, north } })
        {
            var path = new NavMeshPath();
            bool startValid = NavMesh.SamplePosition(pair[0], out var start, 1.2f, NavMesh.AllAreas);
            bool endValid = NavMesh.SamplePosition(pair[1], out var end, 1.2f, NavMesh.AllAreas);
            Check(startValid && endValid && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "navigation connects city gate route " + pair[0].ToString("F2") + " -> " + pair[1].ToString("F2") +
                " sampled=" + startValid + "/" + endValid + " status=" + path.status);
        }
        Warp(west); yield return Wait(.4f);
        yield return Travel(crossing, "walk through west gate into city");
        yield return Wait(.8f);
        Check(music.IsInside && music.MusicSource.volume > .01f && FindAnyObjectByType<AdventureHudUI>().DisplayedArea == music.AreaName,
            "city music fades in and HUD recognises the parented city");
        Check(Mathf.Abs(movement.transform.position.y - city.FloorHeight) < .3f, "player stands on paving instead of floating at old height");
        Capture();
        yield return Travel(north, "walk through north gate onto terrain");
        yield return Wait(music.FadeOutSeconds + .3f);
        Check(!music.IsInside && music.MusicSource.volume < .01f, "leaving moved city bounds fades music back to silence");
        Check(population.AliveCount == population.MaxPopulation, "population remains stable during city traversal");
        Check(runtimeError == null, "no runtime errors in city movement NPCs or music");
        FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
    }
    private IEnumerator Travel(Vector3 target, string label)
    {
        float until = Time.realtimeSinceStartup + Vector3.Distance(movement.transform.position, target) / 3f + 5f;
        while (Time.realtimeSinceStartup < until && Vector3.ProjectOnPlane(target - movement.transform.position, Vector3.up).magnitude > .25f)
        { Move(target - movement.transform.position); yield return null; }
        Move(Vector3.zero); yield return Wait(.15f);
        Check(Vector3.ProjectOnPlane(target - movement.transform.position, Vector3.up).magnitude < .65f,
            label + " actual=" + movement.transform.position.ToString("F3") + " goal=" + target.ToString("F3"));
    }
    private void Warp(Vector3 point) { Move(Vector3.zero); cc.enabled = false; movement.transform.position = point + Vector3.up * .08f; cc.enabled = true; Physics.SyncTransforms(); }
    private void Move(Vector3 direction)
    {
        direction.y = 0; if (pad != null) InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(direction.x, direction.z).normalized });
        if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private void Capture()
    {
        var camera = FindAnyObjectByType<CameraFollow>().FollowCamera;
        var rt = RenderTexture.GetTemporary(1400, 1000, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); image.Apply(); File.WriteAllBytes("Logs/CityAssembly_Gameplay.png", image.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(image); }
    }
    private void Check(bool valid, string message) { passed &= valid; report.AppendLine((valid ? "PASS " : "FAIL ") + message); File.WriteAllText("Logs/CityAssemblyPlaytest.txt", report.ToString()); }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        if (failure != null) { passed = false; report.AppendLine("EXCEPTION " + failure); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        Move(Vector3.zero); if (input != null) input.actions.devices = oldDevices;
        if (pad != null) InputSystem.RemoveDevice(pad); if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived -= OnLog;
        Application.runInBackground = oldBackground; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync; Time.timeScale = oldTimeScale;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/CityAssemblyPlaytest.txt", report.ToString());
        Debug.Log("Grounded city player / NPC / music regression completed. Passed=" + passed);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Test interrupted.")); }
}
#endif
