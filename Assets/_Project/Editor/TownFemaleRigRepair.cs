#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TownFemaleRigRepair
{
    private const string PrivateRig=TownNpcDiversitySetup.Folder+"/Sources/Female_Peasant_Rig.fbx";
    public static SkeletonBone[] ReferencePose(GameObject source, HumanBone[] mapping)
    {
        // Use the installed Editor's own Enforce T-Pose operation, including its
        // character-facing correction and skeleton parent names. Asset bind bones
        // alone are not a calibrated Humanoid reference pose.
        var tool=typeof(ModelImporter).Assembly.GetType("UnityEditor.AvatarSetupTool",true);
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
            var modelBones=tool.GetMethod("GetModelBones",flags).Invoke(null,new object[]{instance.transform,true,null});
            var getHuman=tool.GetMethods(flags).Single(m=>m.Name=="GetHumanBones"&&m.GetParameters()[0].ParameterType==typeof(System.Collections.Generic.Dictionary<string,string>));
            var human=getHuman.Invoke(null,new[]{(object)mapping.ToDictionary(b=>b.humanName,b=>b.boneName),modelBones});
            tool.GetMethod("MakePoseValid",flags).Invoke(null,new[]{human});
            return (SkeletonBone[])tool.GetMethod("GetSkeletonBones",flags).Invoke(null,new object[]{instance.transform});
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/NPC/Women and Builders/Repair Female Reference Pose")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Repair in Edit Mode.");
        var importer=(ModelImporter)AssetImporter.GetAtPath(PrivateRig);
        var description=importer.humanDescription;
        description.skeleton=ReferencePose(AssetDatabase.LoadAssetAtPath<GameObject>(PrivateRig),description.human);
        importer.humanDescription=description;importer.SaveAndReimport();
        foreach(var guid in AssetDatabase.FindAssets("Citizen_Female_ t:Prefab",new[]{TownNpcDiversitySetup.Folder+"/Prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator=root.GetComponentInChildren<Animator>();var model=animator.transform;
                animator.avatar=AssetDatabase.LoadAssetAtPath<GameObject>(PrivateRig).GetComponent<Animator>().avatar;
                model.localScale=Vector3.one;model.localPosition=Vector3.zero;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Play("Locomotion",0,.15f);animator.Update(0);
                var bounds=NpcMerchantSetup.GeometryBounds(animator.gameObject);model.localScale*=1.78f/bounds.size.y;
                bounds=NpcMerchantSetup.GeometryBounds(animator.gameObject);model.position-=Vector3.up*bounds.min.y;
                if(animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y>=animator.GetBoneTransform(HumanBodyBones.Head).position.y-.2f)
                    throw new InvalidOperationException("Female reference pose still has raised idle arms.");
                animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();Inspect();Debug.Log("Female reference pose calibrated and all three prefabs grounded.");
    }
    [MenuItem("Tools/NPC/Women and Builders/Inspect Female Reference Pose")]
    public static void Inspect()
    {
        var report=new StringBuilder();var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var path in new[]{TownNpcDiversitySetup.FemaleBody,PrivateRig,NpcMerchantSetup.Folder+"/Sources/Merchant_Peasant_Rig.fbx"})
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
                report.AppendLine(path);
                foreach(var bone in model.GetComponentsInChildren<Transform>().Where(t=>new[]{"pelvis","Head","clavicle_l","upperarm_l","lowerarm_l","hand_l","upperarm_r","lowerarm_r","hand_r"}.Contains(t.name)))
                    report.AppendLine(bone.name+" position="+bone.position.ToString("F4")+" localRot="+bone.localEulerAngles.ToString("F2"));
                UnityEngine.Object.DestroyImmediate(model);
            }
            foreach(var path in new[]{TownNpcDiversitySetup.Folder+"/Prefabs/Citizen_Female_Teal_Long.prefab",TownNpcSetup.Folder+"/Prefabs/Citizen_Russet.prefab"})
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);var animator=model.GetComponentInChildren<Animator>();
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Play("Locomotion",0,.1f);animator.Update(0);
                report.AppendLine("IDLE "+path);
                foreach(var bone in new[]{HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})report.AppendLine(bone+" "+animator.GetBoneTransform(bone).position.ToString("F4"));
                UnityEngine.Object.DestroyImmediate(model);
            }
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TownFemaleReferencePose.txt",report.ToString());Debug.Log("Female reference pose audit saved.");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
#endif
