#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

public static partial class DemonCliffSetup
{
    private static readonly Dictionary<Material,Material> rockMaterials=new Dictionary<Material,Material>();
    private static GameObject Prop(string source,string name,Vector3 floor,Vector3 size,float yaw=0,bool collision=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Inferno+source);if(asset==null)throw new InvalidOperationException("Missing Inferno source: "+source);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,decorRoot);go.name=name;go.transform.rotation=Quaternion.Euler(0,yaw,0)*asset.transform.rotation;
        var b=BoundsOf(go);go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z));b=BoundsOf(go);go.transform.position+=floor-new Vector3(b.center.x,b.min.y,b.center.z);
        foreach(Transform t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=7;
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(mat=>
        {
            if(mat==null)return null;if(!rockMaterials.TryGetValue(mat,out var copy)){copy=new Material(mat){name="M_Volcanic_"+mat.name};if(copy.HasProperty("_BaseColor"))copy.SetColor("_BaseColor",copy.GetColor("_BaseColor")*new Color(.53f,.48f,.49f,1));AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Materials/"+copy.name+".mat"));rockMaterials.Add(mat,copy);}return copy;
        }).ToArray();
        if(collision){b=BoundsOf(go);var blocker=Group(name+"_Solid_Footprint",decorRoot);blocker.position=b.center;blocker.rotation=Quaternion.identity;var box=blocker.gameObject.AddComponent<BoxCollider>();box.size=new Vector3(b.size.x*.65f,b.size.y,b.size.z*.65f);blocker.gameObject.layer=7;}
        return go;
    }
    private static void MakeLavaAndScenery()
    {
        rockMaterials.Clear();var lavaGroup=Group("04_Lava_Basin_And_NotWalkable_Mask",buildRoot);
        var lava=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/ithappy/Inferno_World_Free/Materials/Lava.mat")){name="M_Demon_Lava"};AssetDatabase.CreateAsset(lava,Folder+"/Materials/M_Demon_Lava.mat");
        MeshObject("Lava_Low_Basin",lavaGroup,Basin.Select(p=>P(p.x,4.25f,p.y)).ToList(),Triangulate(Basin),lava,false);
        for(float z=30;z<98;z+=6)for(float x=11;x<97;x+=6)
        {
            if(!Inside(V(x+3,z+3),Basin)||x<27&&z<58&&z>40)continue;
            var go=Group("Lava_NotWalkable_"+x+"_"+z,lavaGroup).gameObject;go.transform.position=P(x+3,2,z+3);var volume=go.AddComponent<NavMeshModifierVolume>();volume.size=new Vector3(6,14.4f,6);volume.area=1;
        }
        // Only low shoreline rocks block entry into the lava; the west ramp is open.
        var bankVertices=new List<Vector3>();var bankTriangles=new List<int>();
        for(int i=0;i<Basin.Length;i++)
        {
            var a=Basin[i];var b=Basin[(i+1)%Basin.Length];int count=Mathf.CeilToInt(Vector2.Distance(a,b)/2);
            for(int j=0;j<count;j++)
            {
                var p=Vector2.Lerp(a,b,(float)j/count);var q=Vector2.Lerp(a,b,(float)(j+1)/count);var mid=(p+q)*.5f;if(mid.x<16&&mid.y>43&&mid.y<56)continue;
                var pa=P(p.x,Ground(p.x,p.y)+.02f,p.y);var pb=P(q.x,Ground(q.x,q.y)+.02f,q.y);int v=rimVertices.Count;int tr=rimTriangles.Count;int bankStart=bankVertices.Count;Guard("Shoreline_Bank_"+i+"_"+j,pa,pb);
                bankVertices.AddRange(rimVertices.Skip(v));bankTriangles.AddRange(rimTriangles.Skip(tr).Select(t=>t-v+bankStart));
            }
        }
        if(bankVertices.Count>0)MeshObject("Shoreline_Basalt_Retaining_Edge",decorRoot,bankVertices,bankTriangles,rimMaterial,false);
        // Scale source models from their measured bounds; native cliff prefabs are hundreds of metres wide.
        var rocks=new[]{
            new[]{21f,3.8f,40f,9f,8f,9f},new[]{22f,3.8f,59f,8f,8f,9f},new[]{34f,3.8f,63f,11f,8f,8f},
            new[]{49f,3.8f,54f,8f,8f,10f},new[]{56f,3.8f,45f,7f,13f,9f},new[]{59f,3.8f,64f,8f,13f,7f},
            new[]{88f,3.8f,58f,8f,13f,10f},new[]{77f,3.8f,65f,9f,13f,7f},new[]{44f,3.8f,76f,8f,18f,9f},
            new[]{44f,3.8f,88f,8f,20f,11f},new[]{82f,3.8f,82f,8f,19f,11f},new[]{78f,3.8f,96f,11f,23f,8f},
            new[]{48f,3.8f,96f,9f,23f,8f},new[]{89f,3.8f,91f,9f,25f,10f},new[]{93f,3.8f,74f,7f,20f,10f}};
        for(int i=0;i<rocks.Length;i++){var r=rocks[i];Prop("Mountains/Cliff_00"+(3+i%3)+".prefab","Basalt_Outcrop_"+i,P(r[0],r[1],r[2]),P(r[3],r[4],r[5]),i*67);}
        // Broken ritual pillars frame each clear combat space, without occupying the travel line.
        var columns=new[]{P(24,9.8f,43),P(43,9.8f,56),P(58,14.8f,49),P(82,14.8f,57),P(49,20.1f,80),P(78,20.1f,83)};
        for(int i=0;i<columns.Length;i++)Prop("Decorations/ColumnBigBroken_00"+(1+i%2)+".prefab","Ruined_Pillar_"+i,columns[i],P(1.8f,3.1f+i%2,1.8f),i*49,true);
        var bones=new[]{P(28,9.8f,57),P(46,9.8f,46),P(62,14.8f,43),P(83,14.8f,61),P(51,20.1f,88),P(77,20.1f,88)};
        for(int i=0;i<bones.Length;i++)Prop("Decorations/Bone_00"+(3+i%2)+".prefab","Scattered_Bones_"+i,bones[i]+Vector3.up*.04f,P(1.5f,.35f,.75f),i*73);
        // Emissive fissures are decoration only, set back from the main travel lines.
        var fissureVertices=new List<Vector3>();var fissureTriangles=new List<int>();
        for(int stage=0;stage<3;stage++)
        {
            var center=stage==0?P(34,9.83f,49):stage==1?P(70,14.83f,54):P(65,20.13f,81);
            for(int i=0;i<3;i++)
            {
                var a=center+P(-6+i*4,0,4);var points=new[]{a,a+P(.55f,0,.3f),a+P(.92f,0,.08f),a+P(1.15f,0,.7f),a+P(1.8f,0,1.15f)};
                for(int j=1;j<points.Length;j++){var side=Vector3.Cross(Vector3.up,points[j]-points[j-1]).normalized*.027f;Quad(fissureVertices,fissureTriangles,points[j-1]-side,points[j]-side,points[j]+side,points[j-1]+side);}
            }
        }
        MeshObject("Subtle_Ember_Stone_Cracks",decorRoot,fissureVertices,fissureTriangles,glowMaterial,false);
    }
    private static void MakeAtmosphere()
    {
        var group=Group("06_Local_Atmosphere_Audio_And_Braziers",buildRoot);var systems=new List<ParticleSystem>();
        const string fire="Assets/Vefects/Flipbook VFX Bundle Lite URP/Flipbook VFX URP/_ Lite/Prefabs/Lite_VFX_Fire_Loop_01.prefab";
        var positions=new[]{P(18,9.8f,45),P(18,9.8f,54),P(49,9.8f,38),P(78,9.8f,29),P(80,14.8f,49),P(88,14.8f,50),P(56,20.1f,75),P(65,20.1f,75),P(57,20.1f,88),P(73,20.1f,88)};
        for(int i=0;i<positions.Length;i++)
        {
            Prop("Props/Brazier_002.prefab","Brazier_"+i,positions[i],P(1,1.45f,1),i*29,true);
            var flame=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fire),group);flame.name="Fire_Brazier_"+i;flame.transform.position=positions[i]+Vector3.up*1.3f;flame.transform.localScale=Vector3.one*.48f;
            foreach(var audio in flame.GetComponentsInChildren<AudioSource>())audio.enabled=false;
            systems.AddRange(flame.GetComponentsInChildren<ParticleSystem>(true));
            if(i==1||i==5||i==9){var light=Marker("Warm_AccentLight_"+i,group,positions[i]+Vector3.up*1.8f).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.28f,.035f);light.intensity=1.6f;light.range=7;light.shadows=LightShadows.None;}
        }
        var smokeMat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name="M_Local_Smoke"};smokeMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ithappy/Inferno_World_Free/VFX/FX_Textures/FX_Smoke.png"));smokeMat.SetFloat("_Surface",1);smokeMat.SetFloat("_Blend",0);smokeMat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);smokeMat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);smokeMat.SetFloat("_ZWrite",0);smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");smokeMat.renderQueue=3000;AssetDatabase.CreateAsset(smokeMat,Folder+"/Materials/M_Local_Smoke.mat");
        foreach(var p in new[]{P(28,4.5f,34),P(54,4.5f,68),P(92,4.5f,64),P(37,4.5f,88)})
        {
            var go=Marker("Lava_Sparse_Smoke",group,p).gameObject;var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.startLifetime=6;main.startSpeed=.9f;main.startSize=new ParticleSystem.MinMaxCurve(2,4);main.startColor=new Color(.17f,.13f,.16f,.16f);main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=false;var emission=ps.emission;emission.rateOverTime=1.6f;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=1.5f;shape.angle=12;go.transform.rotation=Quaternion.Euler(-90,0,0);go.GetComponent<ParticleSystemRenderer>().sharedMaterial=smokeMat;systems.Add(ps);
        }
        var audioSource=Marker("Lava_Rumble_Local3D",group,P(61,8,61)).gameObject.AddComponent<AudioSource>();audioSource.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Vefects/Flipbook VFX Bundle Lite URP/Flipbook VFX URP/Audio/WAV/Vefects_SFX_Fire_Big_Loop.wav");audioSource.playOnAwake=false;audioSource.loop=true;audioSource.volume=0;audioSource.spatialBlend=1;audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=12;audioSource.maxDistance=65;audioSource.dopplerLevel=0;
        var atmosphere=group.gameObject.AddComponent<DemonCliffAtmosphere>();var serialized=new SerializedObject(atmosphere);var array=serialized.FindProperty("particles");array.arraySize=systems.Count;for(int i=0;i<systems.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=systems[i];serialized.FindProperty("lavaAmbience").objectReferenceValue=audioSource;serialized.ApplyModifiedPropertiesWithoutUndo();
        var volumeGo=Marker("DemonCliffs_Local_Grading",group,P(57,15,64)).gameObject;var box=volumeGo.AddComponent<BoxCollider>();box.isTrigger=true;box.size=P(80,65,72);var volume=volumeGo.AddComponent<Volume>();volume.isGlobal=false;volume.priority=20;volume.blendDistance=7;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();var colors=profile.Add<ColorAdjustments>(true);colors.postExposure.Override(-.28f);colors.contrast.Override(14);colors.saturation.Override(-22);colors.colorFilter.Override(new Color(.94f,.89f,.94f));AssetDatabase.CreateAsset(profile,Folder+"/Materials/DemonCliffsLook.asset");volume.sharedProfile=profile;
    }
}
#endif
