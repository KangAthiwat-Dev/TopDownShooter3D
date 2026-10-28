using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class AlertPanelReveal : MonoBehaviour
{
    private CanvasGroup group;private float elapsed;
    private void OnEnable(){group=GetComponent<CanvasGroup>();elapsed=0;group.alpha=0;}
    private void Update(){elapsed+=Time.unscaledDeltaTime;group.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.45f));}
}
