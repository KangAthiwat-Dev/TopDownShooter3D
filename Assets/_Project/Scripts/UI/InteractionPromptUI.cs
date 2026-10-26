using TMPro;
using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField]
    private PlayerInteractor playerInteractor;

    [SerializeField]
    private GameObject promptPanel;

    [SerializeField]
    private TMP_Text promptText;

    [SerializeField]
    private string inputLabel = "[E]";

    private void Awake()
    {
        if (playerInteractor == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerInteractor =
                    player.GetComponent<PlayerInteractor>();
            }
        }

        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (playerInteractor != null)
        {
            playerInteractor.FocusChanged += UpdatePrompt;
        }
    }

    private void Start()
    {
        if (playerInteractor != null)
        {
            UpdatePrompt(
                playerInteractor.CurrentInteractable
            );
        }
    }

    private void OnDisable()
    {
        if (playerInteractor != null)
        {
            playerInteractor.FocusChanged -= UpdatePrompt;
        }
    }

    private void UpdatePrompt(Interactable interactable)
    {
        bool shouldShow = interactable != null;

        if (promptPanel != null)
        {
            promptPanel.SetActive(shouldShow);
        }

        if (!shouldShow || promptText == null)
        {
            return;
        }

        promptText.text =
            $"{inputLabel} " +
            $"{interactable.InteractionPrompt}";
    }
}