using UnityEngine;

/// <summary>React to the actual modal visibility, so rejected opens and repeated closes stay silent.</summary>
[DisallowMultipleComponent]
public sealed class UIModalAudio : MonoBehaviour
{
    [SerializeField] private UIAudioFeedback feedback;
    private bool opened;
    private void OnEnable(){opened=feedback!=null&&feedback.Play(UIAudioCue.Open);}
    private void OnDisable(){if(opened&&feedback!=null)feedback.Play(UIAudioCue.Close);opened=false;}
}
