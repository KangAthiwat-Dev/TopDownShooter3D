using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;

    [Header("Colors")]
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;

    private void Start()
    {
        if (playerHealth == null)
        {
            return;
        }

        UpdateHealthUI(
            playerHealth.CurrentHealth,
            playerHealth.MaxHealth
        );
    }
    private void Awake()
    {
        if (playerHealth == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }
        }
    }

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.HealthChanged += UpdateHealthUI;

        UpdateHealthUI(
            playerHealth.CurrentHealth,
            playerHealth.MaxHealth
        );
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= UpdateHealthUI;
        }
    }

    private void UpdateHealthUI(float current, float maximum)
    {
        float normalizedHealth =
            maximum > 0f ? current / maximum : 0f;

        healthFill.fillAmount = normalizedHealth;

        healthText.text =
            $"{Mathf.CeilToInt(current)} / " +
            $"{Mathf.CeilToInt(maximum)}";

        if (normalizedHealth > 0.5f)
        {
            healthFill.color = healthyColor;
        }
        else if (normalizedHealth > 0.25f)
        {
            healthFill.color = warningColor;
        }
        else
        {
            healthFill.color = dangerColor;
        }
    }
}