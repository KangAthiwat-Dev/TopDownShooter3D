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

// Real prefab/Animator/attack code, temporary isolated NavMesh. Never saves fixtures.
public sealed class EnemyAwarenessRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly Vector3 origin = new Vector3(2000, 20, 2000);
    private GameObject fixture, targetObject, blocker;
    private EnemyAwarenessTestTarget damageTarget;
    private EnemyAI enemy;
    private NavMeshDataInstance navigation;
    private NavMeshData navigationData;
    private bool finished, passed = true, oldBackground;
    private int oldFps, oldVsync;
    private float oldTimeScale;
    private string runtimeError;

    private IEnumerator Start()
    {
        oldTimeScale = Time.timeScale; Time.timeScale = 1f;
        oldBackground = Application.runInBackground; Application.runInBackground = true;
        oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Enemy awareness real-prefab regression — " + DateTime.Now.ToString("s"));
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
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var ai in FindObjectsByType<EnemyAI>()) ai.gameObject.SetActive(false);
        fixture = new GameObject("Isolated enemy AI fixture (Play Mode only)");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Test ground";
        floor.transform.SetParent(fixture.transform); floor.transform.position = origin + Vector3.down * 0.1f;
        floor.transform.localScale = new Vector3(60, 0.2f, 60);
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box, area = 0, size = new Vector3(60, 0.2f, 60),
            transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one)
        }};
        navigationData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(origin, new Vector3(64, 10, 64)), Vector3.zero, Quaternion.identity);
        navigation = NavMesh.AddNavMeshData(navigationData);
        Check(navigation.valid && NavMesh.SamplePosition(origin, out _, 1, NavMesh.AllAreas), "Isolated runtime navigation created; production map untouched");
        targetObject = new GameObject("Visible target proxy"); targetObject.transform.SetParent(fixture.transform);
        var controller = targetObject.AddComponent<CharacterController>(); controller.height = 1.8f; controller.radius = 0.3f; controller.center = Vector3.up * 0.9f;
        damageTarget = targetObject.AddComponent<EnemyAwarenessTestTarget>();

        // Frozen steering isolates cone tests from random patrol turns, not sight logic.
        PlaceTarget(new Vector3(0, 0, -7)); NewEnemy(); Set(enemy, "rotationSpeed", 0f);
        yield return Wait(0.12f);
        Check(!enemy.CanSeeTarget && !enemy.HasTargetMemory, "Player behind enemy does not cause initial detection");
        PlaceTarget(new Vector3(7, 0, 0)); yield return Wait(0.12f);
        Check(!enemy.CanSeeTarget && !enemy.HasTargetMemory, "Player at 90 degrees remains unseen");
        PlaceTarget(new Vector3(6.062f, 0, 3.5f)); yield return Wait(0.12f);
        Check(!enemy.CanSeeTarget, "60 degrees is outside the 110-degree cone");
        PlaceTarget(new Vector3(4.5f, 0, 5.363f)); yield return Wait(0.15f);
        Check(enemy.CanSeeTarget && enemy.State == EnemyAwarenessState.Chase, "40 degrees is visible and transitions to chase");
        DestroyEnemy();

        PlaceTarget(new Vector3(0, 0, 13)); NewEnemy(); Set(enemy, "rotationSpeed", 0f);
        yield return Wait(0.1f);
        Check(!enemy.HasTargetMemory, "Unaware enemy cannot acquire outside the initial 12m sight range");
        PlaceTarget(new Vector3(0, 0, 10)); yield return Wait(0.12f);
        PlaceTargetWorld(enemy.transform.position + Vector3.forward * 15f); yield return Wait(0.1f);
        Check(enemy.CanSeeTarget && enemy.HasTargetMemory, "Alert enemy can maintain frontal sight between 12m and 16m");
        PlaceTargetWorld(enemy.transform.position + Vector3.forward * 17f); yield return Wait(0.1f);
        Check(!enemy.CanSeeTarget && enemy.HasTargetMemory, "Escaping beyond 16m loses vision without instant amnesia");
        // Production 4+5 second timers are tested below. Short values here also
        // verify that inspector timing changes are honored without a restart.
        Set(enemy, "investigateDuration", 0.2f); Set(enemy, "searchDuration", 0.3f);
        PlaceTarget(new Vector3(0, 0, 100)); yield return Wait(1f);
        Check(!enemy.HasTargetMemory, "A player that escapes far away is eventually forgotten");
        DestroyEnemy();

        foreach (string kind in new[] { "Imp_Green", "Imp_Red", "Imp_Blue", "Puglin" })
        {
            PlaceTarget(new Vector3(0, 0, 100)); NewEnemy(kind);
            Vector3 start = enemy.transform.position; float maxMotion = 0, maxBlend = 0;
            var agent = enemy.GetComponent<NavMeshAgent>(); var animator = enemy.GetComponentInChildren<Animator>();
            // The fixture is deliberately off-camera; production culling is allowed
            // to skip bone transforms there. Force evaluation only on this test copy.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Some clips keep ankle-local rotation fixed while the thigh/knee moves.
            var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); Quaternion before = leg.localRotation;
            float boneMotion = 0;
            float until = Time.realtimeSinceStartup + 1.6f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null; maxMotion = Mathf.Max(maxMotion, agent.velocity.magnitude);
                maxBlend = Mathf.Max(maxBlend, animator.GetFloat("Speed"));
                boneMotion = Mathf.Max(boneMotion, Quaternion.Angle(before, leg.localRotation));
            }
            Check(enemy.State == EnemyAwarenessState.Patrol && !enemy.HasTargetMemory &&
                Vector3.Distance(start, enemy.transform.position) > 0.3f, kind + " patrols after spawn without seeing a player");
            Check(maxMotion > 0.5f && Mathf.Abs(agent.speed - enemy.PatrolSpeed) < 0.01f, kind + " uses walking speed");
            Check(animator.isHuman && maxBlend > 0.3f && maxBlend < 0.65f && boneMotion > 1f, kind + " has animated Humanoid walk blending (not a frozen pose)");
            report.AppendLine($"  {kind} walk max={maxMotion:F2}, blend={maxBlend:F2}, thigh motion={boneMotion:F1} degrees, chase={enemy.ChaseSpeed:F2}, culling={animator.cullingMode}");
            DestroyEnemy();
        }

        PlaceTarget(new Vector3(0, 0, 10)); NewEnemy();
        Vector3 chaseStart = enemy.transform.position;
        float chaseBegan = Time.time;
        yield return Wait(0.85f);
        float chaseActualSpeed = Vector3.Distance(chaseStart, enemy.transform.position) / (Time.time - chaseBegan);
        report.AppendLine($"  CHASE actual displacement speed={chaseActualSpeed:F3}, reported velocity={enemy.GetComponent<NavMeshAgent>().velocity.magnitude:F3}, configured={enemy.ChaseSpeed:F3}, time={Time.time - chaseBegan:F3}, rigidbody interpolation={enemy.GetComponent<Rigidbody>().interpolation}");
        Check(chaseActualSpeed > enemy.PatrolSpeed * 1.7f && chaseActualSpeed > enemy.ChaseSpeed * 0.7f,
            "Actual world displacement while chasing is substantially faster than walking (not only agent.speed/animation)");
        Check(enemy.CanSeeTarget && enemy.State == EnemyAwarenessState.Chase && Vector3.Distance(chaseStart, enemy.transform.position) > 1f, "Visible target is chased along the real NavMesh");
        Check(Mathf.Abs(enemy.GetComponent<NavMeshAgent>().speed - 4.2f) < 0.01f && enemy.GetComponentInChildren<Animator>().GetFloat("Speed") > 0.8f, "Chase retains Green Imp's original 4.2 speed and blends to run");
        Vector3 steadyStart = enemy.transform.position; float steadyBegan = Time.time;
        yield return Wait(0.3f);
        float steadySpeed = Vector3.Distance(steadyStart, enemy.transform.position) / (Time.time - steadyBegan);
        report.AppendLine($"  CHASE steady actual speed={steadySpeed:F3}, configured={enemy.ChaseSpeed:F3}");
        Check(Mathf.Abs(steadySpeed - enemy.ChaseSpeed) < 0.5f,
            "After acceleration, actual Transform speed matches configured chase speed");
        Vector3 remembered = enemy.LastSeenPosition;
        Set(enemy, "rotationSpeed", 0f);
        PlaceTargetWorld(enemy.transform.position - enemy.transform.forward * 5f);
        yield return Wait(0.12f);
        Check(!enemy.CanSeeTarget && enemy.HasTargetMemory && enemy.State == EnemyAwarenessState.Investigate, "Moving behind an alerted enemy does not instantly erase memory");
        Check(Vector3.Distance(remembered, enemy.LastSeenPosition) < 0.01f &&
            Vector3.Distance(enemy.GetComponent<NavMeshAgent>().destination, remembered) < 0.5f, "Hidden player does not update memory/destination: pursue last seen point only");
        DestroyEnemy();

        // Tree trunk uses the same collider-based LOS as actual scene trees.
        PlaceTarget(new Vector3(0, 0, 7));
        blocker = GameObject.CreatePrimitive(PrimitiveType.Capsule); blocker.name = "Tree trunk LOS fixture";
        blocker.transform.SetParent(fixture.transform); blocker.transform.position = origin + new Vector3(0, 2, 3.5f);
        blocker.transform.localScale = new Vector3(2, 3, 2); Physics.SyncTransforms();
        NewEnemy(); Set(enemy, "rotationSpeed", 0f); yield return Wait(0.15f);
        Check(!enemy.CanSeeTarget && !enemy.HasTargetMemory, "Solid tree-trunk collider blocks initial detection");
        DestroyImmediate(blocker); blocker = null; yield return Wait(0.15f);
        Check(enemy.CanSeeTarget, "Emerging from cover reacquires the player"); DestroyEnemy();

        PlaceTarget(new Vector3(0, 0, 8)); NewEnemy(); yield return Wait(0.25f);
        remembered = enemy.LastSeenPosition;
        CreateWall(4f); yield return Wait(0.2f);
        Check(!enemy.CanSeeTarget && enemy.HasTargetMemory, "Wall removes vision but keeps alert memory");
        PlaceTarget(new Vector3(10, 0, 8)); yield return Wait(0.12f);
        Check(Vector3.Distance(remembered, enemy.LastSeenPosition) < 0.01f, "Moving behind a wall never leaks the current target position");
        yield return Until(() => enemy.State == EnemyAwarenessState.Search, 4.5f);
        Check(enemy.State == EnemyAwarenessState.Search && !enemy.IsInAttackRange, "Lost or unreachable last-seen point starts a bounded search, not wall attacks");
        Time.timeScale = 0; yield return null; // Let Unity apply the new scale at its frame boundary.
        Vector3 pausedPosition = enemy.transform.position; Quaternion pausedRotation = enemy.transform.rotation;
        float pausedMemoryTime = enemy.TimeWithoutSight;
        yield return Wait(0.5f);
        report.AppendLine($"  PAUSE memory delta={enemy.TimeWithoutSight - pausedMemoryTime:F4}, displacement={Vector3.Distance(pausedPosition, enemy.transform.position):F4}, turn={Quaternion.Angle(pausedRotation, enemy.transform.rotation):F4}, state={enemy.State}");
        Check(enemy.State == EnemyAwarenessState.Search && Mathf.Abs(enemy.TimeWithoutSight - pausedMemoryTime) < 0.02f &&
            Vector3.Distance(pausedPosition, enemy.transform.position) < 0.03f && Quaternion.Angle(pausedRotation, enemy.transform.rotation) < 0.1f,
            "Inventory-style pause freezes search time, steering and movement");
        Time.timeScale = 1;
        float searchStart = Time.time; Quaternion searchRotation = enemy.transform.rotation;
        yield return Wait(0.5f);
        Check(enemy.HasTargetMemory && (Quaternion.Angle(searchRotation, enemy.transform.rotation) > 1f || enemy.IsMoving), "Search visibly looks around or checks nearby points");
        yield return Until(() => !enemy.HasTargetMemory, 5.5f);
        Check(!enemy.HasTargetMemory && Time.time - searchStart >= 3.5f && enemy.State != EnemyAwarenessState.Chase,
            "Remaining hidden for the search period causes forgetting");
        yield return Wait(1.7f);
        Check(enemy.State == EnemyAwarenessState.Patrol || enemy.State == EnemyAwarenessState.Return, "After forgetting, enemy returns toward spawn/patrol instead of following hidden player");
        PlaceTargetWorld(enemy.transform.position + enemy.transform.forward * 6f); DestroyImmediate(blocker); blocker = null;
        yield return Wait(0.3f);
        report.AppendLine($"  REACQUIRE state={enemy.State}, see={enemy.CanSeeTarget}, distance={Vector3.Distance(enemy.transform.position, targetObject.transform.position):F2}, angle={Vector3.Angle(enemy.transform.forward, targetObject.transform.position - enemy.transform.position):F2}, enemy={enemy.transform.position}, target={targetObject.transform.position}");
        foreach (var hit in Physics.RaycastAll(enemy.transform.position + Vector3.up * 1.45f,
            (targetObject.GetComponent<CharacterController>().bounds.center - enemy.transform.position - Vector3.up * 1.45f).normalized, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            report.AppendLine("  REACQUIRE ray hit " + hit.collider.name);
        Check(enemy.CanSeeTarget && enemy.State == EnemyAwarenessState.Chase, "Player can be detected again after memory has expired");
        DestroyEnemy();

        // Trigger-only VFX/volumes are not cover.
        PlaceTarget(new Vector3(0, 0, 6));
        blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.SetParent(fixture.transform);
        blocker.transform.position = origin + new Vector3(0, 2, 3); blocker.transform.localScale = new Vector3(8, 4, 0.3f);
        blocker.GetComponent<Collider>().isTrigger = true;
        NewEnemy(); yield return Wait(0.2f);
        Check(enemy.CanSeeTarget, "Trigger volumes and VFX do not block sight");
        DestroyImmediate(blocker); blocker = null; DestroyEnemy();

        PlaceTarget(new Vector3(0, 0, 1.3f)); NewEnemy(attack: true); yield return Wait(0.08f);
        Check(enemy.IsInAttackRange && enemy.CanAttackTarget(), "Unobstructed close visible target can start a melee attack");
        CreateWall(0.7f); yield return Wait(0.5f);
        Check(damageTarget.Hits == 0 && !enemy.CanAttackTarget(), "Wall appearing during wind-up prevents delayed damage through cover");
        DestroyEnemy(); DestroyImmediate(blocker); blocker = null; yield return Wait(0.25f);
        NewEnemy(attack: true); yield return Wait(0.55f);
        Check(damageTarget.Hits > 0 && damageTarget.TotalDamage == 8, "Normal close melee still lands Green Imp's unchanged damage");
        var health = enemy.GetComponent<EnemyHealth>(); health.TakeDamage(health.MaxHealth);
        yield return Wait(0.2f);
        Check(!enemy.enabled && !enemy.GetComponent<NavMeshAgent>().enabled && !enemy.CanAttackTarget(), "Death disables navigation/awareness and cancels attacks safely");
        Check(runtimeError == null, "No runtime errors/exceptions during awareness regression");
    }

    private void NewEnemy(string kind = "Imp_Green", bool attack = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_" + kind + ".prefab");
        var go = Instantiate(prefab, origin, Quaternion.identity, fixture.transform);
        enemy = go.GetComponent<EnemyAI>();
        var settings = new SerializedObject(enemy); settings.FindProperty("target").objectReferenceValue = targetObject.transform; settings.ApplyModifiedPropertiesWithoutUndo();
        go.GetComponent<EnemyAttack>().enabled = attack;
    }
    private void DestroyEnemy() { if (enemy != null) DestroyImmediate(enemy.gameObject); enemy = null; }
    private void PlaceTarget(Vector3 offset) => PlaceTargetWorld(origin + offset);
    private void PlaceTargetWorld(Vector3 point)
    {
        var controller = targetObject.GetComponent<CharacterController>(); controller.enabled = false;
        targetObject.transform.position = point; controller.enabled = true; Physics.SyncTransforms();
    }
    private void CreateWall(float z)
    {
        blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Solid wall (temporary)";
        blocker.transform.SetParent(fixture.transform); blocker.transform.position = origin + new Vector3(0, 2.5f, z);
        blocker.transform.localScale = new Vector3(64, 5, 0.3f);
        var obstacle = blocker.AddComponent<NavMeshObstacle>(); obstacle.carving = true; obstacle.carveOnlyStationary = false;
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one;
        Physics.SyncTransforms();
    }
    private static void Set(EnemyAI ai, string property, float value)
    {
        var settings = new SerializedObject(ai); settings.FindProperty(property).floatValue = value; settings.ApplyModifiedPropertiesWithoutUndo();
    }
    private static IEnumerator Wait(float duration) { float until = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Until(Func<bool> condition, float timeout) { float until = Time.realtimeSinceStartup + timeout; while (!condition() && Time.realtimeSinceStartup < until) yield return null; }
    private void Check(bool condition, string message) { report.AppendLine((condition ? "PASS " : "FAIL ") + message); passed &= condition; }
    private void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        if (failure != null) { passed = false; report.AppendLine("EXCEPTION " + failure); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        Application.logMessageReceived -= OnLog;
        Time.timeScale = oldTimeScale; Application.runInBackground = oldBackground;
        Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync;
        if (fixture != null) DestroyImmediate(fixture);
        if (navigation.valid) navigation.Remove();
        if (navigationData != null) DestroyImmediate(navigationData);
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/EnemyAwarenessPlaytest.txt", report.ToString());
        Debug.Log(report.ToString()); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Awareness test interrupted")); }
}

public sealed class EnemyAwarenessTestTarget : MonoBehaviour, IDamageable
{
    public bool IsAlive => true;
    public int Hits { get; private set; }
    public float TotalDamage { get; private set; }
    public void TakeDamage(float amount) { Hits++; TotalDamage += amount; }
}
#endif
