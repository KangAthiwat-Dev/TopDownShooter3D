#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WeaponShopSetup
{
    public const string Folder = "Assets/_Project/Prefabs/Weapons/FantasySwords";
    private static TMP_FontAsset font;
    private static void Ref(UnityEngine.Object owner, string field, UnityEngine.Object value)
    { var s = new SerializedObject(owner); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static Sprite Art(string name) => AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder + "/Sprites/" + name + ".png").OfType<Sprite>().Single(s => s.name == name);
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var t = new GameObject(name, typeof(RectTransform)) { layer = 5 }.GetComponent<RectTransform>(); t.SetParent(parent, false);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0, 1); t.anchoredPosition = new Vector2(x, -y); t.sizeDelta = new Vector2(w, h); return t;
    }
    private static void Full(RectTransform t) { t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero; }
    private static Image Picture(string name, Transform parent, float x, float y, float w, float h, Sprite art)
    { var v = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); v.sprite = art; v.raycastTarget = false; if (art != null) v.type = Image.Type.Sliced; return v; }
    private static TMP_Text Text(string name, Transform parent, float x, float y, float w, float h, string value, float size = 24)
    {
        var v = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>(); v.font = font; v.text = value; v.fontSize = size;
        v.color = new Color(.22f, .13f, .065f); v.raycastTarget = false; v.overflowMode = TextOverflowModes.Overflow;
        v.textWrappingMode = TextWrappingModes.NoWrap; v.alignment = TextAlignmentOptions.MidlineLeft; return v;
    }
    private static Button Button(string name, Transform parent, float x, float y, float w, float h, string value, out TMP_Text label)
    {
        var image = Picture(name, parent, x, y, w, h, Art("dialogue_box_frame")); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var c = button.colors; c.highlightedColor = new Color(1, .89f, .6f); c.pressedColor = new Color(.9f, .75f, .45f); c.disabledColor = new Color(.65f, .62f, .56f); button.colors = c;
        label = Text("Label", image.transform, 15, 4, w - 30, h - 8, value, 23); label.alignment = TextAlignmentOptions.Midline; return button;
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Install Rotating Sword Shop")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
        if (UnityEngine.Object.FindAnyObjectByType<WeaponShopUI>() != null) throw new InvalidOperationException("Shop already installed. Preserve Inspector edits.");
        Directory.CreateDirectory("Logs/SceneBackups");
        EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforeWeaponShop_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        if (font == null) throw new InvalidOperationException("Guild Thai font is required.");
        var definitions = PrepareWeapons();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMeleeEquipment>();
        var wallet = player.GetComponent<PlayerGoldWallet>();
        var walletData = new SerializedObject(wallet); var catalog = walletData.FindProperty("weaponCatalog"); catalog.arraySize = 6;
        for (int i = 0; i < 5; i++) catalog.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        var club = AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>("Assets/_Project/Prefabs/Weapons/TrainingClub.asset");
        catalog.GetArrayElementAtIndex(5).objectReferenceValue = club; walletData.ApplyModifiedPropertiesWithoutUndo();
        var clubData = new SerializedObject(club); clubData.FindProperty("weaponId").stringValue = "training_club"; clubData.ApplyModifiedPropertiesWithoutUndo();
        var npc = UnityEngine.Object.FindObjectsByType<NpcIdleLook>(FindObjectsSortMode.None).Single(n => n.NpcId == "armory_merchant_01");
        var merchant = Undo.AddComponent<WeaponMerchant>(npc.gameObject);
        var stockData = new SerializedObject(merchant); var listings = stockData.FindProperty("catalog"); listings.arraySize = 5;
        int[] prices = { 90, 140, 210, 300, 420 };
        for (int i = 0; i < 5; i++) { var listing = listings.GetArrayElementAtIndex(i); listing.FindPropertyRelative("weapon").objectReferenceValue = definitions[i]; listing.FindPropertyRelative("basePrice").intValue = prices[i]; }
        stockData.FindProperty("interactionPrompt").stringValue = "คุยกับพ่อค้าอาวุธ"; stockData.ApplyModifiedPropertiesWithoutUndo();
        var trigger = new GameObject("Weapon Shop Interaction"); trigger.transform.SetParent(npc.transform, false); trigger.layer = 8;
        var capsule = trigger.AddComponent<CapsuleCollider>(); capsule.isTrigger = true; capsule.height = 1.8f; capsule.radius = .4f; capsule.center = Vector3.up * .9f;
        var pause = UnityEngine.Object.FindAnyObjectByType<GamePauseController>(); var canvas = pause.GetComponent<Canvas>();
        var root = Rect("Weapon Shop", canvas.transform, 0, 0, 0, 0); Full(root);
        var window = Rect("Shop Window", root, 0, 0, 0, 0); Full(window);
        var shade = window.gameObject.AddComponent<Image>(); shade.color = new Color(.02f, .015f, .01f, .75f);
        var panel = Rect("Weapon Ledger", window, 0, 0, 1280, 910); panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f; panel.anchoredPosition = Vector2.zero;
        var paper = panel.gameObject.AddComponent<Image>(); paper.sprite = Art("dialogue_box_frame"); paper.type = Image.Type.Sliced; paper.raycastTarget = false;
        Text("Title", panel, 58, 38, 650, 58, "ร้านดาบนักผจญภัย", 34).fontStyle = FontStyles.Bold;
        var gold = Text("Your Gold", panel, 815, 52, 320, 44, "0 Gold", 28); gold.alignment = TextAlignmentOptions.MidlineRight;
        var close = Button("Close Shop", panel, 1160, 46, 62, 54, "X", out var closeLabel); close.GetComponent<Image>().sprite = Art("inventory_slot_empty"); closeLabel.color = new Color(1, .91f, .65f);
        var timer = Text("Restock Timer", panel, 60, 112, 1100, 40, "สินค้าใหม่ใน 05:00", 25);
        var rows = new WeaponShopUI.Row[3];
        for (int i = 0; i < rows.Length; i++)
        {
            var frame = Picture("Sword Offer " + i, panel, 58, 177 + i * 198, 676, 182, Art("dialogue_box_frame")); frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
            var icon = Picture("Sword Icon", frame.transform, 14, 12, 138, 158, null); icon.preserveAspect = true;
            var name = Text("Sword Name", frame.transform, 167, 24, 480, 44, "", 28);
            var stats = Text("Sword Stats", frame.transform, 167, 79, 480, 36, "", 22);
            var price = Text("Sword Price", frame.transform, 167, 127, 480, 34, "", 26);
            rows[i] = new WeaponShopUI.Row { button = button, frame = frame, icon = icon, name = name, stats = stats, price = price };
        }
        var detail = Picture("Sword Detail", panel, 758, 177, 464, 578, Art("dialogue_box_frame"));
        var selectedName = Text("Selected Name", detail.transform, 22, 20, 420, 44, "", 27); selectedName.alignment = TextAlignmentOptions.Midline;
        var selectedIcon = Picture("Selected Icon", detail.transform, 140, 73, 184, 190, null); selectedIcon.preserveAspect = true;
        var description = Text("Description", detail.transform, 26, 281, 412, 186, "", 23); description.textWrappingMode = TextWrappingModes.Normal; description.alignment = TextAlignmentOptions.TopLeft;
        var priceLabel = Text("Total Price", detail.transform, 26, 505, 412, 46, "", 29); priceLabel.alignment = TextAlignmentOptions.Midline;
        var slots = new Button[2]; var slotLabels = new TMP_Text[2];
        for (int i = 0; i < slots.Length; i++) slots[i] = Button("Equipment Slot " + i, panel, 58 + i * 341, 776, 331, 55, "", out slotLabels[i]);
        var hint = Text("Replacement Hint", panel, 60, 838, 670, 58, "", 21);
        var buy = Button("Buy Sword", panel, 758, 776, 464, 64, "ซื้อดาบ", out var buyLabel); buyLabel.fontSize = 27;
        var status = Text("Shop Status", panel, 60, 144, 1160, 30, "", 21);
        Text("Exit Hint", panel, 776, 852, 420, 32, "Esc เพื่อออกจากร้าน", 21).alignment = TextAlignmentOptions.Midline;
        var ui = root.gameObject.AddComponent<WeaponShopUI>();
        Ref(ui, "equipment", player); Ref(ui, "wallet", wallet); Ref(ui, "health", player.GetComponent<PlayerHealth>()); Ref(ui, "input", player.GetComponent<PlayerInput>()); Ref(ui, "pause", pause);
        Ref(ui, "window", window.gameObject); Ref(ui, "closeButton", close); Ref(ui, "buyButton", buy); Ref(ui, "goldLabel", gold); Ref(ui, "timerLabel", timer); Ref(ui, "selectedName", selectedName);
        Ref(ui, "description", description); Ref(ui, "priceLabel", priceLabel); Ref(ui, "statusLabel", status); Ref(ui, "buyLabel", buyLabel); Ref(ui, "slotHint", hint); Ref(ui, "selectedIcon", selectedIcon);
        var uiData = new SerializedObject(ui); var r = uiData.FindProperty("rows"); r.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            var v = r.GetArrayElementAtIndex(i); v.FindPropertyRelative("button").objectReferenceValue = rows[i].button; v.FindPropertyRelative("frame").objectReferenceValue = rows[i].frame;
            v.FindPropertyRelative("icon").objectReferenceValue = rows[i].icon; v.FindPropertyRelative("name").objectReferenceValue = rows[i].name;
            v.FindPropertyRelative("stats").objectReferenceValue = rows[i].stats; v.FindPropertyRelative("price").objectReferenceValue = rows[i].price;
        }
        foreach (var field in new[] { "slotButtons", "slotLabels" })
        { var list = uiData.FindProperty(field); list.arraySize = 2; for (int i = 0; i < 2; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = field == "slotButtons" ? (UnityEngine.Object)slots[i] : slotLabels[i]; }
        uiData.ApplyModifiedPropertiesWithoutUndo(); Ref(merchant, "shopUI", ui); window.gameObject.SetActive(false);
        foreach (var owner in new UnityEngine.Object[] { wallet, merchant })
        { EditorUtility.SetDirty(owner); if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner); }
        ConfigureThaiInventory();
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/WeaponShopInstallation.txt", "Installed on " + npc.name + "\n3 unique swords / 300 real seconds / price variation +/-20%\nGold and two owned melee slots saved together.\n");
        Selection.activeGameObject = npc.gameObject; Debug.Log("Rotating sword shop installed on NPC_WeaponMerchant.");
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Refine Shop Icons and Thai Inventory")]
    public static void RefinePresentation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        for (int i = 1; i <= 5; i++)
        {
            string iconPath = $"{Folder}/Icons/Sword_{i:00}.png";
            RenderIcon(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Sword_{i:00}.prefab"), iconPath);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
        }
        ConfigureThaiInventory();
        AssetDatabase.SaveAssets(); var scene = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Sword icons and Thai names in hotbar/inventory configured.");
    }
    private static void ConfigureThaiInventory()
    {
        void Thai(TMP_Text text)
        {
            if (text == null) return;
            text.font = font;
            if (text.textWrappingMode == TextWrappingModes.NoWrap)
            {
                var rect = text.rectTransform; float extra = Mathf.Max(0, text.fontSize * 1.85f - rect.sizeDelta.y);
                rect.sizeDelta += Vector2.up * extra; rect.anchoredPosition += Vector2.up * (rect.pivot.y - .5f) * extra;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                EditorUtility.SetDirty(rect);
            }
            EditorUtility.SetDirty(text);
        }
        var hotbar = UnityEngine.Object.FindAnyObjectByType<InventoryHotbarUI>(); var hotbarData = new SerializedObject(hotbar); var slots = hotbarData.FindProperty("slots");
        for (int i = 0; i < slots.arraySize; i++) Thai(slots.GetArrayElementAtIndex(i).FindPropertyRelative("itemName").objectReferenceValue as TMP_Text);
        var panel = UnityEngine.Object.FindAnyObjectByType<InventoryPanelUI>(); var inventoryData = new SerializedObject(panel);
        foreach (string field in new[] { "detailName", "detailDescription" }) Thai(inventoryData.FindProperty(field).objectReferenceValue as TMP_Text);
        foreach (var slot in panel.GetComponentsInChildren<InventorySlotUI>(true)) Thai(new SerializedObject(slot).FindProperty("itemName").objectReferenceValue as TMP_Text);
    }

    private static MeleeWeaponDefinition[] PrepareWeapons()
    {
        Directory.CreateDirectory(Folder + "/Icons"); Directory.CreateDirectory(Folder + "/Definitions"); AssetDatabase.Refresh();
        string[] names = { "ดาบเหล็ก", "ดาบเหมันต์", "ดาบสุริยะ", "ดาบโบราณ", "ดาบหนามรัตติกาล" };
        int[] damage = { 30, 38, 46, 60, 75 }; var result = new MeleeWeaponDefinition[5];
        for (int i = 0; i < result.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Sword_{i + 1:00}.prefab");
            if (prefab == null) throw new InvalidOperationException("Prepare sword model prefabs first.");
            string iconPath = $"{Folder}/Icons/Sword_{i + 1:00}.png";
            if (!File.Exists(iconPath)) RenderIcon(prefab, iconPath);
            AssetDatabase.ImportAsset(iconPath); var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            string path = $"{Folder}/Definitions/Sword_{i + 1:00}.asset";
            var weapon = AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<MeleeWeaponDefinition>(); AssetDatabase.CreateAsset(weapon, path);
                var s = new SerializedObject(weapon); s.FindProperty("weaponId").stringValue = $"fantasy_sword_{i + 1:00}";
                s.FindProperty("displayName").stringValue = names[i]; s.FindProperty("description").stringValue = "เก็บพาดหลังและชักใช้เมื่อศัตรูเข้าประชิด\nโค่นมอนสเตอร์ทั่วไปในครั้งเดียว";
                s.FindProperty("heldPrefab").objectReferenceValue = prefab; s.FindProperty("hudIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                s.FindProperty("damage").floatValue = damage[i]; s.FindProperty("killsInOneHit").boolValue = true;
                s.FindProperty("hitRange").floatValue = 2.5f; s.ApplyModifiedPropertiesWithoutUndo();
            }
            result[i] = weapon;
        }
        return result;
    }
    private static void RenderIcon(GameObject prefab, string path)
    {
        var preview = new PreviewRenderUtility(); Texture2D image = null;
        try
        {
            preview.AddSingleGO(UnityEngine.Object.Instantiate(prefab)); var camera = preview.camera;
            camera.orthographic = true; camera.orthographicSize = .64f; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .11f, .14f, 0);
            camera.transform.position = new Vector3(.3f, .6f, -3); camera.transform.LookAt(new Vector3(0, .43f, 0));
            preview.lights[0].intensity = 1.5f; preview.lights[0].transform.rotation = Quaternion.Euler(45, 35, 0);
            preview.lights[1].intensity = 1; preview.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0); preview.ambientColor = Color.gray * .8f;
            // EndStaticPreview produces an opaque thumbnail. Read the preview render target as RGBA instead.
            preview.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none); preview.Render(true); var rendered = preview.EndPreview();
            var target = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(rendered, target); RenderTexture.active = target;
                image = new Texture2D(256, 256, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        finally { if (image != null) UnityEngine.Object.DestroyImmediate(image); preview.Cleanup(); }
    }
}
#endif
