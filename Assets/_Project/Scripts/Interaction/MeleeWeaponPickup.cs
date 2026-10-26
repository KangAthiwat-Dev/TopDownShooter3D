using UnityEngine;

public class MeleeWeaponPickup : Interactable
{
    [SerializeField] private MeleeWeaponDefinition weapon;
    private PlayerInteractor reservedBy;

    public MeleeWeaponDefinition Weapon => weapon;

    public override bool CanInteract(GameObject interactor)
    {
        if (!base.CanInteract(interactor) || reservedBy != null)
        {
            return false;
        }

        PlayerMeleeEquipment equipment = interactor.GetComponent<PlayerMeleeEquipment>();
        PlayerInteractor collector = interactor.GetComponent<PlayerInteractor>();
        return equipment != null && equipment.CanEquip(weapon) &&
               collector != null && collector.CanStartInteraction;
    }

    public override void Interact(GameObject interactor)
    {
        PlayerInteractor collector = interactor.GetComponent<PlayerInteractor>();
        if (collector != null)
        {
            collector.TryStartPickup(this);
        }
    }

    public bool TryReserve(PlayerInteractor collector)
    {
        if (collector == null || !CanInteract(collector.gameObject))
        {
            return false;
        }

        reservedBy = collector;
        return true;
    }

    public void Release(PlayerInteractor collector)
    {
        if (reservedBy == collector)
        {
            reservedBy = null;
        }
    }

    public bool TryCollect(PlayerInteractor collector)
    {
        if (collector == null || !isActiveAndEnabled || reservedBy != collector)
        {
            return false;
        }

        PlayerMeleeEquipment equipment = collector.GetComponent<PlayerMeleeEquipment>();
        if (equipment == null || !equipment.TryEquip(weapon))
        {
            return false;
        }

        reservedBy = null;
        gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        return true;
    }
}
