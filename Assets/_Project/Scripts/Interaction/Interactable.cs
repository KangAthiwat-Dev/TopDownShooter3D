using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionPrompt = "Interact";

    public string InteractionPrompt => interactionPrompt;

    public virtual bool CanInteract(GameObject interactor)
    {
        return isActiveAndEnabled;
    }

    public abstract void Interact(GameObject interactor);
}