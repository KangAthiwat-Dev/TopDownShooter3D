#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit menu-only, real-scene checks. Fixtures never become saved inventory.
[DefaultExecutionOrder(1000)]
public class InventoryRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<ItemDefinition> testItems = new List<ItemDefinition>();
    private PlayerInventory inventory;
    private PlayerMeleeEquipment equipment;
    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerInput input;
    private PlayerInputGate gate;
    private PlayerInteractor interactor;
    private InventoryPanelUI panel;
    private InventoryHotbarUI hotbar;
    private GamePauseController pause;
    private Keyboard keyboard;
    private Gamepad gamepad;
    private ReadOnlyArray<InputDevice>? originalDevices;
    private bool originalAutoSwitch, originalBackground, finished;
    private int originalFps, originalVSync, shots;
    private string runtimeError;
    private readonly Vector3 origin = new Vector3(1000, 10, 1000);

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        originalFps = Application.targetFrameRate; originalVSync = QualitySettings.vSyncCount;
        originalBackground = Application.runInBackground;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Inventory regression — " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(RunChecks());
        while (stack.Count > 0)
        {
            object next = null; Exception failure = null;
            try
            {
                if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current;
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null) { Finish(failure); yield break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }
    private void FindSceneObjects()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        inventory = movement.GetComponent<PlayerInventory>(); equipment = movement.GetComponent<PlayerMeleeEquipment>();
        health = movement.GetComponent<PlayerHealth>(); input = movement.GetComponent<PlayerInput>();
        gate = movement.GetComponent<PlayerInputGate>(); interactor = movement.GetComponent<PlayerInteractor>();
        panel = FindAnyObjectByType<InventoryPanelUI>(); hotbar = FindAnyObjectByType<InventoryHotbarUI>();
        pause = FindAnyObjectByType<GamePauseController>();
    }
    private void PairDevices()
    {
        originalDevices = null;
        var devices = input.actions.devices;
        if (devices.HasValue)
        {
            var copy = new InputDevice[devices.Value.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = devices.Value[i];
            originalDevices = new ReadOnlyArray<InputDevice>(copy);
        }
        originalAutoSwitch = input.neverAutoSwitchControlSchemes;
        input.neverAutoSwitchControlSchemes = true;
        input.actions.devices = new InputDevice[] { keyboard, gamepad };
    }
    private void RestoreDevices()
    {
        if (input == null) return;
        input.actions.devices = originalDevices; input.neverAutoSwitchControlSchemes = originalAutoSwitch;
    }
    private IEnumerator RunChecks()
    {
        FindSceneObjects();
        Check(inventory != null && gate != null && pause != null && hotbar != null && panel != null, "Scene has all inventory, hotbar, pause and input connections");
        Check(!panel.IsOpen && inventory.OccupiedSlots == 0 && equipment.WeaponCount == 0 && Time.timeScale == 1f,
            "Spawn is unpaused with an empty 8+2 bag; no fake starting stock");
        Check(Get<InventorySlotUI[]>(panel, "slots").Length == 10 && Get<InventoryHotbarUI.SlotView[]>(hotbar, "slots").Length == 10,
            "Inventory and hotbar both have exactly ten views");
        Check(input.actions.FindActionMap("Menus").enabled && input.actions.FindActionMap("Gameplay").enabled,
            "Menu actions stay enabled alongside Gameplay");
        Check(movement.GetComponentInChildren<ShadowDashTrailVfx>(true).UsesIceForm,
            "Scene keeps its configured Ice head instead of silently falling back to old trails");
        foreach (EnemyWaveSpawner spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = origin + Vector3.down * 0.6f; floor.transform.localScale = new Vector3(150, 1, 150);
        Teleport(origin);
        keyboard = InputSystem.AddDevice<Keyboard>("InventoryRegressionKeyboard");
        gamepad = InputSystem.AddDevice<Gamepad>("InventoryRegressionGamepad"); PairDevices();
        yield return Wait(0.15f);
        yield return DataChecks();

        Keys(Key.I); yield return Wait(0.08f); Keys(); yield return Wait(0.04f);
        Check(panel.IsOpen && Time.timeScale == 0f && Cursor.visible && Cursor.lockState == CursorLockMode.None && AudioListener.pause,
            "I opens the modal, pauses time/audio and unlocks the cursor");
        yield return Capture("Empty");
        Keys(Key.Escape); yield return Wait(0.05f); Keys(); yield return Wait(0.04f);
        Check(!panel.IsOpen && Time.timeScale == 1f && !AudioListener.pause, "Escape closes and restores the game");

        // A real pickup action must freeze, retain its reservation and commit once.
        GameObject pickupObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/TrainingClubPickup.prefab"));
        pickupObject.transform.position = movement.transform.position + Vector3.forward;
        Physics.SyncTransforms(); yield return Wait(0.08f);
        Check(interactor.TryStartPickup(pickupObject.GetComponent<MeleeWeaponPickup>()), "Start the existing animated melee pickup");
        yield return Wait(0.06f);
        Check(panel.TryOpen(), "Bag can open during the pickup animation");
        int count = equipment.WeaponCount; yield return Wait(0.35f);
        Check(interactor.IsInteracting && equipment.WeaponCount == count && pickupObject != null,
            "Paused pickup retains reservation without collecting or deleting the world item");
        panel.Close(); yield return Wait(1.8f);
        Check(!interactor.IsInteracting && equipment.WeaponCount == 1 && pickupObject == null,
            "Pickup resumes and collects exactly once after closing");
        Check(equipment.TryEquip(AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>("Assets/_Project/Prefabs/Weapons/TrainingClub.asset")),
            "Second melee weapon uses existing equipment, not copied inventory data");
        Check(!equipment.TryEquip(equipment.CurrentWeapon), "A third melee weapon remains rejected");

        ItemDefinition supply = CreateItem("Alloy Scrap", 20);
        Check(inventory.TryAdd(supply, 25), "Add live supplies through PlayerInventory API");
        Get<Button>(panel, "openButton").onClick.Invoke(); yield return Wait(0.06f);
        var slots = Get<InventorySlotUI[]>(panel, "slots");
        slots[0].Button.onClick.Invoke();
        Check(panel.DetailTitle == supply.DisplayName && slots[0].DisplayedName == supply.DisplayName && !Get<Button>(panel, "selectWeaponButton").gameObject.activeSelf,
            "Supply selection shows details without an unsupported use/drop action");
        inventory.TryRemove(0, 2);
        var bar = Get<InventoryHotbarUI.SlotView[]>(hotbar, "slots");
        Check(bar[0].itemName.text == supply.DisplayName && bar[0].stateLabel.text == "x18" && slots[1].DisplayedName == supply.DisplayName,
            "Live stack changes synchronize both bag and hotbar");
        yield return Capture("Supplies");
        EventSystem.current.SetSelectedGameObject(slots[9].Button.gameObject);
        Check(panel.SelectedSlot == 9, "Keyboard/gamepad focus updates item details");
        Button select = Get<Button>(panel, "selectWeaponButton");
        Check(select.interactable && equipment.SelectedSlotIndex == 0, "Inspecting a stored weapon does not silently equip it");
        select.onClick.Invoke();
        Check(equipment.SelectedSlotIndex == 1 && equipment.WeaponCount == 2 && bar[9].stateLabel.text == "ACTIVE" && bar[8].stateLabel.text == "STORED" && !select.interactable,
            "Select Weapon changes the same equipment and hotbar without duplicating weapons");
        yield return Capture("Weapon");
        Get<Button>(panel, "closeButton").onClick.Invoke(); yield return Wait(0.05f);
        Check(!panel.IsOpen, "On-screen open and close buttons work");

        yield return InputChecks();
        yield return ActionChecks();
        yield return PauseChecks();

        Check(panel.TryOpen(), "Open bag before a death event");
        health.TakeDamage(health.MaxHealth); yield return Wait(0.08f);
        GameOverUI gameOver = FindAnyObjectByType<GameOverUI>();
        Check(!panel.IsOpen && !panel.TryOpen() && pause.IsGameOver && Time.timeScale == 0f,
            "Death closes the bag, prevents reopening and retains the independent Game Over pause");
        panel.Close(); Check(Time.timeScale == 0f, "Closing inventory cannot unpause Game Over");
        var deathPresentation=health.GetComponent<PlayerDeathPresentation>();
        if(deathPresentation!=null)yield return Wait(deathPresentation.GameOverDelay+.1f);
        Check(Get<GameObject>(gameOver,"gameOverPanel").activeInHierarchy,"Game Over becomes visible after the player's death pose");
        Check(EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Get<GameObject>(gameOver, "gameOverPanel").transform),
            "Game Over keeps restart-button focus");
        yield return Capture("GameOver");
        var oldScene = SceneManager.GetActiveScene().handle;
        RestoreDevices(); Keys(); yield return null;
        gameOver.RestartGame();
        float deadline = Time.realtimeSinceStartup + 8f;
        while ((SceneManager.GetActiveScene().handle == oldScene || FindAnyObjectByType<PlayerInventory>() == null) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return Wait(0.15f); FindSceneObjects(); PairDevices();
        Check(health.IsAlive && inventory.OccupiedSlots == 0 && equipment.WeaponCount == 0 && !panel.IsOpen && !pause.IsPaused && Time.timeScale == 1f && !AudioListener.pause &&
            movement.GetComponentInChildren<ShadowDashTrailVfx>(true).UsesIceForm,
            "Actual scene restart restores gameplay, empty inventory, menus and valid Ice references (including unlisted Editor scenes)");
        Keys(Key.Tab); yield return Wait(0.06f); Keys(); yield return Wait(0.03f);
        Check(panel.IsOpen, "Inventory shortcuts still work after scene restart"); panel.Close();
        Check(runtimeError == null, "No runtime errors or exceptions");
    }
    private IEnumerator DataChecks()
    {
        ItemDefinition item = CreateItem("Stack Test", 3);
        int events = 0; Action changed = () => events++; inventory.Changed += changed;
        Check(!inventory.TryAdd(null) && !inventory.TryAdd(item, 0) && !inventory.TryRemove(-1) && !inventory.TryRemove(8) && events == 0,
            "Invalid inventory operations are rejected without notifications");
        Check(inventory.TryAdd(item, 5) && inventory.GetQuantity(0) == 3 && inventory.GetQuantity(1) == 2 && events == 1,
            "Stack overflow fills the next supply slot and emits one change");
        Check(inventory.TryAdd(item, 19) && inventory.OccupiedSlots == 8 && events == 2, "All eight supply slots fill to capacity");
        Check(Get<InventoryHotbarUI.SlotView[]>(hotbar, "slots")[7].stateLabel.text == "x3",
            "Supply slot eight never becomes an ACTIVE melee slot when equipment is empty");
        Check(!inventory.TryAdd(item, int.MaxValue) && !inventory.TryAdd(item) && events == 2 && inventory.GetQuantity(7) == 3,
            "A full/oversized pickup is atomic, with no partial mutation or integer overflow");
        Check(!inventory.TryRemove(0, 4) && inventory.TryRemove(0, 3) && inventory.GetItem(0) == null && inventory.GetQuantity(0) == 0,
            "Removing a stack clears its slot, but excessive removal does not change it");
        ItemDefinition other = CreateItem("Other Material", 1);
        Check(inventory.TryAdd(other) && inventory.GetItem(0) == other && !inventory.TryAdd(item),
            "Different item assets do not merge and empty slots are reusable");
        ClearSupplies(); inventory.Changed -= changed;
        yield return null;
    }
    private IEnumerator InputChecks()
    {
        GameObject enemy = new GameObject("Inventory Regression Target (Temporary)"); enemy.SetActive(false);
        enemy.layer = LayerMask.NameToLayer("Enemy");
        var collider = enemy.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2f;
        var enemyHealth = enemy.AddComponent<EnemyHealth>(); Set(enemyHealth, "maxHealth", 100000f);
        Targetable target = enemy.AddComponent<Targetable>();
        Transform aim = new GameObject("Aim Point").transform; aim.SetParent(enemy.transform, false); aim.localPosition = Vector3.up * 1.4f; Set(target, "aimPoint", aim);
        enemy.transform.position = movement.transform.position + Vector3.forward * 6; enemy.SetActive(true);
        Physics.SyncTransforms(); Invoke(movement.GetComponent<PlayerTargeting>(), "RefreshTarget");
        PlayerRangedWeapon ranged = movement.GetComponent<PlayerRangedWeapon>(); ranged.ProjectileFired += OnShot;
        Keys(Key.Tab); yield return Wait(0.06f); Keys(); yield return Wait(0.03f);
        Check(panel.IsOpen, "Tab opens inventory with menu input still running at timeScale zero");
        Vector3 position = movement.transform.position; float time = Time.time, energy = movement.GetComponent<PlayerEnergy>().CurrentEnergy, hp = enemyHealth.CurrentHealth;
        Keys(Key.W, Key.J, Key.E, Key.Space, Key.M); yield return Wait(0.3f);
        Check(movement.transform.position == position && Time.time == time && Mathf.Approximately(energy, movement.GetComponent<PlayerEnergy>().CurrentEnergy) && hp == enemyHealth.CurrentHealth && shots == 0 &&
            !movement.IsDashing && !movement.IsShadowDashing && !interactor.IsInteracting && ranged.PendingTarget == null,
            "Movement, energy, attacks, interaction and both dashes are blocked while the bag is open");
        Keys(Key.W, Key.J, Key.E, Key.Space, Key.M, Key.Escape); yield return Wait(0.2f);
        Check(!panel.IsOpen && !movement.IsDashing && !movement.IsShadowDashing && shots == 0 &&
            !gate.CanUse("Attack") && !gate.CanUse("Dash") && !gate.CanUse("ShadowDash") && !gate.CanUse("Interact"),
            "Closing with gameplay buttons held requires their release; no queued combat or dash leaks");
        Check(Vector3.Distance(position, movement.transform.position) > 0.05f, "Held movement resumes normally after closing");
        Keys(); yield return Wait(0.08f);
        Check(gate.CanUse("Attack") && gate.CanUse("Dash") && gate.CanUse("ShadowDash") && gate.CanUse("Interact"), "Releasing buttons clears all input gates");
        Teleport(origin); enemy.transform.position = origin + Vector3.forward * 6; Physics.SyncTransforms(); Invoke(movement.GetComponent<PlayerTargeting>(), "RefreshTarget");
        Keys(Key.J); yield return Wait(1.3f);
        Check(shots > 0, "A fresh attack after releasing fires actual barrel-aligned projectiles");
        Keys(); yield return Wait(0.05f); ranged.ProjectileFired -= OnShot; enemy.SetActive(false); Destroy(enemy);
        // Gamepad opens/closes without time advancing or switching away from Gameplay.
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Select)); yield return Wait(0.08f);
        Check(panel.IsOpen, "Gamepad Select opens the bag");
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return Wait(0.04f);
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); yield return Wait(0.08f);
        Check(!panel.IsOpen, "Gamepad East closes the bag");
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return Wait(0.04f);
        Keys(Key.I, Key.Tab); yield return Wait(0.06f); Keys(); yield return Wait(0.03f);
        Check(panel.IsOpen, "Simultaneous I and Tab toggle only once per frame"); panel.Close();
    }
    private IEnumerator ActionChecks()
    {
        GameObject enemy = new GameObject("Paused Melee Target (Temporary)"); enemy.SetActive(false);
        enemy.layer = LayerMask.NameToLayer("Enemy"); enemy.AddComponent<CapsuleCollider>();
        EnemyHealth enemyHealth = enemy.AddComponent<EnemyHealth>(); Set(enemyHealth, "maxHealth", 5000f);
        Targetable target = enemy.AddComponent<Targetable>();
        Transform aim = new GameObject("Aim Point").transform; aim.SetParent(enemy.transform, false); aim.localPosition = Vector3.up;
        Set(target, "aimPoint", aim); enemy.transform.position = movement.transform.position + Vector3.forward * 1.2f; enemy.SetActive(true);
        Physics.SyncTransforms(); Invoke(movement.GetComponent<PlayerTargeting>(), "RefreshTarget");
        PlayerMeleeAttack melee = movement.GetComponent<PlayerMeleeAttack>();
        Keys(Key.J); float deadline = Time.realtimeSinceStartup + 1.2f;
        while (!melee.IsAttacking && Time.realtimeSinceStartup < deadline) yield return null;
        Check(melee.IsAttacking && panel.TryOpen(), "Open bag during a real one-hit melee wind-up");
        yield return Wait(0.4f);
        Check(melee.IsAttacking && enemyHealth.CurrentHealth == 5000f && !equipment.CanSelectSlot(0),
            "Paused melee does not hit early or change its snapshotted weapon");
        Keys(); yield return Wait(0.04f); panel.Close(); yield return Wait(1.6f);
        Check(!melee.IsAttacking && !enemyHealth.IsAlive, "Melee resumes, lands its existing one-hit damage and finishes normally");
        enemy.SetActive(false); Destroy(enemy); yield return Wait(0.06f);
        Keys(Key.W, Key.Space); yield return Wait(0.04f);
        Check(movement.IsDashing && panel.TryOpen(), "Open bag during an active short dash");
        Vector3 position = movement.transform.position; yield return Wait(0.25f);
        Check(movement.IsDashing && movement.transform.position == position, "Short dash freezes in place without losing its action");
        Keys(); yield return Wait(0.04f); panel.Close(); yield return Wait(0.8f);
        Check(!movement.IsDashing, "Short dash completes after resuming");
        Keys(Key.W, Key.M); yield return Wait(0.36f);
        var ice = movement.GetComponentInChildren<ShadowDashIceForm>(true);
        Check(movement.IsShadowDashing && panel.TryOpen(), "Open bag during the real Ice Trails transformation");
        float energy = movement.GetComponent<PlayerEnergy>().CurrentEnergy; position = movement.transform.position;
        yield return Wait(0.3f);
        Check(movement.IsShadowDashing && movement.transform.position == position && Mathf.Approximately(energy, movement.GetComponent<PlayerEnergy>().CurrentEnergy) && !equipment.CanSelectSlot(0),
            "Paused Shadow Dash holds position/energy and forbids swapping an active action's weapon");
        panel.Close(); yield return Wait(0.7f);
        Check(!movement.IsShadowDashing && !ice.ShouldHideCharacter && !ice.IsHeadVisible,
            "Held Shadow input is suppressed after closing; landing restores the visible player");
        Keys(); yield return Wait(0.06f);
    }
    private IEnumerator PauseChecks()
    {
        Time.timeScale = 0.5f; AudioListener.pause = true; Cursor.visible = false; Cursor.lockState = CursorLockMode.None;
        Check(panel.TryOpen(), "Open from pre-existing slow motion/audio state"); panel.Close();
        Check(Time.timeScale == 0.5f && AudioListener.pause && !Cursor.visible, "Closing restores prior time scale, audio and cursor instead of assuming defaults");
        Time.timeScale = 0f; Check(panel.TryOpen(), "Open while an external system already paused time"); panel.Close();
        Check(Time.timeScale == 0f, "Closing cannot clear a pre-existing external time pause");
        Time.timeScale = 1f; AudioListener.pause = false; Cursor.visible = true;
        Check(panel.TryOpen(), "Open before disabling the inventory component"); panel.enabled = false;
        Check(!panel.IsOpen && !pause.IsPaused && Time.timeScale == 1f, "Disabling inventory restores its pause without leaving frozen gameplay");
        panel.enabled = true; yield return null;
        Keys(Key.I); yield return Wait(0.06f); Keys(); yield return Wait(0.03f);
        Check(panel.IsOpen, "Re-enabling inventory restores input callbacks without duplicates"); panel.Close();
    }
    private void OnShot(Projectile _) => shots++;
    private ItemDefinition CreateItem(string title, int stack)
    {
        ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
        Set(item, "displayName", title); Set(item, "maxStack", stack); Set(item, "description", "Temporary regression material. Not saved into the scene.");
        testItems.Add(item); return item;
    }
    private void ClearSupplies() { for (int i = 0; i < PlayerInventory.Capacity; i++) if (inventory.GetItem(i) != null) inventory.TryRemove(i, inventory.GetQuantity(i)); }
    private void Teleport(Vector3 position)
    {
        CharacterController controller = movement.GetComponent<CharacterController>(); controller.enabled = false;
        movement.transform.SetPositionAndRotation(position, Quaternion.identity); controller.enabled = true;
    }
    private IEnumerator Capture(string label)
    {
        yield return new WaitForEndOfFrame(); Directory.CreateDirectory("Logs");
        ScreenCapture.CaptureScreenshot("Logs/Inventory_" + label + ".png", 2); yield return Wait(0.08f);
    }
    private static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(seconds);
    private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
    private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
    private void Check(bool condition, string message)
    {
        if (runtimeError != null) throw new InvalidOperationException(runtimeError);
        if (!condition) throw new InvalidOperationException(message); report.AppendLine("PASS " + message);
    }
    private void OnLog(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace; }
    private void Finish(Exception failure)
    {
        if (finished) return; finished = true;
        try { RestoreDevices(); if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (gamepad != null) InputSystem.RemoveDevice(gamepad); }
        catch (Exception exception) { failure = failure ?? exception; }
        if (panel != null) panel.Close(); if (pause != null) pause.ResetForRestart(); Time.timeScale = 1f; AudioListener.pause = false;
        foreach (ItemDefinition item in testItems) if (item != null) Destroy(item);
        Application.targetFrameRate = originalFps; QualitySettings.vSyncCount = originalVSync; Application.runInBackground = originalBackground;
        Application.logMessageReceived -= OnLog;
        report.AppendLine(failure == null ? "ALL CHECKS PASSED" : "FAIL " + failure);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/InventoryRegression.txt", report.ToString());
        if (failure == null) Debug.Log("Inventory regression: ALL CHECKS PASSED. Logs/InventoryRegression.txt"); else Debug.LogError("Inventory regression failed: " + failure.Message);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Regression interrupted")); }
}
#endif
