#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class SwordAudioSetup
{
    [MenuItem("Tools/Player Audio/Inspect Current Sword Animation")]
    public static void Inspect()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMeleeAttack>();var original=player.GetComponentInChildren<Animator>(true);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animations/Player/PlayerAnimator.controller");
        var state=controller.layers[0].stateMachine.states.Single(s=>s.state.name=="MeleeWeaponAttack").state;
        var clip=(AnimationClip)state.motion;var report=new StringBuilder();report.AppendLine($"Clip={clip.name} length={clip.length} fps={clip.frameRate} stateSpeed={state.speed} path={AssetDatabase.GetAssetPath(clip)}");
        var copy=UnityEngine.Object.Instantiate(original.gameObject);copy.name="Temporary sword pose inspection";copy.hideFlags=HideFlags.HideAndDontSave;
        copy.SetActive(true);var animator=copy.GetComponent<Animator>();animator.runtimeAnimatorController=controller;var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var arm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Vector3 last=Vector3.zero;Quaternion lastRotation=Quaternion.identity;
        try
        {
            AnimationMode.StartAnimationMode();
            for(int i=0;i<=46;i++)
            {
                float normalized=i/46f;AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(copy,clip,normalized*clip.length);AnimationMode.EndSampling();
                Vector3 point=copy.transform.InverseTransformPoint(hand.position);
                report.AppendLine($"frame={i:00} n={normalized:F4} hand={point.ToString("F3")} displacement={(i==0?0:Vector3.Distance(point,last)):F4} armDegrees={(i==0?0:Quaternion.Angle(arm.localRotation,lastRotation)):F2}");last=point;lastRotation=arm.localRotation;
            }
        }
        finally{AnimationMode.StopAnimationMode();UnityEngine.Object.DestroyImmediate(copy);}
        foreach(var path in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/_Project/Audio/sword"}).Select(AssetDatabase.GUIDToAssetPath))
        {var audio=AssetDatabase.LoadAssetAtPath<AudioClip>(path);report.AppendLine($"Audio={audio.name} length={audio.length:F3}s frequency={audio.frequency} channels={audio.channels}");}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SwordAudioInspection.txt",report.ToString());Debug.Log("Sword attack animation and audio timings inspected.");
    }
}
#endif
