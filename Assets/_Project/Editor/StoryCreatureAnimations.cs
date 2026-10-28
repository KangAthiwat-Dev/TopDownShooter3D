#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class StoryCreatureSetup
{
    private static AnimationClip Native(string path, string name) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == name);
    private static AnimationClip CopyClip(string source, string take, string kind, string role, bool loop)
    {
        string path = Folder + "/" + kind + "/" + role + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        var clip = UnityEngine.Object.Instantiate(Native(source,take)); clip.name = kind + "_" + role;
        AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip,settings); AssetDatabase.CreateAsset(clip,path); return clip;
    }

    private static AnimatorController CreatureController(string kind, Dictionary<string,AnimationClip> clips)
    {
        string path = Folder + "/" + kind + "/" + kind + ".controller";
        if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(path)) AssetDatabase.CopyAsset("Assets/_Project/Animations/Enemies/EnemyAnimator.controller",path);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var states = controller.layers[0].stateMachine.states.Select(s=>s.state).ToArray();
        var blend = (BlendTree)states.Single(s=>s.name=="Locomotion").motion; blend.useAutomaticThresholds = false;
        blend.children = new[] {
            new ChildMotion {motion=clips["Idle"],threshold=0,timeScale=1},
            new ChildMotion {motion=clips["Walk"],threshold=.5f,timeScale=1},
            new ChildMotion {motion=clips["Run"],threshold=1,timeScale=1}
        };
        states.Single(s=>s.name=="Attack").motion = clips["Attack"];
        states.Single(s=>s.name=="Death").motion = clips["Death"];
        foreach (var state in states) { state.speed=1; EditorUtility.SetDirty(state); }
        EditorUtility.SetDirty(blend);EditorUtility.SetDirty(controller);return controller;
    }

    // Bake transforms in the editor. The finished Generic clips need no retargeting or IK at runtime.
    private static AnimationClip BakePose(GameObject model,string kind,string role,float duration,bool loop,Action<float> sample)
    {
        string path = Folder + "/" + kind + "/" + role + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if (existing != null) return existing;
        var nodes = model.GetComponentsInChildren<Transform>(true).Where(t=>t!=model.transform).ToArray();
        int frames = Mathf.CeilToInt(duration*30); var keys = new List<Keyframe>[nodes.Length,7];
        for(int n=0;n<nodes.Length;n++) for(int c=0;c<7;c++) keys[n,c]=new List<Keyframe>();
        for(int f=0;f<=frames;f++)
        {
            float time = duration*f/frames; sample(loop && f==frames ? 0 : time);
            for(int n=0;n<nodes.Length;n++)
            {
                var p=nodes[n].localPosition; var q=nodes[n].localRotation;
                float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                for(int c=0;c<7;c++) keys[n,c].Add(new Keyframe(time,values[c]));
            }
        }
        var clip=new AnimationClip{name=kind+"_"+role,frameRate=30};
        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
        for(int n=0;n<nodes.Length;n++) for(int c=0;c<7;c++)
        {
            var curve=new AnimationCurve(keys[n,c].ToArray());
            for(int k=0;k<curve.length;k++) { AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear); }
            clip.SetCurve(AnimationUtility.CalculateTransformPath(nodes[n],model.transform),typeof(Transform),properties[c],curve);
        }
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=loop;
        AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,path);return clip;
    }

    private static Transform Bone(GameObject go,string name) => go.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    private static void SolveLeg(Transform[] chain,Vector3 goal)
    {
        var foot=chain[chain.Length-1];
        float reach=0;for(int i=1;i<chain.Length;i++)reach+=Vector3.Distance(chain[i-1].position,chain[i].position);
        goal=chain[0].position+Vector3.ClampMagnitude(goal-chain[0].position,reach*.985f);
        for(int attempt=0;attempt<14;attempt++)
        {
            for(int i=chain.Length-2;i>=0;i--)
            {
                var from=foot.position-chain[i].position;var to=goal-chain[i].position;
                if(from.sqrMagnitude<.000001f||to.sqrMagnitude<.000001f)continue;
                var change=Quaternion.FromToRotation(from,to);
                chain[i].rotation=Quaternion.RotateTowards(Quaternion.identity,change,22)*chain[i].rotation;
            }
            if((foot.position-goal).sqrMagnitude<.000004f)break;
        }
    }

    private static AnimationClip SpiderDeath(GameObject model)
    {
        var idle=Native(SpiderSource,"idle_loop"); idle.SampleAnimation(model,0);
        var body=Bone(model,"body");var initialBody=body.localRotation;var initialPosition=body.localPosition;
        var nodes=model.GetComponentsInChildren<Transform>(true).Where(t=>t!=model.transform).ToArray();
        var positions=nodes.Select(t=>t.localPosition).ToArray();var rotations=nodes.Select(t=>t.localRotation).ToArray();
        for(int side=0;side<2;side++) for(int leg=1;leg<=4;leg++)
        {
            string suffix=(side==0?"L":"R")+leg;
            var chain=new[]{Bone(model,"femur_"+suffix),Bone(model,"tibia_"+suffix),Bone(model,"metatarsus_"+suffix),Bone(model,"tarsus_"+suffix)};
            var goal=Vector3.Lerp(chain.Last().position,body.position,.68f)+Vector3.up*.09f;
            SolveLeg(chain,goal);
        }
        var curled=nodes.Select(t=>t.localRotation).ToArray();
        return BakePose(model,"GiantSpider","Death",1.1f,false,time=>{
            float t=Mathf.SmoothStep(0,1,time/.75f);
            for(int i=0;i<nodes.Length;i++){nodes[i].localPosition=positions[i];nodes[i].localRotation=Quaternion.Slerp(rotations[i],curled[i],t);}
            body.localRotation=Quaternion.AngleAxis(t*68,Vector3.forward)*initialBody;
            body.localPosition=initialPosition+new Vector3(.15f,-.1f,0)*t;
            body.position-=Vector3.up*NpcMerchantSetup.GeometryBounds(model).min.y;
        });
    }

    private static Dictionary<string,AnimationClip> WolfAnimations(GameObject wolf)
    {
        var clips=new Dictionary<string,AnimationClip>();
        foreach(var entry in new[]{("Idle","idle"),("Walk","walk"),("Run","run"),("Attack","attack1"),("Death","die")})
            clips[entry.Item1]=CopyClip(WolfPack+"/Animation/Wolfboss@"+entry.Item2+".FBX",entry.Item2,"WolfBoss",entry.Item1,entry.Item1=="Idle"||entry.Item1=="Walk"||entry.Item1=="Run");
        return clips;
    }
}
#endif
