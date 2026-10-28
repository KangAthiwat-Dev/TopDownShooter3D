#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class StoryCreatureSetup
{
    private static void PreviewActor(string prefabPath,string output,string role,float progress)
    {
        var preview=new PreviewRenderUtility();Texture2D image=null;
        try
        {
            var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            foreach(var agent in actor.GetComponentsInChildren<NavMeshAgent>(true))agent.enabled=false;
            preview.AddSingleGO(actor);actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var animator=actor.GetComponentInChildren<Animator>();animator.enabled=false;
            var clip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name==role||c.name.EndsWith("_"+role));clip.SampleAnimation(animator.gameObject,clip.length*progress);
            var skins=animator.GetComponentsInChildren<SkinnedMeshRenderer>();var bounds=skins[0].bounds;foreach(var skin in skins.Skip(1))bounds.Encapsulate(skin.bounds);
            float scale=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.11f,.13f);camera.fieldOfView=32;camera.nearClipPlane=.01f;camera.farClipPlane=60;
            camera.transform.position=bounds.center+new Vector3(.95f,.65f,1.35f)*scale;camera.transform.LookAt(bounds.center);
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(20,210,0);preview.ambientColor=Color.gray*.65f;
            preview.BeginStaticPreview(new Rect(0,0,900,700));preview.Render(true);image=preview.EndStaticPreview();File.WriteAllBytes("Logs/"+output+".png",image.EncodeToPNG());
        }
        finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();}
    }
    [MenuItem("Tools/Story Creatures/Capture Prepared Creatures and Wizard")]
    public static void CaptureCreatures()
    {
        if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)return;
        PreviewActor(WolfPrefab,"AlphaWolf_Idle","Idle",.2f);
        foreach(string role in new[]{"Walk","Run","Death"})
            for(int i=0;i<3;i++)PreviewActor(WolfPrefab,"AlphaWolf_"+role+"_"+i,role,i*.45f);
        PreviewActor(SpiderPrefab,"GiantSpider_Idle","Idle",.2f);PreviewActor(SpiderPrefab,"GiantSpider_Death","Death",.95f);
        // The placeholder keeps the existing NPC controller and native Humanoid idle.
        var preview=new PreviewRenderUtility();Texture2D image=null;
        try
        {
            var wizard=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefab));
            foreach(var b in wizard.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;preview.AddSingleGO(wizard);
            var animator=wizard.GetComponentInChildren<Animator>();animator.Rebind();animator.Update(0);animator.Play("Locomotion",0,.2f);animator.Update(0);
            var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.11f,.13f);camera.fieldOfView=30;camera.nearClipPlane=.01f;camera.farClipPlane=30;
            camera.transform.position=new Vector3(2,1.6f,4.5f);camera.transform.LookAt(Vector3.up*1.1f);
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(20,210,0);preview.ambientColor=Color.gray*.65f;
            preview.BeginStaticPreview(new Rect(0,0,700,850));preview.Render(true);image=preview.EndStaticPreview();File.WriteAllBytes("Logs/ElderWizard_Placeholder.png",image.EncodeToPNG());
        }
        finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();}
    }
}
#endif
