#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class DemonLavaSurfacePreview
{
    private const string Pending="DemonLava.AnimationPreview";
    private static Camera preview;private static double began;private static int phase;private static Color32[] first;
    private static bool previousBackground;private static string error;private static float previousTimeScale;
    static DemonLavaSurfacePreview()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/World/Lava Surfaces/Preview and Verify Animation")]
    private static void Run()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            previousBackground=Application.runInBackground;previousTimeScale=Time.timeScale;Application.runInBackground=true;Time.timeScale=1;error=null;phase=0;first=null;
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
            var go=new GameObject("Temporary lava animation preview");preview=go.AddComponent<Camera>();preview.enabled=false;preview.transform.position=new Vector3(42,97,-10);preview.transform.LookAt(new Vector3(42,5,59));preview.orthographic=true;preview.orthographicSize=50;preview.farClipPlane=250;preview.cullingMask=~(1<<5);preview.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            began=EditorApplication.timeSinceStartup;Application.logMessageReceived+=OnLog;EditorApplication.update+=Tick;
        }
        if(state==PlayModeStateChange.ExitingPlayMode)
        {EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Application.runInBackground=previousBackground;Time.timeScale=previousTimeScale;SessionState.EraseBool(Pending);}
    }
    private static void OnLog(string message,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=message;}
    private static void Tick()
    {
        if(preview==null||EditorApplication.timeSinceStartup-began<(phase==0?1:3.5))return;
        var rt=RenderTexture.GetTemporary(1200,960,24);var active=RenderTexture.active;var image=new Texture2D(1200,960,TextureFormat.RGB24,false);
        try
        {
            preview.targetTexture=rt;preview.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,960),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes("Logs/DemonLava_Animation_"+phase+".png",image.EncodeToPNG());
            if(phase==0){first=pixels;phase=1;return;}
            int changed=0,hot=0;for(int i=0;i<pixels.Length;i++)
            {var a=first[i];var b=pixels[i];if(b.r>b.g*1.2f&&b.r>110&&b.b<90){hot++;if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>24)changed++;}}
            var log=new StringBuilder("New lava animation in actual Play Mode "+DateTime.Now.ToString("s")+"\n");log.AppendLine((hot>500?"PASS":"FAIL")+" visible hot lava pixels="+hot);log.AppendLine((changed>300?"PASS":"FAIL")+" animated lava pixels changed="+changed);log.AppendLine((error==null?"PASS":"FAIL")+" runtime errors="+(error??"none"));File.WriteAllText("Logs/DemonLavaAnimation.txt",log.ToString());
            EditorApplication.update-=Tick;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        }
        catch(Exception ex)
        {File.WriteAllText("Logs/DemonLavaAnimation.txt","FAIL "+ex);EditorApplication.update-=Tick;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
        finally{preview.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}
#endif
