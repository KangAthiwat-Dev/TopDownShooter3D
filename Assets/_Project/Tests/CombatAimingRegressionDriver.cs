#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only integration fixture. Runs the real PlayerInput, movement,
// Animator, IK and projectile loop; no replacement combat implementation.
public class CombatAimingRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly Vector3 origin = new Vector3(1000f, 10f, 1000f);
    private PlayerAttackController attack;
    private PlayerMovement movement;
    private PlayerCombatFacing facing;
    private PlayerTargeting targeting;
    private PlayerRangedWeapon weapon;
    private PlayerInput playerInput;
    private UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? originalActionDevices;
    private Keyboard keyboard;
    private GameObject enemy;
    private Targetable target;
    private EnemyHealth enemyHealth;
    private int shots;
    private int requests;
    private int confirmed;
    private int phaseShots;
    private string phase;
    private string shotFailure;
    private string runtimeFailure;
    private float lastShotTime = -10f;
    private Projectile lastProjectile;
    private Vector3 lastVelocity;
    private int originalTargetFrameRate;
    private int originalVSync;
    private bool originalRunInBackground;
    private bool finished;

    private IEnumerator Start()
    {
        originalTargetFrameRate = Application.targetFrameRate;
        originalVSync = QualitySettings.vSyncCount;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        report.AppendLine("Combat aiming integration regression — " + DateTime.Now.ToString("s"));
        Application.logMessageReceived += OnLogMessage;
        var work = new Stack<IEnumerator>();
        work.Push(RunChecks());
        while (work.Count > 0)
        {
            object next = null;
            Exception failure = null;
            try
            {
                if (!work.Peek().MoveNext()) work.Pop();
                else next = work.Peek().Current;
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null)
            {
                Finish(failure);
                yield break;
            }
            if (next is IEnumerator nested) work.Push(nested);
            else if (next != null || work.Count > 0) yield return next;
        }
        Finish(null);
    }

    private IEnumerator RunChecks()
    {
        attack = FindAnyObjectByType<PlayerAttackController>();
        Check(attack != null, "Scene has the real player");
        movement = attack.GetComponent<PlayerMovement>();
        facing = attack.GetComponent<PlayerCombatFacing>();
        targeting = attack.GetComponent<PlayerTargeting>();
        weapon = attack.GetComponent<PlayerRangedWeapon>();
        playerInput = attack.GetComponent<PlayerInput>();
        foreach (EnemyWaveSpawner spawner in FindObjectsByType<EnemyWaveSpawner>())
        {
            spawner.StopAllCoroutines();
            spawner.enabled = false;
        }
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>()) ai.gameObject.SetActive(false);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Regression Floor (Temporary)";
        floor.transform.position = origin + Vector3.down * 0.6f;
        floor.transform.localScale = new Vector3(80f, 1f, 80f);
        keyboard = InputSystem.AddDevice<Keyboard>("CombatRegressionKeyboard");
        // Input System exposes a view of a mutable internal array. Copy it;
        // retaining the view would restore null/overwritten device entries.
        var previousDevices = playerInput.actions.devices;
        if (previousDevices.HasValue)
        {
            var copy = new InputDevice[previousDevices.Value.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = previousDevices.Value[i];
            originalActionDevices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(copy);
        }
        playerInput.actions.devices = new InputDevice[] { keyboard };
        weapon.ProjectileFired += OnProjectileFired;
        attack.RangedAttackRequested += OnRequested;
        attack.RangedShotFired += OnConfirmed;
        CreateEnemy();

        yield return ResetPlayer(180f);
        phase = "first shot, facing backwards";
        phaseShots = 0;
        float startTime = Time.time;
        float previousYaw = attack.transform.eulerAngles.y;
        Keys(Key.J);
        while (Time.time - startTime < 1f)
        {
            yield return null;
            float yaw = attack.transform.eulerAngles.y;
            float change = Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
            Check(change <= 1080f * Time.deltaTime + 0.5f, "Root turn respects angular speed (no snap)", false);
            previousYaw = yaw;
            CheckShotFailure();
        }
        Check(phaseShots >= 2, "Facing backwards turns smoothly and then fires");

        yield return ExerciseMovement("backpedal", new[] { Key.J, Key.S }, 0.9f);
        yield return ExerciseMovement("strafe right", new[] { Key.J, Key.D }, 0.9f);
        yield return ExerciseMovement("strafe left", new[] { Key.J, Key.A }, 0.9f);
        yield return ExerciseMovement("diagonal backpedal", new[] { Key.J, Key.A, Key.S }, 0.75f);
        yield return ExerciseMovement("sprint strafe", new[] { Key.J, Key.D, Key.LeftShift }, 0.7f);

        // Repeat the real loop at several requested frame rates; do not fake dt.
        foreach (int fps in new[] { 30, 120 })
        {
            Application.targetFrameRate = fps;
            yield return ExerciseMovement("strafe, requested " + fps + " FPS", new[] { Key.J, Key.D }, 0.8f);
        }
        Application.targetFrameRate = 60;

        yield return ResetPlayer(0f);
        phase = "moving enemy while strafing";
        phaseShots = 0;
        Keys(Key.J, Key.D);
        startTime = Time.time;
        while (Time.time - startTime < 0.9f)
        {
            float angle = (Time.time - startTime) * Mathf.PI * 0.5f;
            enemy.transform.position = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 8f;
            yield return null;
            CheckShotFailure();
        }
        Check(phaseShots >= 2, "Tracks a moving enemy while strafing, without bending projectiles");

        yield return ResetPlayer(0f);
        phase = "release and return to movement facing";
        Keys(Key.J);
        yield return new WaitForSeconds(0.4f);
        int shotsBeforeRelease = shots;
        Keys(Key.S);
        yield return new WaitForSeconds(0.2f);
        Check(facing.IsAiming, "Aim hold prevents an immediate turn back to movement on release");
        // The scene's turn speed is user-tunable (currently 10, not 15).
        // Exponential Slerp needs ln(180/10)/rate to settle a half turn to 10°.
        float turnSettle = Mathf.Log(18f) / Mathf.Max(0.01f, Get<float>(movement, "turnSpeed"));
        float releaseWait = Get<float>(facing, "aimHoldDuration") + turnSettle + 0.1f;
        yield return new WaitForSeconds(Mathf.Max(0f, releaseWait - 0.2f));
        Check(!facing.IsAiming && Vector3.Dot(attack.transform.forward, Vector3.back) > 0.95f,
            "After aim hold expires, the player faces movement normally");
        Check(shots == shotsBeforeRelease, "Releasing attack stops all new shots during aim hold");

        yield return ResetPlayer(0f);
        phase = "switch target behind player";
        Targetable previousTarget = target;
        GameObject previousEnemy = enemy;
        previousEnemy.SetActive(false);
        CreateEnemy();
        enemy.transform.position = origin + Vector3.back * 7f;
        Physics.SyncTransforms();
        Invoke(targeting, "RefreshTarget");
        phaseShots = 0;
        Keys(Key.J, Key.D);
        yield return new WaitForSeconds(0.9f);
        CheckShotFailure();
        Check(targeting.CurrentTarget == target && target != previousTarget, "Auto-target switches to a different enemy");
        Check(phaseShots >= 1, "Turns to a target behind the player before firing");
        Destroy(previousEnemy);

        // A projectile already in flight must NOT home when its target moves.
        if (lastProjectile != null)
        {
            Projectile inFlight = lastProjectile;
            Vector3 velocity = lastVelocity;
            Keys();
            enemy.transform.position += Vector3.right * 20f;
            yield return new WaitForSeconds(0.08f);
            Check(inFlight != null && Vector3.Distance(inFlight.GetComponent<Rigidbody>().linearVelocity, velocity) < 0.01f,
                "In-flight projectile keeps its original velocity when enemy moves");
        }
        else Check(false, "A projectile exists for the no-homing check");

        yield return CheckCancellation("release during wind-up", () => Keys());
        yield return CheckCancellation("dash during wind-up", () => Keys(Key.J, Key.Space, Key.S));
        yield return CheckCancellation("shadow dash during wind-up", () => Keys(Key.J, Key.M, Key.S));
        yield return CheckCancellation("interaction lock during wind-up", () => movement.SetInteractionLocked(true));
        yield return CheckCancellation("disable attack during wind-up", () => attack.enabled = false);
        yield return CheckCancellation("disable movement during wind-up", () => movement.enabled = false);
        yield return CheckCancellation("enemy disabled during wind-up", () => enemy.SetActive(false));
        yield return CheckCancellation("enemy exits range during wind-up", () => enemy.transform.position = origin + Vector3.forward * 20f);
        yield return CheckCancellation("enemy dies during wind-up", () => enemyHealth.TakeDamage(enemyHealth.CurrentHealth));

        if (enemy != null) Destroy(enemy);
        CreateEnemy();
        yield return ResetPlayer(0f);
        phase = "close threat switches to melee";
        enemy.transform.position = origin + Vector3.forward * 1.4f;
        Physics.SyncTransforms();
        Invoke(targeting, "RefreshTarget");
        phaseShots = 0;
        float hp = enemyHealth.CurrentHealth;
        Keys(Key.J);
        yield return new WaitForSeconds(1.5f);
        Check(phaseShots == 0, "Close threat never fires a queued ranged shot");
        Check(enemyHealth.CurrentHealth < hp && !facing.IsAiming, "Unarmed melee still deals damage and owns facing");
        Keys();
        yield return new WaitForSeconds(1f);
        yield return ExerciseMovement("return to ranged after melee", new[] { Key.J, Key.A }, 0.9f);

        yield return ResetPlayer(0f);
        phase = "ranged damage";
        hp = enemyHealth.CurrentHealth;
        Keys(Key.J);
        yield return new WaitForSeconds(1.4f);
        Check(enemyHealth.CurrentHealth < hp, "Straight barrel-aligned projectiles hit and deal damage");
        Check(Mathf.Approximately((hp - enemyHealth.CurrentHealth) % 20f, 0f), "Ranged damage remains 20 per hit");
        Check(shots == confirmed && requests > shots, "Confirmed shots match spawned projectiles; cancelled requests do not fire");
        CheckShotFailure();
        report.AppendLine("SUMMARY shots=" + shots + " confirmed=" + confirmed + " requests=" + requests);
    }

    private IEnumerator ExerciseMovement(string label, Key[] keys, float duration)
    {
        yield return ResetPlayer(0f);
        phase = label;
        phaseShots = 0;
        Vector3 start = attack.transform.position;
        Keys(keys);
        float until = Time.time + duration;
        int samples = 0;
        while (Time.time < until)
        {
            yield return null;
            if (facing.IsAiming)
            {
                Vector3 direction = target.AimPoint.position - attack.transform.position;
                direction.y = 0f;
                Check(Vector3.Angle(attack.transform.forward, direction) <= 8f, label + " keeps facing the enemy", false);
                samples++;
            }
            CheckShotFailure();
        }
        Check(phaseShots >= 1 && samples > 0, label + " shoots while moving without fighting rotation");
        Check(Vector3.Distance(start, attack.transform.position) > 1f, label + " retains actual movement");
        report.AppendLine("  elapsed=" + duration + " shots=" + phaseShots + " dt=" + Time.deltaTime.ToString("F4"));
    }

    private IEnumerator CheckCancellation(string label, Action cancel)
    {
        yield return ResetPlayer(180f);
        phase = label;
        Set(facing, "rotationSpeed", 30f); // Deliberately make wind-up observable.
        phaseShots = 0;
        Keys(Key.J);
        yield return new WaitForSeconds(0.12f);
        Check(weapon.PendingTarget == target && phaseShots == 0, label + " starts with a pending, not yet aligned shot");
        cancel();
        yield return new WaitForSeconds(0.14f);
        Check(weapon.PendingTarget == null && phaseShots == 0, label + " cancels safely");
        Keys();
        movement.SetInteractionLocked(false);
        movement.enabled = true;
        attack.enabled = true;
        yield return new WaitForSeconds(0.4f);
        Check(phaseShots == 0, label + " does not fire later");
        if (!enemy.activeSelf) enemy.SetActive(true);
        Set(facing, "rotationSpeed", 1080f);
    }

    private IEnumerator ResetPlayer(float yaw)
    {
        Keys();
        movement.SetInteractionLocked(false);
        attack.enabled = true;
        yield return new WaitForSeconds(0.65f);
        CharacterController controller = attack.GetComponent<CharacterController>();
        controller.enabled = false;
        attack.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;
        facing.ClearAim();
        weapon.CancelPendingShot();
        enemy.transform.position = origin + Vector3.forward * 6f;
        Physics.SyncTransforms();
        Invoke(targeting, "RefreshTarget");
        yield return null;
    }

    private void CreateEnemy()
    {
        enemy = new GameObject("Aiming Regression Enemy (Temporary)");
        enemy.SetActive(false);
        enemy.layer = LayerMask.NameToLayer("Enemy");
        CapsuleCollider collider = enemy.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up;
        collider.height = 2f;
        collider.radius = 0.4f;
        enemyHealth = enemy.AddComponent<EnemyHealth>();
        Set(enemyHealth, "maxHealth", 100000f);
        target = enemy.AddComponent<Targetable>();
        Transform aim = new GameObject("Aim Point").transform;
        aim.SetParent(enemy.transform, false);
        aim.localPosition = Vector3.up * 1.4f;
        Set(target, "aimPoint", aim);
        enemy.transform.position = origin + Vector3.forward * 6f;
        enemy.SetActive(true);
    }

    private void OnProjectileFired(Projectile projectile)
    {
        shots++;
        phaseShots++;
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        Vector3 velocity = body.linearVelocity;
        float directionError = Vector3.Angle(velocity, weapon.MuzzlePoint.forward);
        float aimError = Vector3.Angle(velocity, target.AimPoint.position - weapon.MuzzlePoint.position);
        Vector3 bodyDirection = target.AimPoint.position - attack.transform.position;
        bodyDirection.y = 0f;
        if (Vector3.Distance(projectile.transform.position, weapon.MuzzlePoint.position) > 0.001f ||
            directionError > 0.05f || aimError > 2.05f || Mathf.Abs(velocity.magnitude - 18f) > 0.01f ||
            Vector3.Angle(attack.transform.forward, bodyDirection) > 5.05f ||
            !attack.GetComponent<PlayerAnimationController>().IsRangedPoseReady)
            shotFailure = "Shot does not match final muzzle pose/speed: " + phase;
        if (Time.time - lastShotTime < attack.RangedAttackInterval - 0.005f) shotFailure = "Actual shot cooldown shorter than configured ranged interval";
        if (!attack.IsAttackHeld || movement.IsDashing || movement.IsShadowDashing || movement.IsInteractionLocked ||
            !facing.IsAiming || targeting.IsCloseTarget) shotFailure = "Shot fired in a blocked state: " + phase;
        lastShotTime = Time.time;
        lastProjectile = projectile;
        lastVelocity = velocity;
        report.AppendLine("SHOT " + phase + " t=" + Time.time.ToString("F3") +
                          " barrelError=" + directionError.ToString("F3") + " aimError=" + aimError.ToString("F3"));
    }

    private void OnRequested(Targetable _) => requests++;
    private void OnConfirmed(Targetable _) => confirmed++;
    private void Keys(params Key[] keys)
    {
        if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }
    private void CheckShotFailure()
    {
        Check(shotFailure == null, shotFailure, false);
        Check(runtimeFailure == null, runtimeFailure, false);
    }
    private void OnLogMessage(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            runtimeFailure = message + "\n" + stackTrace;
    }
    private void Check(bool condition, string message, bool log = true)
    {
        if (!condition) throw new InvalidOperationException(message);
        if (log) report.AppendLine("PASS " + message);
    }
    private static void Set(object owner, string field, object value) => owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static T Get<T>(object owner, string field) => (T)owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Invoke(object owner, string method) => owner.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);

    private void Finish(Exception failure)
    {
        if (finished) return;
        finished = true;
        if (weapon != null) weapon.ProjectileFired -= OnProjectileFired;
        if (attack != null)
        {
            attack.RangedAttackRequested -= OnRequested;
            attack.RangedShotFired -= OnConfirmed;
        }
        try
        {
            if (playerInput != null && playerInput.actions != null) playerInput.actions.devices = originalActionDevices;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        }
        catch (Exception exception) { failure = failure ?? exception; }
        Application.targetFrameRate = originalTargetFrameRate;
        QualitySettings.vSyncCount = originalVSync;
        Application.runInBackground = originalRunInBackground;
        if (failure == null && runtimeFailure != null) failure = new InvalidOperationException(runtimeFailure);
        Application.logMessageReceived -= OnLogMessage;
        report.AppendLine(failure == null ? "ALL CHECKS PASSED" : "FAIL " + failure);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CombatAimingRegression.txt", report.ToString());
        if (failure == null) Debug.Log("Combat aiming regression: ALL CHECKS PASSED. Logs/CombatAimingRegression.txt");
        else Debug.LogError("Combat aiming regression failed: " + failure.Message + ". Logs/CombatAimingRegression.txt");
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }

    private void OnDestroy()
    {
        if (!finished) Finish(new InvalidOperationException("Regression interrupted before completion"));
    }
}
#endif
