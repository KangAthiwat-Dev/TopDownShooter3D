#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class AdventureHudSetup
{
    public const string Folder="Assets/_Project/UI/AdventureHUD";
    public const string OriginalMusic="Assets/_Project/Audio/SoundsBack/PerituneMaterial_Pastorale3.mp3";
    private const string Art="Assets/_Project/UI/HUD/png_512/";
    private static readonly Color Ink=new Color(.22f,.13f,.065f),Cream=new Color(1f,.92f,.76f),Gold=new Color(.95f,.76f,.36f);
    private static TMP_FontAsset font;
    private static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    [Serializable] private sealed class Record {public string backup;public int preservedComponents;public string[] sources,hashes;}
    private static string Hash(string path)
    {using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    private static void FolderAt(string path)
    {if(AssetDatabase.IsValidFolder(path))return;FolderAt(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    private static void RequireScene()
    {if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");}
    private static void PrepareSprites(params string[] names)
    {
        FolderAt(Folder+"/Sprites");sprites.Clear();
        if(names.Length==0)names=new[]{"health_bar_frame","health_bar_full","mana_bar","xp_bar","inventory_slot_empty","inventory_slot_selected","compass_minimap_frame","cooldown_radial","dialogue_box_frame","tooltip_frame","cancel_button","confirm_button"};
        foreach(string name in names)
        {
            string target=Folder+"/Sprites/"+name+".png";
            if(!File.Exists(target)&&!AssetDatabase.CopyAsset(Art+name+".png",target))throw new IOException(target);
            var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(target));var pixels=image.GetPixels32();
            int minX=image.width,minY=image.height,maxX=0,maxY=0;
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)if(pixels[y*image.width+x].a>12){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            UnityEngine.Object.DestroyImmediate(image);
            var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Multiple;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var rects=new List<SpriteRect>();
            void Slice(string slice,Rect rect,Vector4 border)
            {
                var previous=provider.GetSpriteRects().FirstOrDefault(r=>r.name==slice);
                rects.Add(new SpriteRect{name=slice,rect=rect,pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,border=border,spriteID=previous!=null?previous.spriteID:GUID.Generate()});
            }
            Vector4 border=name=="dialogue_box_frame"||name=="tooltip_frame"?Vector4.one*38f:name.StartsWith("inventory_slot_")?Vector4.one*80f:Vector4.zero;
            Slice(name,new Rect(minX,minY,maxX-minX+1,maxY-minY+1),border);
            if(name=="health_bar_full"||name=="mana_bar"||name=="xp_bar")Slice(name+"_fill",new Rect(62,231,388,45),Vector4.zero);
            provider.SetSpriteRects(rects.ToArray());var ids=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if(ids!=null)ids.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();importer.SaveAndReimport();
            foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>())sprites[sprite.name]=sprite;
        }
    }
    private static Sprite S(string name)=>sprites[name];
    private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform)){layer=5};var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);
        Place(rt,anchor,pivot,position,size);return rt;
    }
    private static void Place(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}
    private static Image Picture(string name,Transform parent,Vector2 position,Vector2 size,Sprite sprite,Color color,bool sliced=false)
    {
        var image=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(position.x,-position.y),size).gameObject.AddComponent<Image>();
        image.sprite=sprite;image.color=color;image.raycastTarget=false;if(sliced)image.type=Image.Type.Sliced;return image;
    }
    private static TMP_Text Text(string name,Transform parent,Vector2 position,Vector2 size,string value,float sizeValue,Color color)
    {
        var text=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(position.x,-position.y),size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.text=value;text.fontSize=sizeValue;text.color=color;text.raycastTarget=false;text.richText=false;
        text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
    private static void Ref(SerializedObject data,string field,UnityEngine.Object value)=>data.FindProperty(field).objectReferenceValue=value;
    private static void Wire(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var data=new SerializedObject(owner);Ref(data,field,value);data.ApplyModifiedPropertiesWithoutUndo();}
    private static void Full(RectTransform rt)
    {rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;rt.localScale=Vector3.one;}
    private static void TrackOverrides(GameObject root)
    {foreach(var c in root.GetComponentsInChildren<Component>(true))if(c!=null){EditorUtility.SetDirty(c);if(PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);}}
    [MenuItem("Tools/HUD and Audio/Resume Prepared HUD Installation")]
    private static void Resume()
    {
        RequireScene();if(File.Exists(Folder+"/BuildRecord.json"))throw new InvalidOperationException("Installation is complete; do not replace manual adjustments.");
        // Recover this install's own incomplete objects only. Source UI was retained.
        var canvas=UnityEngine.Object.FindFirstObjectByType<GameOverUI>().GetComponent<Canvas>();
        var partial=canvas.transform.Find("AdventureHUD");if(partial!=null)Undo.DestroyObjectImmediate(partial.gameObject);
        var zone=UnityEngine.Object.FindFirstObjectByType<TownMusicZone>();if(zone!=null)Undo.DestroyObjectImmediate(zone.gameObject);
        foreach(string name in new[]{"PlayerHealthPanel","PlayerEnergyPanel"}){var old=canvas.transform.Find(name);if(old!=null)old.gameObject.SetActive(true);}
        Install();
    }
    [MenuItem("Tools/HUD and Audio/Install Fantasy HUD and City Music")]
    public static void Install()
    {
        RequireScene();if(File.Exists(Folder+"/BuildRecord.json"))throw new InvalidOperationException("Already installed; preserve HUD adjustments.");
        var canvas=UnityEngine.Object.FindFirstObjectByType<GameOverUI>().GetComponent<Canvas>();var player=UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        var protectedComponents=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null&&!c.transform.IsChildOf(canvas.transform)).ToArray();
        var before=protectedComponents.ToDictionary(c=>c.GetEntityId().ToString(),c=>EditorJsonUtility.ToJson(c));
        var sources=Directory.GetFiles(Art,"*.png").SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{OriginalMusic,OriginalMusic+".meta"}).ToArray();
        var record=new Record{sources=sources,hashes=sources.Select(Hash).ToArray()};
        Directory.CreateDirectory("Logs/SceneBackups");record.backup="Logs/SceneBackups/WorldPrototype_BeforeAdventureHud_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),record.backup,true))throw new IOException("Cannot back up live scene.");
        FolderAt(Folder);PrepareSprites();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var music=BuildMusic(player.transform);
        var root=Rect("AdventureHUD",canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);Full(root);Undo.RegisterCreatedObjectUndo(root.gameObject,"Install fantasy HUD");
        var hud=root.gameObject.AddComponent<AdventureHudUI>();var config=new SerializedObject(hud);
        Ref(config,"movement",player);Ref(config,"energy",player.GetComponent<PlayerEnergy>());Ref(config,"health",player.GetComponent<PlayerHealth>());
        Ref(config,"attack",player.GetComponent<PlayerAttackController>());Ref(config,"equipment",player.GetComponent<PlayerMeleeEquipment>());
        Ref(config,"view",UnityEngine.Object.FindFirstObjectByType<CameraFollow>().FollowCamera);Ref(config,"city",music);Ref(config,"map",UnityEngine.Object.FindFirstObjectByType<ForestMapLayout>());
        BuildVitals(root,canvas,player);BuildHotbar(root,canvas,player);BuildReadouts(root,config);config.ApplyModifiedPropertiesWithoutUndo();
        BuildProgressionHud(root,player);
        SkinInventory(canvas);SkinPromptAndDeath(canvas);
        RefinePresentation(canvas,hud);
        // Existing modal windows must render above the newly added ambient HUD.
        var inventory=canvas.GetComponentInChildren<InventoryPanelUI>(true);inventory.transform.SetAsLastSibling();
        var death=new SerializedObject(canvas.GetComponent<GameOverUI>()).FindProperty("gameOverPanel").objectReferenceValue as GameObject;death.transform.SetAsLastSibling();
        TrackOverrides(canvas.gameObject);
        var changed=protectedComponents.Where(c=>c==null||EditorJsonUtility.ToJson(c)!=before[c.GetEntityId().ToString()]).ToArray();
        if(changed.Length>0)throw new InvalidOperationException("Unexpected changes outside HUD scope: "+string.Join(",",changed.Select(c=>c!=null?c.name:"destroyed")));
        if(!sources.Select(Hash).SequenceEqual(record.hashes))throw new InvalidOperationException("Original HUD/audio imports changed.");
        record.preservedComponents=protectedComponents.Length;File.WriteAllText(Folder+"/BuildRecord.json",JsonUtility.ToJson(record,true));
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();Selection.activeGameObject=root.gameObject;
    }
    private static TownMusicZone BuildMusic(Transform player)
    {
        FolderAt(Folder+"/Audio");string path=Folder+"/Audio/City_Pastorale3.mp3";
        if(!File.Exists(path)&&!AssetDatabase.CopyAsset(OriginalMusic,path))throw new IOException("Music copy failed");
        var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;
        settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.75f;importer.defaultSampleSettings=settings;importer.loadInBackground=true;importer.SaveAndReimport();
        var go=new GameObject("City_Music_Zone",typeof(AudioSource),typeof(TownMusicZone));Undo.RegisterCreatedObjectUndo(go,"Add city music");
        var source=go.GetComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);source.loop=true;source.playOnAwake=false;source.spatialBlend=0;source.volume=0;source.dopplerLevel=0;
        var zone=go.GetComponent<TownMusicZone>();var data=new SerializedObject(zone);Ref(data,"player",player);Ref(data,"music",source);Ref(data,"clip",source.clip);data.ApplyModifiedPropertiesWithoutUndo();return zone;
    }
    [MenuItem("Tools/HUD and Audio/Validate Fantasy HUD and City Music")]
    public static void Validate()
    {
        RequireScene();var log=new StringBuilder("Adventure HUD / city music "+DateTime.Now.ToString("s")+"\n");bool passed=true;
        void Check(bool value,string message){passed&=value;log.AppendLine((value?"PASS ":"FAIL ")+message);}
        var hud=UnityEngine.Object.FindFirstObjectByType<AdventureHudUI>();var city=UnityEngine.Object.FindFirstObjectByType<TownMusicZone>();var hotbar=UnityEngine.Object.FindFirstObjectByType<InventoryHotbarUI>();
        Check(hud!=null&&city!=null,"One active adventure HUD and city music zone installed");
        Check(UnityEngine.Object.FindObjectsByType<PlayerHealthUI>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<PlayerEnergyUI>(FindObjectsSortMode.None).Length==1,"Exactly one active health and energy presenter");
        Check(new SerializedObject(hotbar).FindProperty("slots").arraySize==10,"Ten hotbar slots: eight supplies and two melee");
        Check(hotbar.GetComponentInParent<AdventureHudUI>()!=null&&new SerializedObject(hotbar).FindProperty("emptySlotSprite").objectReferenceValue!=null,"Hotbar uses fantasy sprites and original inventory/equipment data");
        Check(city.MusicSource!=null&&city.MusicSource.clip!=null&&city.MusicSource.loop&&!city.MusicSource.playOnAwake&&city.MusicSource.spatialBlend==0&&city.MusicSource.volume==0,"Looping 2D music starts silent, never on scene awake");
        Check(city.MusicVolume<=.25f&&city.FadeInSeconds>=3&&city.FadeOutSeconds>=2,"Moderate background volume and gradual entry/exit fades");
        Check(((AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(city.MusicSource.clip))).defaultSampleSettings.loadType==AudioClipLoadType.Streaming,"Long music clip streams from disk");
        var panel=UnityEngine.Object.FindFirstObjectByType<InventoryPanelUI>();var fields=new SerializedObject(panel);
        Check(fields.FindProperty("slots").arraySize==10&&fields.FindProperty("openButton").objectReferenceValue!=null&&fields.FindProperty("closeButton").objectReferenceValue!=null,"Original inventory modal and buttons remain wired");
        Check(UnityEngine.Object.FindFirstObjectByType<CameraViewSettings>().DefaultView==CameraViewMode.TopDown,"Top-down remains the default camera");
        var record=JsonUtility.FromJson<Record>(File.ReadAllText(Folder+"/BuildRecord.json"));
        Check(record.sources.Select(Hash).SequenceEqual(record.hashes),"Original HUD PNGs/imports and music/import preserved");
        Check(File.Exists(record.backup)&&record.preservedComponents>0,"Fresh backup; "+record.preservedComponents+" non-HUD components preserved");
        log.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AdventureHudValidation.txt",log.ToString());if(passed)Debug.Log(log.ToString());else Debug.LogError(log.ToString());
    }
}
#endif
