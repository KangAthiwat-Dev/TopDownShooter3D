using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AudioOptionsUI:MonoBehaviour
{
    [Serializable] public sealed class Row {public GameAudioBus bus;public Slider slider;public TMP_Text value;}
    [SerializeField] private Row[] rows;
    [SerializeField] private Button defaults;
    private bool subscribed;
    private void Awake()
    {
        foreach(var row in rows){var bus=row.bus;row.slider.onValueChanged.AddListener(v=>GameAudioSettings.SetVolume(bus,v));}
        defaults.onClick.AddListener(GameAudioSettings.ResetToDefaults);
    }
    private void OnEnable(){GameAudioSettings.Ensure();GameAudioSettings.Changed+=Refresh;subscribed=true;Refresh();}
    private void OnDisable()
    {
        if(subscribed){GameAudioSettings.Changed-=Refresh;subscribed=false;}
        if(Application.isPlaying)GameAudioSettings.FlushPreferences();
    }
    private void Refresh()
    {
        foreach(var row in rows)
        {
            float value=GameAudioSettings.Volume(row.bus);row.slider.SetValueWithoutNotify(value);
            row.value.text=Mathf.RoundToInt(value*100)+"%";
        }
    }
}
