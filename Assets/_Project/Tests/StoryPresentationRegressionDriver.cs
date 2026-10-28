#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public sealed class StoryPresentationRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private readonly List<string> errors = new List<string>();
    private bool done, backgroundBefore;
    private int fpsBefore, vsyncBefore;
    private float timeBefore;
    private PlayerMovement player;
    private InventoryPanelUI bag;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private string walletBefore;
    private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    private IEnumerator Start()
    {
        backgroundBefore = Application.runInBackground; fpsBefore = Application.targetFrameRate;
        vsyncBefore = QualitySettings.vSyncCount; timeBefore = Time.timeScale;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Story NPC / live kingdom minimap regression " + DateTime.Now.ToString("s"));
        var routine = Checks();
        while (true)
        {
            object next = null; Exception failure = null; bool moved = false;
            try { moved = routine.MoveNext(); if (moved) next = routine.Current; }
            catch (Exception error) { failure = error; }
            if (failure != null) { Finish(failure); yield break; }
            if (!moved) break;
            yield return next;
        }
        Finish(null);
    }

    private IEnumerator Checks()
    {
        yield return null;
        player = FindAnyObjectByType<PlayerMovement>(); startPosition = player.transform.position; startRotation = player.transform.rotation;
        walletBefore = player.GetComponent<PlayerGoldWallet>().CaptureSave();
        // Disable encounters only in the disposable Play Mode scene; never edit inventory or persistent progress.
        foreach (var director in FindObjectsByType<WorldMonsterDirector>(FindObjectsSortMode.None)) director.gameObject.SetActive(false);
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)) spawner.gameObject.SetActive(false);
        foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        player.enabled = false;
        var map = FindAnyObjectByType<KingdomMinimapUI>();
        var area = Get<RectTransform>(map, "mapArea");
        var marker = Get<RectTransform>(map, "playerMarker");
        var marks = Get<KingdomMinimapUI.LandmarkView[]>(map, "landmarks");
        Check(FindObjectsByType<KingdomMinimapUI>(FindObjectsSortMode.None).Length == 1 && map.Player == player.transform, "One minimap follows the actual player");
        Check(area.GetComponent<RawImage>().texture != null && area.GetComponent<RectMask2D>() != null, "Baked world overview loads inside the clipped map area");
        Check(map.LandmarkCount == 4 && marks.Select(v => v.landmark.displayName).Distinct().Count() == 4, "Four unique named landmarks are connected");
        var hud = FindAnyObjectByType<AdventureHudUI>();
        Check(Get<RectTransform>(hud,"compass") == null && Get<TMP_Text>(hud,"heading") == null, "Old compass and heading are disconnected");
        Check(map.GetComponentsInChildren<MinimapPlayerArrow>().All(a => a.GetComponent<CanvasRenderer>() != null), "Player arrow and legend have valid UI renderers");
        Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).All(c => !c.name.Contains("kingdom overview")), "No live minimap camera adds world-rendering cost");
        var actors = FindObjectsByType<StoryActor>(FindObjectsSortMode.None);
        var guard = actors.Single(a => a.ActorId == "oakshade_guard");
        var animator = guard.GetComponentInChildren<Animator>();
        Check(animator.isHuman && animator.avatar.isValid && animator.runtimeAnimatorController != null, "Village guard uses a valid animated Humanoid rig");
        var skins = guard.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skin in skins) report.AppendLine($"GUARD {skin.name}: {skin.sharedMesh?.vertexCount} vertices, enabled={skin.enabled}");
        Check(skins.Count(s => s.name.StartsWith("Starter_") && s.sharedMesh != null) >= 6, "Existing modular armor is bound to the guard skeleton");
        Check(skins.SelectMany(s => s.sharedMaterials).All(m => m != null && m.shader.isSupported && !ShaderUtil.ShaderHasError(m.shader)), "Guard materials resolve to supported shaders");
        Check(guard.GetComponent<NavMeshObstacle>() != null && NavMesh.SamplePosition(guard.transform.position, out var ground, 1, NavMesh.AllAreas) && Mathf.Abs(ground.position.y - guard.transform.position.y) < .2f, "Guard stands on a reachable village NavMesh patch");
        Check(actors.Any(a => a.ActorId == "silverwell_alchemist" && a.GetComponent<PotionMerchant>() != null) && actors.Any(a => a.ActorId == "silverwell_guild_receptionist" && a.GetComponent<GuildBuyerInteractable>() != null) && actors.Any(a => a.ActorId == "silverwell_blacksmith" && a.GetComponent<WeaponMerchant>() != null), "Three existing shop NPCs retain their working services and story identities");
        var boss = Resources.FindObjectsOfTypeAll<KingOfDemonAI>().Single(b => b.gameObject.scene.IsValid());
        Check(boss.DisplayName == "MALACHAR" && boss.GetComponent<StoryActor>().ActorId == "malachar", "KingOfDemon is named Malachar without replacing its AI");
        Check(new[] { "Red", "Blue", "Green" }.All(c => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Items/Potions/Models/Potion_" + c + ".prefab") != null), "All three approved potion models remain available");
        yield return new WaitForSecondsRealtime(.25f);
        float idleTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        yield return new WaitForSecondsRealtime(.25f);
        Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > idleTime, "Guard idle animation advances in the running game");
        foreach (var view in marks)
        {
            Warp(view.landmark.transform.position);
            yield return new WaitForSecondsRealtime(.3f);
            Check(Vector2.Distance(map.DisplayedPlayerPosition,map.WorldToMap(player.transform.position)) < .01f, "Live player marker matches " + view.landmark.displayName);
            Check(Vector2.Distance(view.marker.anchoredPosition,map.WorldToMap(view.landmark.transform.position)) < .01f && view.label.text == view.landmark.mapLabel, "Location label and anchor match " + view.landmark.displayName);
        }
        var min = map.WorldMinimum; var max = map.WorldMaximum;
        var center = new Vector3((min.x+max.x)*.5f, 10, (min.y+max.y)*.5f);
        Warp(center); yield return null;
        Check(map.DisplayedPlayerPosition.sqrMagnitude < .001f, "World center maps to image center");
        Warp(center + Vector3.forward * 5); yield return null;
        Check(map.DisplayedPlayerPosition.y > 0 && Mathf.Abs(map.DisplayedPlayerPosition.x) < .01f, "World north points up on the map");
        Warp(center + Vector3.right * 5); player.transform.rotation = Quaternion.Euler(0,90,0); yield return null;
        Check(map.DisplayedPlayerPosition.x > 0 && Vector3.Dot(marker.up,Vector3.right) > .99f, "World east maps right and the player arrow follows east-facing yaw");
        Warp(new Vector3(max.x+100,10,max.y+100)); yield return null;
        Check(map.DisplayedPlayerPosition.x <= area.rect.width*.5f-12 && map.DisplayedPlayerPosition.y <= area.rect.height*.5f-12, "Out-of-bounds player arrow remains visible within the frame");
        var city = marks.Single(m => m.landmark.displayName == "Silverwell Town");
        Warp(city.landmark.transform.position); yield return new WaitForSecondsRealtime(.6f);
        Check(hud.DisplayedArea == "SILVERWELL TOWN", "Town HUD uses the story location name");
        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/StoryMinimap_Play.png",2);
        yield return new WaitForSecondsRealtime(.2f);
        bag = FindAnyObjectByType<InventoryPanelUI>();
        Check(bag.TryOpen(), "Inventory remains available with the minimap installed");
        yield return null;
        var branch = Get<GameObject>(bag,"window").transform;
        while (branch.parent != map.transform.parent && branch.parent != null) branch = branch.parent;
        Check(branch.parent == map.transform.parent && branch.GetSiblingIndex() > map.transform.GetSiblingIndex(), "Inventory modal draws above the ambient minimap");
        bag.Close();
        Warp(guard.transform.position + guard.transform.forward * 2);
        yield return new WaitForSecondsRealtime(1.5f);
        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/StoryGuard_Play.png",2);
        Check(hud.DisplayedArea == "OAKSHADE VILLAGE", "Village HUD uses the story location name");
        Check(walletBefore == player.GetComponent<PlayerGoldWallet>().CaptureSave(), "Verification leaves Gold, items and equipped weapons unchanged");
        Check(errors.Count == 0, "No runtime errors or missing glyph warnings during map and NPC checks");
        yield return new WaitForSecondsRealtime(.2f);
    }

    private void Warp(Vector3 point)
    {
        var controller = player.GetComponent<CharacterController>(); bool enabledBefore = controller.enabled;
        controller.enabled = false; player.transform.position = point; controller.enabled = enabledBefore; Physics.SyncTransforms();
    }
    private void Check(bool condition, string label)
    {
        report.AppendLine((condition ? "PASS " : "FAIL ") + label);
        File.WriteAllText("Logs/StoryPresentationPlaytest.txt", report.ToString());
        if (!condition) throw new InvalidOperationException(label);
    }
    private void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning && message.Contains("Unicode")) errors.Add(message + "\n" + trace);
    }
    private void Finish(Exception failure)
    {
        if (done) return; done = true;
        if (bag != null && bag.IsOpen) bag.Close();
        if (player != null) { Warp(startPosition); player.transform.rotation = startRotation; player.enabled = true; }
        Application.logMessageReceived -= OnLog;
        Application.runInBackground = backgroundBefore; Application.targetFrameRate = fpsBefore; QualitySettings.vSyncCount = vsyncBefore; Time.timeScale = timeBefore;
        if (failure != null) report.AppendLine("EXCEPTION " + failure);
        foreach (var error in errors) report.AppendLine("RUNTIME ERROR " + error);
        bool passed = failure == null && errors.Count == 0;
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        File.WriteAllText("Logs/StoryPresentationPlaytest.txt",report.ToString());
        Debug.Log("Story presentation verification complete. Passed=" + passed);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!done) Finish(new InvalidOperationException("Story verification interrupted.")); }
}
#endif
