#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class StoryCreatureSetup
{
    private static Material WizardMaterial(string sourceName,Color tint)
    {
        string path="Assets/_Project/Story/Materials/Wizard_"+sourceName+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/PlayerHuman/M_PlayerHuman_"+sourceName+".mat")){name="Wizard_"+sourceName};
        material.SetColor("_BaseColor",tint);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.12f);
        AssetDatabase.CreateAsset(material,path);return material;
    }
    private static Material StaffMaterial(string name,Color color,bool glow=false)
    {
        string path="Assets/_Project/Story/Materials/Wizard_Staff_"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Wizard Staff "+name};material.color=color;material.SetFloat("_Smoothness",.25f);
        if(glow){material.SetColor("_EmissionColor",color*1.2f);material.EnableKeyword("_EMISSION");}
        AssetDatabase.CreateAsset(material,path);return material;
    }
    private static void StaffPiece(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var piece=GameObject.CreatePrimitive(type);piece.name=name;piece.transform.SetParent(parent,false);piece.transform.localPosition=position;piece.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());piece.GetComponent<Renderer>().sharedMaterial=material;
    }
    private static void BuildWizard()
    {
        if(UnityEngine.Object.FindObjectsByType<StoryActor>(FindObjectsSortMode.None).Any(a=>a.ActorId=="elder_wizard"))return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefab);
        if(prefab==null)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Story/NPCs/NPC_Oakshade_Guard.prefab"));root.name="NPC_ElderWizard_Placeholder";
            try
            {
                if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var animator=root.GetComponentInChildren<Animator>();animator.gameObject.name="Elder Wizard Visual (replaceable)";
                foreach(var node in animator.GetComponentsInChildren<Transform>(true))
                {
                    if(node.name=="Beards"||node.name=="Hairs")node.gameObject.SetActive(true);
                    if(node.parent!=null&&node.parent.name=="Beards")node.gameObject.SetActive(node.name=="Beard3");
                    if(node.parent!=null&&node.parent.name=="Hairs")node.gameObject.SetActive(node.name=="Hair2");
                    if(node.name=="Starter_Shoulders"||node.name=="Starter_Gloves"||node.name=="Starter_Helmet")node.gameObject.SetActive(false);
                }
                foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if(skin.name.StartsWith("Starter_"))skin.sharedMaterial=WizardMaterial("StarterArmor_1",new Color(.46f,.35f,.64f));
                    else if(skin.name=="Neck"||skin.name=="Arms"||skin.name=="Hands")skin.sharedMaterial=WizardMaterial("Body",new Color(1,.77f,.62f));
                    else if(skin.name=="Face1")skin.sharedMaterial=WizardMaterial("Face1",new Color(1,.77f,.62f));
                    else if(skin.name=="Hair2"||skin.name=="Beard3"||skin.name=="Eyebrow1")skin.sharedMaterial=WizardMaterial(skin.name=="Eyebrow1"?"Eyebrows":skin.name,new Color(.85f,.85f,.8f));
                }
                animator.Rebind();animator.Play("Locomotion",0,0);animator.Update(0);
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var staff=new GameObject("Temporary Wizard Staff").transform;staff.SetParent(root.transform,false);
                // Author upright at the sampled idle pose, then bind to the existing hand.
                staff.position=new Vector3(hand.position.x,.02f,hand.position.z+.035f);
                var wood=StaffMaterial("Wood",new Color(.19f,.1f,.055f));var brass=StaffMaterial("Brass",new Color(.56f,.36f,.11f));var orb=StaffMaterial("Orb",new Color(.2f,.65f,1),true);
                StaffPiece(staff,"Wooden Shaft",PrimitiveType.Cylinder,new Vector3(0,.88f,0),new Vector3(.045f,.88f,.045f),wood);
                StaffPiece(staff,"Brass Collar",PrimitiveType.Cylinder,new Vector3(0,1.73f,0),new Vector3(.1f,.07f,.1f),brass);
                StaffPiece(staff,"Arcane Orb",PrimitiveType.Sphere,new Vector3(0,1.91f,0),Vector3.one*.22f,orb);
                staff.SetParent(hand,true);
                Actor(root,"elder_wizard","Elder Wizard","Temporary opening-story wizard; visual can be replaced later");
                var look=root.GetComponent<NpcIdleLook>();Text(look,"npcId","elder_wizard_01");Text(look,"displayName","Elder Wizard");
                PrefabUtility.SaveAsPrefabAsset(root,WizardPrefab);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefab);
        }
        var sceneWizard=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var layout=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var spawn=UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>();
        Vector3 center=spawn!=null?spawn.Position:player.transform.position;
        if(Vector3.Distance(center,layout.sanctuary.position)>28)center=layout.sanctuary.position;
        bool found=false;
        for(int i=0;i<12;i++)
        {
            var desired=center+Quaternion.Euler(0,i*30,0)*Vector3.forward*4.5f;
            if(!NavMesh.SamplePosition(desired,out var hit,3,NavMesh.AllAreas)||Physics.CheckCapsule(hit.position+Vector3.up*.45f,hit.position+Vector3.up*1.5f,.37f,1<<7,QueryTriggerInteraction.Ignore))continue;
            sceneWizard.transform.position=hit.position;found=true;break;
        }
        if(!found){UnityEngine.Object.DestroyImmediate(sceneWizard);throw new InvalidOperationException("No clear wizard standing position near the summoning ruins.");}
        sceneWizard.transform.SetParent(layout.sanctuary,true);
        var direction=Vector3.ProjectOnPlane(center-sceneWizard.transform.position,Vector3.up);if(direction.sqrMagnitude>.01f)sceneWizard.transform.rotation=Quaternion.LookRotation(direction);
        var watching=sceneWizard.GetComponent<NpcIdleLook>();watching.SetLookTarget(player.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(watching);
    }
}
#endif
