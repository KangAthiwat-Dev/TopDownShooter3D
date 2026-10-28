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
using UnityEngine.InputSystem;

public sealed class SwordAudioRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();private readonly List<bool> played=new List<bool>();
    private PlayerMovement player;private PlayerMeleeAttack melee;private PlayerSwordAudio audioControl;private AudioSource source;private EnemyHealth target;private GameObject fixture;private MeleeWeaponDefinition weapon;
    private bool done,oldBackground;private int oldFps,oldVsync,commits;private readonly Vector3 origin=new Vector3(2000,20,2000);
    private static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=Log;
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;player=FindAnyObjectByType<PlayerMovement>();melee=player.GetComponent<PlayerMeleeAttack>();audioControl=player.GetComponent<PlayerSwordAudio>();source=Get<AudioSource>(audioControl,"source");
        Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        player.GetComponent<PlayerProgressionState>().RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");player.GetComponent<PlayerInput>().actions.devices=Array.Empty<InputDevice>();
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false);foreach(var encounter in FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None))encounter.enabled=false;
        foreach(var wave in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){wave.StopAllCoroutines();wave.enabled=false;}foreach(var hp in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))hp.gameObject.SetActive(false);
        FindAnyObjectByType<WorldAmbientAudio>().enabled=false;
        fixture=new GameObject("Temporary sword sound fixture");var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=7;floor.transform.SetParent(fixture.transform);floor.transform.position=origin+Vector3.down*.1f;floor.transform.localScale=new Vector3(20,.2f,20);
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=origin;cc.enabled=true;player.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
        var obj=new GameObject("Sword damage target");obj.SetActive(false);obj.layer=6;obj.transform.SetParent(fixture.transform);obj.transform.position=origin+Vector3.forward*1.4f;
        target=obj.AddComponent<EnemyHealth>();Set(target,"maxHealth",1000f);Set(target,"experienceReward",0);var box=obj.AddComponent<BoxCollider>();box.center=Vector3.up;box.size=new Vector3(.5f,1.8f,.5f);
        var aim=new GameObject("Aim").transform;aim.SetParent(obj.transform,false);aim.localPosition=Vector3.up;var targetable=obj.AddComponent<Targetable>();Set(targetable,"aimPoint",aim);obj.SetActive(true);
        weapon=Instantiate(AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>("Assets/_Project/Prefabs/Weapons/FantasySwords/Definitions/Sword_01.asset"));Set(weapon,"damage",30f);Set(weapon,"killsInOneHit",false);
        player.GetComponent<PlayerMeleeEquipment>().RestoreLoadout(new[]{weapon,(MeleeWeaponDefinition)null},0);audioControl.SwingPlayed+=final=>played.Add(final);melee.SwingCommitted+=armed=>{if(armed)commits++;};
        yield return Wait(1.5f);
        Check(Get<AudioClip>(audioControl,"normalSwing").name=="musicholder-sword-sound-260274"&&Get<AudioClip>(audioControl,"finalSwing").name=="dragon-studio-violent-sword-slice-393848","Requested normal and final sword files are assigned");
        Check(source.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Effects)&&!source.loop&&!source.playOnAwake,"Sword audio uses the existing Effects volume control and never loops");
        yield return Attack(false,1);
        Check(target.CurrentHealth==970&&commits==1,"A full animated sword attack still applies exactly one original damage commit");
        yield return Attack(false,2);yield return Attack(true,3,true);
        Check(played.SequenceEqual(new[]{false,false,true})&&target.CurrentHealth==910&&commits==3,"Three real attacks play normal, normal, final while all three keep the same damage");
        yield return Attack(false,4);yield return Wait(2.15f);yield return Attack(false,5);yield return Attack(false,6);yield return Attack(true,7);
        Check(played.SequenceEqual(new[]{false,false,true,false,false,false,true}),"After a quiet interval the audio sequence starts again from its first normal swing");
        Request(targetable);yield return Wait(.025f);melee.enabled=false;yield return Wait(.25f);
        Check(played.Count==7&&!melee.IsAttacking,"Cancelling before the sword moves produces no delayed sound");melee.enabled=true;yield return Wait(.15f);yield return Attack(false,8);
        player.GetComponent<PlayerMeleeEquipment>().RestoreLoadout(new MeleeWeaponDefinition[2],-1);Request(targetable);yield return Until(()=>!melee.IsAttacking,2);
        Check(played.Count==8&&target.CurrentHealth==745,"Unarmed punches stay silent and retain their original damage");
        source.clip=Get<AudioClip>(audioControl,"finalSwing");source.Play();player.GetComponent<PlayerHealth>().TakeDamage(999999);yield return Wait(.1f);
        Check(!source.isPlaying,"Player death immediately stops sword audio");
        Check(errors.Count==0,"No runtime errors during real attacks, sound sequences, interruption, pause or death");
    }
    private IEnumerator Attack(bool final,int expectedCount,bool pauseDuring=false)
    {
        int before=played.Count;Request(target.GetComponent<Targetable>());Check(melee.IsAttacking&&played.Count==before,"Attack "+expectedCount+" waits for the animation swing before playing audio");
        yield return Until(()=>played.Count>before,2);
        Check(played.Count==expectedCount&&played.Last()==final&&source.isPlaying&&source.clip==Get<AudioClip>(audioControl,final?"finalSwing":"normalSwing"),"Attack "+expectedCount+" plays the correct clip once");
        if(expectedCount==1)
        {
            yield return Wait(.05f);var samples=new float[1024];float energy=0;
            for(int i=0;i<5;i++){source.GetOutputData(samples,0);energy=Mathf.Max(energy,Mathf.Sqrt(samples.Sum(s=>s*s)/samples.Length));yield return Wait(.02f);}
            report.AppendLine("Sword RMS="+energy.ToString("F6"));Check(energy>.00001f,"The normal sword sound produces real audio after skipping its quiet lead-in");
        }
        if(pauseDuring)
        {
            var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.PauseMenu,true);float time=source.time,hp=target.CurrentHealth;yield return Wait(.25f);
            Check(AudioListener.pause&&Mathf.Abs(source.time-time)<.1f&&target.CurrentHealth==hp&&played.Count==expectedCount,"Pause freezes sword audio, animation and pending damage");pause.SetPaused(GamePauseReason.PauseMenu,false);
        }
        yield return Until(()=>!melee.IsAttacking,2);Check(!melee.IsAttacking&&played.Count==expectedCount,"Attack "+expectedCount+" finishes without repeating its sound");yield return Wait(.04f);
    }
    private void Request(Targetable t)=>typeof(PlayerMeleeAttack).GetMethod("HandleMeleeAttack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(melee,new object[]{t});
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/SwordAudioPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(fixture!=null)Destroy(fixture);if(weapon!=null)Destroy(weapon);if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);File.WriteAllText("Logs/SwordAudioPlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Sword audio test interrupted"));}
}
#endif
