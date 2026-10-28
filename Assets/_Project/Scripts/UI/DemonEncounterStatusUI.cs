using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DemonEncounterStatusUI : MonoBehaviour
{
    private DemonArenaEncounter[] encounters;
    private TMP_Text label;
    private void Start()
    {
        encounters=FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None);
        var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var go=new GameObject("Wave and gate status",typeof(RectTransform));go.transform.SetParent(transform,false);
        label=go.AddComponent<TextMeshProUGUI>();
        foreach(var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))if(text!=label&&text.font!=null&&text.font.HasCharacter('ร')){label.font=text.font;break;}
        label.fontSize=23;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.8f,.5f);label.raycastTarget=false;
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-230);rect.sizeDelta=new Vector2(900,38);
    }
    private void Update()
    {
        label.text="";
        foreach(var encounter in encounters)
        {
            if(encounter==null||encounter.Arena==null||!encounter.Arena.PlayerInside)continue;
            if(!encounter.Unlocked){label.text="ต้องชนะ Demon Lord และผู้พิทักษ์ก่อน";return;}
            if(encounter.Completed){label.text="เคลียร์พื้นที่แล้ว";return;}
            if(encounter.WaveIndex>=0){label.text=encounter.CurrentTitle+"  •  เหลือ "+encounter.LivingCount+" ตัว";return;}
        }
    }
}
