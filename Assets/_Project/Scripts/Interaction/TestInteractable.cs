using UnityEngine;

public class TestInteractable : Interactable
{
    [SerializeField]
    private string message = "Interaction successful.";

    public override void Interact(GameObject interactor)
    {
        Debug.Log(
            $"{interactor.name}: {message}"
        );
    }
}