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

public sealed class EnvironmentBridgeRegressionDriver : MonoBehaviour
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
        report.AppendLine("Environment bridge actual-player test " + DateTime.Now.ToString("s"));
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
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();
        var population=FindAnyObjectByType<WorldMonsterDirector>();if(population!=null)population.gameObject.SetActive(false);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        if(input.actions.devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        pad=InputSystem.AddDevice<Gamepad>("BridgeTestPad");keyboard=InputSystem.AddDevice<Keyboard>("BridgeTestKeyboard");input.actions.devices=new InputDevice[]{pad,keyboard};
        FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown,false);yield return Wait(1);
        foreach(string name in new[]{"brige","brige_wood"})
        {
            var bridge=GameObject.Find(name).transform;
            Check(bridge.GetComponentsInChildren<MeshCollider>().All(c=>c.enabled&&c.sharedMesh!=null),name+" has valid mesh collision on every piece");
            var start=bridge.Find("Crossing_Start").position;var end=bridge.Find("Crossing_End").position;
            var path=new NavMeshPath();Check(NavMesh.SamplePosition(start,out var a,1.2f,NavMesh.AllAreas)&&NavMesh.SamplePosition(end,out var b,1.2f,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,name+" connects both banks on navigation");
            Warp(start);yield return Wait(.3f);yield return Travel(end,name+" actual player crosses bridge");
            Check(movement.transform.position.y>3.5f,name+" keeps player above the water");
            yield return Travel(start,name+" player returns across every seam");
        }
        var water=GameObject.Find("World/Environment/UNS_Water_Detailed");var material=water.GetComponentInChildren<Renderer>().sharedMaterial;
        Check(material.shader.name=="Project/Gentle River Water"&&material.GetFloat("_WaveHeight")>.01f&&material.GetFloat("_WaveHeight")<.03f,"Water uses small animated ripples");
        Check(!water.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"Water does not create an invisible walking floor");
        Check(runtimeError==null,"No runtime errors crossing either bridge");
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
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); image.Apply(); File.WriteAllBytes("Logs/EnvironmentBridges_Gameplay.png", image.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(image); }
    }
    private void Check(bool valid, string message) { passed &= valid; report.AppendLine((valid ? "PASS " : "FAIL ") + message); File.WriteAllText("Logs/EnvironmentBridgePlaytest.txt", report.ToString()); }
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
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/EnvironmentBridgePlaytest.txt", report.ToString());
        Debug.Log("Environment bridge regression completed. Passed=" + passed);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Test interrupted.")); }
}
#endif
