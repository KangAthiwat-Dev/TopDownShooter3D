#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIAudioRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private string error;private bool done;
    private InventoryPanelUI bag;private PlayerUpgradePanelUI skills;private UIAudioFeedback feedback;
    private static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private IEnumerator Start()
    {
        Application.logMessageReceived+=Log;var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;feedback=FindAnyObjectByType<UIAudioFeedback>();bag=FindAnyObjectByType<InventoryPanelUI>();skills=FindAnyObjectByType<PlayerUpgradePanelUI>();
        foreach(var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))enemy.enabled=false;
        int opens=0,closes=0,hovers=0;feedback.Played+=cue=>{if(cue==UIAudioCue.Open)opens++;else if(cue==UIAudioCue.Close)closes++;else hovers++;};
        var windowSource=Get<AudioSource>(feedback,"windows");var hoverSource=Get<AudioSource>(feedback,"hover");
        Check(!windowSource.isPlaying&&!hoverSource.isPlaying,"Startup remains silent");
        Check(windowSource.ignoreListenerPause&&hoverSource.ignoreListenerPause&&windowSource.spatialBlend==0&&hoverSource.spatialBlend==0,"Dedicated UI sources are 2D and bypass gameplay audio pause");
        Check(FindObjectsByType<UIModalAudio>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==7,"Inventory, skills, journal, dialogue and three shops have window audio hooks");
        Check(bag.TryOpen()&&opens==1&&AudioListener.pause&&windowSource.isPlaying&&windowSource.clip.name=="open_ui","Opening the actual inventory plays open_ui while gameplay audio is paused");
        Check(!bag.TryOpen()&&opens==1,"Rejected or repeated opens do not replay the sound");
        yield return Wait(.15f);var slot=bag.GetComponentsInChildren<InventorySlotUI>().First().Button.gameObject;
        var pointer=new PointerEventData(EventSystem.current){pointerId=-1};
        ExecuteEvents.Execute(slot,pointer,ExecuteEvents.pointerEnterHandler);
        Check(hovers==1&&hoverSource.isPlaying&&hoverSource.clip.name=="hover_slot","Inventory slot pointer-enter plays hover_slot while paused");
        ExecuteEvents.Execute(slot,pointer,ExecuteEvents.pointerEnterHandler);Check(hovers==1,"Staying inside a slot does not repeatedly play hover");
        ExecuteEvents.Execute(slot,pointer,ExecuteEvents.pointerExitHandler);ExecuteEvents.Execute(slot,pointer,ExecuteEvents.pointerEnterHandler);Check(hovers==1,"Very fast slot re-entry is rate limited using real time");
        yield return Wait(.1f);ExecuteEvents.Execute(slot,pointer,ExecuteEvents.pointerExitHandler);ExecuteEvents.Execute(slot,pointer,ExecuteEvents.pointerEnterHandler);
        Check(hovers==2,"Hover cooldown expires even with timeScale zero");
        bag.Close();Check(closes==1&&windowSource.clip.name=="close_ui"&&windowSource.isPlaying,"Closing inventory plays close_ui");bag.Close();Check(closes==1,"Closing an already closed menu stays silent");
        Check(skills.TryOpen()&&opens==2,"Skills modal uses the same open sound");skills.ShowPage(false);Check(opens==2&&closes==1,"Switching skill tabs does not masquerade as opening or closing a window");skills.Close();Check(closes==2,"Closing skills uses the close sound");
        Check(feedback.GetComponentInParent<Canvas>()!=null&&FindObjectsByType<UISlotHoverAudio>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>=35,"Hover audio also covers hotbar, guild materials and weapon/potion shop slots");
        Check(error==null,"No runtime errors during UI audio, pointer hover or paused playback");
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool okay,string text){report.AppendLine((okay?"PASS ":"FAIL ")+text);if(!okay)throw new InvalidOperationException(text);}
    private void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=text+"\n"+trace;}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;if(bag!=null)bag.Close();if(skills!=null)skills.Close();Application.logMessageReceived-=Log;
        if(failure!=null)report.AppendLine(failure.ToString());if(error!=null)report.AppendLine(error);report.AppendLine(failure==null&&error==null?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/UIAudioPlaytest.txt",report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("UI audio test interrupted"));}
}
#endif
