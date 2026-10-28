#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerSkillsSetup
{
    public const string Folder="Assets/_Project/CombatSkills";
    private const string Combat="Assets/Vefects/Flipbook VFX Bundle Lite URP/Combat Flipbook VFX URP/_ Lite/Particles/";
    private static TMP_FontAsset font;
    private static readonly Color Ink=new Color(.22f,.13f,.065f);
    private static readonly StringBuilder report=new StringBuilder();
    private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);}
    private static void List(UnityEngine.Object owner,string field,UnityEngine.Object[] values)
    {var s=new SerializedObject(owner);var a=s.FindProperty(field);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    private static Sprite Art(string name)=>AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/"+name+".png").OfType<Sprite>().First();
    [MenuItem("Tools/Player Skills/Install Thai Skills and Combat Effects")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype.");
        if(UnityEngine.Object.FindAnyObjectByType<PlayerCombatSkills>()!=null)throw new InvalidOperationException("Skills are already installed. Keep Inspector adjustments.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeCombatSkills_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();report.Clear();report.AppendLine("Thai combat skills installation "+DateTime.Now.ToString("s"));
        var explosion=Burst(Combat+"Lite_VFX_Explosion_02_Pivot_Loop.prefab","Arcane Explosion",1,3.8f);
        var thunder=Burst(Combat+"Lite_VFX_Electric_Impact_01_Random_Rotate_Bunch_Loop_CV_02.prefab","Thunder Impact",1,2.4f);
        var critical=Burst(RangedVfxSetup.ImpactPath,"Critical Impact",1,1.1f);
        var slash=Burst("Assets/Assets_Imported/Free Slash VFX/Prefabs/Slash VFX.prefab","Sword Slash",.7f,0);
        var heal=Burst("Assets/Vefects/Flipbook VFX Bundle Lite URP/Flipbook VFX URP/_ Lite/Prefabs/Lite_VFX_Charge_02.prefab","Life Steal",1,1.5f);
        var pierced=Projectile(false,explosion);var bomb=Projectile(true,explosion);
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();var skills=Undo.AddComponent<PlayerCombatSkills>(player.gameObject);
        var feedback=Undo.AddComponent<PlayerSkillFeedback>(player.gameObject);Set(feedback,"swordSlash",slash);Set(feedback,"criticalHit",critical);Set(feedback,"thunderBurst",thunder);Set(feedback,"healBurst",heal);
        var weapon=player.GetComponent<PlayerRangedWeapon>();Set(weapon,"piercingProjectile",pierced);Set(weapon,"explosiveProjectile",bomb);
        var state=player.GetComponent<PlayerProgressionState>();var stateData=new SerializedObject(state);var ranks=stateData.FindProperty("upgradeRanks");int oldSize=ranks.arraySize;ranks.arraySize=PlayerProgressionState.UpgradeCount;
        for(int i=oldSize;i<ranks.arraySize;i++)ranks.GetArrayElementAtIndex(i).intValue=0;stateData.ApplyModifiedPropertiesWithoutUndo();
        BuildUI(player);
        RefineCombatArt();UseThaiWeaponLabel();
        foreach(var c in new Component[]{skills,feedback,weapon,state}){EditorUtility.SetDirty(c);if(PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        report.AppendLine("Existing five upgrade IDs and levels are preserved. New progression save v3 reads v1/v2.");
        report.AppendLine("Rapid Fire reuses FireRate. Arcane Shield reuses Vitality resistance. New skill sounds are intentionally absent.");
        File.WriteAllText("Logs/PlayerSkillsInstallation.txt",report.ToString());Debug.Log(report.ToString());
    }
    private static RangedBurstVfx Burst(string sourcePath,string name,float scale,float size)
    {
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);if(source==null)throw new InvalidOperationException("Missing VFX "+sourcePath);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source,preview);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.name=name;root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one*scale;
            Clean(root);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer is ParticleSystemRenderer psr&&psr.renderMode==ParticleSystemRenderMode.None)continue;
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null||material.shader==null||ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Invalid material in "+name+": "+renderer.name);
                    report.AppendLine(name+" material="+material.name+" shader="+material.shader.name);
                }
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            var systems=root.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var p in systems)
            {
                p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;
                main.loop=false;main.prewarm=false;main.playOnAwake=false;main.useUnscaledTime=false;main.stopAction=ParticleSystemStopAction.None;
                main.startDelay=0;main.duration=.6f;main.startLifetime=name=="Arcane Explosion"?.6f:Mathf.Min(.65f,Mathf.Max(.2f,main.startLifetime.constantMax));main.maxParticles=Mathf.Min(64,Mathf.Max(8,main.maxParticles));
                if(size>0)main.startSize=size;
                var emission=p.emission;emission.rateOverTime=0;emission.rateOverDistance=0;
                var renderer=p.GetComponent<ParticleSystemRenderer>();
                if(renderer!=null&&renderer.renderMode!=ParticleSystemRenderMode.None)
                {
                    if(emission.burstCount==0)emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                    emission.enabled=true;
                }
            }
            var burst=root.AddComponent<RangedBurstVfx>();List(burst,"particles",systems);
            var settings=new SerializedObject(burst);settings.FindProperty("lifetime").floatValue=1.5f;settings.ApplyModifiedPropertiesWithoutUndo();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name.Replace(" ","")+".prefab");return prefab.GetComponent<RangedBurstVfx>();
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    private static void Clean(GameObject root)
    {
        // Keep the imported visual geometry/particles, remove demo behaviours and all gameplay/audio.
        foreach(var mono in root.GetComponentsInChildren<MonoBehaviour>(true))if(mono!=null)UnityEngine.Object.DestroyImmediate(mono);
        foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<AudioSource>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(c);
    }
    private static Material Glow(string name,Color color,bool sphere=false)
    {
        var material=new Material(Shader.Find(sphere?"Universal Render Pipeline/Unlit":"TopDownShooter/VFX/Energy Tracer URP")){name=name};
        material.SetColor(sphere?"_BaseColor":"_Color",color);AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");return material;
    }
    private static Projectile Projectile(bool explosive,RangedBurstVfx explosion)
    {
        var root=PrefabUtility.LoadPrefabContents(RangedVfxSetup.ProjectilePath);
        try
        {
            root.name=explosive?"Arcane Explosive Orb":"Arcane Piercing Tracer";
            var trail=root.GetComponent<TrailRenderer>();trail.Clear();
            var core=Glow(explosive?"OrbTrail":"PiercingCore",explosive?new Color(3.2f,1.2f,.2f,1):new Color(.3f,1.7f,3.2f,1));
            foreach(var line in root.GetComponentsInChildren<LineRenderer>(true))line.sharedMaterial=core;
            trail.sharedMaterial=core;trail.widthMultiplier=explosive?.09f:.035f;trail.time=explosive?.12f:.06f;
            if(explosive)
            {
                foreach(var line in root.GetComponentsInChildren<LineRenderer>(true))UnityEngine.Object.DestroyImmediate(line.gameObject);
                var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);SceneManager.MoveGameObjectToScene(ball,root.scene);ball.name="Glowing Arcane Core";ball.transform.SetParent(root.transform,false);ball.transform.localScale=Vector3.one*.22f;UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.GetComponent<Renderer>().sharedMaterial=Glow("ArcaneOrbCore",new Color(3,1.4f,.25f),true);
                const string source="Assets/Vefects/Flipbook VFX Bundle Lite URP/Pixel Craft VFX URP/_ Lite/Prefabs/Lite_VFX_Fireball_Projectile_01_Color_Loop_Static.prefab";
                var fire=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),root.scene);
                PrefabUtility.UnpackPrefabInstance(fire,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);Clean(fire);fire.name="Fireball Envelope";fire.transform.SetParent(root.transform,false);fire.transform.localPosition=Vector3.zero;fire.transform.localRotation=Quaternion.identity;fire.transform.localScale=Vector3.one*.25f;
                foreach(var p in fire.GetComponentsInChildren<ParticleSystem>(true)){var main=p.main;main.playOnAwake=true;main.loop=true;main.prewarm=false;main.useUnscaledTime=false;main.maxParticles=Mathf.Min(main.maxParticles,32);}
            }
            var projectile=root.GetComponent<Projectile>();Set(projectile,"explosionVfx",explosion);
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+(explosive?"ExplosiveOrb":"PiercingTracer")+".prefab").GetComponent<Projectile>();
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static TMP_Text Text(string name,Transform parent,float x,float y,float w,float h,string value,float size)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=value;t.color=Ink;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;
    }
    private static Button Button(string name,Transform parent,float x,float y,float w,float h,string value)
    {
        var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=Art("tooltip_frame");image.type=Image.Type.Sliced;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var t=Text("Label",r,8,4,w-16,h-8,value,24);t.alignment=TextAlignmentOptions.Midline;
        var c=b.colors;c.highlightedColor=new Color(1,.86f,.45f);c.pressedColor=new Color(.87f,.69f,.35f);c.disabledColor=new Color(.64f,.6f,.51f);b.colors=c;return b;
    }
    private static void BuildUI(PlayerMovement player)
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        var ui=UnityEngine.Object.FindAnyObjectByType<PlayerUpgradePanelUI>();var settings=new SerializedObject(ui);
        var previous=(GameObject)settings.FindProperty("window").objectReferenceValue;
        var root=ui.transform;UnityEngine.Object.DestroyImmediate(previous);
        var window=Rect("Thai Skill Window",root,0,0,0,0);window.anchorMin=Vector2.zero;window.anchorMax=Vector2.one;window.offsetMin=window.offsetMax=Vector2.zero;
        var shade=window.gameObject.AddComponent<Image>();shade.color=new Color(.02f,.015f,.01f,.76f);
        var panel=Rect("Character Skills",window,0,0,1450,970);panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one*.5f;panel.anchoredPosition=Vector2.zero;
        var paper=panel.gameObject.AddComponent<Image>();paper.sprite=Art("dialogue_box_frame");paper.type=Image.Type.Sliced;paper.raycastTarget=false;
        Text("Title",panel,62,36,680,56,"พัฒนาผู้เล่นและสกิล",34).fontStyle=FontStyles.Bold;
        var points=Text("Points",panel,710,47,475,42,"",25);points.alignment=TextAlignmentOptions.MidlineRight;
        var close=Button("Close Skills",panel,1220,40,165,48,"ปิด  [Esc]");
        var basics=Button("Basic Stats",panel,65,112,650,58,"พลังพื้นฐาน");var combat=Button("Combat Skills",panel,735,112,650,58,"สกิลต่อสู้");
        int[] order={5,6,7,8,9,10,4,0,1,2,3};var buttons=new Button[11];var ranks=new TMP_Text[11];var previews=new TMP_Text[11];var cards=new GameObject[11];
        for(int n=0;n<order.Length;n++)
        {
            int id=order[n],index=n<8?n:n-8;var r=Rect(PlayerCombatSkills.Name((PlayerUpgrade)id),panel,65+index%2*670,193+index/2*160,650,150);cards[id]=r.gameObject;
            var background=r.gameObject.AddComponent<Image>();background.color=new Color(.28f,.15f,.05f,.13f);background.raycastTarget=false;
            Text("Skill Name",r,18,10,450,37,PlayerCombatSkills.Name((PlayerUpgrade)id),27).fontStyle=FontStyles.Bold;
            ranks[id]=Text("Rank",r,497,12,128,32,"0 / 3",24);ranks[id].alignment=TextAlignmentOptions.MidlineRight;
            previews[id]=Text("Current and Next",r,18,53,520,90,"",22);
            buttons[id]=Button("Spend One Point",r,558,65,74,62,"+1");
        }
        var summary=Text("Current Stats",panel,65,852,1320,36,"",23);
        Text("Skill Help",panel,65,906,1320,32,"1 แต้มต่อขั้น · ผสมสกิลได้ · กระสุนทะลุ+ระเบิดจะแตกเมื่อถึงเป้าสุดท้ายหรือชนฉาก",21);
        Set(ui,"window",window.gameObject);Set(ui,"closeButton",close);Set(ui,"pointsLabel",points);Set(ui,"summary",summary);Set(ui,"basicsTab",basics);Set(ui,"combatTab",combat);
        List(ui,"upgradeButtons",buttons);List(ui,"rankLabels",ranks);List(ui,"previews",previews);List(ui,"cards",cards);
        var open=(TMP_Text)new SerializedObject(ui).FindProperty("openLabel").objectReferenceValue;open.font=font;open.fontSize=22;open.text="[C] พัฒนาสกิล";EditorUtility.SetDirty(open);
        for(int i=0;i<cards.Length;i++)cards[i].SetActive(i!=1&&i!=2&&i!=3);window.gameObject.SetActive(false);EditorUtility.SetDirty(ui);
    }
    [MenuItem("Tools/Player Skills/Refine Skill Presentation")]
    public static void RefinePresentation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var root=PrefabUtility.LoadPrefabContents(Folder+"/ArcaneExplosion.prefab");
        try
        {foreach(var p in root.GetComponentsInChildren<ParticleSystem>(true)){p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;main.startLifetime=.6f;}PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ArcaneExplosion.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var ui=UnityEngine.Object.FindAnyObjectByType<PlayerUpgradePanelUI>();var data=new SerializedObject(ui);var cards=data.FindProperty("cards");
        int[] order={5,6,7,8,9,10,4,0,1,2,3};
        for(int n=0;n<order.Length;n++){var r=((GameObject)cards.GetArrayElementAtIndex(order[n]).objectReferenceValue).GetComponent<RectTransform>();int i=n<8?n:n-8;r.anchoredPosition=new Vector2(65+i%2*670,-193-i/2*160);r.sizeDelta=new Vector2(650,150);EditorUtility.SetDirty(r);}
        foreach(var t in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if(t.name=="Current Stats")t.rectTransform.anchoredPosition=new Vector2(65,-852);
            if(t.name=="Skill Help"){t.rectTransform.anchoredPosition=new Vector2(65,-906);t.text="1 แต้มต่อขั้น · ผสมสกิลได้ · กระสุนทะลุ+ระเบิดจะแตกเมื่อถึงเป้าสุดท้ายหรือชนฉาก";}
            EditorUtility.SetDirty(t);
        }
        RefineCombatArt();
        UseThaiWeaponLabel();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    private static Material WarmMaterial(Material source,string name,bool explosion)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(source){name=name};AssetDatabase.CreateAsset(m,path);}
        foreach(var p in new[]{("_R",new Color(3,1.3f,.2f,1)),("_G",new Color(1.8f,.35f,.03f,1)),("_B",new Color(.65f,.07f,.015f,1)),("_Outline",new Color(.4f,.045f,.01f,1))})
            if(m.HasProperty(p.Item1))m.SetColor(p.Item1,p.Item2);
        if(m.HasProperty("_Emissive"))m.SetFloat("_Emissive",explosion?1.2f:1.5f);if(m.HasProperty("_Cull"))m.SetFloat("_Cull",0);
        if(m.HasProperty("_ZTest"))m.SetFloat("_ZTest",4);EditorUtility.SetDirty(m);return m;
    }
    private static void UseThaiWeaponLabel()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<AdventureHudUI>();if(hud==null)return;
        var label=(TMP_Text)new SerializedObject(hud).FindProperty("weaponName").objectReferenceValue;
        if(label==null)return;label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=25;EditorUtility.SetDirty(label);
    }
    private static void RefineCombatArt()
    {
        var root=PrefabUtility.LoadPrefabContents(Folder+"/ArcaneExplosion.prefab");
        try
        {
            foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))if(r.renderMode!=ParticleSystemRenderMode.None&&r.sharedMaterial!=null)
                r.sharedMaterial=WarmMaterial(r.sharedMaterial,"ArcaneExplosionFire",true);
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ArcaneExplosion.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        root=PrefabUtility.LoadPrefabContents(Folder+"/ExplosiveOrb.prefab");
        try
        {
            var envelope=root.transform.Find("Fireball Envelope");
            if(envelope!=null)foreach(var p in envelope.GetComponentsInChildren<ParticleSystem>(true))
            {
                p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                main.startSize=.8f;main.startLifetime=.15f;main.startSpeed=0;main.maxParticles=8;var shape=p.shape;shape.enabled=false;
                var e=p.emission;e.rateOverTime=12;e.rateOverDistance=0;e.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                var r=p.GetComponent<ParticleSystemRenderer>();if(r!=null&&r.renderMode!=ParticleSystemRenderMode.None&&r.sharedMaterial!=null)r.sharedMaterial=WarmMaterial(r.sharedMaterial,"ArcaneOrbFire",false);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ExplosiveOrb.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        root=PrefabUtility.LoadPrefabContents(Folder+"/SwordSlash.prefab");
        try
        {
            foreach(var p in root.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.transform!=root.transform&&(p.name.Contains("Shaft")||p.name=="Flying"||p.name=="Impact")).ToArray())
                if(p!=null)UnityEngine.Object.DestroyImmediate(p.gameObject);
            root.transform.localScale=Vector3.one*.55f;
            var systems=root.GetComponentsInChildren<ParticleSystem>(true);List(root.GetComponent<RangedBurstVfx>(),"particles",systems);
            var renderer=root.GetComponent<ParticleSystemRenderer>();
            if(renderer!=null&&renderer.sharedMaterial!=null)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SwordSlashBlue.mat");
                if(material==null){material=new Material(renderer.sharedMaterial){name="SwordSlashBlue"};AssetDatabase.CreateAsset(material,Folder+"/SwordSlashBlue.mat");}
                if(material.HasProperty("_Color"))material.SetColor("_Color",new Color(2,3,4,1));renderer.sharedMaterial=material;EditorUtility.SetDirty(material);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/SwordSlash.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
