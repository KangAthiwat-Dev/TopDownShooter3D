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
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class KingOfDemonRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private bool passed = true, finished, oldBackground;
    private int oldFps, oldVsync;
    private float oldTimeScale;
    private string runtimeError;
    private Keyboard keyboard;
    private PlayerInput input;
    private ReadOnlyArray<InputDevice>? oldDevices;
    private PlayerMovement movement;
    private CharacterController controller;
    private PlayerHealth player;
    private KingOfDemonAI boss;
    private BossHealthUI ui;
    private BossVFXController effects;
    private GamePauseController pause;
    private int slashHits, fireballs, shockwaves, shots, deaths;

    private IEnumerator Start()
    {
        oldBackground = Application.runInBackground;
        oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount; oldTimeScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.timeScale = 1;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Actual player / boss combat and HUD " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception failure = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; }
            catch (Exception error) { failure = error; }
            if (failure != null) { Finish(failure); yield break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }

    private IEnumerator Checks()
    {
        boss = FindAnyObjectByType<KingOfDemonAI>(); ui = FindObjectsByType<BossHealthUI>().First(presenter => presenter.name == "Boss Encounter HUD");
        effects = boss.GetComponent<BossVFXController>();
        movement = FindAnyObjectByType<PlayerMovement>(); player = movement.GetComponent<PlayerHealth>();
        controller = movement.GetComponent<CharacterController>(); input = movement.GetComponent<PlayerInput>();
        pause = FindAnyObjectByType<GamePauseController>();
        foreach (var wave in FindObjectsByType<EnemyWaveSpawner>()) { wave.StopAllCoroutines(); wave.enabled = false; }
        foreach (var enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        if (input.actions.devices.HasValue) oldDevices = new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        keyboard = InputSystem.AddDevice<Keyboard>("BossEncounterTestKeyboard");
        input.actions.devices = new InputDevice[] { keyboard };
        FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown, false);
        // Survive the full skill sequence without changing any boss damage values.
        Field(player, "maxHealth", 1000f);
        Field(player, "<CurrentHealth>k__BackingField", 1000f);
        boss.MeleeHit += () => slashHits++;
        effects.FireballReleased += () => fireballs++;
        effects.ShockwaveReleased += () => shockwaves++;
        movement.GetComponent<PlayerRangedWeapon>().ProjectileFired += _ => shots++;
        boss.Health.Died += () => deaths++;
        yield return Wait(1.6f);
        Check(boss.State == DemonBossState.Dormant && !ui.IsVisible && boss.Health.IsInvulnerable, "outside arena: dormant boss, hidden HUD, protected health");
        float before = boss.Health.CurrentHealth; boss.Health.TakeDamage(50);
        Check(boss.Health.CurrentHealth == before, "cannot damage boss from outside encounter");
        Check(!boss.Health.AllowInstantKill, "ordinary melee one-hit-kill rule does not instantly kill boss");
        Check(!boss.TryGetComponent(out BossSkillTester tester) || !tester.enabled, "skill tester cannot overwrite gameplay time");
        Check(boss.GetComponent<NavMeshAgent>().isOnNavMesh, "boss is on the authored platform navigation");

        var home = boss.HomePosition;
        Warp(home + Vector3.forward * 9f);
        yield return Wait(.65f);
        Check(boss.Arena.PlayerInside && boss.IsEngaged && ui.IsVisible && ui.DisplayedHealth > .99f, "arena entry activates AI and fades in full boss HUD");
        Check(movement.GetComponent<PlayerTargeting>().CurrentTarget == boss.GetComponent<Targetable>(), "existing player auto-aim acquires boss");
        PressKeys(Key.J); yield return Wait(2.3f); PressKeys(); yield return Wait(.5f);
        Check(shots > 0 && boss.Health.CurrentHealth < before && ui.DisplayedHealth < .99f,
            "actual player gun/projectiles reduce boss health and HUD; shots=" + shots + " HP=" + boss.Health.CurrentHealth);
        yield return Capture("Combat");

        var capsule = boss.GetComponent<CapsuleCollider>();
        float bodyRadius = capsule.radius * Mathf.Abs(boss.transform.lossyScale.x);
        var approach = Vector3.ProjectOnPlane(player.transform.position - boss.transform.position, Vector3.up).normalized;
        Warp(boss.transform.position + approach * (bodyRadius + controller.radius + .35f));
        yield return Wait(.35f);
        Check(movement.GetComponent<PlayerTargeting>().IsCloseTarget,
            "player can enter melee mode beside the enlarged boss collider");
        before = boss.Health.CurrentHealth;
        PressKeys(Key.J); yield return Wait(1.8f); PressKeys(); yield return Wait(.3f);
        Check(movement.GetComponent<PlayerAttackController>().CurrentMode == AttackMode.Melee &&
            boss.Health.CurrentHealth < before && boss.Health.IsAlive,
            "actual player melee damages the enlarged boss without a one-hit kill");
        Warp(home + Vector3.forward * 9f);
        yield return Wait(.25f);

        var frozenPosition = boss.transform.position;
        var frozenState = boss.State;
        pause.SetPaused(GamePauseReason.Inventory, true); yield return Wait(.4f);
        Check(Time.timeScale == 0f && Vector3.Distance(frozenPosition, boss.transform.position) < .01f && boss.State == frozenState,
            "inventory pause freezes boss and preserves timeScale=0");
        pause.SetPaused(GamePauseReason.Inventory, false);
        float deadline = Time.realtimeSinceStartup + 24f;
        while ((slashHits == 0 || fireballs == 0 || shockwaves == 0) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(slashHits > 0 && fireballs > 0 && shockwaves > 0,
            "animation events deliver all three skills: melee hits=" + slashHits + ", fireballs=" + fireballs + ", shockwaves=" + shockwaves);
        Check(player.CurrentHealth < 1000f && player.IsAlive, "boss skills damage actual PlayerHealth");
        Check(Vector3.Distance(home, boss.transform.position) > 1f && boss.Arena.Contains(boss.transform.position), "boss runs toward player while staying in arena");

        Warp(home + Vector3.left * 45f);
        yield return Wait(.55f);
        Check(!boss.Arena.PlayerInside && !ui.IsVisible && boss.Health.IsInvulnerable, "arena exit hides HUD and disengages boss");
        Check(FindObjectsByType<MagicProjectile>().Length == 0 && FindObjectsByType<ShockwaveRing>().Length == 0, "disengage cancels outstanding boss projectiles and waves");
        deadline = Time.realtimeSinceStartup + 12f;
        while (boss.State != DemonBossState.Dormant && Time.realtimeSinceStartup < deadline) yield return null;
        Check(boss.State == DemonBossState.Dormant && Vector3.Distance(home, boss.transform.position) < .6f &&
            boss.Health.CurrentHealth == boss.Health.MaxHealth, "boss returns home and resets encounter health");

        yield return CollisionChecks();
        Warp(home + Vector3.forward * 8f); yield return Wait(.6f);
        Check(boss.IsEngaged && ui.DisplayedHealth > .99f, "re-entering starts a fresh encounter");
        boss.Health.TakeDamage(300f); yield return Wait(.4f);
        Check(Mathf.Abs(ui.DisplayedHealth - .75f) < .02f, "boss HUD fill follows 900 / 1200 health without number labels");
        boss.Health.TakeDamage(99999f); boss.Health.TakeDamage(99999f);
        yield return Wait(.5f);
        Check(deaths == 1 && boss.State == DemonBossState.Dead && !boss.GetComponent<Targetable>().IsTargetable &&
            !boss.GetComponent<CapsuleCollider>().enabled && ui.DisplayedHealth < .01f, "boss death fires once, clears target/collision and drains HUD to zero");
        Check(boss.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Death"), "death animation plays");
        yield return Capture("Defeated");
        yield return Wait(2.4f);
        Check(!ui.IsVisible, "zero health is held briefly, then boss HUD fades out");
        Check(FindObjectsByType<VFXAutoDestroy>().Any(v => v.name.StartsWith("FX_DeathBurst")), "authored death animation event still creates death burst");
        Warp(home + Vector3.left * 45f); yield return Wait(.2f);
        Warp(home + Vector3.forward * 8f); yield return Wait(.5f);
        Check(boss.State == DemonBossState.Dead && !ui.IsVisible && boss.Health.CurrentHealth == 0f, "defeated boss does not respawn on arena re-entry");
        player.TakeDamage(99999f); yield return Wait(.2f);
        Check(pause.IsGameOver && Time.timeScale == 0f && !ui.IsVisible, "game-over pause remains compatible with boss HUD");
        Check(runtimeError == null, "no runtime errors during boss encounter");
        pause.ResetForRestart();
    }

    private IEnumerator CollisionChecks()
    {
        movement.enabled = false;
        var point = new Vector3(0, 100, 0);
        Warp(point);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Temporary boss projectile wall";
        wall.transform.position = point + new Vector3(-2, 1, 0);
        wall.transform.localScale = new Vector3(.15f, 3, 3);
        Physics.SyncTransforms();
        var projectile = Instantiate(effects.fireballPrefab, point + new Vector3(-4, 1, 0), Quaternion.identity).GetComponent<MagicProjectile>();
        float hp = player.CurrentHealth;
        projectile.Launch(Vector3.right, 80f, 25f, "TakeDamage", boss.gameObject);
        yield return Wait(.25f);
        Check(player.CurrentHealth == hp && projectile.transform.position.x < -1.8f, "fast fireball sweep hits thin wall rather than tunnelling through");
        Destroy(wall); Destroy(projectile.gameObject); yield return null;
        projectile = Instantiate(effects.fireballPrefab, point + new Vector3(-4, 1, 0), Quaternion.identity).GetComponent<MagicProjectile>();
        projectile.Launch(Vector3.right, 80f, 25f, "TakeDamage", boss.gameObject);
        yield return Wait(.25f);
        Check(Mathf.Abs(player.CurrentHealth - (hp - 25)) < .01f, "swept fireball hits player once for configured damage");
        Destroy(projectile.gameObject);
        hp = player.CurrentHealth;
        var ring = Instantiate(effects.shockwavePrefab, point + new Vector3(2, -8, 0), Quaternion.identity).GetComponent<ShockwaveRing>();
        ring.Init(7, 20, "TakeDamage", boss.gameObject); yield return Wait(.8f);
        Check(player.CurrentHealth == hp, "shockwave cannot hit player on a different floor"); Destroy(ring.gameObject);
        ring = Instantiate(effects.shockwavePrefab, point + new Vector3(2, 0, 0), Quaternion.identity).GetComponent<ShockwaveRing>();
        ring.Init(7, 20, "TakeDamage", boss.gameObject); yield return Wait(.8f);
        Check(Mathf.Abs(player.CurrentHealth - (hp - 20)) < .01f, "shockwave sweep hits same-floor player exactly once"); Destroy(ring.gameObject);
        hp = player.CurrentHealth;
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = point + new Vector3(1, 1, 0); wall.transform.localScale = new Vector3(.2f, 3, 3); Physics.SyncTransforms();
        ring = Instantiate(effects.shockwavePrefab, point + new Vector3(2, 0, 0), Quaternion.identity).GetComponent<ShockwaveRing>();
        ring.Init(7, 20, "TakeDamage", boss.gameObject); yield return Wait(.8f);
        Check(player.CurrentHealth == hp, "solid wall blocks shockwave damage"); Destroy(ring.gameObject); Destroy(wall);
        movement.enabled = true;
    }

    private static void Field(object instance, string name, object value) => instance.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private void Warp(Vector3 position)
    {
        PressKeys(); controller.enabled = false; movement.transform.position = position + Vector3.up * .06f;
        controller.enabled = true; Physics.SyncTransforms();
    }
    private void PressKeys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }
    private static IEnumerator Capture(string label)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/KingOfDemon_" + label + ".png");
        yield return Wait(.2f);
    }
    private void Check(bool ok, string text)
    {
        passed &= ok; report.AppendLine((ok ? "PASS " : "FAIL ") + text);
        File.WriteAllText("Logs/KingOfDemonCombatTest.txt", report.ToString());
    }
    private void OnLog(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace; }
    private void Finish(Exception exception)
    {
        if (finished) return; finished = true;
        if (exception != null) { passed = false; report.AppendLine("EXCEPTION " + exception); }
        if (runtimeError != null) { passed = false; report.AppendLine("RUNTIME ERROR " + runtimeError); }
        PressKeys();
        if (input != null) input.actions.devices = oldDevices;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived -= OnLog;
        Application.runInBackground = oldBackground; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync; Time.timeScale = oldTimeScale;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        File.WriteAllText("Logs/KingOfDemonCombatTest.txt", report.ToString());
        Debug.Log("Boss combat and HUD regression completed. Passed=" + passed);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Test interrupted.")); }
}
#endif
