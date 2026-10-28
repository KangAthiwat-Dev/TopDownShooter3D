#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static partial class AudioOptionsSetup
{
    [MenuItem("Tools/Audio Options/Inspect Mixer Authoring API")]
    public static void InspectMixer()
    {
        var report=new StringBuilder();
        foreach(var name in new[]{"UnityEditor.Audio.AudioMixerController","UnityEditor.Audio.AudioMixerGroupController","UnityEditor.Audio.AudioMixerSnapshotController"})
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
            report.AppendLine(name+" = "+type);
            if(type==null)continue;
            foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m=>new[]{"Creat","Expos","Param","Child","Volume","Snapshot","Group"}.Any(s=>m.Name.Contains(s))))report.AppendLine(m.ToString());
            foreach(var p in type.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))report.AppendLine("PROPERTY "+p.Name+" "+p.PropertyType);
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AudioMixerAuthoring.txt",report.ToString());Debug.Log("Mixer authoring API inspected.");
    }
}
#endif
