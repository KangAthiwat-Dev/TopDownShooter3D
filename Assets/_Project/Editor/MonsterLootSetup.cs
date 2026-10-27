#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MonsterLootSetup
{
    public const string Folder = "Assets/_Project/Items/MonsterLoot";
    public const string Icons = "Assets/Assets_Imported/free-rpg-hunting-game-512x512-icons/PNG/without background";
    public const string PickupPath = Folder + "/WorldLootPickup.prefab";
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var data=new SerializedObject(owner);data.FindProperty(field).objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
    private static ItemDefinition Item(string id,string title,int iconNumber,string description)
    {
        string path=Folder+"/Items/"+id+".asset";
        var old=AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);if(old!=null)return old;
        string image=Icons+"/"+iconNumber+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(image);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=512;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=512;
        importer.SaveAndReimport();
        var item=ScriptableObject.CreateInstance<ItemDefinition>();item.name=title;
        var data=new SerializedObject(item);data.FindProperty("itemId").stringValue=id;data.FindProperty("displayName").stringValue=title;
        data.FindProperty("description").stringValue=description;data.FindProperty("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(image);
        data.FindProperty("category").enumValueIndex=(int)InventoryItemCategory.Resource;data.FindProperty("maxStack").intValue=99;data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item,path);return item;
    }
    private static MonsterDropTable Table(string id,params MonsterDropTable.Entry[] entries)
    {
        string path=Folder+"/Tables/"+id+".asset";var existing=AssetDatabase.LoadAssetAtPath<MonsterDropTable>(path);if(existing!=null)return existing;
        var table=ScriptableObject.CreateInstance<MonsterDropTable>();table.name=id;table.entries=entries;AssetDatabase.CreateAsset(table,path);return table;
    }
    private static LootPickup BuildPickup()
    {
        var existing=AssetDatabase.LoadAssetAtPath<LootPickup>(PickupPath);if(existing!=null)return existing;
        var shader=Shader.Find("TopDownShooter/Loot Icon");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Loot icon shader did not compile.");
        var material=new Material(shader){name="Loot_Icon_Unlit"};AssetDatabase.CreateAsset(material,Folder+"/Loot_Icon_Unlit.mat");
        var glow=new Material(shader){name="Loot_Halo_and_Trail"};AssetDatabase.CreateAsset(glow,Folder+"/Loot_Halo_and_Trail.mat");
        var root=new GameObject("WorldLootPickup");
        try
        {
            var pickup=root.AddComponent<LootPickup>();
            var visual=new GameObject("Billboard Visual").transform;visual.SetParent(root.transform,false);
            var image=new GameObject("Item Icon");image.transform.SetParent(visual,false);image.transform.localScale=Vector3.one*.85f;
            var sprite=image.AddComponent<SpriteRenderer>();sprite.sharedMaterial=material;sprite.sortingOrder=10;
            sprite.sprite=AssetDatabase.LoadAssetAtPath<ItemDefinition>(Folder+"/Items/monster_bone.asset").Icon;
            var halo=new GameObject("Ground Halo");halo.transform.SetParent(root.transform,false);halo.transform.localPosition=Vector3.down*.56f;
            var ring=halo.AddComponent<LineRenderer>();ring.useWorldSpace=false;ring.loop=true;ring.positionCount=32;ring.widthMultiplier=.025f;
            ring.sharedMaterial=glow;ring.startColor=ring.endColor=new Color(1,.74f,.32f,.45f);
            ring.shadowCastingMode=ShadowCastingMode.Off;ring.receiveShadows=false;
            for(int i=0;i<32;i++){float a=i*Mathf.PI/16;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*.3f,0,Mathf.Sin(a)*.3f));}
            var tailObject=new GameObject("Magnet Trail");tailObject.transform.SetParent(root.transform,false);var trail=tailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial=glow;trail.time=.13f;trail.minVertexDistance=.06f;trail.startWidth=.065f;trail.endWidth=0;
            trail.startColor=new Color(1,.85f,.47f,.65f);trail.endColor=new Color(1,.85f,.47f,0);trail.emitting=false;
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.autodestruct=false;
            Ref(pickup,"icon",sprite);Ref(pickup,"visual",visual);Ref(pickup,"halo",halo);Ref(pickup,"trail",trail);
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=LayerMask.NameToLayer("Ignore Raycast");
            return PrefabUtility.SaveAsPrefabAsset(root,PickupPath).GetComponent<LootPickup>();
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    [MenuItem("Tools/Enemies/Monster Loot/Install Drops and Player Magnet")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        EnsureFolder(Folder+"/Items");EnsureFolder(Folder+"/Tables");
        var bone=Item("monster_bone","Monster Bone",13,"A sturdy monster bone. Keep it as a hunting material for future trading or crafting.");
        var fang=Item("monster_fang","Monster Fang",1,"A sharp fang harvested from a monster. A hunting material for future trading or crafting.");
        var claw=Item("monster_claw","Monster Claw",4,"A curved monster claw. A hunting material for future trading or crafting.");
        var hide=Item("beast_hide","Beast Hide",25,"A thick piece of beast hide. A hunting material for future trading or crafting.");
        var meat=Item("raw_meat","Raw Meat",20,"Raw meat from a hunted beast. Stored as a material; cooking and consumption come later.");
        var horn=Item("demon_horn","Demon Horn",2,"A horn from a demon creature. A hunting material for future trading or crafting.");
        var wing=Item("demon_wing","Demon Wing",7,"A leathery wing from a demon. A hunting material for future trading or crafting.");
        var scale=Item("dragon_scale","Dragon Scale",16,"A tough dragon scale. A rare hunting material for future trading or crafting.");
        MonsterDropTable.Entry E(ItemDefinition item,int min,int max,float chance=1)=>new MonsterDropTable.Entry{item=item,minimum=min,maximum=max,chance=chance};
        var tables=new Dictionary<string,MonsterDropTable>
        {
            ["Enemy_Spider"]=Table("Spider",E(fang,1,2),E(claw,1,1,.3f)),
            ["Enemy_Bear"]=Table("Bear",E(hide,1,2),E(meat,1,3,.65f),E(claw,1,1,.35f)),
            ["Enemy_Imp_Green"]=Table("Imp_Green",E(horn,1,1),E(wing,1,1,.2f)),
            ["Enemy_Imp_Blue"]=Table("Imp_Blue",E(horn,1,2),E(wing,1,1,.3f)),
            ["Enemy_Imp_Red"]=Table("Imp_Red",E(horn,2,2),E(wing,1,1,.45f)),
            ["Enemy_Puglin"]=Table("Puglin",E(hide,1,2),E(claw,1,2,.5f)),
            ["Enemy_Skeleton"]=Table("Skeleton",E(bone,1,3)),
            ["Enemy_GoblinDemon"]=Table("Goblin_Demon",E(horn,1,1),E(bone,1,2,.35f),E(claw,1,1,.25f)),
            ["Enemy_GoblinChief"]=Table("Goblin_Chief",E(horn,2,4),E(bone,2,3,.6f),E(claw,1,2,.5f)),
            ["KingOfDemon_Boss"]=Table("KingOfDemon",E(horn,4,7),E(wing,2,4),E(bone,3,5)),
            ["DemonLord_Boss"]=Table("DemonLord",E(horn,3,5),E(wing,1,3),E(claw,2,4)),
            ["DragonNightmare_Guardian"]=Table("DragonNightmare",E(scale,4,7),E(claw,2,3),E(bone,2,4,.7f))
        };
        var pickup=BuildPickup();
        Directory.CreateDirectory("Logs/SceneBackups");var scene=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeMonsterLoot_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        foreach(var pair in tables)
        {
            string path="Assets/_Project/Prefabs/Enemies/"+pair.Key+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var dropper=root.GetComponent<EnemyLootDropper>();if(dropper==null)dropper=root.AddComponent<EnemyLootDropper>();
                Ref(dropper,"table",pair.Value);PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var pair in new[]{("KingOfDemon","KingOfDemon_Boss"),("DemonLord","DemonLord_Boss"),("DragonNightmare","DragonNightmare_Guardian")})
        {
            var actor=GameObject.Find(pair.Item1);if(actor==null)throw new InvalidOperationException("Missing scene boss "+pair.Item1);
            var dropper=actor.GetComponent<EnemyLootDropper>();if(dropper==null)dropper=Undo.AddComponent<EnemyLootDropper>(actor);
            Ref(dropper,"table",tables[pair.Item2]);EditorUtility.SetDirty(dropper);
            if(PrefabUtility.IsPartOfPrefabInstance(dropper))PrefabUtility.RecordPrefabInstancePropertyModifications(dropper);
        }
        var world=UnityEngine.Object.FindAnyObjectByType<MonsterLootWorld>();
        if(world==null)
        {
            var go=new GameObject("Monster_Loot");go.transform.SetParent(GameObject.Find("World").transform,false);world=go.AddComponent<MonsterLootWorld>();
        }
        var player=GameObject.FindGameObjectWithTag("Player");
        Ref(world,"pickupPrefab",pickup);Ref(world,"inventory",player.GetComponent<PlayerInventory>());Ref(world,"playerHealth",player.GetComponent<PlayerHealth>());
        Ref(world,"view",Camera.main);Ref(world,"terrain",UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain);
        EditorUtility.SetDirty(world);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=world.gameObject;Validate();
    }

    [MenuItem("Tools/Enemies/Monster Loot/Validate Drops and Items")]
    public static void Validate()
    {
        var report=new StringBuilder("Monster drops validation "+DateTime.Now.ToString("s")+"\n");
        var items=AssetDatabase.FindAssets("t:ItemDefinition",new[]{Folder}).Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        if(items.Length!=8||items.Select(i=>i.ItemId).Distinct().Count()!=8)throw new InvalidOperationException("Expected 8 unique hunting materials.");
        foreach(var item in items)
        {
            if(item.Icon==null||item.MaxStack!=99)throw new InvalidOperationException("Invalid item "+item.name);
            report.AppendLine("PASS "+item.ItemId+" icon="+AssetDatabase.GetAssetPath(item.Icon)+" stack="+item.MaxStack);
        }
        var tables=AssetDatabase.FindAssets("t:MonsterDropTable",new[]{Folder});
        if(tables.Length!=12)throw new InvalidOperationException("Expected 12 enemy drop tables.");
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/Prefabs/Enemies"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            if(Path.GetFileNameWithoutExtension(path)=="EnemyBase"||Path.GetFileNameWithoutExtension(path)=="EnemyDummy")continue;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var droppers=prefab.GetComponents<EnemyLootDropper>();
            if(droppers.Length!=1||droppers[0].Table==null||!droppers[0].Table.entries.Any(e=>e.item!=null&&e.chance==1))throw new InvalidOperationException("Missing guaranteed enemy drop "+path);
            report.AppendLine("PASS "+prefab.name+" table="+droppers[0].Table.name+" (one death listener)");
        }
        var world=UnityEngine.Object.FindAnyObjectByType<MonsterLootWorld>();if(world==null||world.Inventory==null)throw new InvalidOperationException("Missing world/player integration.");
        report.AppendLine("PASS World loot connects to the player's existing 8 supplies; 2 melee slots remain separate.");
        File.WriteAllText("Logs/MonsterLootValidation.txt",report.ToString());Debug.Log(report.ToString());
    }
}
#endif
