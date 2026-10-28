using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GameAlertKind { LevelUp,QuestCompleted,GameSaved,Victory,Town,Village,Forest,Castle }

/// <summary>Non-blocking art banners; actual gameplay transitions own their signals.</summary>
public sealed class GameAlertsUI : MonoBehaviour
{
    [Serializable] public sealed class Art { public GameAlertKind kind;public Sprite sprite; }
    private sealed class Message { public GameAlertKind kind;public string detail;public float duration; }
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerProgressionState progression;
    [SerializeField] private PlayerStoryProgress story;
    [SerializeField] private AdventureHudUI hud;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private UIAudioFeedback audioFeedback;
    [SerializeField] private DemonArenaEncounter[] victories;
    [SerializeField] private Art[] art;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image emblem;
    [SerializeField] private TMP_Text detail;
    private readonly List<Message> queue=new List<Message>();
    private readonly HashSet<DemonArenaEncounter> announced=new HashSet<DemonArenaEncounter>();
    private Message current;private float elapsed,zoneSince,nextSave;
    private string area,candidateArea;
    private StoryQuestStatus defense,wolf;
    public bool IsVisible=>group.alpha>.01f;
    public GameAlertKind? CurrentKind=>current?.kind;
    private void OnEnable()
    {
        progression.LeveledUp+=OnLevel;story.Changed+=OnStory;health.Died+=OnDeath;
        foreach(var e in victories)if(e!=null)e.Changed+=OnEncounter;
        defense=story.DefenseStatus;wolf=story.WolfStatus;
        group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
    }
    private void OnDisable()
    {
        if(progression!=null)progression.LeveledUp-=OnLevel;if(story!=null)story.Changed-=OnStory;if(health!=null)health.Died-=OnDeath;
        foreach(var e in victories)if(e!=null)e.Changed-=OnEncounter;
    }
    private void OnLevel(int before,int after)=>Show(GameAlertKind.LevelUp,$"เลเวล {before} → {after}  •  มีแต้มพัฒนา {progression.AvailableUpgradePoints} แต้ม",3.4f);
    private void OnStory()
    {
        if(defense!=StoryQuestStatus.Completed&&story.DefenseStatus==StoryQuestStatus.Completed)Show(GameAlertKind.QuestCompleted,story.DefenseQuest.title+"  •  รับรางวัลแล้ว",3.5f);
        if(wolf!=StoryQuestStatus.Completed&&story.WolfStatus==StoryQuestStatus.Completed)Show(GameAlertKind.QuestCompleted,story.WolfQuest.title+"  •  รับรางวัลแล้ว",3.5f);
        defense=story.DefenseStatus;wolf=story.WolfStatus;
    }
    private void OnEncounter()
    {
        foreach(var e in victories)if(e!=null&&e.Completed&&announced.Add(e))Show(GameAlertKind.Victory,e.CurrentTitle+"  •  เคลียร์พื้นที่แล้ว",4.5f);
    }
    private void OnDeath(){queue.Clear();current=null;group.alpha=0;}
    public void Saved()
    {
        if(Time.unscaledTime<nextSave)return;nextSave=Time.unscaledTime+1;
        Show(GameAlertKind.GameSaved,"บันทึกเลเวล ไอเทม และความคืบหน้าภารกิจแล้ว",2.2f,true);
    }
    public void Show(GameAlertKind kind,string text,float duration=3,bool priority=false)
    {
        if(health==null||!health.IsAlive)return;
        foreach(var a in art)if(a.kind==kind&&a.sprite!=null)
        {
            var message=new Message{kind=kind,detail=text,duration=duration};
            if(priority){if(current!=null)queue.Insert(0,current);current=null;queue.Insert(0,message);}
            else
            {
                if(kind==GameAlertKind.LevelUp)queue.RemoveAll(m=>m.kind==kind);
                if(queue.Count>=8)queue.RemoveAt(0);queue.Add(message);
            }
            return;
        }
    }
    private void Update()
    {
        if(!health.IsAlive)return;
        PollArea();
        if(current==null&&queue.Count>0)
        {
            current=queue[0];queue.RemoveAt(0);elapsed=0;
            foreach(var a in art)if(a.kind==current.kind){emblem.sprite=a.sprite;break;}
            emblem.rectTransform.sizeDelta=current.kind==GameAlertKind.Victory?new Vector2(650,360):new Vector2(900,300);
            detail.rectTransform.anchoredPosition=new Vector2(50,current.kind==GameAlertKind.Victory?-380:-305);
            detail.text=current.detail;audioFeedback?.Play(UIAudioCue.Open);
        }
        if(current==null){group.alpha=0;return;}
        if(pause!=null&&pause.IsPaused&&current.kind!=GameAlertKind.GameSaved){group.alpha=0;return;}
        elapsed+=Time.unscaledDeltaTime;
        float fade=Mathf.Min(Mathf.Clamp01(elapsed/.28f),Mathf.Clamp01((current.duration-elapsed)/.4f));
        group.alpha=fade;group.transform.localScale=Vector3.one*Mathf.Lerp(.94f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.35f)));
        if(elapsed>=current.duration){current=null;group.alpha=0;}
    }
    private void PollArea()
    {
        if(hud==null||pause!=null&&pause.IsPaused)return;
        string location=hud.DisplayedArea;
        if(location!=candidateArea){candidateArea=location;zoneSince=Time.unscaledTime;return;}
        if(area==location||Time.unscaledTime-zoneSince<.8f)return;area=location;
        if(location=="SILVERWELL TOWN")Show(GameAlertKind.Town,"ซิลเวอร์เวลล์ • เมืองแห่งนักผจญภัย");
        else if(location=="OAKSHADE VILLAGE")Show(GameAlertKind.Village,"หมู่บ้านโอ๊กเชด");
        else if(location=="THORNVEIL FOREST")Show(GameAlertKind.Forest,"ป่าธอร์นเวล • ระวังอันตรายระหว่างทาง");
        else if(location=="MALACHAR'S CASTLE")Show(GameAlertKind.Castle,"ปราสาทมาลาคาร์ • อาณาเขตจอมมาร");
    }
}
