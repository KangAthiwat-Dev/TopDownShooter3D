using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergyUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerEnergy playerEnergy;

    [SerializeField]
    private Image energyFill;

    [SerializeField]
    private TMP_Text energyText;

    [Header("Colors")]
    [SerializeField]
    private Color normalColor =
        new Color(0.45f, 0.2f, 1f);

    [SerializeField]
    private Color lowColor =
        new Color(1f, 0.25f, 0.5f);

    private void OnEnable()
    {
        if (playerEnergy != null)
        {
            playerEnergy.EnergyChanged +=
                HandleEnergyChanged;
        }
    }

    private void Start()
    {
        if (playerEnergy == null)
        {
            Debug.LogError(
                "PlayerEnergyUI: PlayerEnergy is missing.",
                this
            );

            return;
        }

        UpdateUI(
            playerEnergy.CurrentEnergy,
            playerEnergy.MaxEnergy
        );
    }

    private void OnDisable()
    {
        if (playerEnergy != null)
        {
            playerEnergy.EnergyChanged -=
                HandleEnergyChanged;
        }
    }

    private void HandleEnergyChanged(
        float currentEnergy,
        float maxEnergy
    )
    {
        UpdateUI(currentEnergy, maxEnergy);
    }

    private void UpdateUI(
        float currentEnergy,
        float maxEnergy
    )
    {
        float normalizedEnergy =
            maxEnergy > 0f
                ? currentEnergy / maxEnergy
                : 0f;

        if (energyFill != null)
        {
            energyFill.fillAmount =
                normalizedEnergy;

            energyFill.color = Color.Lerp(
                lowColor,
                normalColor,
                normalizedEnergy
            );
        }

        if (energyText != null)
        {
            energyText.text =
                $"{Mathf.CeilToInt(currentEnergy)}" +
                $" / {Mathf.CeilToInt(maxEnergy)}";
        }
    }
}