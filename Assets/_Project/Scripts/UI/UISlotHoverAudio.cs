using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UISlotHoverAudio : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    [SerializeField] private UIAudioFeedback feedback;
    private bool inside;
    [SerializeField] private Image highlight;
    [SerializeField] private Color hoverTint=new Color(1,.83f,.42f,.1f);
    public void OnPointerEnter(PointerEventData data)
    {
        if(inside||!isActiveAndEnabled)return;
        inside=true;if(feedback!=null)feedback.Play(UIAudioCue.Hover);
    }
    public void OnPointerExit(PointerEventData data)=>inside=false;
    private void Update()
    {
        if(highlight==null)return;Color target=hoverTint;target.a=inside?hoverTint.a:0;
        highlight.color=Color.Lerp(highlight.color,target,1-Mathf.Exp(-24*Time.unscaledDeltaTime));
    }
    private void OnDisable(){inside=false;if(highlight!=null)highlight.color=Color.clear;}
}
