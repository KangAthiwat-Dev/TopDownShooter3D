using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One non-interactive overlay for all visible ordinary monsters and dragons.</summary>
public sealed class MonsterOverheadHealthUI : MonoBehaviour
{
    [SerializeField, Min(1)] private float visibleDistance = 38;
    private sealed class Bar
    {
        public RectTransform root, fill, trail;
        public Targetable target;
        public Collider body;
        public bool dragon;
        public float fraction = 1;
    }
    private readonly Dictionary<EnemyHealth,Bar> bars = new Dictionary<EnemyHealth,Bar>();
    private readonly List<EnemyHealth> stale = new List<EnemyHealth>();
    private readonly Stack<Bar> pool = new Stack<Bar>();
    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera view;
    public int VisibleCount { get; private set; }
    private void Awake()
    {
        canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=15;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        canvasRect=GetComponent<RectTransform>();
    }
    private bool Eligible(EnemyHealth health)
    {
        if(health==null||!health.isActiveAndEnabled||!health.IsAlive||health.GetComponent<KingOfDemonAI>()!=null)return false;
        var encounter=health.GetComponent<EncounterCombatant>();
        return encounter!=null?!encounter.UsesBossHud:health.GetComponent<EnemyAI>()!=null;
    }
    private void LateUpdate()
    {
        if(view==null)view=Camera.main;
        if(view==null)return;
        VisibleCount=0;stale.Clear();
        foreach(var pair in bars)if(!Eligible(pair.Key))stale.Add(pair.Key);
        foreach(var key in stale){var bar=bars[key];bar.root.gameObject.SetActive(false);pool.Push(bar);bars.Remove(key);}
        foreach(var health in EnemyHealth.Active)
        {
            if(!Eligible(health))continue;
            bool nearby=(health.transform.position-view.transform.position).sqrMagnitude<=visibleDistance*visibleDistance;
            if(!nearby){if(bars.TryGetValue(health,out var hidden))hidden.root.gameObject.SetActive(false);continue;}
            if(!bars.TryGetValue(health,out var bar))
            {
                bar=pool.Count>0?pool.Pop():CreateBar();bars.Add(health,bar);
                bar.target=health.GetComponent<Targetable>();bar.body=health.GetComponent<Collider>();
                bar.dragon=health.GetComponent<EncounterCombatant>()!=null;bar.fraction=health.CurrentHealth/health.MaxHealth;
                bar.root.sizeDelta=new Vector2(bar.dragon?92:58,bar.dragon?9:7);
                bar.fill.GetComponent<Image>().color=bar.dragon?new Color(.94f,.42f,.17f):new Color(.82f,.12f,.12f);
            }
            var anchor=bar.target!=null?bar.target.AimPoint.position:health.transform.position+Vector3.up*1.8f;
            if(bar.body!=null&&bar.body.enabled)anchor.y=Mathf.Max(anchor.y,bar.body.bounds.max.y);
            anchor.y+=.3f;
            var screen=view.WorldToScreenPoint(anchor);
            bool show=screen.z>0&&screen.x>8&&screen.x<Screen.width-8&&screen.y>8&&screen.y<Screen.height-8&&
                BossCombatSight.Clear(view.transform,view.transform.position,health.transform,anchor);
            bar.root.gameObject.SetActive(show);
            if(!show)continue;
            VisibleCount++;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var point);bar.root.anchoredPosition=point;
            float ratio=health.CurrentHealth/health.MaxHealth;
            bar.fraction=Mathf.MoveTowards(bar.fraction,ratio,Time.unscaledDeltaTime*.7f);
            bar.fill.anchorMax=new Vector2(ratio,1);bar.trail.anchorMax=new Vector2(bar.fraction,1);
        }
    }
    private Bar CreateBar()
    {
        var root=new GameObject("Monster health",typeof(RectTransform),typeof(Image));root.transform.SetParent(transform,false);
        root.GetComponent<Image>().color=new Color(.04f,.025f,.025f,.9f);root.GetComponent<Image>().raycastTarget=false;
        var inset=new GameObject("Inset",typeof(RectTransform)).GetComponent<RectTransform>();inset.SetParent(root.transform,false);
        inset.anchorMin=Vector2.zero;inset.anchorMax=Vector2.one;inset.offsetMin=Vector2.one;inset.offsetMax=-Vector2.one;
        return new Bar{root=root.GetComponent<RectTransform>(),trail=Fill(inset,"Damage trail",new Color(1,.75f,.25f)),fill=Fill(inset,"Health",Color.red)};
    }
    private static RectTransform Fill(Transform parent,string name,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        return rect;
    }
}
