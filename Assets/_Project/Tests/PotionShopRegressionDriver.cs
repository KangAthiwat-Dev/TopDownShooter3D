#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public sealed class PotionShopRegressionDriver : MonoBehaviour
{
    private const string PotionFolder = "Assets/_Project/Items/Potions";
    private const string SourceFolder = "Assets/Assets_Imported/Magic_Props";
    private readonly StringBuilder report = new StringBuilder(); private readonly List<string> errors = new List<string>();
    private readonly List<GameObject> temporary = new List<GameObject>(); private readonly List<string> keysToDelete = new List<string>();
    private PlayerMovement player; private PlayerInventory inventory; private PlayerGoldWallet wallet; private PlayerHealth health; private PlayerEnergy energy; private PlayerPotionUse use;
    private PotionMerchant merchant; private PotionShopUI ui; private InventoryPanelUI bag; private PlayerInput input; private GamePauseController pause;
    private Keyboard keyboard; private Mouse mouse; private ReadOnlyArray<InputDevice>? devicesBefore;
    private string walletBefore; private bool persistBefore, switchBefore, backgroundBefore, passed = true, done; private int fpsBefore, vsyncBefore; private float timeBefore;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private IEnumerator Start()
    {
        backgroundBefore = Application.runInBackground; fpsBefore = Application.targetFrameRate; vsyncBefore = QualitySettings.vSyncCount; timeBefore = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.timeScale = 1; Application.logMessageReceived += OnLog;
        report.AppendLine("Potion merchant / mouse / inventory use / save regression " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception error = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; } catch (Exception e) { error = e; }
            if (error != null) { Finish(error); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;
        player = FindAnyObjectByType<PlayerMovement>(); inventory = player.GetComponent<PlayerInventory>(); wallet = player.GetComponent<PlayerGoldWallet>(); health = player.GetComponent<PlayerHealth>(); energy = player.GetComponent<PlayerEnergy>(); use = player.GetComponent<PlayerPotionUse>();
        merchant = FindAnyObjectByType<PotionMerchant>(); ui = FindAnyObjectByType<PotionShopUI>(); bag = FindAnyObjectByType<InventoryPanelUI>(); input = player.GetComponent<PlayerInput>(); pause = FindAnyObjectByType<GamePauseController>();
        walletBefore = wallet.CaptureSave(); persistBefore = Get<bool>(wallet, "persistGold"); Set(wallet, "persistGold", false);
        inventory.RestoreContents(new ItemDefinition[8], new int[8]); wallet.RestoreSave("{\"version\":1,\"gold\":0}");
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false); foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        if (input.actions.devices.HasValue) devicesBefore = new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray()); switchBefore = input.neverAutoSwitchControlSchemes; input.neverAutoSwitchControlSchemes = true;
        keyboard = InputSystem.AddDevice<Keyboard>("PotionTestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("PotionTestMouse"); input.actions.devices = new InputDevice[] { keyboard, mouse }; Keys();
        Check(merchant.Contact.Role == CityServiceRole.PotionStore && FindObjectsByType<PotionMerchant>(FindObjectsSortMode.None).Length == 1, "Only the existing female Potion Store contact receives the shop");
        Check(Directory.GetFiles(SourceFolder + "/FBX", "*.fbx").Length == 3, "Only the three potion FBX models remain in Magic_Props");
        var red = merchant.GetPotion(0); var blue = merchant.GetPotion(1); var green = merchant.GetPotion(2);
        Check(red.Effect == PotionEffect.Heal && blue.Effect == PotionEffect.Energy && green.Effect == PotionEffect.Guard && new[] { red, blue, green }.All(p => p.Icon != null && p.Category == InventoryItemCategory.Consumable && p.MaxStack == 10 && !p.CanSell), "Three priced, stackable, unsellable potions have real model icons and distinct effects");
        foreach (string color in new[] { "Red", "Blue", "Green" })
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PotionFolder + "/Models/Potion_" + color + ".prefab"); var renderers = model.GetComponentsInChildren<Renderer>();
            Check(renderers.Length > 0 && model.GetComponentsInChildren<Collider>(true).Length == 0 && model.GetComponentsInChildren<Transform>(true).All(t => !t.name.StartsWith("UCX_")) && renderers.SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader.isSupported && !ShaderUtil.ShaderHasError(m.shader)), color + " bottle has valid URP materials and no visible collision proxy");
        }
        Warp(merchant.transform.position + Vector3.forward * 10); yield return Wait(.12f); Check(!ui.TryOpen(merchant), "Distant players cannot open the potion shop");
        var point = merchant.Contact.InteractionPoint; Vector3 approach = point != null && Vector3.Distance(point.position, merchant.transform.position) < 2 ? point.position : merchant.transform.position + merchant.transform.forward * 1.4f;
        Warp(approach); yield return Wait(.3f);
        Check(player.GetComponent<PlayerInteractor>().CurrentInteractable == merchant, "Approaching the current NPC position discovers the E prompt");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(wall); wall.layer = 7; wall.transform.position = Vector3.Lerp(merchant.transform.position, player.transform.position, .5f) + Vector3.up * 1.2f; wall.transform.localScale = new Vector3(.2f, 2, .2f); Physics.SyncTransforms();
        Check(!merchant.IsPlayerInRange(player.gameObject), "A wall between player and merchant blocks interaction"); Destroy(wall); yield return Wait(.08f);
        Keys(Key.E); yield return Wait(.1f); Keys(); yield return Wait(.05f);
        Check(ui.IsOpen && Time.timeScale == 0 && Cursor.visible, "Real E opens a paused, mouse-controlled potion shop");
        Check(!bag.TryOpen() && !FindAnyObjectByType<PlayerUpgradePanelUI>().TryOpen(), "Inventory and upgrades cannot overlap a shop conversation");
        var rows = Get<PotionShopUI.Row[]>(ui, "rows"); yield return Click(rows[0].button); ui.BuySelected();
        Check(wallet.Gold == 0 && inventory.OccupiedSlots == 0 && !Get<Button>(ui, "buyButton").interactable, "Insufficient Gold consumes neither money nor bag space");
        wallet.TryCredit(1000); yield return Click(Get<Button>(ui, "moreButton")); yield return Click(Get<Button>(ui, "moreButton"));
        Check(ui.Quantity == 3 && Get<TMP_Text>(ui, "totalLabel").text.Contains("75"), "Mouse quantity controls show the exact three-bottle price");
        bool atomic = false; Action observer = () => { atomic = Count(red) == 3 && wallet.Gold == 925; ui.BuySelected(); }; wallet.Changed += observer;
        yield return Click(Get<Button>(ui, "buyButton")); wallet.Changed -= observer;
        Check(atomic && Count(red) == 3 && wallet.Gold == 925, "Purchase and reentrant callbacks update bottles and Gold together exactly once");
        yield return Click(rows[1].button); ui.SetQuantity(2); yield return Click(Get<Button>(ui, "buyButton"));
        yield return Click(rows[2].button); ui.SetQuantity(2); yield return Click(Get<Button>(ui, "buyButton"));
        Check(Count(blue) == 2 && Count(green) == 2 && wallet.Gold == 805 && inventory.OccupiedSlots == 3, "All three potion types buy into supply stacks at their configured prices");
        Check(!merchant.TryBuy(0, blue, 25, 1, player.gameObject, out _) && !merchant.TryBuy(0, red, 1, 1, player.gameObject, out _) && !merchant.TryBuy(0, red, 25, -1, player.gameObject, out _) && wallet.Gold == 805, "Stale items, changed prices and invalid quantities cannot charge the player");
        yield return Screenshot("PotionShop_Store");
        foreach (var t in ui.GetComponentsInChildren<TMP_Text>(true)) { t.ForceMeshUpdate(); Check(t.text.All(c => char.IsWhiteSpace(c) || t.font.HasCharacter(c, true, true)), "Shop glyphs resolve in " + t.name); }
        ui.Close(); yield return Wait(.12f); Check(bag.TryOpen(), "Purchased potions can be selected in the existing inventory");
        var useButton = Get<Button>(bag, "selectWeaponButton"); bag.SelectSlot(Slot(red));
        Check(!useButton.interactable && !use.TryUse(Slot(red), red) && Count(red) == 3, "Full health disables healing without consuming a bottle");
        float max = health.MaxHealth; health.TakeDamage(max * .8f / (1 - health.DamageReduction)); float before = health.CurrentHealth;
        bag.SelectSlot(Slot(red)); yield return Click(useButton);
        Check(Mathf.Abs(health.CurrentHealth - Mathf.Min(max, before + max * .5f)) < .01f && Count(red) == 2, "Using the inventory action consumes one red bottle and heals fifty percent of maximum HP");
        bag.SelectSlot(Slot(red)); yield return Click(useButton); Check(Mathf.Approximately(health.CurrentHealth, max) && Count(red) == 1 && !useButton.interactable, "Healing caps at maximum HP and further full-health use is blocked");
        energy.ConsumeShadowDashEnergy(100); bag.SelectSlot(Slot(blue)); yield return Click(useButton);
        Check(Mathf.Abs(energy.CurrentEnergy - energy.MaxEnergy * .6f) < .01f && Count(blue) == 1, "Blue potion restores sixty percent of shared sprint and transformation energy");
        yield return Click(useButton); Check(Mathf.Approximately(energy.CurrentEnergy, energy.MaxEnergy) && Count(blue) == 0, "The second energy bottle caps energy and clears its empty stack");
        float baseReduction = health.DamageReduction; bag.SelectSlot(Slot(green)); yield return Click(useButton);
        Check(health.PotionGuardRemaining > 29 && Count(green) == 1 && Mathf.Abs(health.DamageReduction - (1 - (1 - baseReduction) * .8f)) < .001f, "Green potion grants a separate twenty-percent guard for thirty gameplay seconds");
        Check(!use.TryUse(Slot(green), green) && Count(green) == 1, "Active guard cannot be stacked or refreshed by wasting another bottle");
        float guardBefore = health.PotionGuardRemaining; yield return Wait(.2f); Check(Mathf.Approximately(guardBefore, health.PotionGuardRemaining), "Potion buff time pauses while the inventory is open");
        before = health.CurrentHealth; health.TakeDamage(10); Check(Mathf.Abs(before - health.CurrentHealth - 10 * (1 - baseReduction) * .8f) < .01f, "Guard reduces real damage multiplicatively without replacing permanent upgrades");
        yield return Screenshot("PotionShop_Use"); bag.Close(); yield return Wait(.2f); Check(health.PotionGuardRemaining < guardBefore, "Guard countdown resumes during gameplay");
        Set(health, "potionGuardUntil", Time.time + .1f); yield return Wait(.3f); Check(health.PotionGuardRemaining == 0 && Mathf.Abs(health.DamageReduction - baseReduction) < .001f, "Expired guard restores the original permanent damage reduction");
        string saved = wallet.CaptureSave(); int savedGold = wallet.Gold, savedRed = Count(red), savedGreen = Count(green);
        inventory.RestoreContents(new ItemDefinition[8], new int[8]); wallet.RestoreSave("{\"version\":1,\"gold\":0}"); Check(wallet.RestoreSave(saved) && Count(red) == savedRed && Count(green) == savedGreen && wallet.Gold == savedGold, "Gold and the remaining potion quantities roundtrip in one save record");
        Check(!wallet.RestoreSave("{\"version\":1,\"gold\":999,\"items\":[\"potion_red\"],\"counts\":[-1]}") && wallet.Gold == savedGold, "Invalid saved item counts cannot overwrite the current wallet");
        yield return VerifyNewPlayer(saved, savedGold, red, savedRed);
        var resources = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Items/MonsterLoot/Items" }).Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        foreach (var item in resources) { if (inventory.OccupiedSlots == 8) break; inventory.TryAdd(item, item.MaxStack); }
        inventory.TryAdd(red, 10 - Count(red));
        Warp(approach); yield return Wait(.15f); Check(ui.TryOpen(merchant), "A full bag can still open the potion shop"); ui.SelectPotion(0); int gold = wallet.Gold; ui.BuySelected();
        Check(inventory.OccupiedSlots == 8 && Count(red) == 10 && wallet.Gold == gold && !Get<Button>(ui, "buyButton").interactable, "A full bag rejects purchases atomically instead of taking Gold");
        ui.SelectPotion(2); yield return Click(Get<Button>(ui, "buyButton")); Check(Count(green) == savedGreen + 1 && wallet.Gold == gold - 40, "An existing potion stack can still grow when all eight slots are occupied");
        Check(!use.TryUse(Slot(green), green), "Potions cannot be consumed behind an open shop dialog");
        yield return Screenshot("PotionShop_FullBag"); Keys(Key.Escape); yield return Wait(.1f); Keys(); Check(!ui.IsOpen && !pause.IsPaused && Time.timeScale == 1, "Escape restores normal gameplay from the potion shop");
        yield return Wait(.12f); ui.TryOpen(merchant); pause.SetPaused(GamePauseReason.Inventory, true); ui.Close(); Check(pause.Reasons == GamePauseReason.Inventory, "Closing the shop keeps another system's independent pause"); pause.SetPaused(GamePauseReason.Inventory, false);
        yield return Wait(.12f); ui.TryOpen(merchant); ui.enabled = false; Check(!pause.IsPaused && !ui.IsOpen, "Disabling the potion shop releases its dialogue pause"); ui.enabled = true;
        yield return Wait(.12f); ui.TryOpen(merchant); health.TakeDamage(99999); yield return Wait(.1f);
        Check(!ui.IsOpen && pause.IsGameOver && !use.TryUse(Slot(red), red), "Death closes shopping and potions cannot resurrect a dead player");
        Check(errors.Count == 0, "No runtime errors, missing scripts or missing glyph warnings during potion buying and use");
    }
    private IEnumerator VerifyNewPlayer(string saved, int gold, ItemDefinition red, int count)
    {
        string key = "PotionShop.Test." + Guid.NewGuid().ToString("N"); keysToDelete.Add(key); PlayerPrefs.SetString(key, saved); PlayerPrefs.Save();
        var go = new GameObject("Temporary potion save reader"); temporary.Add(go); go.SetActive(false); var inv = go.AddComponent<PlayerInventory>(); var w = go.AddComponent<PlayerGoldWallet>();
        Set(w, "saveKey", key); Set(w, "itemCatalog", Get<ItemDefinition[]>(wallet, "itemCatalog")); go.SetActive(true); yield return null;
        int readCount = Enumerable.Range(0, 8).Where(i => inv.GetItem(i) == red).Sum(inv.GetQuantity);
        Check(w.Gold == gold && readCount == count && inv.OccupiedSlots > 0, "A new player instance reloads the saved Gold and potion stacks");
    }
    private int Slot(ItemDefinition item) { for (int i = 0; i < 8; i++) if (inventory.GetItem(i) == item) return i; return -1; }
    private int Count(ItemDefinition item) => Enumerable.Range(0, 8).Where(i => inventory.GetItem(i) == item).Sum(inventory.GetQuantity);
    private IEnumerator Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); var rect = button.GetComponent<RectTransform>(); Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return Wait(.05f); InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return Wait(.05f);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return Wait(.07f);
    }
    private void Warp(Vector3 point) { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = point + Vector3.up * .03f; cc.enabled = true; Physics.SyncTransforms(); }
    private void Keys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private IEnumerator Screenshot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/" + name + ".png", 2); yield return Wait(.1f); }
    private void Check(bool valid, string label) { passed &= valid; report.AppendLine((valid ? "PASS " : "FAIL ") + label); File.WriteAllText("Logs/PotionShopPlaytest.txt", report.ToString()); if (!valid) throw new InvalidOperationException(label); }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning && message.Contains("Unicode")) errors.Add(message + "\n" + trace); }
    private void Finish(Exception error)
    {
        if (done) return; done = true; if (error != null) { passed = false; report.AppendLine("EXCEPTION " + error); } foreach (var e in errors) { passed = false; report.AppendLine("RUNTIME ERROR " + e); }
        Keys(); if (ui != null) ui.Close(); if (bag != null) bag.Close(); if (wallet != null && walletBefore != null) { wallet.RestoreSave(walletBefore); Set(wallet, "persistGold", persistBefore); }
        if (input != null) { input.actions.devices = devicesBefore; input.neverAutoSwitchControlSchemes = switchBefore; } if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
        foreach (var go in temporary) if (go != null) Destroy(go); foreach (var key in keysToDelete) PlayerPrefs.DeleteKey(key); if (keysToDelete.Count > 0) PlayerPrefs.Save();
        Application.logMessageReceived -= OnLog; Application.runInBackground = backgroundBefore; Application.targetFrameRate = fpsBefore; QualitySettings.vSyncCount = vsyncBefore; Time.timeScale = timeBefore;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/PotionShopPlaytest.txt", report.ToString()); Debug.Log("Potion shop regression complete. Passed=" + passed); EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Potion shop test interrupted.")); }
}
#endif
