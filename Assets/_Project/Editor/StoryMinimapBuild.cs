#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class StoryWorldSetup
{
    private const string OverviewPath = Folder + "/Map/Everwood_Overview.png";
    private static void MapBounds(ForestMapLayout layout, WorldMapLandmark[] marks, out Vector2 minimum, out Vector2 maximum)
    {
        Vector3 position = layout.terrain.transform.position, size = layout.terrain.terrainData.size;
        minimum = new Vector2(position.x,position.z); maximum = minimum + new Vector2(size.x,size.z);
        foreach (var mark in marks)
        {
            var p = new Vector2(mark.transform.position.x,mark.transform.position.z);
            minimum = Vector2.Min(minimum,p-Vector2.one*18); maximum = Vector2.Max(maximum,p+Vector2.one*18);
        }
        var center = (minimum+maximum)*.5f; float half = Mathf.Max(maximum.x-minimum.x,maximum.y-minimum.y)*.5f+4;
        minimum = center-Vector2.one*half; maximum = center+Vector2.one*half;
    }
    private static void BakeOverview(Vector2 min, Vector2 max)
    {
        Directory.CreateDirectory(Folder + "/Map");
        var characters = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(m => m is PlayerMovement || m is NpcIdleLook || m is TownNpcBrain || m is EnemyHealth)
            .SelectMany(m => m.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
        var previous = characters.Select(r => r.forceRenderingOff).ToArray();
        var go = new GameObject("Temporary baked kingdom overview"){hideFlags=HideFlags.HideAndDontSave}; var camera = go.AddComponent<Camera>();
        var target = new RenderTexture(1024,1024,24); var texture = new Texture2D(1024,1024,TextureFormat.RGB24,false); var active = RenderTexture.active;
        try
        {
            foreach (var r in characters) r.forceRenderingOff = true;
            camera.enabled=false; camera.orthographic=true; camera.orthographicSize=(max.x-min.x)*.5f;
            camera.transform.position = new Vector3((min.x+max.x)*.5f,400,(min.y+max.y)*.5f); camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.075f,.06f);camera.nearClipPlane=.1f;camera.farClipPlane=1000;camera.cullingMask=~(1<<5);
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1024,1024),0,0);texture.Apply();File.WriteAllBytes(OverviewPath,texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<characters.Length;i++)if(characters[i]!=null)characters[i].forceRenderingOff=previous[i];
            RenderTexture.active=active;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);
        }
        AssetDatabase.ImportAsset(OverviewPath,ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(OverviewPath);importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
    }
    private static RectTransform MapRect(string name, Transform parent, Vector2 position, Vector2 size, bool center=false)
    {
        var t=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();t.SetParent(parent,false);
        t.anchorMin=t.anchorMax=t.pivot=center?Vector2.one*.5f:new Vector2(0,1);t.anchoredPosition=position;t.sizeDelta=size;return t;
    }
    private static TMP_Text MapText(string name,Transform parent,Vector2 position,Vector2 size,string value,int fontSize)
    {
        var text=MapRect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        text.text=value;text.fontSize=fontSize;text.color=new Color(1,.93f,.72f);text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;
        text.overflowMode=TextOverflowModes.Overflow;text.alignment=TextAlignmentOptions.MidlineLeft;return text;
    }
    private static void CreateMinimap(ForestMapLayout layout,WorldMapLandmark[] marks)
    {
        MapBounds(layout,marks,out var min,out var max); BakeOverview(min,max);
        var canvas=UnityEngine.Object.FindAnyObjectByType<GamePauseController>().transform;
        var hud=UnityEngine.Object.FindAnyObjectByType<AdventureHudUI>();var fields=new SerializedObject(hud);
        var oldCompass=fields.FindProperty("compass").objectReferenceValue as RectTransform;
        if(oldCompass!=null)oldCompass.parent.gameObject.SetActive(false);
        fields.FindProperty("compass").objectReferenceValue=null;fields.FindProperty("heading").objectReferenceValue=null;fields.ApplyModifiedPropertiesWithoutUndo();
        var root=MapRect("Everwood Minimap",canvas,new Vector2(-24,-24),new Vector2(340,386));root.anchorMin=root.anchorMax=root.pivot=Vector2.one;
        var frame=root.gameObject.AddComponent<Image>();frame.sprite=AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/dialogue_box_frame.png").OfType<Sprite>().First();frame.type=Image.Type.Sliced;frame.raycastTarget=false;
        var title=MapText("Kingdom",root,new Vector2(22,-7),new Vector2(260,36),"EVERWOOD",21);title.fontStyle=FontStyles.Bold;
        var north=MapText("North",root,new Vector2(294,-8),new Vector2(28,36),"N",19);north.color=title.color;
        var area=MapRect("Map Area",root,new Vector2(16,-46),new Vector2(308,308));area.gameObject.AddComponent<RectMask2D>();
        var image=area.gameObject.AddComponent<RawImage>();image.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(OverviewPath);image.raycastTarget=false;
        var views=new KingdomMinimapUI.LandmarkView[marks.Length];
        for(int i=0;i<marks.Length;i++)
        {
            var point=MapRect(marks[i].displayName,area,Vector2.zero,new Vector2(9,9),true);var dot=point.gameObject.AddComponent<Image>();dot.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");dot.color=marks[i].color;dot.raycastTarget=false;
            var label=MapText("Place Name",point,new Vector2(9,10),new Vector2(115,46),marks[i].mapLabel,14);label.rectTransform.anchorMin=label.rectTransform.anchorMax=Vector2.one*.5f;label.fontStyle=FontStyles.Bold;label.outlineColor=Color.black;label.outlineWidth=.28f;
            views[i]=new KingdomMinimapUI.LandmarkView{landmark=marks[i],marker=point,label=label};
        }
        var forest=MapText("Forest Name",area,new Vector2(93,-139),new Vector2(140,32),"Thornveil Forest",14);forest.fontStyle=FontStyles.Bold;forest.outlineColor=Color.black;forest.outlineWidth=.25f;
        var playerMarker=MapRect("Player Position",area,Vector2.zero,new Vector2(20,24),true);var arrow=playerMarker.gameObject.AddComponent<MinimapPlayerArrow>();arrow.color=new Color(1,1,.65f);arrow.raycastTarget=false;
        var legendArrow=MapRect("Player Legend",root,new Vector2(25,-365),new Vector2(18,22),true);legendArrow.anchorMin=legendArrow.anchorMax=new Vector2(0,1);var legend=legendArrow.gameObject.AddComponent<MinimapPlayerArrow>();legend.color=arrow.color;legend.raycastTarget=false;
        var legendText=MapText("Legend",root,new Vector2(44,-356),new Vector2(264,28),"ตำแหน่งของคุณ",18);legendText.color=title.color;
        var ui=root.gameObject.AddComponent<KingdomMinimapUI>();Ref(ui,"player",UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform);Ref(ui,"mapArea",area);Ref(ui,"playerMarker",playerMarker);
        var s=new SerializedObject(ui);s.FindProperty("worldMinimum").vector2Value=min;s.FindProperty("worldMaximum").vector2Value=max;var rows=s.FindProperty("landmarks");rows.arraySize=views.Length;
        for(int i=0;i<views.Length;i++){var row=rows.GetArrayElementAtIndex(i);row.FindPropertyRelative("landmark").objectReferenceValue=views[i].landmark;row.FindPropertyRelative("marker").objectReferenceValue=views[i].marker;row.FindPropertyRelative("label").objectReferenceValue=views[i].label;}s.ApplyModifiedPropertiesWithoutUndo();
        // Ambient map belongs below all existing modal UI (shops, inventory and upgrades).
        root.SetAsFirstSibling();
    }
    [MenuItem("Tools/Story/Rebuild Minimap Overview After Map Edits")]
    public static void RebuildMinimap()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var ui=UnityEngine.Object.FindAnyObjectByType<KingdomMinimapUI>();if(ui==null)throw new InvalidOperationException("Install the minimap first.");
        var marks=UnityEngine.Object.FindObjectsByType<WorldMapLandmark>(FindObjectsSortMode.None);MapBounds(UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>(),marks,out var min,out var max);BakeOverview(min,max);
        var s=new SerializedObject(ui);s.FindProperty("worldMinimum").vector2Value=min;s.FindProperty("worldMaximum").vector2Value=max;s.ApplyModifiedPropertiesWithoutUndo();
        ui.GetComponentInChildren<RawImage>(true).texture=AssetDatabase.LoadAssetAtPath<Texture2D>(OverviewPath);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);Debug.Log("Minimap overview rebuilt from current world positions.");
    }
}
#endif
