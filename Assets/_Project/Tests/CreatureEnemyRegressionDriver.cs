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

// Exercises actual Generic creature rigs, AI, audio, attack, death and wave events.
public sealed class CreatureEnemyRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly Vector3 origin = new Vector3(2000, 20, 2000);
    private GameObject fixture, actor;
    private CreatureDamageTarget target;
    private NavMeshData data;
    private NavMeshDataInstance navigation;
    private bool finished, passed = true, oldBackground, oldAudioPause;
    private int oldFps, oldVsync;
    private float oldScale;
    private string runtimeError;
    private readonly List<GameObject> waveActors = new List<GameObject>();

    private IEnumerator Start()
    {
        oldBackground = Application.runInBackground; oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount; oldScale = Time.timeScale; oldAudioPause = AudioListener.pause;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.timeScale = 1; AudioListener.pause = false;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Spider/Bear real-prefab regression — " + DateTime.Now.ToString("s"));
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
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        fixture = new GameObject("Temporary isolated creature fixture");
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(60, 0.2f, 60), transform = Matrix4x4.TRS(origin + Vector3.down * 0.1f, Quaternion.identity, Vector3.one) } };
        data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, new Bounds(origin, new Vector3(64, 10, 64)), Vector3.zero, Quaternion.identity); navigation = NavMesh.AddNavMeshData(data);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(fixture.transform); floor.transform.position = origin + Vector3.down * 0.1f; floor.transform.localScale = new Vector3(60, 0.2f, 60);
        var targetObject = new GameObject("Creature target proxy"); targetObject.transform.SetParent(fixture.transform); var cc = targetObject.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.3f; cc.center = Vector3.up * 0.9f;
        target = targetObject.AddComponent<CreatureDamageTarget>();
        Check(navigation.valid && NavMesh.SamplePosition(origin, out _, 1, NavMesh.AllAreas), "Temporary navigation created without altering the real forest NavMesh");
        foreach (string kind in new[] { "Spider", "Bear" }) yield return CheckCreature(kind);

        // Real spawner and death subscriptions, not just a manually edited prefab.
        PlaceTarget(origin); var marker = new GameObject("Temporary creature spawn marker"); marker.transform.SetParent(fixture.transform); marker.transform.position = origin + Vector3.forward * 8;
        var waveObject = new GameObject("Temporary creature wave"); waveObject.SetActive(false); waveObject.transform.SetParent(fixture.transform);
        var wave = waveObject.AddComponent<EnemyWaveSpawner>(); var waveSettings = new SerializedObject(wave);
        waveSettings.FindProperty("player").objectReferenceValue = target.transform; waveSettings.FindProperty("firstWaveDelay").floatValue = 0;
        waveSettings.FindProperty("spawnInterval").floatValue = 0.05f;
        var points = waveSettings.FindProperty("spawnPoints"); points.arraySize = 1; points.GetArrayElementAtIndex(0).objectReferenceValue = marker.transform;
        var waves = waveSettings.FindProperty("waves"); waves.arraySize = 1; var entries = waves.GetArrayElementAtIndex(0).FindPropertyRelative("enemies"); entries.arraySize = 2;
        for (int i = 0; i < 2; i++) { entries.GetArrayElementAtIndex(i).FindPropertyRelative("enemyPrefab").objectReferenceValue = Prefab(i == 0 ? "Spider" : "Bear"); entries.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = 1; }
        waveSettings.ApplyModifiedPropertiesWithoutUndo(); int completions = 0; wave.AllWavesCompleted += () => completions++; waveObject.SetActive(true);
        yield return Wait(0.35f);
        waveActors.AddRange(FindObjectsByType<EnemyVoice>().Where(v => Vector3.Distance(v.transform.position, origin) < 30).Select(v => v.gameObject));
        Check(wave.AliveEnemies == 2 && waveActors.Count == 2 && waveActors.All(a => a.GetComponent<NavMeshAgent>().isOnNavMesh), "Real wave spawner instantiates both new creatures on valid navigation");
        foreach (var a in waveActors) a.GetComponent<EnemyHealth>().TakeDamage(a.GetComponent<EnemyHealth>().MaxHealth);
        yield return Wait(0.2f);
        Check(wave.AliveEnemies == 0 && completions == 1, "Creature deaths update the real wave count and complete it once");
        foreach (var a in waveActors) a.GetComponent<EnemyHealth>().TakeDamage(100);
        yield return null; Check(wave.AliveEnemies == 0 && completions == 1, "Repeated corpse damage cannot decrement the wave twice");
        Check(runtimeError == null, "No runtime errors during creature animation, perception, combat, sound or wave lifecycle");
    }

    private IEnumerator CheckCreature(string kind)
    {
        PlaceTarget(origin + Vector3.forward * 100); target.ResetDamage();
        actor = Instantiate(Prefab(kind), origin, Quaternion.identity, fixture.transform);
        var ai = actor.GetComponent<EnemyAI>(); var agent = actor.GetComponent<NavMeshAgent>(); var animator = actor.GetComponentInChildren<Animator>();
        var attack = actor.GetComponent<EnemyAttack>(); var health = actor.GetComponent<EnemyHealth>(); var voice = actor.GetComponent<EnemyVoice>();
        var settings = new SerializedObject(ai); settings.FindProperty("target").objectReferenceValue = target.transform; settings.ApplyModifiedPropertiesWithoutUndo();
        var voiceSettings = new SerializedObject(voice); voice.enabled = false; voiceSettings.FindProperty("ambientInterval").vector2Value = new Vector2(0.12f, 0.12f); voiceSettings.ApplyModifiedPropertiesWithoutUndo(); voice.enabled = true;
        var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray(); var initialRotations = bones.Select(t => t.localRotation).ToArray();
        float motion = 0; Vector3 start = actor.transform.position; float until = Time.realtimeSinceStartup + 1.1f;
        while (Time.realtimeSinceStartup < until) { yield return null; for (int i = 0; i < bones.Length; i++) motion = Mathf.Max(motion, Quaternion.Angle(initialRotations[i], bones[i].localRotation)); }
        Check(!animator.isHuman && animator.isInitialized && motion > 2, kind + " Generic rig is genuinely animated, not a Humanoid T-pose");
        Check(ai.State == EnemyAwarenessState.Patrol && Vector3.Distance(start, actor.transform.position) > 0.3f && Mathf.Abs(agent.speed - ai.PatrolSpeed) < 0.01f, kind + " walks/patrols without initially seeing the player");
        Check(animator.GetFloat("Speed") <= 0.65f, kind + " uses walk/idle blend while unaware");
        Check(voice.VoicePlayCount > 0 && voice.Source.isPlaying && !voice.Source.loop && voice.Source.spatialBlend == 1, kind + " ambient voice plays as non-looping 3D sound");
        var clip = voice.AttackClip; clip.LoadAudioData(); yield return Wait(0.1f);
        float[] samples = new float[clip.samples * clip.channels]; bool decoded = clip.GetData(samples, 0);
        Check(decoded && samples.Any(s => Mathf.Abs(s) > 0.001f), kind + " configured voice contains real non-silent audio data");

        PlaceTarget(actor.transform.position + actor.transform.forward * 10);
        yield return Wait(0.65f); start = actor.transform.position; float began = Time.time;
        yield return Wait(0.3f); float speed = Vector3.Distance(start, actor.transform.position) / (Time.time - began);
        Check(ai.State == EnemyAwarenessState.Chase && ai.CanSeeTarget && speed > ai.PatrolSpeed * 1.5f && Mathf.Abs(speed - ai.ChaseSpeed) < 0.8f, kind + " chases faster than walking with matching actual world speed");
        Check(animator.GetFloat("Speed") > 0.8f, kind + " blends to its own running animation/rate");
        var mesh = actor.transform.Find("ModelRoot/CreatureVisual");
        Check(Vector3.Distance(mesh.position, actor.transform.position) < 2.5f, kind + " visual remains attached to the navigation root (no accumulating root motion)");
        report.AppendLine($"  {kind}: actual run={speed:F2}, configured={ai.ChaseSpeed:F2}, walk={ai.PatrolSpeed:F2}, bone motion={motion:F1}");
        Vector3 lastSeen = ai.LastSeenPosition;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(fixture.transform); wall.transform.position = actor.transform.position + actor.transform.forward * 3 + Vector3.up * 2;
        wall.transform.rotation = Quaternion.LookRotation(actor.transform.forward); wall.transform.localScale = new Vector3(16, 4, 0.3f); Physics.SyncTransforms();
        yield return Wait(0.12f);
        Check(!ai.CanSeeTarget && ai.HasTargetMemory && Vector3.Distance(ai.LastSeenPosition, lastSeen) < 0.1f, kind + " uses solid cover and last-seen memory instead of tracking through walls");
        DestroyImmediate(wall);

        agent.Warp(origin); actor.transform.rotation = Quaternion.identity;
        float reach = settings.FindProperty("attackRange").floatValue; PlaceTarget(origin + Vector3.forward * reach * 0.75f);
        yield return Wait(0.1f);
        float delay = new SerializedObject(attack).FindProperty("hitDelay").floatValue;
        Check(ai.CanAttackTarget() && voice.Source.clip == voice.AttackClip && voice.Source.isPlaying, kind + " real attack starts its configured voice");
        yield return Wait(delay * 0.5f);
        Check(target.Hits == 0, kind + " cannot damage before the animation wind-up");
        yield return Wait(delay * 0.75f + 0.1f);
        Check(target.Hits == 1 && Mathf.Approximately(target.TotalDamage, kind == "Spider" ? 10 : 20), kind + " actual timed attack damages the target once");
        int count = voice.VoicePlayCount; health.TakeDamage(5);
        Check(health.CurrentHealth == health.MaxHealth - 5 && voice.Source.isPlaying && voice.VoicePlayCount >= count, kind + " damage feedback reuses voices without layering repeated long roars");
        health.TakeDamage(health.MaxHealth); yield return Wait(0.2f);
        Check(!ai.enabled && !agent.enabled && !actor.GetComponent<Targetable>().IsTargetable && animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), kind + " death stops AI/targeting and plays its own death clip");
        Check(voice.Source.clip == voice.DeathClip && voice.Source.isPlaying, kind + " death voice survives AI/attack shutdown");
        Time.timeScale = 0; AudioListener.pause = true; yield return null;
        int samplePosition = voice.Source.timeSamples; yield return Wait(0.25f);
        Check(Mathf.Abs(voice.Source.timeSamples - samplePosition) < 2048 && actor != null, kind + " death audio and cleanup obey inventory-style pause");
        Time.timeScale = 1; AudioListener.pause = false;
        float destroyDelay = new SerializedObject(actor.GetComponent<EnemyDeathController>()).FindProperty("destroyDelay").floatValue;
        yield return Wait(destroyDelay + 0.2f);
        Check(actor == null, kind + " corpse and audio source clean up after animation/voice finish");
    }

    private void PlaceTarget(Vector3 point) { var cc = target.GetComponent<CharacterController>(); cc.enabled = false; target.transform.position = point; cc.enabled = true; Physics.SyncTransforms(); }
    private static GameObject Prefab(string kind) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_" + kind + ".prefab");
    private static IEnumerator Wait(float duration) { float until = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < until) yield return null; }
    private void Check(bool ok, string text) { passed &= ok; report.AppendLine((ok ? "PASS " : "FAIL ") + text); }
    private void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        if (failure != null) { passed = false; report.AppendLine("EXCEPTION " + failure); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        Application.logMessageReceived -= OnLog;
        foreach (var a in waveActors) if (a != null) DestroyImmediate(a);
        if (fixture != null) DestroyImmediate(fixture); if (navigation.valid) navigation.Remove(); if (data != null) DestroyImmediate(data);
        Time.timeScale = oldScale; AudioListener.pause = oldAudioPause; Application.runInBackground = oldBackground; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CreaturePlaytest.txt", report.ToString()); Debug.Log(report.ToString());
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Creature regression interrupted")); }
}

public sealed class CreatureDamageTarget : MonoBehaviour, IDamageable
{
    public bool IsAlive => true;
    public int Hits { get; private set; }
    public float TotalDamage { get; private set; }
    public void TakeDamage(float amount) { Hits++; TotalDamage += amount; }
    public void ResetDamage() { Hits = 0; TotalDamage = 0; }
}
#endif
