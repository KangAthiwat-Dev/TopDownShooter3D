using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CityServiceNpc))]
public sealed class PotionMerchant : Interactable
{
    [Serializable] public sealed class Listing { public ItemDefinition potion; [Min(1)] public int price = 25; }
    [SerializeField] private CityServiceNpc contact;
    [SerializeField] private PotionShopUI shopUI;
    [SerializeField] private Listing[] catalog;
    [SerializeField, Min(.5f)] private float interactionRange = 2.5f;
    [SerializeField] private LayerMask walls = 1 << 7;
    public CityServiceNpc Contact => contact;
    public PotionShopUI ShopUI => shopUI;
    public int Count => catalog?.Length ?? 0;
    public ItemDefinition GetPotion(int index) => index >= 0 && index < Count ? catalog[index]?.potion : null;
    public int GetPrice(int index) => index >= 0 && index < Count && catalog[index] != null ? catalog[index].price : 0;
    public bool IsPlayerInRange(GameObject player)
    {
        if (!isActiveAndEnabled || player == null || contact == null || !contact.isActiveAndEnabled || contact.Role != CityServiceRole.PotionStore) return false;
        Vector3 delta = player.transform.position - transform.position;
        return Mathf.Abs(delta.y) < 1.8f && Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude <= interactionRange * interactionRange &&
            !Physics.Linecast(transform.position + Vector3.up * 1.2f, player.transform.position + Vector3.up * 1.2f, walls, QueryTriggerInteraction.Ignore);
    }
    public override bool CanInteract(GameObject player)
    {
        var interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
        return base.CanInteract(player) && IsPlayerInRange(player) && shopUI != null && !shopUI.IsOpen && shopUI.PlayerObject == player && interactor != null && interactor.CanStartInteraction;
    }
    public override void Interact(GameObject player) { if (CanInteract(player)) shopUI.TryOpen(this); }
    public bool TryBuy(int index, ItemDefinition expected, int expectedPrice, int quantity, GameObject player, out string result)
    {
        result = "ซื้อไม่ได้ กรุณาเลือก Potion ใหม่";
        var potion = GetPotion(index); int price = GetPrice(index);
        if (shopUI == null || !shopUI.IsOpen || shopUI.Merchant != this || shopUI.PlayerObject != player || !IsPlayerInRange(player) ||
            potion == null || potion != expected || potion.Effect == PotionEffect.None || price < 1 || price != expectedPrice || quantity < 1 || quantity > 10) return false;
        var health = player.GetComponent<PlayerHealth>(); var inventory = player.GetComponent<PlayerInventory>(); var wallet = player.GetComponent<PlayerGoldWallet>();
        if (health == null || !health.IsAlive || inventory == null || wallet == null) return false;
        if ((long)price * quantity > wallet.Gold) { result = "Gold ไม่พอ"; return false; }
        if (!inventory.CanAdd(potion, quantity)) { result = "กระเป๋าเต็ม ไม่มีที่สำหรับ Potion จำนวนนี้"; return false; }
        if (!inventory.TryBuy(potion, quantity, price, wallet)) return false;
        result = $"ซื้อ {potion.DisplayName} ×{quantity} ใช้ {price * quantity:N0} Gold"; return true;
    }
}
