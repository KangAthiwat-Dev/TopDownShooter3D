using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
public sealed class BossHealthUI : MonoBehaviour
{
    [SerializeField] private KingOfDemonAI boss;
    [SerializeField] private EncounterCombatant encounterBoss;
    [SerializeField] private int displayOrder;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageTrail;
    [SerializeField] private TMP_Text bossName;
    [SerializeField, Min(.05f)] private float fadeSeconds = .3f;
    private CanvasGroup group;
    private bool wasVisible;
    private RectTransform rect;
    private Vector2 restingPosition;
    private static readonly List<BossHealthUI> Presenters = new List<BossHealthUI>();
    private IBossHealthSource Source => encounterBoss != null ? (IBossHealthSource)encounterBoss : boss;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPresenters() => Presenters.Clear();
    private void OnEnable() { if (!Presenters.Contains(this)) Presenters.Add(this); }
    private void OnDisable() => Presenters.Remove(this);
    public bool IsVisible => group != null && group.alpha > .01f;
    public float DisplayedHealth => healthFill != null ? healthFill.fillAmount : 0f;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        restingPosition = rect.anchoredPosition;
        group.alpha = 0;
        group.blocksRaycasts = false;
        group.interactable = false;
    }
    private void Update()
    {
        var source = Source;
        bool visible = source != null && source.ShowHealthBar;
        float fraction = source != null && source.Health != null ? source.Health.CurrentHealth / source.Health.MaxHealth : 1f;
        if (visible && !wasVisible)
        {
            healthFill.fillAmount = fraction;
            if (damageTrail != null) damageTrail.fillAmount = fraction;
        }
        if (bossName != null && source != null) bossName.text = source.DisplayName;
        int row = Presenters.Count(ui => ui != this && ui.displayOrder < displayOrder && ui.Source != null && ui.Source.ShowHealthBar);
        rect.anchoredPosition = restingPosition + Vector2.down * row * 84f;
        if (healthFill != null) healthFill.fillAmount = Mathf.MoveTowards(healthFill.fillAmount, fraction, Time.unscaledDeltaTime * 3f);
        if (damageTrail != null) damageTrail.fillAmount = Mathf.MoveTowards(damageTrail.fillAmount, fraction, Time.unscaledDeltaTime * .35f);
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
        wasVisible = visible;
    }
}
