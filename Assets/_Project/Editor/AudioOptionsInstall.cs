#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class AudioOptionsSetup
{
    public const string MixerPath="Assets/_Project/Resources/Audio/GameAudio.mixer";
    private static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static AudioMixer mixer;
    private static AudioMixerGroup[] buses;
    private static TMP_FontAsset font;
    private static UnityEngine.Object Field(UnityEngine.Object owner,string name)=>new SerializedObject(owner).FindProperty(name)?.objectReferenceValue;
    private static void Ref(UnityEngine.Object owner,string name,UnityEngine.Object value)
    {var so=new SerializedObject(owner);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);}
    [MenuItem("Tools/Audio Options/Install Menu Music and Volume Controls")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var world=SceneManager.GetActiveScene();if(world.name!=GameMenuUI.WorldScene)throw new Exception("Open WorldPrototype first.");
        if(UnityEngine.Object.FindObjectsByType<AudioOptionsUI>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)throw new Exception("Volume controls already installed; preserve Inspector edits.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(world,"Logs/SceneBackups/BeforeAudioOptions_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        File.Copy(GameMenuSetup.MainPath,"Logs/SceneBackups/BeforeMenuMusic_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        BuildMixer();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        int prefabs=0,sources=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset.GetComponentsInChildren<AudioSource>(true).Length==0)continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                sources+=RouteRoot(root);PrefabUtility.SaveAsPrefabAsset(root,path);prefabs++;
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var root in world.GetRootGameObjects())sources+=RouteRoot(root);
        BuildOptions(UnityEngine.Object.FindAnyObjectByType<GameMenuUI>());
        EditorSceneManager.MarkSceneDirty(world);EditorSceneManager.SaveScene(world);
        var title=EditorSceneManager.OpenScene(GameMenuSetup.MainPath,OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(title);
            var menu=title.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameMenuUI>(true)).Single();BuildOptions(menu);
            var musicObject=new GameObject("Main Menu Emerald Hill Music",typeof(AudioSource));var music=musicObject.AddComponent<MainMenuMusic>();
            var source=musicObject.GetComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.ignoreListenerPause=true;source.outputAudioMixerGroup=buses[(int)GameAudioBus.Music];
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundsBack/adventure/PeriTune_Emerald_Hill_loop.wav");
            if(clip==null)throw new Exception("Emerald Hill WAV is missing.");source.clip=clip;Ref(music,"clip",clip);Ref(music,"source",source);
            foreach(var root in title.GetRootGameObjects())sources+=RouteRoot(root);
            source.outputAudioMixerGroup=buses[(int)GameAudioBus.Music];EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(title);EditorSceneManager.SaveScene(title);
        }
        finally{SceneManager.SetActiveScene(world);EditorSceneManager.CloseScene(title,true);}
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/AudioOptionsInstallation.txt",$"AudioMixer with Master / Music / Ambience / Effects / UI; {prefabs} project prefabs and {sources} audio sources routed. Emerald Hill streams and loops only in MainMenu. Live Thai sliders in both Options pages, independent saved mix.\n");
        Debug.Log("Menu music and five saved volume controls installed in title and pause menus.");
    }
    private static void BuildMixer()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));AssetDatabase.Refresh();
        mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.Audio.AudioMixerController")).First(t=>t!=null);
        if(mixer==null)mixer=(AudioMixer)type.GetMethod("CreateMixerControllerAtPath",Flags).Invoke(null,new object[]{MixerPath});
        var master=type.GetProperty("masterGroup",Flags).GetValue(mixer);var groupType=master.GetType();
        var children=Array.CreateInstance(groupType,4);buses=new AudioMixerGroup[5];buses[0]=(AudioMixerGroup)master;
        for(int i=1;i<5;i++)
        {
            var group=mixer.FindMatchingGroups("").FirstOrDefault(g=>g.name==((GameAudioBus)i).ToString());
            if(group==null)group=(AudioMixerGroup)type.GetMethod("CreateNewGroup",Flags).Invoke(mixer,new object[]{((GameAudioBus)i).ToString(),false});
            children.SetValue(group,i-1);buses[i]=(AudioMixerGroup)group;
        }
        groupType.GetProperty("children",Flags).SetValue(master,children);
        var exposedProperty=type.GetProperty("exposedParameters",Flags);var parameterType=exposedProperty.PropertyType.GetElementType();var parameters=Array.CreateInstance(parameterType,5);
        for(int i=0;i<5;i++)
        {
            var value=Activator.CreateInstance(parameterType);var guid=groupType.GetMethod("GetGUIDForVolume",Flags).Invoke(buses[i],null);
            parameterType.GetFields(Flags).Single(f=>f.FieldType==guid.GetType()).SetValue(value,guid);
            parameterType.GetFields(Flags).Single(f=>f.FieldType==typeof(string)).SetValue(value,((GameAudioBus)i)+"Volume");parameters.SetValue(value,i);
            EditorUtility.SetDirty(buses[i]);
        }
        // Newly created controllers have no editor views until explicitly supplied.
        var viewProperty=type.GetProperty("views",Flags);var viewType=viewProperty.PropertyType.GetElementType();
        var view=Activator.CreateInstance(viewType);var guidsType=groupType.GetProperty("groupID",Flags).PropertyType;var guids=Array.CreateInstance(guidsType,5);
        for(int i=0;i<5;i++)guids.SetValue(groupType.GetProperty("groupID",Flags).GetValue(buses[i]),i);
        viewType.GetFields(Flags).Single(f=>f.FieldType==typeof(string)).SetValue(view,"All");
        viewType.GetFields(Flags).Single(f=>f.FieldType==guids.GetType()).SetValue(view,guids);
        var views=Array.CreateInstance(viewType,1);views.SetValue(view,0);viewProperty.SetValue(mixer,views);
        exposedProperty.SetValue(mixer,parameters);type.GetMethod("OnChangedExposedParameter",Flags).Invoke(mixer,null);
        var so=new SerializedObject(mixer);var update=so.FindProperty("m_UpdateMode");if(update!=null)update.intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mixer);AssetDatabase.SaveAssets();
        if(mixer.FindMatchingGroups("").Length!=5)throw new Exception("Mixer group hierarchy was not created correctly.");
    }
    private static int RouteRoot(GameObject root)
    {
        var sources=root.GetComponentsInChildren<AudioSource>(true);
        foreach(var source in sources)Route(source,source.GetComponentInParent<WorldMonsterCamp>()!=null?GameAudioBus.Ambience:GameAudioBus.Effects);
        foreach(var ui in root.GetComponentsInChildren<UIAudioFeedback>(true)){Route(Field(ui,"windows") as AudioSource,GameAudioBus.UI);Route(Field(ui,"hover") as AudioSource,GameAudioBus.UI);}
        foreach(var city in root.GetComponentsInChildren<TownMusicZone>(true))Route(Field(city,"music") as AudioSource,GameAudioBus.Music);
        foreach(var ambient in root.GetComponentsInChildren<WorldAmbientAudio>(true))
        {Route(Field(ambient,"forestSource") as AudioSource,GameAudioBus.Ambience);Route(Field(ambient,"villageSource") as AudioSource,GameAudioBus.Ambience);Route(Field(ambient,"demonSource") as AudioSource,GameAudioBus.Ambience);Route(Field(ambient,"adventureSource") as AudioSource,GameAudioBus.Music);Route(Field(ambient,"combatSource") as AudioSource,GameAudioBus.Music);}
        foreach(var lava in root.GetComponentsInChildren<DemonCliffAtmosphere>(true))Route(Field(lava,"lavaAmbience") as AudioSource,GameAudioBus.Ambience);
        return sources.Length;
    }
    private static void Route(AudioSource source,GameAudioBus bus)
    {
        if(source==null)return;source.outputAudioMixerGroup=buses[(int)bus];EditorUtility.SetDirty(source);
        if(PrefabUtility.IsPartOfPrefabInstance(source))PrefabUtility.RecordPrefabInstancePropertyModifications(source);
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {var r=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;}
    private static TMP_Text Text(string name,Transform parent,float x,float y,float w,float h,string value,int size)
    {var text=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=new Color(.22f,.13f,.065f);text.raycastTarget=false;return text;}
    private static Sprite Art(string name)=>AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/"+name+".png").OfType<Sprite>().First();
    private static Button Button(string name,Transform parent,float x,float y,float width,string label,UIAudioFeedback feedback)
    {
        var rect=Rect(name,parent,x,y,width,52);var image=rect.gameObject.AddComponent<Image>();image.sprite=Art("tooltip_frame");image.type=Image.Type.Sliced;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        Text("Label",rect,8,2,width-16,48,label,24).alignment=TextAlignmentOptions.Midline;Ref(rect.gameObject.AddComponent<UISlotHoverAudio>(),"feedback",feedback);return button;
    }
    private static void BuildOptions(GameMenuUI menu)
    {
        var page=(GameObject)Field(menu,"optionsPage");var oldBack=(Button)Field(menu,"optionsBack");float width=oldBack.GetComponent<RectTransform>().sizeDelta.x;
        for(int i=page.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(page.transform.GetChild(i).gameObject);
        var feedback=(UIAudioFeedback)Field(menu,"audioFeedback");Text("Audio heading",page.transform,55,185,width,50,"ตั้งค่าเสียง",32);
        string[] labels={"เสียงหลัก","เพลง","เสียงบรรยากาศ","เอฟเฟกต์","เสียง UI"};var sliders=new Slider[5];var values=new TMP_Text[5];
        for(int i=0;i<5;i++)
        {
            float y=255+i*75;Text(labels[i],page.transform,55,y,width-90,36,labels[i],26);
            values[i]=Text("Value "+i,page.transform,55+width-80,y,80,36,"100%",25);values[i].alignment=TextAlignmentOptions.Right;
            sliders[i]=Slider(page.transform,55,y+35,width);
        }
        float buttonWidth=(width-18)/2;
        var defaults=Button("Reset Audio",page.transform,55,660,buttonWidth,"คืนค่าเริ่มต้น",feedback);var back=Button("Options Back",page.transform,55+buttonWidth+18,660,buttonWidth,"กลับ",feedback);
        Text("Save hint",page.transform,55,735,width,38,"ปรับแล้วบันทึกให้อัตโนมัติ",22);
        Ref(menu,"optionsBack",back);
        var ui=page.AddComponent<AudioOptionsUI>();Ref(ui,"defaults",defaults);var so=new SerializedObject(ui);var rows=so.FindProperty("rows");rows.arraySize=5;
        for(int i=0;i<5;i++){var row=rows.GetArrayElementAtIndex(i);row.FindPropertyRelative("bus").enumValueIndex=i;row.FindPropertyRelative("slider").objectReferenceValue=sliders[i];row.FindPropertyRelative("value").objectReferenceValue=values[i];}so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Slider Slider(Transform parent,float x,float y,float width)
    {
        var root=Rect("Volume Slider",parent,x,y,width,28);var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;
        var track=Rect("Track",root,10,8,width-20,12).gameObject.AddComponent<Image>();track.color=new Color(.2f,.13f,.065f);
        var fillArea=Rect("Fill Area",root,10,8,width-20,12);var fill=Rect("Fill",fillArea,0,0,width-20,12).gameObject.AddComponent<Image>();fill.color=new Color(.86f,.58f,.18f);fill.raycastTarget=false;
        fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.sizeDelta=Vector2.zero;fill.rectTransform.anchoredPosition=Vector2.zero;
        var handleArea=Rect("Handle Area",root,10,0,width-20,28);var handle=Rect("Handle",handleArea,0,0,24,32).gameObject.AddComponent<Image>();handle.color=new Color(1,.86f,.53f);handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.anchoredPosition=new Vector2(0,-14);
        var outline=handle.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.3f,.16f,.04f);outline.effectDistance=new Vector2(2,-2);
        var slider=root.gameObject.AddComponent<Slider>();slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;slider.minValue=0;slider.maxValue=1;slider.value=1;
        handle.rectTransform.sizeDelta=new Vector2(24,0);handle.rectTransform.anchoredPosition=Vector2.zero;
        return slider;
    }
    [MenuItem("Tools/Audio Options/Refine Slider Handles")]
    public static void RefineSliderHandles()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var original=SceneManager.GetActiveScene();
        void Fix(Scene scene)
        {
            foreach(var row in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AudioOptionsUI>(true)).SelectMany(ui=>(AudioOptionsUI.Row[])ui.GetType().GetField("rows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui)))
            {row.slider.handleRect.sizeDelta=new Vector2(24,0);row.slider.handleRect.anchoredPosition=Vector2.zero;}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        Fix(original);var title=EditorSceneManager.OpenScene(GameMenuSetup.MainPath,OpenSceneMode.Additive);
        try{Fix(title);}finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(title,true);}
        Debug.Log("Audio slider handles aligned with their tracks.");
    }
}
#endif
