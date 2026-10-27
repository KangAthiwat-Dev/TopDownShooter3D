#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CameraViewPresetsSetup
{
    private const string RecordPath = "Assets/_Project/World/CameraViewPresetsRecord.json";
    [Serializable] private sealed class Record
    {
        public string backup;
        public CameraViewPreset retainedThirdPerson;
        public int preservedComponents;
    }
    [Serializable] private sealed class OldFixedCamera
    {
        public Vector3 cameraLocalPosition;
        public Quaternion cameraLocalRotation,rigRotation;
        public float fieldOfView;
    }
    private static void RequireScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }
    [MenuItem("Tools/Camera/View Presets/Install Top Down Default and Keep Third Person")]
    public static void Install()
    {
        RequireScene();
        var follow=UnityEngine.Object.FindFirstObjectByType<CameraFollow>();var camera=follow.FollowCamera;
        if(follow.GetComponent<CameraViewSettings>()!=null || File.Exists(RecordPath))throw new InvalidOperationException("View presets already installed; preserve authored settings.");
        var movement=UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();var f=new SerializedObject(follow);
        var third=new CameraViewPreset {
            localPosition=camera.transform.localPosition,localEulerAngles=camera.transform.localEulerAngles,
            rigEulerAngles=follow.transform.eulerAngles,targetOffset=f.FindProperty("targetOffset").vector3Value,
            fieldOfView=camera.fieldOfView,nearClipPlane=camera.nearClipPlane,
            followMovementHeading=follow.FollowsMovementHeading,
            alignBehindPlayerOnStart=f.FindProperty("alignBehindTargetOnStart").boolValue,
            cameraRelativeMovement=movement.UsesCameraRelativeMovement,
            avoidSolidObstacles=follow.GetComponent<CameraBoomCollision>().enabled
        };
        Component[] allowed={follow,follow.transform,camera,camera.transform,movement,follow.GetComponent<CameraBoomCollision>(),follow.GetComponent<CameraOcclusionFade>(),follow.GetComponent<CameraDashEffect>()};
        var components=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null&&!allowed.Contains(c)).ToArray();
        var before=components.ToDictionary(c=>c.GetEntityId().ToString(),c=>EditorJsonUtility.ToJson(c));
        Directory.CreateDirectory("Logs/SceneBackups");
        var record=new Record {backup="Logs/SceneBackups/WorldPrototype_BeforeViewPresets_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",retainedThirdPerson=third};
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),record.backup,true))throw new IOException("Could not back up the live scene.");
        Undo.RecordObjects(allowed.Where(c=>c!=null).Cast<UnityEngine.Object>().ToArray(),"Restore top-down default and retain third person");
        var settings=Undo.AddComponent<CameraViewSettings>(follow.gameObject);var config=new SerializedObject(settings);
        config.FindProperty("follow").objectReferenceValue=follow;config.FindProperty("movement").objectReferenceValue=movement;
        config.FindProperty("defaultView").enumValueIndex=0;config.ApplyModifiedProperties();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(third),settings.HighThirdPersonPreset);
        var original=JsonUtility.FromJson<OldFixedCamera>(File.ReadAllText("Assets/_Project/World/CameraOcclusion/FixedCameraRecord.json"));
        settings.TopDownPreset.localPosition=original.cameraLocalPosition;
        settings.TopDownPreset.localEulerAngles=original.cameraLocalRotation.eulerAngles;
        settings.TopDownPreset.rigEulerAngles=original.rigRotation.eulerAngles;
        settings.TopDownPreset.fieldOfView=original.fieldOfView;
        settings.ApplyView(CameraViewMode.TopDown,false);
        foreach(var component in allowed)if(component!=null)EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(settings);
        var changed=components.Where(c=>c==null||EditorJsonUtility.ToJson(c)!=before[c.GetEntityId().ToString()]).ToArray();
        if(changed.Length>0)throw new InvalidOperationException("Unexpected non-camera changes: "+string.Join(",",changed.Select(c=>c!=null?c.name:"destroyed")));
        record.preservedComponents=components.Length;File.WriteAllText(RecordPath,JsonUtility.ToJson(record,true));
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Validate();Selection.activeGameObject=follow.gameObject;
    }
    [MenuItem("Tools/Camera/View Presets/Validate Top Down Default")]
    public static void Validate()
    {
        RequireScene();var follow=UnityEngine.Object.FindFirstObjectByType<CameraFollow>();var camera=follow.FollowCamera;var settings=follow.GetComponent<CameraViewSettings>();
        var movement=UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();var log=new StringBuilder("Camera view presets "+DateTime.Now.ToString("s")+"\n");bool pass=true;
        void Check(bool value,string label){pass&=value;log.AppendLine((value?"PASS ":"FAIL ")+label);}
        Check(settings!=null&&settings.DefaultView==CameraViewMode.TopDown,"Top-down is the authored default");
        Check(!follow.FollowsMovementHeading&&!movement.UsesCameraRelativeMovement,"Fixed top-down yaw and original world-axis movement restored");
        Check(Vector3.Distance(camera.transform.localPosition,settings.TopDownPreset.localPosition)<.001f&&Quaternion.Angle(camera.transform.localRotation,Quaternion.Euler(settings.TopDownPreset.localEulerAngles))<.001f&&Mathf.Abs(camera.fieldOfView-45f)<.001f,"Original top-down camera offset, 50 degree pitch and 45 degree FOV");
        var record=JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath));
        Check(JsonUtility.ToJson(settings.HighThirdPersonPreset)==JsonUtility.ToJson(record.retainedThirdPerson),"Previous third-person pose and control/collision behaviour retained as a separate preset");
        Check(settings.HighThirdPersonPreset.cameraRelativeMovement&&settings.HighThirdPersonPreset.followMovementHeading&&settings.HighThirdPersonPreset.avoidSolidObstacles,"Third-person orbit, view-relative input and solid camera collision remain available");
        Check(!follow.GetComponent<CameraBoomCollision>().enabled,"Third-person collision does not zoom the fixed top-down view");
        Check(follow.GetComponent<CameraOcclusionFade>()==null||!follow.GetComponent<CameraOcclusionFade>().enabled,"Transparency disabled");
        Check(follow.GetComponent<CameraDashEffect>()!=null&&new SerializedObject(follow).FindProperty("enableRunningBob").boolValue,"Sprint bob and dash FOV retained");
        Check(File.Exists(record.backup)&&record.preservedComponents>0,"Fresh backup; "+record.preservedComponents+" non-camera components preserved");
        log.AppendLine(pass?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CameraViewPresetsValidation.txt",log.ToString());
        if(pass)Debug.Log(log.ToString());else Debug.LogError(log.ToString());
    }
    [MenuItem("Tools/Camera/View Presets/Preview Top Down in Play Mode")]
    private static void TopDown()=>UnityEngine.Object.FindFirstObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown,false);
    [MenuItem("Tools/Camera/View Presets/Preview Third Person in Play Mode")]
    private static void ThirdPerson()=>UnityEngine.Object.FindFirstObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.HighThirdPerson,false);
    [MenuItem("Tools/Camera/View Presets/Preview Top Down in Play Mode",true)]
    [MenuItem("Tools/Camera/View Presets/Preview Third Person in Play Mode",true)]
    private static bool CanPreview()=>EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<CameraViewSettings>()!=null;
}
#endif
