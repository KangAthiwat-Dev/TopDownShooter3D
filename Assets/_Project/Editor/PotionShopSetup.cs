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

public static class PotionShopSetup
{
    public const string Folder = "Assets/_Project/Items/Potions";
    public const string Source = "Assets/Assets_Imported/Magic_Props";
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
        v.color = new Color(.22f, .13f, .065f); v.raycastTarget = false; v.overflowMode = TextOverflowModes.Overflow; v.textWrappingMode = TextWrappingModes.NoWrap;
        v.alignment = TextAlignmentOptions.MidlineLeft; return v;
    }
    private static Button Button(string name, Transform parent, float x, float y, float w, float h, string value, out TMP_Text label)
    {
        var frame = Picture(name, parent, x, y, w, h, Art("dialogue_box_frame")); frame.raycastTarget = true;
        var b = frame.gameObject.AddComponent<Button>(); b.targetGraphic = frame;
        var c = b.colors; c.highlightedColor = new Color(1, .89f, .6f); c.pressedColor = new Color(.9f, .75f, .45f); c.disabledColor = new Color(.65f, .62f, .56f); b.colors = c;
        label = Text("Label", frame.transform, 12, 4, w - 24, h - 8, value, 24); label.alignment = TextAlignmentOptions.Midline; return b;
    }
    [MenuItem("Tools/NPC/Potion Shop/Install Three Potions and Shop")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
        if (UnityEngine.Object.FindAnyObjectByType<PotionShopUI>() != null) throw new InvalidOperationException("Potion shop is already installed. Preserve Inspector adjustments.");
        Directory.CreateDirectory("Logs/SceneBackups"); EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforePotionShop_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        var potions = BuildAssets();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>(); var wallet = player.GetComponent<PlayerGoldWallet>(); var pause = UnityEngine.Object.FindAnyObjectByType<GamePauseController>();
        var catalog = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Items" }).Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(i => i.ItemId).ToArray();
        var walletData = new SerializedObject(wallet); var list = walletData.FindProperty("itemCatalog"); list.arraySize = catalog.Length;
        for (int i = 0; i < catalog.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i]; walletData.ApplyModifiedPropertiesWithoutUndo();
        var use = Undo.AddComponent<PlayerPotionUse>(player.gameObject); Ref(use, "pause", pause);
        var npc = UnityEngine.Object.FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None).Single(n => n.Role == CityServiceRole.PotionStore);
        var merchant = Undo.AddComponent<PotionMerchant>(npc.gameObject); Ref(merchant, "contact", npc);
        var data = new SerializedObject(merchant); var entries = data.FindProperty("catalog"); entries.arraySize = 3;
        int[] prices = { 25, 20, 40 };
        for (int i = 0; i < 3; i++) { var entry = entries.GetArrayElementAtIndex(i); entry.FindPropertyRelative("potion").objectReferenceValue = potions[i]; entry.FindPropertyRelative("price").intValue = prices[i]; }
        data.FindProperty("interactionPrompt").stringValue = "คุยกับแม่ค้าขาย Potion"; data.ApplyModifiedPropertiesWithoutUndo();
        var trigger = new GameObject("Potion Shop Interaction") { layer = 8 }; trigger.transform.SetParent(npc.transform, false);
        var collider = trigger.AddComponent<CapsuleCollider>(); collider.height = 1.8f; collider.radius = .4f; collider.center = Vector3.up * .9f; collider.isTrigger = true;
        var root = Rect("Potion Shop", pause.transform, 0, 0, 0, 0); Full(root);
        var window = Rect("Potion Window", root, 0, 0, 0, 0); Full(window); var shade = window.gameObject.AddComponent<Image>(); shade.color = new Color(.02f, .015f, .01f, .75f);
        var panel = Rect("Apothecary Ledger", window, 0, 0, 1280, 850); panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f; panel.anchoredPosition = Vector2.zero;
        var paper = panel.gameObject.AddComponent<Image>(); paper.sprite = Art("dialogue_box_frame"); paper.type = Image.Type.Sliced; paper.raycastTarget = false;
        Text("Title", panel, 58, 38, 650, 58, "ร้าน Potion นักผจญภัย", 34).fontStyle = FontStyles.Bold;
        var gold = Text("Your Gold", panel, 815, 52, 320, 44, "0 Gold", 28); gold.alignment = TextAlignmentOptions.MidlineRight;
        var close = Button("Close Shop", panel, 1160, 46, 62, 54, "X", out var closeText); close.GetComponent<Image>().sprite = Art("inventory_slot_empty"); closeText.color = new Color(1, .91f, .65f);
        Text("Subtitle", panel, 60, 108, 1140, 40, "ซื้อเก็บไว้ แล้วใช้ Potion จากกระเป๋า", 25);
        var rows = new PotionShopUI.Row[3];
        for (int i = 0; i < 3; i++)
        {
            var frame = Picture("Potion Offer " + i, panel, 58, 165 + 186 * i, 676, 174, Art("dialogue_box_frame")); frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
            var icon = Picture("Potion Icon", frame.transform, 16, 14, 136, 144, null); icon.preserveAspect = true;
            var name = Text("Potion Name", frame.transform, 167, 20, 480, 44, "", 28);
            var effect = Text("Effect", frame.transform, 167, 75, 480, 38, "", 22);
            var price = Text("Price", frame.transform, 167, 124, 480, 36, "", 24);
            rows[i] = new PotionShopUI.Row { button = button, frame = frame, icon = icon, name = name, effect = effect, price = price };
        }
        var card = Picture("Potion Detail", panel, 758, 165, 464, 546, Art("dialogue_box_frame"));
        var nameText = Text("Selected Name", card.transform, 22, 18, 420, 44, "", 27); nameText.alignment = TextAlignmentOptions.Midline;
        var selectedIcon = Picture("Selected Icon", card.transform, 140, 67, 184, 157, null); selectedIcon.preserveAspect = true;
        var description = Text("Description", card.transform, 28, 238, 408, 138, "", 23); description.textWrappingMode = TextWrappingModes.Normal; description.alignment = TextAlignmentOptions.TopLeft;
        var less = Button("Less", card.transform, 28, 385, 64, 48, "-", out var lessLabel);
        var more = Button("More", card.transform, 372, 385, 64, 48, "+", out var moreLabel);
        foreach (var b in new[] { less, more }) b.GetComponent<Image>().sprite = Art("inventory_slot_empty"); lessLabel.color = moreLabel.color = new Color(1, .91f, .65f);
        var quantity = Text("Quantity", card.transform, 104, 387, 256, 44, "1", 28); quantity.alignment = TextAlignmentOptions.Midline;
        var total = Text("Total", card.transform, 26, 440, 412, 44, "", 28); total.alignment = TextAlignmentOptions.Midline;
        var buy = Button("Buy Potions", card.transform, 28, 486, 408, 48, "ซื้อ Potion", out var buyLabel);
        var status = Text("Shop Status", panel, 60, 728, 1160, 44, "", 23);
        Text("Help", panel, 60, 784, 1160, 38, "เลือก Potion > จำนวน > ซื้อ  |  ใช้จากกระเป๋า  |  Esc เพื่อออก", 24);
        var ui = root.gameObject.AddComponent<PotionShopUI>();
        Ref(ui, "inventory", player); Ref(ui, "wallet", wallet); Ref(ui, "health", player.GetComponent<PlayerHealth>()); Ref(ui, "input", player.GetComponent<PlayerInput>()); Ref(ui, "pause", pause);
        Ref(ui, "window", window.gameObject); Ref(ui, "closeButton", close); Ref(ui, "buyButton", buy); Ref(ui, "moreButton", more); Ref(ui, "lessButton", less);
        Ref(ui, "goldLabel", gold); Ref(ui, "selectedName", nameText); Ref(ui, "description", description); Ref(ui, "quantityLabel", quantity); Ref(ui, "totalLabel", total); Ref(ui, "statusLabel", status); Ref(ui, "buyLabel", buyLabel); Ref(ui, "selectedIcon", selectedIcon);
        var uiData = new SerializedObject(ui); list = uiData.FindProperty("rows"); list.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var row = list.GetArrayElementAtIndex(i); row.FindPropertyRelative("button").objectReferenceValue = rows[i].button; row.FindPropertyRelative("frame").objectReferenceValue = rows[i].frame;
            row.FindPropertyRelative("icon").objectReferenceValue = rows[i].icon; row.FindPropertyRelative("name").objectReferenceValue = rows[i].name; row.FindPropertyRelative("effect").objectReferenceValue = rows[i].effect; row.FindPropertyRelative("price").objectReferenceValue = rows[i].price;
        }
        uiData.ApplyModifiedPropertiesWithoutUndo(); Ref(merchant, "shopUI", ui); window.gameObject.SetActive(false);
        var inventory = UnityEngine.Object.FindAnyObjectByType<InventoryPanelUI>(); var invData = new SerializedObject(inventory);
        foreach (string field in new[] { "selectLabel", "detailStats" })
        { var t = (TMP_Text)invData.FindProperty(field).objectReferenceValue; t.font = font; t.overflowMode = TextOverflowModes.Overflow; t.alignment = TextAlignmentOptions.Midline; EditorUtility.SetDirty(t); }
        var useButton = (Button)invData.FindProperty("selectWeaponButton").objectReferenceValue; useButton.GetComponent<RectTransform>().sizeDelta = new Vector2(276, 48);
        var useLabel = (TMP_Text)invData.FindProperty("selectLabel").objectReferenceValue; useLabel.rectTransform.sizeDelta = new Vector2(252, 44); useLabel.fontSize = 20;
        var buffRoot = Rect("Potion Guard HUD", pause.transform, 22, 342, 328, 44); var buffPanel = Picture("Guard Panel", buffRoot, 0, 0, 328, 44, Art("tooltip_frame"));
        var buffText = Text("Guard Remaining", buffPanel.transform, 14, 3, 300, 38, "", 23); buffText.color = new Color(.2f, .36f, .08f); buffText.alignment = TextAlignmentOptions.Midline;
        var buff = buffRoot.gameObject.AddComponent<PotionGuardUI>(); Ref(buff, "health", player.GetComponent<PlayerHealth>()); Ref(buff, "panel", buffPanel.gameObject); Ref(buff, "label", buffText); buffPanel.gameObject.SetActive(false);
        foreach (var owner in new UnityEngine.Object[] { wallet, merchant, use, useButton, useLabel }) { EditorUtility.SetDirty(owner); if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner); }
        buffRoot.SetAsFirstSibling(); root.SetAsLastSibling(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/PotionShopInstallation.txt", "Potion shop installed on " + npc.name + "\nRed: 50% HP / 25 Gold; Blue: 60% Energy / 20 Gold; Green: 20% guard for 30 gameplay seconds / 40 Gold.\n3 model prefabs, palette materials, icons, consumables, inventory use and joint Gold/inventory save.\n");
        Debug.Log("Potion shop installed with three usable and saved potions."); Selection.activeGameObject = npc.gameObject;
    }
    private static ItemDefinition[] BuildAssets()
    {
        foreach (string sub in new[] { "Materials", "Models", "Icons", "Definitions" }) Directory.CreateDirectory(Folder + "/" + sub); AssetDatabase.Refresh();
        var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/T_Magic_Palette_BC.png");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("URP Lit is unavailable.");
        string[] types = { "Matte", "Cloth", "Gem", "Metal" }; float[] metallic = { 0, 0, .18f, .62f }, smooth = { .28f, .05f, .76f, .62f }; var materials = new Material[4];
        for (int i = 0; i < types.Length; i++)
        {
            string path = Folder + "/Materials/Potion_" + types[i] + ".mat"; materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (materials[i] != null) continue;
            var m = new Material(shader) { name = "Potion_" + types[i], enableInstancing = true }; m.SetTexture("_BaseMap", palette); m.SetFloat("_Metallic", metallic[i]); m.SetFloat("_Smoothness", smooth[i]); AssetDatabase.CreateAsset(m, path); materials[i] = m;
        }
        string[] colors = { "Red", "Blue", "Green" }, names = { "โพชั่นฟื้นเลือด", "โพชั่นฟื้นพลัง", "โพชั่นเกราะเวท" };
        string[] descriptions = { "ฟื้นเลือด 50% ของเลือดสูงสุด\nใช้เมื่อบาดเจ็บ ไม่ชุบชีวิต\nเลือดเต็มจะไม่เสียขวด", "ฟื้นพลัง 60% ของพลังสูงสุด\nใช้พลังสำหรับวิ่งและแปลงร่าง\nพลังเต็มจะไม่เสียขวด", "ลดความเสียหาย 20% นาน 30 วิ\nนับเวลาเฉพาะขณะเล่น\nไม่ซ้อนทับเกราะเวทขวดเดิม" };
        var result = new ItemDefinition[3];
        for (int i = 0; i < 3; i++)
        {
            string prefabPath = $"{Folder}/Models/Potion_{colors[i]}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var root = new GameObject("Potion_" + colors[i]);
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Source}/FBX/SM_Magic_PotionBottle_{colors[i]}.fbx");
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source); model.transform.SetParent(root.transform, false); PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("UCX_", StringComparison.OrdinalIgnoreCase)).ToArray()) if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => materials[Mathf.Max(0, Array.FindIndex(types, type => m != null && m.name.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0))]).ToArray();
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                    var renderers = model.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) throw new InvalidOperationException("Bottle has no visible mesh.");
                    var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float scale = .28f / bounds.size.y; model.transform.localScale *= scale; model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            string iconPath = $"{Folder}/Icons/Potion_{colors[i]}.png"; if (!File.Exists(iconPath)) RenderIcon(prefab, iconPath); AssetDatabase.ImportAsset(iconPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            string itemPath = $"{Folder}/Definitions/Potion_{colors[i]}.asset"; var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, itemPath); var s = new SerializedObject(item);
                s.FindProperty("itemId").stringValue = "potion_" + colors[i].ToLowerInvariant(); s.FindProperty("displayName").stringValue = names[i]; s.FindProperty("description").stringValue = descriptions[i];
                s.FindProperty("category").enumValueIndex = (int)InventoryItemCategory.Consumable; s.FindProperty("maxStack").intValue = 10;
                s.FindProperty("potionEffect").enumValueIndex = i + 1; s.FindProperty("potionStrength").floatValue = i == 0 ? .5f : i == 1 ? .6f : .2f; s.FindProperty("potionDuration").floatValue = i == 2 ? 30 : 0;
                s.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath); s.ApplyModifiedPropertiesWithoutUndo();
            }
            result[i] = item;
        }
        return result;
    }
    private static void RenderIcon(GameObject prefab, string path)
    {
        var preview = new PreviewRenderUtility(); Texture2D image = null;
        try
        {
            preview.AddSingleGO(UnityEngine.Object.Instantiate(prefab)); var camera = preview.camera;
            camera.orthographic = true; camera.orthographicSize = .18f; camera.nearClipPlane = .01f; camera.farClipPlane = 20; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .11f, .14f, 0);
            camera.transform.position = new Vector3(.4f, .4f, -.8f); camera.transform.LookAt(Vector3.up * .14f);
            preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(45, 35, 0); preview.lights[1].intensity = .8f; preview.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0); preview.ambientColor = Color.gray * .8f;
            preview.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none); preview.Render(true); var rendered = preview.EndPreview(); var target = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
            try { Graphics.Blit(rendered, target); RenderTexture.active = target; image = new Texture2D(256, 256, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        finally { if (image != null) UnityEngine.Object.DestroyImmediate(image); preview.Cleanup(); }
    }
}
#endif
