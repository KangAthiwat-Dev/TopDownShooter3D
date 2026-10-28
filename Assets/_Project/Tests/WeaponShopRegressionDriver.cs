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

public sealed class WeaponShopRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<string> errors = new List<string>();
    private readonly List<GameObject> temporary = new List<GameObject>();
    private readonly List<string> temporaryKeys = new List<string>();
    private PlayerMovement player; private PlayerMeleeEquipment equipment; private PlayerGoldWallet wallet; private WeaponMerchant merchant; private WeaponShopUI ui;
    private GamePauseController pause; private PlayerInput input; private Keyboard keyboard; private Mouse mouse;
    private ReadOnlyArray<InputDevice>? devicesBefore;
    private string walletBefore, stockBefore; private bool persistGoldBefore, persistStockBefore, switchBefore, backgroundBefore, done, passed = true;
    private int fpsBefore, vsyncBefore; private float timeBefore;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private IEnumerator Start()
    {
        backgroundBefore = Application.runInBackground; fpsBefore = Application.targetFrameRate; vsyncBefore = QualitySettings.vSyncCount; timeBefore = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.timeScale = 1; Application.logMessageReceived += OnLog;
        report.AppendLine("Weapon merchant E / mouse / purchase / rotation / save regression " + DateTime.Now.ToString("s"));
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
        yield return null; // All wallet Start/loadout restoration runs before temporary state is installed.
        player = FindAnyObjectByType<PlayerMovement>(); equipment = player.GetComponent<PlayerMeleeEquipment>(); wallet = player.GetComponent<PlayerGoldWallet>();
        merchant = FindAnyObjectByType<WeaponMerchant>(); ui = FindAnyObjectByType<WeaponShopUI>(); pause = FindAnyObjectByType<GamePauseController>(); input = player.GetComponent<PlayerInput>();
        walletBefore = wallet.CaptureSave(); stockBefore = merchant.CaptureStock();
        persistGoldBefore = Get<bool>(wallet, "persistGold"); persistStockBefore = Get<bool>(merchant, "persistStock"); Set(wallet, "persistGold", false); Set(merchant, "persistStock", false);
        Set(Get<object>(merchant, "stock"), "nextRefresh", 0L); merchant.RefreshIfDue();
        wallet.RestoreSave("{\"version\":1,\"gold\":0,\"weapons\":[\"\",\"\"],\"selected\":-1}");
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false); foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        if (input.actions.devices.HasValue) devicesBefore = new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        switchBefore = input.neverAutoSwitchControlSchemes; input.neverAutoSwitchControlSchemes = true;
        keyboard = InputSystem.AddDevice<Keyboard>("WeaponShopTestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("WeaponShopTestMouse"); input.actions.devices = new InputDevice[] { keyboard, mouse }; Keys();
        Check(merchant.gameObject.name == "NPC_WeaponMerchant" && FindObjectsByType<WeaponMerchant>(FindObjectsSortMode.None).Length == 1, "Only the existing named weapon merchant receives the shop");
        var listings = Get<WeaponMerchant.Listing[]>(merchant, "catalog");
        Check(listings.Length == 5 && listings.All(l => l.weapon.HeldPrefab != null && l.weapon.HudIcon != null) && listings.Select(l => l.weapon.WeaponId).Distinct().Count() == 5, "Five sword definitions have stable IDs, models and icons");
        Check(merchant.OfferCount == 3 && Enumerable.Range(0, 3).Select(i => merchant.GetOffer(i).Weapon).Distinct().Count() == 3, "Each restock offers three distinct swords");
        Check(Enumerable.Range(0, 3).All(i => { var o = merchant.GetOffer(i); var b = listings.Single(l => l.weapon == o.Weapon).basePrice; return o.Price >= Mathf.RoundToInt(b * .8f) && o.Price <= Mathf.RoundToInt(b * 1.2f); }), "Random prices stay inside the configured twenty-percent range");
        Warp(merchant.transform.position + Vector3.forward * 10); yield return Wait(.1f);
        Check(!merchant.CanInteract(player.gameObject) && !ui.TryOpen(merchant), "Distant players cannot open or buy from the merchant");
        // This anchor was built with the armory, before the merchant was introduced.
        var approach = merchant.transform.parent.parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Future_Weapon_Shop_Interaction_Anchor");
        Vector3 near = approach != null && Vector3.Distance(approach.position, merchant.transform.position) < 2 ? approach.position : merchant.transform.position + merchant.transform.forward * 1.5f;
        Warp(near); yield return Wait(.35f);
        Check(player.GetComponent<PlayerInteractor>().CurrentInteractable == merchant, "The real armory approach detects the E interaction prompt");
        Keys(Key.E); yield return Wait(.1f); Keys(); yield return Wait(.05f);
        Check(ui.IsOpen && Time.timeScale == 0 && Cursor.visible, "Pressing real E opens the mouse shop and pauses gameplay");
        Check(!FindAnyObjectByType<InventoryPanelUI>().TryOpen() && !FindAnyObjectByType<PlayerUpgradePanelUI>().TryOpen(), "Inventory and upgrades cannot overlap the shop");
        var position = player.transform.position; Keys(Key.W, Key.J, Key.Space, Key.M, Key.E); yield return Wait(.15f); Keys();
        Check(player.transform.position == position && !player.IsDashing && !player.IsShadowDashing, "Movement, firing, dashes and repeated E stay blocked while shopping");
        var rows = Get<WeaponShopUI.Row[]>(ui, "rows");
        yield return Click(rows[0].button); var first = merchant.GetOffer(0);
        Check(ui.SelectedIndex == 0 && rows[0].icon.sprite == first.Weapon.HudIcon && Get<TMP_Text>(ui, "priceLabel").text.Contains(first.Price.ToString()), "Actual mouse selection previews the chosen sword and its current price");
        int gold = wallet.Gold; ui.BuySelected();
        Check(wallet.Gold == gold && equipment.WeaponCount == 0 && !merchant.GetOffer(0).Sold && !Get<Button>(ui, "buyButton").interactable, "Insufficient Gold changes neither equipment, stock nor currency");
        wallet.TryCredit(5000);
        bool observedAtomic = false; Action observer = () => observedAtomic = equipment.GetWeapon(0) == first.Weapon && wallet.Gold == 5000 - first.Price && merchant.GetOffer(0).Sold;
        wallet.Changed += observer;
        yield return Click(Get<Button>(ui, "buyButton")); wallet.Changed -= observer;
        Check(observedAtomic && equipment.GetWeapon(0) == first.Weapon && equipment.WeaponCount == 1, "Buying deducts the exact Gold, reserves stock and equips the sword before observers run");
        Check(ui.EquipmentSlot == 1, "After the first purchase the next empty slot is selected automatically");
        gold = wallet.Gold; ui.BuySelected(); ui.BuySelected();
        Check(wallet.Gold == gold && equipment.WeaponCount == 1, "Rapid duplicate clicks cannot purchase sold stock twice");
        Check(FindAnyObjectByType<PlayerGoldUI>().DisplayedGold.Contains(gold.ToString("N0")), "Gold HUD updates after a sword purchase");
        var visuals = Get<GameObject[]>(equipment, "weaponVisuals"); var back = Get<Transform>(equipment, "weaponBack"); var hand = Get<Transform>(equipment, "weaponHand");
        equipment.SetWeaponVisible(true); equipment.SetWeaponDrawn(false);
        Check(visuals[0] != null && visuals[0].transform.parent == back && Quaternion.Angle(visuals[0].transform.localRotation, first.Weapon.BackRotation) < .1f, "Purchased sword is stowed diagonally on the player's back");
        equipment.SetWeaponDrawn(true); Check(visuals[0].transform.parent == hand, "Purchased sword uses the existing automatic melee hand socket"); equipment.SetWeaponDrawn(false);
        yield return Click(rows[1].button); yield return Click(Get<Button[]>(ui, "slotButtons")[1]); var second = merchant.GetOffer(1);
        yield return Click(Get<Button>(ui, "buyButton"));
        Check(equipment.WeaponCount == 2 && equipment.GetWeapon(1) == second.Weapon && wallet.Gold == gold - second.Price, "The second sword uses the second melee slot without touching supply slots");
        yield return Screenshot("WeaponShop_Purchase");
        ui.Close(); yield return Wait(.12f); string beforeReopen = merchant.CaptureStock(); Keys(Key.E); yield return Wait(.1f); Keys(); yield return Wait(.05f);
        Check(ui.IsOpen && merchant.CaptureStock() == beforeReopen && ui.EquipmentSlot == -1, "Reopening keeps the same sold stock and requires a slot choice when both slots are full");
        yield return Click(rows[2].button); Check(!Get<Button>(ui, "buyButton").interactable, "A full loadout cannot be silently overwritten");
        yield return Click(Get<Button[]>(ui, "slotButtons")[0]);
        Check(Get<TMP_Text>(ui, "slotHint").text.Contains(first.Weapon.DisplayName) && Get<TMP_Text>(ui, "buyLabel").text.Contains("แทนที่"), "Replacement names the old sword and explicitly labels the purchase action");
        var third = merchant.GetOffer(2); gold = wallet.Gold; yield return Click(Get<Button>(ui, "buyButton"));
        Check(equipment.WeaponCount == 2 && equipment.GetWeapon(0) == third.Weapon && equipment.GetWeapon(1) == second.Weapon && wallet.Gold == gold - third.Price, "Explicit replacement swaps only the selected slot and charges once");
        string boughtSave = wallet.CaptureSave(); wallet.RestoreSave("{\"version\":1,\"gold\":0,\"weapons\":[\"\",\"\"],\"selected\":-1}"); wallet.RestoreSave(boughtSave);
        Check(equipment.GetWeapon(0) == third.Weapon && equipment.GetWeapon(1) == second.Weapon && wallet.Gold == gold - third.Price, "Gold and both purchased melee slots roundtrip in one save record");
        string stockSaved = merchant.CaptureStock();
        Check(!merchant.RestoreStock("{}") && !merchant.RestoreStock("{\"version\":2}") && merchant.CaptureStock() == stockSaved, "Malformed or future stock saves cannot overwrite valid offers");
        string rotation = merchant.GetOffer(0).Rotation; var priorWeapons = Enumerable.Range(0, 3).Select(i => merchant.GetOffer(i).Weapon).ToArray(); var oldOffer = merchant.GetOffer(2);
        Set(Get<object>(merchant, "stock"), "nextRefresh", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1);
        yield return Wait(1.6f);
        Check(Time.timeScale == 0 && merchant.GetOffer(0).Rotation != rotation && Enumerable.Range(0, 3).Any(i => !priorWeapons.Contains(merchant.GetOffer(i).Weapon)), "Stock automatically rotates on real time even while the shop pauses the world");
        Check(ui.SelectedIndex == -1 && !Get<Button>(ui, "buyButton").interactable && ui.Status.Contains("รอบใหม่"), "A rotation clears the previous selection and asks the player to check the new price");
        gold = wallet.Gold;
        Check(!merchant.TryBuy(2, oldOffer, 0, player.gameObject, out _) && wallet.Gold == gold, "A stale offer token cannot charge a changed price or buy the new stock");
        yield return Click(rows[0].button); yield return Click(Get<Button[]>(ui, "slotButtons")[0]); yield return Screenshot("WeaponShop_Restock");
        foreach (var text in Get<GameObject>(ui, "window").GetComponentsInChildren<TMP_Text>(true))
        { text.ForceMeshUpdate(); Check(text.text.All(c => char.IsWhiteSpace(c) || text.font.HasCharacter(c, true, true)), "Font resolves shop text in " + text.name); }
        yield return VerifyNewInstances(listings);
        Keys(Key.Escape); yield return Wait(.08f); Keys();
        Check(!ui.IsOpen && Time.timeScale == 1 && !pause.IsPaused, "Escape closes the shop and restores gameplay");
        var inventoryUI = FindAnyObjectByType<InventoryPanelUI>();
        Check(inventoryUI.TryOpen(), "Inventory opens normally after shopping"); inventoryUI.SelectSlot(PlayerInventory.Capacity); yield return Wait(.1f);
        Check(inventoryUI.DetailTitle == equipment.GetWeapon(0).DisplayName, "Purchased sword and its Thai name appear in the inventory melee slot");
        foreach (var text in inventoryUI.GetComponentsInChildren<TMP_Text>(true))
            text.ForceMeshUpdate();
        yield return Screenshot("WeaponShop_Inventory"); inventoryUI.Close();
        yield return Wait(.12f); Keys(Key.E); yield return Wait(.1f); Keys(); yield return Wait(.04f);
        pause.SetPaused(GamePauseReason.Inventory, true); ui.Close(); Check(pause.Reasons == GamePauseReason.Inventory && Time.timeScale == 0, "Closing the shop preserves another system's pause reason"); pause.SetPaused(GamePauseReason.Inventory, false);
        yield return Wait(.12f); Keys(Key.E); yield return Wait(.1f); Keys(); yield return Wait(.04f); ui.enabled = false;
        Check(!ui.IsOpen && !pause.IsPaused, "Disabling the shop releases its dialogue pause"); ui.enabled = true;
        yield return Wait(.12f); Keys(Key.E); yield return Wait(.1f); Keys(); yield return Wait(.04f);
        player.GetComponent<PlayerHealth>().TakeDamage(99999); yield return Wait(.1f);
        Check(!ui.IsOpen && pause.IsGameOver && Time.timeScale == 0, "Player death closes the shop and retains Game Over pause");
        Check(errors.Count == 0, "No runtime errors or missing glyph warnings during shop, rotation, replacement and save");
    }
    private IEnumerator VerifyNewInstances(WeaponMerchant.Listing[] listings)
    {
        string stockKey = "WeaponShop.Test.Stock." + Guid.NewGuid().ToString("N"); temporaryKeys.Add(stockKey); PlayerPrefs.SetString(stockKey, merchant.CaptureStock()); PlayerPrefs.Save();
        var stockReader = new GameObject("Temporary stock reload"); temporary.Add(stockReader); stockReader.SetActive(false);
        var reader = stockReader.AddComponent<WeaponMerchant>(); Set(reader, "catalog", listings); Set(reader, "saveKey", stockKey); stockReader.SetActive(true); yield return null;
        Check(reader.CaptureStock() == merchant.CaptureStock(), "A new merchant instance reloads the same prices, timer and sold stock");
        string key = "WeaponShop.Test.Wallet." + Guid.NewGuid().ToString("N"); temporaryKeys.Add(key);
        var writer = MakeSavePlayer(key, listings.Select(l => l.weapon).ToArray()); yield return null;
        var writerWallet = writer.GetComponent<PlayerGoldWallet>(); var writerGear = writer.GetComponent<PlayerMeleeEquipment>(); writerWallet.TryCredit(500);
        Check(writerGear.TryPurchase(listings[0].weapon, 1, 100, writerWallet), "Disposable save profile can commit a real equipment purchase");
        Destroy(writer); yield return null;
        var playerReader = MakeSavePlayer(key, listings.Select(l => l.weapon).ToArray()); yield return null;
        Check(playerReader.GetComponent<PlayerGoldWallet>().Gold == 400 && playerReader.GetComponent<PlayerMeleeEquipment>().GetWeapon(1) == listings[0].weapon && playerReader.GetComponent<PlayerMeleeEquipment>().SelectedSlotIndex == 1, "A new player instance reloads Gold, sword model and selected melee slot");
    }
    private GameObject MakeSavePlayer(string key, MeleeWeaponDefinition[] catalog)
    {
        var go = new GameObject("Temporary equipment save player"); temporary.Add(go); go.SetActive(false);
        var gear = go.AddComponent<PlayerMeleeEquipment>(); var hand = new GameObject("Hand").transform; hand.SetParent(go.transform); var back = new GameObject("Back").transform; back.SetParent(go.transform);
        Set(gear, "weaponHand", hand); Set(gear, "weaponBack", back); var w = go.AddComponent<PlayerGoldWallet>(); Set(w, "saveKey", key); Set(w, "weaponCatalog", catalog); go.SetActive(true); return go;
    }
    private IEnumerator Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); var rect = button.GetComponent<RectTransform>(); Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return Wait(.05f);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return Wait(.05f);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return Wait(.07f);
    }
    private void Warp(Vector3 position) { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = position + Vector3.up * .03f; cc.enabled = true; Physics.SyncTransforms(); }
    private void Keys(params Key[] keys) { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private IEnumerator Screenshot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/" + name + ".png", 2); yield return Wait(.1f); }
    private void Check(bool valid, string label) { passed &= valid; report.AppendLine((valid ? "PASS " : "FAIL ") + label); File.WriteAllText("Logs/WeaponShopPlaytest.txt", report.ToString()); if (!valid) throw new InvalidOperationException(label); }
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning && message.Contains("Unicode")) errors.Add(message + "\n" + trace); }
    private void Finish(Exception error)
    {
        if (done) return; done = true; if (error != null) { passed = false; report.AppendLine("EXCEPTION " + error); } foreach (var e in errors) { passed = false; report.AppendLine("RUNTIME ERROR " + e); }
        Keys(); if (ui != null) ui.Close(); if (wallet != null && walletBefore != null) { wallet.RestoreSave(walletBefore); Set(wallet, "persistGold", persistGoldBefore); }
        if (merchant != null && stockBefore != null) { merchant.RestoreStock(stockBefore); Set(merchant, "persistStock", persistStockBefore); }
        if (input != null) { input.actions.devices = devicesBefore; input.neverAutoSwitchControlSchemes = switchBefore; }
        if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
        foreach (var obj in temporary) if (obj != null) Destroy(obj); foreach (var key in temporaryKeys) PlayerPrefs.DeleteKey(key); if (temporaryKeys.Count > 0) PlayerPrefs.Save();
        Application.logMessageReceived -= OnLog; Application.runInBackground = backgroundBefore; Application.targetFrameRate = fpsBefore; QualitySettings.vSyncCount = vsyncBefore; Time.timeScale = timeBefore;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/WeaponShopPlaytest.txt", report.ToString()); Debug.Log("Weapon shop regression complete. Passed=" + passed);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Weapon shop test interrupted.")); }
}
#endif
