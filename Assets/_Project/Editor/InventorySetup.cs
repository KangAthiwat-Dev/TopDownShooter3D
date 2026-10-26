using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InventorySetup
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/InventoryPanel.prefab";
    private static readonly Color Ink = new Color(0.035f, 0.052f, 0.075f, 1f);
    private static readonly Color Gold = new Color(1f, 0.76f, 0.32f);
    private static readonly Color Muted = new Color(0.58f, 0.66f, 0.72f);
    private static TMP_FontAsset font;

    [MenuItem("Tools/Inventory/Build and Install Inventory")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        BuildPrefab();
        Scene initial = SceneManager.GetActiveScene();
        try
        {
            InstallScene("Assets/_Project/Scenes/WorldPrototype.unity");
            InstallScene("Assets/_Project/Scenes/PrototypeArena.unity");
        }
        finally { if (initial.IsValid() && initial.isLoaded) SceneManager.SetActiveScene(initial); }
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log("Inventory installed in WorldPrototype and PrototypeArena. I/Tab to open, Escape to close. Scene backups: Logs/SceneBackups/*BeforeInventory*. Supplies start empty; melee slots reuse the existing equipment.");
    }
    private static void BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("Default TMP font is missing");
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("InventoryInterface", typeof(RectTransform), typeof(InventoryPanelUI));
        SceneManager.MoveGameObjectToScene(root, preview);
        try
        {
            Stretch(root.GetComponent<RectTransform>());
            RectTransform open = Rect("Open Inventory", root.transform, new Vector2(24, 24), new Vector2(218, 48));
            open.anchorMin = open.anchorMax = Vector2.zero; open.pivot = Vector2.zero; open.anchoredPosition = new Vector2(24, 24);
            Button openButton = Button(open, "[I / TAB]  INVENTORY", 17, Gold);
            Image overlay = Image("Inventory Window", root.transform, Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.035f, 0.82f), true);
            Stretch(overlay.rectTransform);
            RectTransform panel = Rect("Pack", overlay.transform, Vector2.zero, new Vector2(1240, 760));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f); panel.anchoredPosition = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>(); panelImage.color = Ink; panelImage.raycastTarget = false;
            Image("Top Accent", panel, Vector2.zero, new Vector2(1240, 3), Gold);
            Text("Eyebrow", panel, new Vector2(36, 22), new Vector2(600, 18), "FIELD EQUIPMENT  /  PACK 01", 14, Gold).characterSpacing = 2f;
            Text("Title", panel, new Vector2(34, 44), new Vector2(650, 43), "INVENTORY", 36, Color.white).fontStyle = FontStyles.Bold;
            TMP_Text capacity = Text("Capacity", panel, new Vector2(36, 92), new Vector2(1000, 24), "SUPPLIES  0 / 8     |     MELEE  0 / 2", 16, Muted);
            Button close = Button(Rect("Close Inventory", panel, new Vector2(1144, 28), new Vector2(60, 48)), "X", 20, Color.white);
            Image("Divider", panel, new Vector2(36, 128), new Vector2(1168, 1), new Color(0.22f, 0.28f, 0.34f));
            Text("Supply Heading", panel, new Vector2(36, 141), new Vector2(500, 24), "SUPPLIES", 17, Muted).characterSpacing = 2;
            Text("Detail Heading", panel, new Vector2(780, 141), new Vector2(424, 24), "ITEM DETAILS", 17, Muted).characterSpacing = 2;
            var slotViews = new InventorySlotUI[10];
            for (int i = 0; i < PlayerInventory.Capacity; i++)
                slotViews[i] = Slot(panel, i, new Vector2(36 + (i % 4) * 180, 177 + (i / 4) * 160), new Vector2(164, 144), false);
            Text("Melee Heading", panel, new Vector2(36, 509), new Vector2(704, 26), "MELEE WEAPONS", 17, Gold).characterSpacing = 2;
            for (int i = 0; i < PlayerMeleeEquipment.Capacity; i++)
                slotViews[PlayerInventory.Capacity + i] = Slot(panel, PlayerInventory.Capacity + i, new Vector2(36 + i * 360, 548), new Vector2(344, 120), true);
            Image card = Image("Detail Card", panel, new Vector2(780, 177), new Vector2(424, 491), new Color(0.055f, 0.077f, 0.105f));
            Image("Detail Icon Frame", card.transform, new Vector2(24, 24), new Vector2(94, 94), new Color(0.09f, 0.135f, 0.17f));
            Image icon = Image("Detail Icon", card.transform, new Vector2(36, 36), new Vector2(70, 70), Color.white);
            icon.preserveAspect = true;
            TMP_Text glyph = Text("Detail Glyph", card.transform, new Vector2(24, 24), new Vector2(94, 94), "+", 48, Gold);
            glyph.alignment = TextAlignmentOptions.Center;
            TMP_Text type = Text("Item Type", card.transform, new Vector2(140, 29), new Vector2(260, 21), "EMPTY SLOT", 14, Gold);
            TMP_Text name = Text("Item Name", card.transform, new Vector2(140, 57), new Vector2(260, 65), "Nothing here yet", 27, Color.white);
            name.enableAutoSizing = true; name.fontSizeMin = 19; name.fontSizeMax = 27; name.fontStyle = FontStyles.Bold;
            TMP_Text description = Text("Description", card.transform, new Vector2(24, 147), new Vector2(376, 158), "", 22, new Color(0.76f, 0.81f, 0.85f));
            TMP_Text stats = Text("Item Stats", card.transform, new Vector2(24, 321), new Vector2(376, 104), "", 18, Muted);
            Button select = Button(Rect("Select Weapon", card.transform, new Vector2(24, 437), new Vector2(376, 36)), "SELECT WEAPON", 15, Gold);
            Image("Footer Divider", panel, new Vector2(36, 692), new Vector2(1168, 1), new Color(0.22f, 0.28f, 0.34f));
            Text("Footer Controls", panel, new Vector2(36, 710), new Vector2(750, 26), "I / TAB   CLOSE      ESC   BACK      CLICK / D-PAD   INSPECT", 17, Muted);
            TMP_Text pause = Text("Pause Hint", panel, new Vector2(968, 710), new Vector2(236, 26), "GAME PAUSED", 17, Gold);
            pause.alignment = TextAlignmentOptions.Right;

            var data = new SerializedObject(root.GetComponent<InventoryPanelUI>());
            Ref(data, "window", overlay.gameObject); Ref(data, "openButton", openButton); Ref(data, "closeButton", close); Ref(data, "selectWeaponButton", select);
            Ref(data, "capacityText", capacity); Ref(data, "detailName", name); Ref(data, "detailType", type); Ref(data, "detailDescription", description);
            Ref(data, "detailStats", stats); Ref(data, "detailGlyph", glyph); Ref(data, "detailIcon", icon); Ref(data, "selectLabel", select.GetComponentInChildren<TMP_Text>());
            SerializedProperty slots = data.FindProperty("slots"); slots.arraySize = slotViews.Length;
            for (int i = 0; i < slotViews.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static InventorySlotUI Slot(Transform parent, int index, Vector2 position, Vector2 size, bool melee)
    {
        Image frame = Image("Slot " + (index + 1).ToString("00"), parent, position, size, new Color(0.23f, 0.29f, 0.35f));
        Image fill = Image("Fill", frame.transform, new Vector2(1, 1), size - Vector2.one * 2f, new Color(0.052f, 0.074f, 0.1f), true);
        Button button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = fill;
        Image icon = Image("Icon", frame.transform, melee ? new Vector2(20, 35) : new Vector2(54, 35), new Vector2(54, 54), Color.white);
        icon.preserveAspect = true;
        TMP_Text glyph = Text("Glyph", frame.transform, melee ? new Vector2(20, 30) : new Vector2(54, 28), new Vector2(54, 60), "+", 39, Muted);
        glyph.alignment = TextAlignmentOptions.Center;
        Text("Slot Number", frame.transform, new Vector2(12, 9), new Vector2(50, 18), (index + 1).ToString("00"), 13, Muted);
        TMP_Text quantity = Text("Quantity", frame.transform, new Vector2(size.x - 66, 9), new Vector2(54, 20), "", 16, Color.white);
        quantity.alignment = TextAlignmentOptions.Right;
        TMP_Text name = Text("Item Name", frame.transform, melee ? new Vector2(92, 41) : new Vector2(12, 99), melee ? new Vector2(240, 26) : new Vector2(140, 24), "EMPTY SLOT", 18, Color.white);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_Text status = Text("Status", frame.transform, melee ? new Vector2(92, 77) : new Vector2(12, 125), new Vector2(size.x - (melee ? 104 : 24), 15), melee ? "MELEE" : "SUPPLY", 11, Muted);
        InventorySlotUI view = frame.gameObject.AddComponent<InventorySlotUI>();
        var settings = new SerializedObject(view);
        Ref(settings, "button", button); Ref(settings, "frame", frame); Ref(settings, "background", fill); Ref(settings, "icon", icon);
        Ref(settings, "glyph", glyph); Ref(settings, "itemName", name); Ref(settings, "quantity", quantity); Ref(settings, "status", status);
        settings.ApplyModifiedPropertiesWithoutUndo(); return view;
    }
    private static void InstallScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            Directory.CreateDirectory("Logs/SceneBackups");
            if (!EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/" + scene.name + "_BeforeInventory_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true))
                throw new IOException("Could not back up " + scene.name);
            PlayerHealth health = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerHealth>(true)).First();
            GameOverUI gameOver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameOverUI>(true)).First();
            Canvas canvas = gameOver.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Game Over must be on HUDCanvas");
            PlayerInventory inventory = health.GetComponent<PlayerInventory>() ?? Undo.AddComponent<PlayerInventory>(health.gameObject);
            GamePauseController pause = canvas.GetComponent<GamePauseController>() ?? Undo.AddComponent<GamePauseController>(canvas.gameObject);
            PlayerInputGate gate = health.GetComponent<PlayerInputGate>() ?? Undo.AddComponent<PlayerInputGate>(health.gameObject);
            SetRef(pause, "playerHealth", health); SetRef(gate, "pauseController", pause); SetRef(gameOver, "pauseController", pause);
            InventoryPanelUI panel = canvas.GetComponentInChildren<InventoryPanelUI>(true);
            if (panel == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                Undo.RegisterCreatedObjectUndo(instance, "Install Inventory"); instance.transform.SetParent(canvas.transform, false);
                panel = instance.GetComponent<InventoryPanelUI>();
            }
            SetRef(panel, "inventory", inventory); SetRef(panel, "equipment", health.GetComponent<PlayerMeleeEquipment>());
            SetRef(panel, "playerInput", health.GetComponent<UnityEngine.InputSystem.PlayerInput>()); SetRef(panel, "playerHealth", health); SetRef(panel, "pauseController", pause);
            InventoryHotbarUI hotbar = canvas.GetComponentInChildren<InventoryHotbarUI>(true);
            if (hotbar == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/InventoryHotbar.prefab"), scene);
                instance.transform.SetParent(canvas.transform, false); Undo.RegisterCreatedObjectUndo(instance, "Install Hotbar");
                hotbar = instance.GetComponent<InventoryHotbarUI>();
            }
            SetRef(hotbar, "inventory", inventory); SetRef(hotbar, "equipment", health.GetComponent<PlayerMeleeEquipment>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save inventory wiring: " + scene.name);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static void SetRef(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        Undo.RecordObject(owner, "Wire Inventory"); var data = new SerializedObject(owner); Ref(data, field, value); data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }
    private static void Ref(SerializedObject data, string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 }; var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(position.x, -position.y); rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool raycast = false)
    { Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image; }
    private static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value, float fontSize, Color color)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.text = value; text.color = color; text.raycastTarget = false;
        text.richText = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static Button Button(RectTransform rect, string label, float size, Color color)
    {
        Image background = rect.gameObject.AddComponent<Image>(); background.color = new Color(0.13f, 0.18f, 0.22f);
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        TMP_Text text = Text("Label", rect, Vector2.zero, rect.sizeDelta, label, size, color); text.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
