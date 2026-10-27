using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerProgressionUI : MonoBehaviour
{
    [SerializeField] private PlayerProgressionState state;
    [SerializeField] private TMP_Text levelText,rankText;
    [SerializeField] private Image progressFill;
    public float DisplayedProgress=>progressFill!=null?progressFill.fillAmount:0f;
    public string DisplayedLevel=>levelText!=null?levelText.text:string.Empty;
    public string DisplayedRank=>rankText!=null?rankText.text:string.Empty;
    private void OnEnable(){if(state!=null)state.Changed+=Refresh;Refresh();}
    private void OnDisable(){if(state!=null)state.Changed-=Refresh;}
    private void Refresh()
    {
        if(state==null)return;
        if(levelText!=null)levelText.text="LV. "+state.Level;
        if(rankText!=null)rankText.text=state.Rank;
        if(progressFill!=null)progressFill.fillAmount=state.LevelProgress;
    }
}
