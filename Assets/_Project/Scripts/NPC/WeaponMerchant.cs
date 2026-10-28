using System;
using System.Linq;
using UnityEngine;

/// <summary>Three distinct, finite offers; real-time rotation survives reopening and scene reloads.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(NpcIdleLook))]
public sealed class WeaponMerchant : Interactable
{
    [Serializable] public sealed class Listing
    {
        public MeleeWeaponDefinition weapon;
        [Min(1)] public int basePrice = 90;
    }
    public readonly struct Offer
    {
        public readonly MeleeWeaponDefinition Weapon;
        public readonly int Price;
        public readonly bool Sold;
        public readonly string Rotation;
        public Offer(MeleeWeaponDefinition weapon, int price, bool sold, string rotation)
        { Weapon = weapon; Price = price; Sold = sold; Rotation = rotation; }
    }
    [Serializable] private sealed class SavedOffer { public string id; public int price; public bool sold; }
    [Serializable] private sealed class StockSave { public int version; public long nextRefresh; public string rotation; public SavedOffer[] offers; }
    [SerializeField] private WeaponShopUI shopUI;
    [SerializeField] private Listing[] catalog;
    [SerializeField, Range(1, 3)] private int offerCount = 3;
    [SerializeField, Min(5)] private int refreshSeconds = 300;
    [SerializeField, Range(0, .5f)] private float priceVariation = .2f;
    [SerializeField, Min(.5f)] private float interactionRange = 2.5f;
    [SerializeField] private LayerMask walls = 1 << 7;
    [SerializeField] private bool persistStock = true;
    [SerializeField] private string saveKey = "TopDownShooter3D.WeaponMerchant.armory_01.v1";
    private StockSave stock;
    private float nextCheck;
    private bool purchasing;
    public event Action Changed;
    public WeaponShopUI ShopUI => shopUI;
    public int OfferCount => stock?.offers?.Length ?? 0;
    public long NextRefreshUtc => stock?.nextRefresh ?? 0;
    public int SecondsRemaining => (int)Math.Max(0, Math.Min(int.MaxValue, NextRefreshUtc - UtcNow));
    private static long UtcNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void Awake()
    {
        if (persistStock && PlayerPrefs.HasKey(saveKey)) RestoreStock(PlayerPrefs.GetString(saveKey));
        RefreshIfDue();
    }
    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .5f; RefreshIfDue();
    }
    public bool IsPlayerInRange(GameObject player)
    {
        if (player == null || !isActiveAndEnabled) return false;
        Vector3 offset = player.transform.position - transform.position;
        return Mathf.Abs(offset.y) < 1.8f && Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude <= interactionRange * interactionRange &&
            !Physics.Linecast(transform.position + Vector3.up * 1.2f, player.transform.position + Vector3.up * 1.2f, walls, QueryTriggerInteraction.Ignore);
    }
    public override bool CanInteract(GameObject player)
    {
        var interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
        return base.CanInteract(player) && IsPlayerInRange(player) && shopUI != null && !shopUI.IsOpen &&
            shopUI.PlayerObject == player && interactor != null && interactor.CanStartInteraction;
    }
    public override void Interact(GameObject player) { if (CanInteract(player)) shopUI.TryOpen(this); }
    public Offer GetOffer(int index)
    {
        if (stock == null || index < 0 || index >= OfferCount) return default;
        var saved = stock.offers[index];
        var listing = catalog.FirstOrDefault(l => l != null && l.weapon != null && l.weapon.WeaponId == saved.id);
        return new Offer(listing?.weapon, saved.price, saved.sold, stock.rotation);
    }
    public void RefreshIfDue()
    {
        if (purchasing || stock != null && UtcNow < stock.nextRefresh) return;
        var available = catalog?.Where(l => l != null && l.weapon != null && l.weapon.HeldPrefab != null && l.basePrice > 0)
            .GroupBy(l => l.weapon.WeaponId).Select(g => g.First()).ToArray();
        if (available == null || available.Length == 0) return;
        var previous = stock;
        var random = new System.Random(Guid.NewGuid().GetHashCode());
        for (int i = available.Length - 1; i > 0; i--)
        { int j = random.Next(i + 1); var swap = available[i]; available[i] = available[j]; available[j] = swap; }
        int count = Mathf.Min(Mathf.Clamp(offerCount, 1, 3), available.Length);
        // If possible, every rotation brings at least one different sword.
        if (available.Length > count && previous != null && previous.offers.Length == count &&
            available.Take(count).All(l => previous.offers.Any(o => o.id == l.weapon.WeaponId)))
        { var swap = available[count - 1]; available[count - 1] = available[count]; available[count] = swap; }
        stock = new StockSave { version = 1, nextRefresh = UtcNow + Mathf.Max(5, refreshSeconds), rotation = Guid.NewGuid().ToString("N"), offers = new SavedOffer[count] };
        for (int i = 0; i < count; i++)
        {
            var listing = available[i];
            PriceRange(listing, out int low, out int high);
            int price = random.Next(low, high + 1);
            var old = previous?.offers.FirstOrDefault(o => o.id == listing.weapon.WeaponId);
            if (high > low && old != null && old.price == price) price = price == high ? low : price + 1;
            stock.offers[i] = new SavedOffer { id = listing.weapon.WeaponId, price = price };
        }
        SaveStock(true); Changed?.Invoke();
    }
    private void PriceRange(Listing listing, out int low, out int high)
    {
        float variation = Mathf.Clamp(priceVariation, 0, .5f);
        int basis = Mathf.Clamp(listing.basePrice, 1, PlayerGoldWallet.MaximumGold);
        low = Mathf.Clamp(Mathf.RoundToInt(basis * (1 - variation)), 1, PlayerGoldWallet.MaximumGold);
        high = Mathf.Clamp(Mathf.RoundToInt(basis * (1 + variation)), low, PlayerGoldWallet.MaximumGold);
    }
    public bool TryBuy(int index, Offer expected, int equipmentSlot, GameObject player, out string result)
    {
        result = "ซื้อไม่ได้ กรุณาเลือกรายการใหม่";
        if (purchasing || shopUI == null || !shopUI.IsOpen || shopUI.Merchant != this || shopUI.PlayerObject != player || !IsPlayerInRange(player)) return false;
        RefreshIfDue();
        var current = GetOffer(index);
        if (current.Weapon == null || current.Sold || current.Rotation != expected.Rotation || current.Weapon != expected.Weapon || current.Price != expected.Price)
        { result = "สินค้าหรือราคาเปลี่ยนแล้ว กรุณาเลือกดาบใหม่"; return false; }
        var wallet = player.GetComponent<PlayerGoldWallet>(); var equipment = player.GetComponent<PlayerMeleeEquipment>(); var health = player.GetComponent<PlayerHealth>();
        if (wallet == null || equipment == null || health == null || !health.IsAlive) return false;
        if (wallet.Gold < current.Price) { result = "Gold ไม่พอสำหรับดาบเล่มนี้"; return false; }
        if (equipmentSlot < 0 || equipmentSlot >= PlayerMeleeEquipment.Capacity) { result = "เลือกช่องอาวุธที่จะใส่ก่อนซื้อ"; return false; }
        purchasing = true; bool success = false;
        try
        {
            stock.offers[index].sold = true;
            // Stage stock before the wallet's save flush, alongside Gold and the acquired equipment.
            SaveStock(false);
            success = equipment.TryPurchase(current.Weapon, equipmentSlot, current.Price, wallet);
            result = success ? $"ซื้อ {current.Weapon.DisplayName} สำเร็จ ใช้ {current.Price:N0} Gold" : "ยังเปลี่ยนอาวุธไม่ได้ในขณะนี้";
        }
        finally
        {
            if (!success) { stock.offers[index].sold = false; SaveStock(true); }
            purchasing = false;
        }
        Changed?.Invoke(); return success;
    }
    public string CaptureStock() => stock != null ? JsonUtility.ToJson(stock) : "";
    public bool RestoreStock(string json)
    {
        if (purchasing) return false;
        StockSave data;
        try { data = JsonUtility.FromJson<StockSave>(json); } catch (ArgumentException) { return false; }
        if (data == null || data.version != 1 || string.IsNullOrEmpty(data.rotation) || data.nextRefresh <= 0 || data.offers == null ||
            data.offers.Length != Mathf.Min(Mathf.Clamp(offerCount, 1, 3), catalog?.Length ?? 0) || data.offers.Length == 0) return false;
        if (data.offers.Any(o => o == null || string.IsNullOrEmpty(o.id)) || data.offers.Select(o => o.id).Distinct().Count() != data.offers.Length) return false;
        foreach (var offer in data.offers)
        {
            var listing = catalog.FirstOrDefault(l => l != null && l.weapon != null && l.weapon.WeaponId == offer.id);
            if (listing == null) return false;
            PriceRange(listing, out int low, out int high);
            if (offer.price < low || offer.price > high) return false;
        }
        stock = data; Changed?.Invoke(); return true;
    }
    private void SaveStock(bool flush)
    {
        if (!persistStock || stock == null) return;
        PlayerPrefs.SetString(saveKey, CaptureStock()); if (flush) PlayerPrefs.Save();
    }
}
