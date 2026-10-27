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

public sealed class DemonLordEncounterRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder log = new StringBuilder();
    private readonly HashSet<string> lordEffects = new HashSet<string>(), dragonSkills = new HashSet<string>();
    private EncounterCombatant lord, dragon;
    private BossHealthUI lordUi, dragonUi;
    private DemonLordVFX fx;
    private PlayerHealth player;
    private PlayerMovement movement;
    private CharacterController cc;
    private PlayerInput input;
    private PlayerRangedWeapon weapon;
    private Keyboard keyboard;
    private GamePauseController pause;
    private ReadOnlyArray<InputDevice>? oldDevices;
    private bool passed = true, finished, oldBackground;
    private int oldFps, oldVsync, lordHits, dragonHits, shots;
    private float oldScale;
    private string runtimeError;

    private IEnumerator Start()
    {
        oldBackground = Application.runInBackground; oldFps = Application.targetFrameRate;
        oldVsync = QualitySettings.vSyncCount; oldScale = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.timeScale = 1;
        Application.logMessageReceived += OnLog;
        log.AppendLine("DemonLord and Nightmare Dragon actual-player test " + DateTime.Now.ToString("s"));
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
        lord = FindObjectsByType<EncounterCombatant>().First(a => a.name == "DemonLord");
        dragon = FindObjectsByType<EncounterCombatant>().First(a => a.name == "DragonNightmare");
        fx = lord.GetComponent<DemonLordVFX>();
        var presenters = FindObjectsByType<BossHealthUI>();
        lordUi = presenters.First(ui => ui.name == "DemonLord Encounter HUD");
        dragonUi = presenters.First(ui => ui.name == "Nightmare Dragon HUD");
        movement = FindAnyObjectByType<PlayerMovement>(); cc = movement.GetComponent<CharacterController>();
        player = movement.GetComponent<PlayerHealth>(); input = movement.GetComponent<PlayerInput>();
        weapon = movement.GetComponent<PlayerRangedWeapon>(); pause = FindAnyObjectByType<GamePauseController>();
        foreach (var wave in FindObjectsByType<EnemyWaveSpawner>()) { wave.StopAllCoroutines(); wave.enabled = false; }
        foreach (var enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        FindAnyObjectByType<KingOfDemonAI>().gameObject.SetActive(false);
        if (input.actions.devices.HasValue) oldDevices = new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        keyboard = InputSystem.AddDevice<Keyboard>("DemonLordTestKeyboard"); input.actions.devices = new InputDevice[] { keyboard };
        Field(player, "maxHealth", 2000f); Field(player, "<CurrentHealth>k__BackingField", 2000f);
        FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown, false);
        fx.EffectReleased += effect => lordEffects.Add(effect);
        lord.MeleeHit += () => lordHits++; dragon.MeleeHit += () => dragonHits++;
        dragon.SkillStarted += skill => dragonSkills.Add(skill.name);
        weapon.ProjectileFired += _ => shots++;
        yield return Wait(1.6f);
        Check(lord.State == EncounterState.Dormant && dragon.State == EncounterState.Dormant &&
            !lordUi.IsVisible && !dragonUi.IsVisible, "both enemies dormant and health HUD hidden outside arena");
        Check(lord.GetComponent<NavMeshAgent>().isOnNavMesh && dragon.GetComponent<NavMeshAgent>().isOnNavMesh, "both enemies stand on the authored platform navigation");
        Check(dragon.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m =>
            m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit" && m.shader.isSupported)), "Nightmare Dragon uses supported textured URP material");
        var dragonAnimator = dragon.GetComponentInChildren<Animator>();
        Check(dragonAnimator.avatar != null && dragonAnimator.avatar.isValid && !dragonAnimator.avatar.isHuman,
            "dragon retains valid Generic rig");
        var beforeLord = lord.transform.position; var beforeDragon = dragon.transform.position;
        yield return Wait(3.5f);
        Check(Vector3.Distance(beforeLord, lord.transform.position) > .1f && Vector3.Distance(beforeDragon, dragon.transform.position) > .1f,
            "both guards patrol before detecting player");

        Warp(lord.transform.position + Vector3.forward * 7f); yield return Wait(.65f);
        Check(lord.Arena.PlayerInside && lord.IsEngaged && dragon.IsEngaged && lordUi.IsVisible && dragonUi.IsVisible,
            "same arena activates both AIs and both health bars");
        Check(Mathf.Abs(lordUi.GetComponent<RectTransform>().anchoredPosition.y - dragonUi.GetComponent<RectTransform>().anchoredPosition.y) >= 80,
            "two boss health bars occupy separate HUD rows");
        float lordHp = lord.Health.CurrentHealth;
        Hold(Key.J); yield return Wait(1.8f); Hold(); yield return Wait(.4f);
        Check(shots > 0 && lord.Health.CurrentHealth < lordHp && lordUi.DisplayedHealth < .99f,
            "actual player gun damages DemonLord and updates its HUD; HP=" + lord.Health.CurrentHealth);

        Warp(dragon.transform.position + Vector3.forward * 6.5f); yield return Wait(.3f);
        float dragonHp = dragon.Health.CurrentHealth;
        Hold(Key.J); yield return Wait(1.7f); Hold(); yield return Wait(.4f);
        Check(dragon.Health.CurrentHealth < dragonHp && dragonUi.DisplayedHealth < .99f,
            "actual player gun damages Nightmare Dragon and updates its HUD; HP=" + dragon.Health.CurrentHealth);
        yield return Capture("Combat");
        var p1 = lord.transform.position; var p2 = dragon.transform.position;
        pause.SetPaused(GamePauseReason.Inventory, true); yield return Wait(.4f);
        Check(Time.timeScale == 0 && Vector3.Distance(p1, lord.transform.position) < .01f && Vector3.Distance(p2, dragon.transform.position) < .01f,
            "inventory pause freezes both enemies without a tester overwriting time");
        pause.SetPaused(GamePauseReason.Inventory, false);

        Warp(lord.transform.position + lord.transform.forward * 2.7f);
        float deadline = Time.realtimeSinceStartup + 14f;
        while ((!lordEffects.Contains("Slash") || !lordEffects.Contains("Nova")) && Time.realtimeSinceStartup < deadline) yield return null;
        Warp(lord.transform.position + lord.transform.forward * 7f);
        deadline = Time.realtimeSinceStartup + 22f;
        while ((!lordEffects.Contains("Bolt") || !lordEffects.Contains("Curse") || dragonSkills.Count < 3) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(new[] { "Slash", "Bolt", "Nova", "Curse" }.All(lordEffects.Contains), "DemonLord animation events execute slash, blood bolt, nova and curse: " + string.Join(",", lordEffects));
        Check(dragonSkills.Count == 3 && dragonHits > 0, "dragon executes bite, claw and horn attacks with real damage: " + string.Join(",", dragonSkills));
        Check(lordHits > 0 && player.CurrentHealth < 2000 && player.IsAlive, "melee events damage actual PlayerHealth; lord=" + lordHits + " dragon=" + dragonHits);
        Check(lord.Arena.Contains(lord.transform.position) && lord.Arena.Contains(dragon.transform.position), "both enemies stay in DemonLord arena");
        var head = dragon.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
        Check(Vector3.Distance(head.position, dragon.transform.position) < 6f, "generic dragon animation remains attached to navigation root");

        Warp(lord.HomePosition + Vector3.left * 45f, false); yield return Wait(.6f);
        Check(!lordUi.IsVisible && !dragonUi.IsVisible && lord.Health.IsInvulnerable && dragon.Health.IsInvulnerable,
            "leaving arena hides both bars and protects returning enemies");
        Check(FindObjectsByType<BloodProjectile>().Length == 0 && FindObjectsByType<BloodNova>().Length == 0 && FindObjectsByType<CurseCircle>().Length == 0,
            "leaving cancels every outstanding damaging spell");
        bool lordHome = false, dragonHome = false; deadline = Time.realtimeSinceStartup + 16f;
        while ((!lordHome || !dragonHome) && Time.realtimeSinceStartup < deadline)
        {
            lordHome |= lord.State == EncounterState.Dormant && Vector3.Distance(lord.transform.position, lord.HomePosition) < .7f;
            dragonHome |= dragon.State == EncounterState.Dormant && Vector3.Distance(dragon.transform.position, dragon.HomePosition) < .7f;
            yield return null;
        }
        Check(lordHome && dragonHome && lord.Health.CurrentHealth == lord.Health.MaxHealth && dragon.Health.CurrentHealth == dragon.Health.MaxHealth,
            "both enemies return home and restore encounter health");

        Warp(lord.HomePosition + Vector3.forward * 6f); yield return Wait(.7f);
        lord.Health.TakeDamage(99999f); yield return Wait(.5f);
        Check(lord.State == EncounterState.Dead && !lord.GetComponent<Targetable>().IsTargetable && lordUi.DisplayedHealth < .01f,
            "DemonLord dies and drains only its own health bar");
        yield return Wait(2.3f);
        Check(lordEffects.Contains("Death") && !lordUi.IsVisible && dragonUi.IsVisible && dragon.Health.IsAlive,
            "DemonLord death mist plays; surviving dragon remains active with its own HUD");
        Field(weapon, "damage", 9999f);
        Warp(dragon.transform.position + dragon.transform.forward * 6.5f); yield return Wait(.4f);
        Hold(Key.J); yield return Wait(1f); Hold(); yield return Wait(.5f);
        Check(dragon.State == EncounterState.Dead && !dragon.GetComponent<CapsuleCollider>().enabled &&
            dragonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "actual player shot kills dragon and plays its death animation");
        yield return Capture("Defeated"); yield return Wait(2.4f);
        Check(!lordUi.IsVisible && !dragonUi.IsVisible, "all defeated enemy health bars fade out");
        Warp(lord.HomePosition + Vector3.left * 45, false); yield return Wait(.2f);
        Warp(lord.HomePosition + Vector3.forward * 6); yield return Wait(.4f);
        Check(lord.State == EncounterState.Dead && dragon.State == EncounterState.Dead, "defeated enemies do not respawn when re-entering arena");
        yield return SpellChecks();
        Check(runtimeError == null, "no runtime errors through full two-enemy encounter");
    }

    private IEnumerator SpellChecks()
    {
        movement.enabled = false; var point = new Vector3(0, 100, 0); Warp(point, false);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = point + new Vector3(-2, 1, 0);
        wall.transform.localScale = new Vector3(.15f, 3, 3); Physics.SyncTransforms();
        float hp = player.CurrentHealth;
        var bolt = Instantiate(fx.boltPrefab, point + new Vector3(-4, 1, 0), Quaternion.identity).GetComponent<BloodProjectile>();
        bolt.homing = 0; bolt.Launch(Vector3.right, 80, 20, "TakeDamage", lord.gameObject, player.transform);
        yield return Wait(.25f);
        Check(player.CurrentHealth == hp && bolt.transform.position.x < -1.8f, "blood bolt sweep cannot tunnel through thin wall");
        Destroy(wall); Destroy(bolt.gameObject); yield return null;
        bolt = Instantiate(fx.boltPrefab, point + new Vector3(-4, 1, 0), Quaternion.identity).GetComponent<BloodProjectile>();
        bolt.homing = 0; bolt.Launch(Vector3.right, 80, 20, "TakeDamage", lord.gameObject, player.transform); yield return Wait(.25f);
        Check(Mathf.Abs(player.CurrentHealth - (hp - 20)) < .01f, "blood bolt applies configured damage exactly once"); Destroy(bolt.gameObject);
        hp = player.CurrentHealth;
        var curse = Instantiate(fx.cursePrefab, point, Quaternion.identity).GetComponent<CurseCircle>();
        curse.Init(3, .5f, 35, "TakeDamage", lord.gameObject); yield return Wait(.2f);
        pause.SetPaused(GamePauseReason.Inventory, true); yield return Wait(.6f);
        Check(player.CurrentHealth == hp && curse != null, "curse telegraph countdown pauses with inventory");
        pause.SetPaused(GamePauseReason.Inventory, false); yield return Wait(.5f);
        Check(Mathf.Abs(player.CurrentHealth - (hp - 35)) < .01f, "curse explodes once after warning delay");
        hp = player.CurrentHealth;
        curse = Instantiate(fx.cursePrefab, point, Quaternion.identity).GetComponent<CurseCircle>();
        curse.Init(3, .4f, 35, "TakeDamage", lord.gameObject); Warp(point + Vector3.right * 5, false); yield return Wait(.6f);
        Check(player.CurrentHealth == hp, "player can dodge out of the fixed curse circle");
        Warp(point, false);
        var nova = Instantiate(fx.novaPrefab, point + new Vector3(2, -8, 0), Quaternion.identity).GetComponent<BloodNova>();
        nova.Init(6, 25, "TakeDamage", lord.gameObject); yield return Wait(.6f);
        Check(player.CurrentHealth == hp, "blood nova cannot hit a different floor"); Destroy(nova.gameObject);
        movement.enabled = true;
    }
    private void Warp(Vector3 position, bool fitArena = true)
    {
        Hold();
        if (fitArena)
        {
            position = lord.Arena.ClampToArena(position, 1.5f);
            if (NavMesh.SamplePosition(position, out var hit, 2, NavMesh.AllAreas)) position = hit.position;
        }
        cc.enabled = false; movement.transform.position = position + Vector3.up * .06f; cc.enabled = true; Physics.SyncTransforms();
        var targeting = movement.GetComponent<PlayerTargeting>(); targeting.enabled = false; targeting.enabled = true;
    }
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private void Hold(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float duration) { float end = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < end) yield return null; }
    private static IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/DemonLordDragon_" + name + ".png"); yield return Wait(.2f); }
    private void Check(bool valid, string message)
    { passed &= valid; log.AppendLine((valid ? "PASS " : "FAIL ") + message); File.WriteAllText("Logs/DemonLordDragonCombatTest.txt", log.ToString()); }
    private void OnLog(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace; }
    private void Finish(Exception error)
    {
        if (finished) return; finished = true;
        if (error != null) { passed = false; log.AppendLine("EXCEPTION " + error); }
        if (runtimeError != null) { passed = false; log.AppendLine("RUNTIME ERROR " + runtimeError); }
        Hold(); if (input != null) input.actions.devices = oldDevices;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived -= OnLog;
        Application.runInBackground = oldBackground; Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync; Time.timeScale = oldScale;
        log.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/DemonLordDragonCombatTest.txt", log.ToString());
        Debug.Log("DemonLord and Dragon regression complete. Passed=" + passed); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Test interrupted.")); }
}
#endif
