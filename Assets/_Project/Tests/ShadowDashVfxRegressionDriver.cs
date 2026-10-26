#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(1000)]
public class ShadowDashVfxRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private PlayerMovement movement;
    private PlayerDashVisual dashVisual;
    private PlayerEnergy energy;
    private PlayerInput input;
    private ShadowDashTrailVfx effect;
    private TrailRenderer glow, smoke, distortion, shortTrail;
    private AudioSource audioLoop;
    private Renderer[] body;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? originalDevices;
    private int originalFps, originalVSync;
    private bool originalBackground, finished;
    private string runtimeError;
    private ShadowDashWindVfx wind;
    private bool monitorCover, previousHidden;

    private IEnumerator Start()
    {
        originalFps = Application.targetFrameRate;
        originalVSync = QualitySettings.vSyncCount;
        originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog;
        report.AppendLine("Shadow Trail regression — " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>();
        stack.Push(RunChecks());
        while (stack.Count > 0)
        {
            object next = null;
            Exception failure = null;
            try
            {
                if (!stack.Peek().MoveNext()) stack.Pop();
                else next = stack.Peek().Current;
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null) { Finish(failure); yield break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }

    private IEnumerator RunChecks()
    {
        foreach (string name in new[] { "TopDownShooter/VFX/Shadow Trail URP", "TopDownShooter/VFX/Shadow Trail Distortion URP" })
        {
            Shader shader = Shader.Find(name);
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader compiles: " + name);
        }
        movement = FindAnyObjectByType<PlayerMovement>();
        dashVisual = movement.GetComponent<PlayerDashVisual>();
        energy = movement.GetComponent<PlayerEnergy>();
        input = movement.GetComponent<PlayerInput>();
        effect = movement.GetComponentInChildren<ShadowDashTrailVfx>(true);
        Check(effect != null && Get<ShadowDashTrailVfx>(dashVisual, "shadowDashVfx") == effect, "Scene wires the new VFX controller");
        Transform visual = Get<Transform>(dashVisual, "visual");
        Check(!effect.transform.IsChildOf(visual), "VFX is outside the hierarchy hidden during Shadow Dash");
        body = visual.GetComponentsInChildren<Renderer>(true);
        glow = Get<TrailRenderer>(effect, "glowTrail");
        smoke = Get<TrailRenderer>(effect, "smokeTrail");
        distortion = Get<TrailRenderer>(effect, "distortionTrail");
        audioLoop = Get<AudioSource>(effect, "loopAudio");
        shortTrail = Get<TrailRenderer>(dashVisual, "dashTrail");
        ParticleSystem legacy = Get<ParticleSystem>(dashVisual, "shadowDashParticles");
        Check(legacy == null || !legacy.gameObject.activeSelf, "Legacy particles are retained but disabled");

        foreach (EnemyWaveSpawner spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>()) enemy.gameObject.SetActive(false);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(1000f, 9.4f, 1000f);
        floor.transform.localScale = new Vector3(150f, 1f, 150f);
        Teleport(new Vector3(1000f, 10f, 1000f));
        keyboard = InputSystem.AddDevice<Keyboard>("ShadowVfxRegressionKeyboard");
        var devices = input.actions.devices;
        if (devices.HasValue)
        {
            var copy = new InputDevice[devices.Value.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = devices.Value[i];
            originalDevices = new ReadOnlyArray<InputDevice>(copy);
        }
        input.actions.devices = new InputDevice[] { keyboard };
        yield return new WaitForSeconds(0.15f);
        Check(!effect.IsEmitting && (glow == null || (!glow.emitting && !glow.enabled)) && !audioLoop.isPlaying, "No visible trail or loop sound on spawn");

        Keys(Key.W);
        yield return new WaitForSeconds(0.25f);
        Check(!effect.IsEmitting && (glow == null || (!glow.emitting && !glow.enabled)), "Normal movement does not emit Shadow VFX");
        Keys(Key.W, Key.Space);
        yield return new WaitForSeconds(0.08f);
        Check(movement.IsDashing && shortTrail.emitting && !effect.IsEmitting, "Short Dash keeps its original independent trail");
        if (effect.UsesIceForm)
            Check(HasBaseState("DashShield") && CurrentClipContains("Shield_Dash"), "Normal Dash uses the UAL2 Shield_Dash animation");
        Keys();
        yield return new WaitForSeconds(0.3f);
        if (effect.UsesIceForm)
        {
            yield return RunIceChecks();
            yield break;
        }
        if (effect.UsesWindVortex)
        {
            yield return RunWindChecks();
            yield break;
        }
        Keys(Key.W, Key.M);
        yield return new WaitForSeconds(0.45f);
        Check(movement.IsShadowDashing && effect.IsEmitting && glow.emitting && smoke.emitting, "M starts both Shadow trails");
        Check(glow.positionCount > 2 && smoke.positionCount > 2, "Trails generate actual moving geometry");
        Check(glow.enabled && smoke.enabled && AllBodyHidden(), "Character hides, but Shadow trails remain visible");
        Check(movement.GetComponent<PlayerHealth>().IsInvulnerable && energy.CurrentEnergy < energy.MaxEnergy, "Invulnerability and energy drain stay intact");
        Check(audioLoop.isPlaying && !audioLoop.playOnAwake && audioLoop.volume > 0f, "Looping SFX plays only during Shadow Dash");
        Directory.CreateDirectory("Logs");
        ScreenCapture.CaptureScreenshot("Logs/ShadowTrail_Purple.png");
        yield return new WaitForSeconds(0.1f);

        Material shared = glow.sharedMaterial;
        float sharedIntensity = shared.GetFloat("_IntensityMultiplier");
        GameObject cloneObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/ShadowDashTrails.prefab"));
        cloneObject.transform.position = movement.transform.position + Vector3.right * 30f;
        var clone = cloneObject.GetComponent<ShadowDashTrailVfx>();
        TrailRenderer cloneGlow = Get<TrailRenderer>(clone, "glowTrail");
        Color cloneColor = cloneGlow.colorGradient.Evaluate(0.2f);
        effect.ApplyPreset(ShadowTrailPreset.AzureVoid);
        effect.SetDimensions(1.1f, 0.24f);
        effect.SetIntensity(0.7f);
        yield return new WaitForSeconds(0.15f);
        Check(Mathf.Approximately(glow.widthMultiplier, 1.1f) && Mathf.Approximately(glow.time, 0.24f), "Width and lifetime update live");
        Check(glow.colorGradient.Evaluate(0.2f).b > glow.colorGradient.Evaluate(0.2f).r, "Azure preset updates live");
        Check(cloneGlow.sharedMaterial == shared && cloneGlow.colorGradient.Evaluate(0.2f) == cloneColor &&
              Mathf.Approximately(shared.GetFloat("_IntensityMultiplier"), sharedIntensity), "Customization is isolated per instance; shared materials stay unchanged");
        var properties = new MaterialPropertyBlock();
        glow.GetPropertyBlock(properties);
        Check(Mathf.Approximately(properties.GetFloat("_IntensityMultiplier"), 0.7f), "Intensity uses a per-instance property block");
        ScreenCapture.CaptureScreenshot("Logs/ShadowTrail_Azure.png");
        yield return new WaitForSeconds(0.05f);
        effect.ApplyPreset(ShadowTrailPreset.CrimsonDark);
        yield return new WaitForSeconds(0.15f);
        ScreenCapture.CaptureScreenshot("Logs/ShadowTrail_Crimson.png");
        yield return new WaitForSeconds(0.05f);
        effect.ApplyPreset(ShadowTrailPreset.PurpleShadow);
        effect.SetDimensions(0.8f, 0.32f);
        effect.SetIntensity(1f);
        Destroy(cloneObject);

        Camera main = Camera.main;
        if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            CameraOverrideOption previous = data.requiresColorOption;
            data.requiresColorOption = CameraOverrideOption.Off;
            yield return null;
            yield return null;
            Check(!distortion.enabled && glow.emitting, "Without Opaque Texture only refraction disables; core VFX stays active");
            data.requiresColorOption = previous;
            yield return null;
        }
        Teleport(movement.transform.position + Vector3.right * 25f);
        yield return null;
        yield return null;
        var points = new Vector3[glow.positionCount];
        glow.GetPositions(points);
        foreach (Vector3 point in points)
            Check(Vector3.Distance(point, movement.transform.position) < 7f, "Teleport clears old distant geometry", false);
        Check(true, "Teleport does not connect a trail across the map");

        Keys();
        yield return new WaitForSeconds(0.04f);
        Check(!effect.IsEmitting && !glow.emitting && glow.positionCount > 0, "Release stops emission but preserves the fading tail");
        Check(!AllBodyHidden(), "Character restores when Shadow Dash ends");
        yield return new WaitForSeconds(effect.TrailLifetime + 0.15f);
        Check(!glow.enabled && !smoke.enabled && !distortion.enabled && !audioLoop.isPlaying, "All tails and loop audio expire after release");

        Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W);
        yield return new WaitForSeconds(0.2f);
        Keys(Key.M, Key.W, Key.Space);
        yield return new WaitForSeconds(0.06f);
        Check(movement.IsShadowDashing && !movement.IsDashing && effect.IsEmitting,
            "Space during Shadow Dash preserves the existing no-interruption rule and does not mix effects");
        Keys();
        yield return new WaitForSeconds(0.15f);
        Keys(Key.W, Key.Space);
        yield return new WaitForSeconds(0.06f);
        Check(movement.IsDashing && !effect.IsEmitting && shortTrail.emitting, "After releasing Shadow Dash, Short Dash uses only its own trail");
        Keys();
        yield return new WaitForSeconds(0.8f);
        Set(energy, "<CurrentEnergy>k__BackingField", 30f);
        Keys(Key.M, Key.W);
        yield return new WaitForSeconds(1.5f);
        Check(!movement.IsShadowDashing && !effect.IsEmitting && !audioLoop.isPlaying && !AllBodyHidden(), "Energy exhaustion restores character and stops trails/audio");
        yield return new WaitForSeconds(0.8f);
        Check(!effect.IsEmitting, "Holding M after exhaustion does not restart automatically");
        Keys();
        yield return new WaitForSeconds(0.1f);
        Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W);
        yield return new WaitForSeconds(0.2f);
        dashVisual.enabled = false;
        yield return null;
        Check(!effect.IsEmitting && glow.positionCount <= 1 && !audioLoop.isPlaying && !AllBodyHidden(), "Disabling the visual controller cleans up immediately");
        dashVisual.enabled = true;
        Keys();
        yield return new WaitForSeconds(0.1f);
        Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W);
        yield return new WaitForSeconds(0.15f);
        movement.enabled = false;
        yield return new WaitForSeconds(0.2f);
        Check(!effect.IsEmitting && !audioLoop.isPlaying && !AllBodyHidden(), "Disabling movement cannot leave Shadow VFX running");
        Check(runtimeError == null, runtimeError);
    }

    private IEnumerator RunIceChecks()
    {
        ShadowDashIceForm form = effect.IceForm;
        Transform visual = Get<Transform>(dashVisual, "visual");
        Renderer head = Get<MeshRenderer>(form, "headRenderer");
        foreach (string name in new[] { "TopDownShooter/VFX/Ice Trail Comet Head", "Vefects/SH_Vefects_URP_VFX_Character_Showoff" })
        {
            Shader shader = Shader.Find(name);
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader compiles: " + name);
        }
        Check(!effect.UsesWindVortex && !effect.GetComponent<ShadowDashWindVfx>().enabled,
            "Rigid wind geometry is disabled; actual imported Ice TrailRenderers are installed");
        Material originalIce = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vefects/Trails VFX/VFX/Materials/M_VFX_BIRP_Trail_Ice_01.mat");
        Check(glow.sharedMaterial.GetTexture("_Mask") == originalIce.GetTexture("_Mask") &&
            glow.sharedMaterial.GetTexture("_Distortion") == originalIce.GetTexture("_Distortion"), "Ice tail retains the real asset's mask/noise textures");
        Check(head.GetComponent<Collider>() == null && head.GetComponent<MeshFilter>().sharedMesh.vertexCount > 100 && glow.numCapVertices >= 8,
            "Leading head is a rounded 3D volume with no gameplay collider; tail caps are rounded too");
        Check(!movement.GetComponentInChildren<Animator>().applyRootMotion, "Humanoid dash clips do not drive the gameplay transform through root motion");
        var equipment = movement.GetComponent<PlayerMeleeEquipment>();
        Check(equipment.TryEquip(AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>("Assets/_Project/Prefabs/Weapons/TrainingClub.asset")), "Equip a back weapon for the real transformation");
        yield return new WaitForSeconds(0.1f);
        GameObject backWeapon = Get<GameObject[]>(equipment, "weaponVisuals")[0];
        Check(!form.IsHeadVisible && !AllBodyHidden(), "No ice head or hidden player while idle");
        Keys(Key.M, Key.W);
        yield return new WaitForSeconds(0.07f);
        yield return new WaitForEndOfFrame();
        Check(movement.IsShadowDashing && form.IsEntering && !AllBodyHidden() && !effect.IsEmitting && backWeapon.activeSelf,
            "M responds immediately, showing Roll before the body/weapon transforms and tails emit");
        Check(HasBaseState("ShadowRollEnter") && CurrentClipContains("ShadowRoll_Enter"), "Entry plays only the start of UAL1 Roll");
        yield return CaptureIce("Roll", true);
        yield return new WaitForSeconds(form.EntryDuration);
        yield return new WaitForEndOfFrame();
        Check(effect.IsEmitting && glow.emitting && smoke.emitting && glow.positionCount > 3 && AllBodyHidden() && form.IsHeadVisible,
            "Roll hands off to the real Ice tail plus rounded comet head");
        Check(visual.localScale.sqrMagnitude < 0.001f && !backWeapon.activeSelf, "Player and back weapon smoothly shrink/hide into the form");
        Check(movement.GetComponent<PlayerHealth>().IsInvulnerable && energy.CurrentEnergy < energy.MaxEnergy && audioLoop.isPlaying,
            "Energy, invulnerability and the restored Ice loop sound work normally");
        Check(Mathf.Abs(form.Axis.y) < 0.001f && Vector3.Dot(form.Axis, Vector3.forward) > 0.95f, "Rounded head faces along the horizontal dash direction");
        yield return CaptureIce("Active", true);
        yield return CaptureIce("GameView", false);

        Material shared = glow.sharedMaterial;
        Color originalShared = shared.GetColor("_Color01");
        GameObject cloneObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/ShadowDashTrails.prefab"));
        cloneObject.transform.position = movement.transform.position + Vector3.right * 30f;
        var clone = cloneObject.GetComponent<ShadowDashTrailVfx>();
        Color clonePalette = Get<Color>(clone, "materialColor1");
        float cloneRadius = Get<float>(clone.IceForm, "radius");
        effect.ApplyPreset(ShadowTrailPreset.Fire); effect.SetDimensions(1.4f, 0.24f); Set(form, "radius", 0.85f);
        yield return null; yield return new WaitForEndOfFrame();
        var properties = new MaterialPropertyBlock();
        glow.GetPropertyBlock(properties); Color tailColor = properties.GetColor("_Color01");
        head.GetPropertyBlock(properties); Color headColor = properties.GetColor("_CoreColor");
        Check(tailColor.r > tailColor.b && headColor == tailColor && Mathf.Approximately(glow.widthMultiplier, 1.4f),
            "Preset, head/tail colors and tail dimensions customize together live");
        Check(clonePalette == Get<Color>(clone, "materialColor1") && Mathf.Approximately(Get<float>(clone.IceForm, "radius"), cloneRadius) &&
            shared.GetColor("_Color01") == originalShared, "Per-instance customization never changes another prefab instance or shared material");
        Set(effect, "overrideMaterialColors", false); effect.RefreshAppearance();
        yield return null; yield return new WaitForEndOfFrame();
        glow.GetPropertyBlock(properties);
        Check(properties.GetColor("_Color01") == originalShared, "Disabling the color override restores the source Ice material palette");
        effect.ApplyPreset(ShadowTrailPreset.Ice); effect.SetDimensions(1.35f, 0.3f); Set(form, "radius", 0.72f); Destroy(cloneObject);
        Camera main = Camera.main;
        if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            CameraOverrideOption previous = data.requiresColorOption; data.requiresColorOption = CameraOverrideOption.Off;
            yield return null; yield return null;
            Check(!distortion.enabled && glow.emitting && form.IsHeadVisible, "Without Opaque Texture only refraction turns off; head and Ice trails remain");
            data.requiresColorOption = previous;
        }
        Keys(Key.M, Key.D); yield return new WaitForSeconds(0.3f);
        Check(Vector3.Dot(form.Axis, Vector3.right) > 0.95f, "Comet head smoothly follows a direction change");
        Teleport(movement.transform.position + Vector3.right * 25f); yield return null; yield return null;
        var points = new Vector3[glow.positionCount]; glow.GetPositions(points);
        foreach (Vector3 point in points) Check(Vector3.Distance(point, effect.transform.position) < 8f, "No teleport bridge", false);
        Check(true, "Teleport clears the tail without stretching it across the map");

        Keys(); yield return new WaitForSeconds(0.12f); yield return new WaitForEndOfFrame();
        Check(!effect.IsEmitting && !glow.emitting && glow.enabled && form.IsHeadVisible && !AllBodyHidden() && form.CharacterScale > 0.5f,
            "Release fades the comet, preserves the dying tail, and smoothly reveals the player");
        Check(HasBaseState("ShadowNinjaLand") && CurrentClipContains("ShadowNinjaJump_Land"), "Return plays UAL2 NinjaJump_Land rather than a standing pop");
        PlayerAnimationController animation = movement.GetComponent<PlayerAnimationController>();
        Check(!animation.TryPlayMeleeAttack(false) && !animation.TryPlayPickup(), "Melee and pickup cannot replace the landing impact pose");
        yield return CaptureIce("Land", true);
        yield return new WaitForSeconds(0.5f);
        Check(!form.IsHeadVisible && !glow.enabled && !smoke.enabled && !distortion.enabled && !audioLoop.isPlaying &&
            !AllBodyHidden() && visual.localScale == Vector3.one && backWeapon.activeSelf, "Landing settles to locomotion with full-size body, weapons and no lingering effects");
        Check(HasBaseState("Locomotion"), "Landing blends back into the locomotion tree");

        Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(0.07f);
        Keys(); yield return new WaitForSeconds(0.04f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(form.EntryDuration + 0.1f);
        Check(AllBodyHidden() && form.IsHeadVisible, "Very short taps and rapid re-entry cannot leave a stuck partial form");
        Keys(Key.M, Key.W, Key.Space); yield return new WaitForSeconds(0.04f);
        Check(movement.IsShadowDashing && !movement.IsDashing, "Space cannot interrupt held Shadow Dash (existing gameplay rule)");
        Keys(); yield return new WaitForSeconds(0.04f);
        Keys(Key.W, Key.Space); yield return new WaitForSeconds(0.06f); yield return new WaitForEndOfFrame();
        Check(movement.IsDashing && HasBaseState("DashShield") && !form.IsHeadVisible && !glow.enabled && !AllBodyHidden() && visual.localScale.magnitude > 1f,
            "Space after release cleanly interrupts the visual landing with full-size Shield Dash and its original trail");
        Keys(); yield return new WaitForSeconds(0.8f);
        Set(energy, "<CurrentEnergy>k__BackingField", 20f); Keys(Key.M, Key.W);
        yield return new WaitForSeconds(1.4f);
        Check(!effect.IsEmitting && !form.IsHeadVisible && !AllBodyHidden() && !audioLoop.isPlaying, "Energy exhaustion lands and fully cleans up the form");
        yield return new WaitForSeconds(0.8f);
        Check(!effect.IsEmitting, "Holding M after exhaustion still requires release before restarting");
        Keys(); yield return new WaitForSeconds(0.1f); Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(form.EntryDuration + 0.1f);
        dashVisual.enabled = false; yield return null;
        Check(!form.IsHeadVisible && !AllBodyHidden() && visual.localScale == Vector3.one && !audioLoop.isPlaying, "Disabling visuals restores body size and visibility immediately");
        dashVisual.enabled = true; Keys(); yield return new WaitForSeconds(0.1f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(form.EntryDuration + 0.1f);
        movement.enabled = false; yield return new WaitForSeconds(0.7f);
        Check(!effect.IsEmitting && !form.IsHeadVisible && !AllBodyHidden() && !audioLoop.isPlaying, "Disabled movement cannot leave an invisible player or Ice VFX behind");
        Check(runtimeError == null, "No runtime errors or exceptions");
    }

    private bool HasBaseState(string name)
    {
        Animator animator = movement.GetComponentInChildren<Animator>();
        int hash = Animator.StringToHash("Base Layer." + name);
        return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == hash);
    }
    private bool CurrentClipContains(string name)
    {
        Animator animator = movement.GetComponentInChildren<Animator>();
        foreach (AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(0)) if (clip.clip.name.Contains(name)) return true;
        if (animator.IsInTransition(0))
            foreach (AnimatorClipInfo clip in animator.GetNextAnimatorClipInfo(0)) if (clip.clip.name.Contains(name)) return true;
        return false;
    }
    private IEnumerator CaptureIce(string label, bool side)
    {
        Camera camera = Camera.main;
        if (camera == null) yield break;
        CameraFollow follow = camera.GetComponentInParent<CameraFollow>();
        bool wasEnabled = follow != null && follow.enabled;
        Vector3 local = camera.transform.localPosition; Quaternion rotation = camera.transform.localRotation;
        try
        {
            if (side)
            {
                if (follow != null) follow.enabled = false;
                Vector3 target = effect.transform.position;
                camera.transform.position = target + new Vector3(5f, 2.2f, -4f);
                camera.transform.LookAt(target);
            }
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory("Logs"); ScreenCapture.CaptureScreenshot("Logs/IceDash_" + label + ".png");
            yield return null;
        }
        finally
        {
            camera.transform.SetLocalPositionAndRotation(local, rotation);
            if (follow != null) follow.enabled = wasEnabled;
        }
    }

    private IEnumerator RunWindChecks()
    {
        wind = effect.GetComponent<ShadowDashWindVfx>();
        var equipment = movement.GetComponent<PlayerMeleeEquipment>();
        var club = AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>("Assets/_Project/Prefabs/Weapons/TrainingClub.asset");
        Check(equipment.TryEquip(club), "Equip a real back weapon for transformation visibility checks");
        yield return null;
        GameObject backWeapon = Get<GameObject[]>(equipment, "weaponVisuals")[0];
        Shader shader = Shader.Find("TopDownShooter/VFX/Cartoon Shadow Wind");
        Check(wind != null && shader != null && !ShaderUtil.ShaderHasError(shader), "Cartoon wind component and URP shader are valid");
        Check(!wind.IsVisible && !wind.ShouldHideCharacter, "Cartoon wind is idle on normal movement and Short Dash");
        monitorCover = true; previousHidden = AllBodyHidden();
        Keys(Key.M, Key.W);
        yield return new WaitForSeconds(0.06f);
        Check(movement.IsShadowDashing && !AllBodyHidden() && wind.IsVisible,
            "Movement starts immediately while wind grows around the still-visible player");
        Check(backWeapon.activeSelf, "Back weapon remains visible until wind covers the player");
        yield return new WaitForSeconds(wind.EnterDuration);
        Check(AllBodyHidden() && wind.IsVisible && wind.Envelope > 0.99f, "Wind fully transforms into a horizontal vortex");
        Check(!backWeapon.activeSelf, "Back weapon hides together with the covered player");
        Check((glow == null || (!glow.enabled && !glow.emitting)) && (smoke == null || !smoke.enabled) && (distortion == null || !distortion.enabled),
            "Old stiff trails and distortion do not render in cartoon mode");
        Check(movement.GetComponent<PlayerHealth>().IsInvulnerable && energy.CurrentEnergy < energy.MaxEnergy,
            "Invulnerability and energy drain are unchanged");
        Check(audioLoop.clip == null ? !audioLoop.isPlaying : audioLoop.isPlaying && audioLoop.volume > 0f,
            "Loop sound is optional and only plays when a clip is assigned");
        Check(Mathf.Abs(wind.Axis.y) < 0.001f && Vector3.Dot(wind.Axis, Vector3.forward) > 0.95f,
            "Vortex axis stays horizontal and follows W movement");
        MeshFilter filter = wind.GetComponentsInChildren<MeshFilter>()[0];
        Mesh mesh = filter.sharedMesh;
        Check(mesh.vertexCount > 500 && mesh.bounds.size.x > 1f && mesh.bounds.size.y > 1f && mesh.bounds.size.z > 2f,
            "Curved ribbons generate a real three-dimensional horizontal volume");
        Vector3 sample = mesh.vertices[42];
        yield return new WaitForSeconds(0.12f);
        Check((sample - mesh.vertices[42]).sqrMagnitude > 0.001f, "Ribbons rotate and flutter over time, not just a rigid static trail");
        yield return CaptureWind("Active");

        Material shared = filter.GetComponent<MeshRenderer>().sharedMaterial;
        Color originalShared = shared.GetColor("_MainColor");
        GameObject cloneObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/ShadowDashTrails.prefab"));
        cloneObject.transform.position = movement.transform.position + Vector3.right * 30f;
        ShadowDashTrailVfx clone = cloneObject.GetComponent<ShadowDashTrailVfx>();
        Color cloneColor = Get<Gradient>(clone, "glowColors").Evaluate(0.07f);
        float cloneRadius = Get<float>(cloneObject.GetComponent<ShadowDashWindVfx>(), "radius");
        Set(wind, "radius", 1.1f);
        effect.ApplyPreset(ShadowTrailPreset.AzureVoid);
        effect.SetIntensity(0.7f);
        yield return new WaitForSeconds(0.08f);
        var block = new MaterialPropertyBlock();
        filter.GetComponent<MeshRenderer>().GetPropertyBlock(block);
        Color azure = block.GetColor("_MainColor");
        Check(azure.b > azure.r && shared.GetColor("_MainColor") == originalShared &&
            Get<Gradient>(clone, "glowColors").Evaluate(0.07f) == cloneColor &&
            Mathf.Approximately(Get<float>(cloneObject.GetComponent<ShadowDashWindVfx>(), "radius"), cloneRadius),
            "Wind palette/shape customize per instance without changing shared materials");
        Set(wind, "radius", 0.95f); effect.ApplyPreset(ShadowTrailPreset.PurpleShadow); effect.SetIntensity(1f);
        Destroy(cloneObject);

        Camera main = Camera.main;
        if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            CameraOverrideOption previous = data.requiresColorOption;
            data.requiresColorOption = CameraOverrideOption.Off;
            yield return null; yield return null;
            Check(wind.IsVisible && (distortion == null || !distortion.enabled), "Cartoon wind works without Opaque Texture or screen refraction");
            data.requiresColorOption = previous;
        }
        Keys(Key.M, Key.D);
        yield return new WaitForSeconds(0.35f);
        Check(Vector3.Dot(wind.Axis, Vector3.right) > 0.95f && Mathf.Abs(wind.Axis.y) < 0.001f,
            "Horizontal wind smoothly turns with the new dash direction");
        Teleport(movement.transform.position + Vector3.right * 25f);
        yield return null; yield return null;
        Check(mesh.bounds.size.magnitude < 8f, "Teleport cannot stretch local ribbons across the map");

        Keys();
        yield return new WaitForSeconds(0.05f);
        Check(!movement.IsShadowDashing && AllBodyHidden() && wind.IsVisible, "Release begins covering before the body is revealed");
        yield return new WaitForSeconds(0.09f);
        // Visibility is applied in LateUpdate, after coroutine continuations.
        yield return new WaitForEndOfFrame();
        Check(!AllBodyHidden() && wind.CoverAmount > 0.95f, "Player reappears underneath a fully covering wind shell");
        Check(backWeapon.activeSelf, "Back weapon returns under the same wind cover");
        yield return CaptureWind("Return");
        yield return new WaitForSeconds(wind.ExitDuration + 0.1f);
        Check(!wind.IsVisible && !AllBodyHidden() && !audioLoop.isPlaying, "Wind unwinds, restores body and stops sound completely");

        Keys(Key.M, Key.W); yield return new WaitForSeconds(0.06f);
        Keys(); yield return new WaitForSeconds(0.06f);
        Keys(Key.M, Key.D); yield return new WaitForSeconds(wind.EnterDuration + 0.05f);
        Check(AllBodyHidden() && wind.IsVisible, "Rapid tap and re-entry finish the transition without a stuck body");
        Keys(Key.M, Key.D, Key.Space); yield return new WaitForSeconds(0.06f);
        Check(movement.IsShadowDashing && !movement.IsDashing, "Space still cannot interrupt held Shadow Dash");
        Keys(); yield return new WaitForSeconds(wind.ExitDuration + 0.1f);
        // Advance visual timestamps to simulate a frame longer than a complete
        // transition without freezing the editor or changing gameplay time.
        Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(0.05f);
        Set(wind, "phaseStart", Time.time - wind.EnterDuration - 0.01f);
        yield return new WaitForEndOfFrame();
        Check(AllBodyHidden() && Mathf.Approximately(wind.CoverAmount, 1f), "A slow entry frame still fully covers the body swap");
        yield return null;
        Keys(); yield return new WaitForSeconds(0.03f);
        Set(wind, "phaseStart", Time.time - wind.ExitDuration - 0.01f);
        yield return new WaitForEndOfFrame();
        Check(!AllBodyHidden() && Mathf.Approximately(wind.CoverAmount, 1f), "A slow return frame still fully covers the body swap");
        yield return new WaitForSeconds(wind.ExitDuration + 0.1f);
        Keys(Key.W, Key.Space); yield return new WaitForSeconds(0.06f);
        Check(movement.IsDashing && !wind.IsVisible && shortTrail.emitting, "Short Dash remains independent after returning");
        Keys(); yield return new WaitForSeconds(0.8f);
        Set(energy, "<CurrentEnergy>k__BackingField", 20f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(1.3f);
        Check(!movement.IsShadowDashing && !wind.IsVisible && !AllBodyHidden() && !audioLoop.isPlaying,
            "Energy exhaustion finishes the visual return and sound cleanup");
        yield return new WaitForSeconds(0.8f);
        Check(!effect.IsEmitting, "Holding M after exhaustion does not auto-restart");
        Keys(); yield return new WaitForSeconds(0.1f);
        Set(energy, "<CurrentEnergy>k__BackingField", 100f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(wind.EnterDuration + 0.05f);
        monitorCover = false;
        dashVisual.enabled = false; yield return null;
        Check(!wind.IsVisible && !AllBodyHidden() && !audioLoop.isPlaying, "Disabling the visual controller restores everything immediately");
        dashVisual.enabled = true; Keys(); yield return new WaitForSeconds(0.1f);
        Keys(Key.M, Key.W); yield return new WaitForSeconds(wind.EnterDuration + 0.05f);
        movement.enabled = false; yield return new WaitForSeconds(wind.ExitDuration + 0.15f);
        Check(!wind.IsVisible && !AllBodyHidden() && !audioLoop.isPlaying, "Disabled movement cannot leave a vortex or invisible player behind");
        Check(runtimeError == null, "No runtime errors or exceptions");
    }

    private IEnumerator CaptureWind(string label)
    {
        Camera camera = Camera.main;
        if (camera == null) yield break;
        CameraFollow follow = camera.GetComponent<CameraFollow>();
        bool wasEnabled = follow != null && follow.enabled;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        try
        {
            if (follow != null) follow.enabled = false;
            Vector3 target = effect.transform.position;
            camera.transform.position = target + new Vector3(5f, 2.2f, -4f);
            camera.transform.LookAt(target);
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory("Logs");
            ScreenCapture.CaptureScreenshot("Logs/ShadowWind_" + label + ".png");
            yield return new WaitForSeconds(0.05f);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            if (follow != null) follow.enabled = wasEnabled;
        }
    }

    private void LateUpdate()
    {
        if (!monitorCover || wind == null || body == null) return;
        bool hiddenNow = AllBodyHidden();
        if (hiddenNow != previousHidden && wind.CoverAmount < 0.98f)
            runtimeError = "Body visibility changed before the wind completely covered it (cover=" + wind.CoverAmount + ").";
        previousHidden = hiddenNow;
    }

    private bool AllBodyHidden()
    {
        foreach (Renderer renderer in body)
            if (renderer != null && renderer.gameObject.activeInHierarchy && renderer.enabled) return false;
        return true;
    }
    private void Teleport(Vector3 position)
    {
        CharacterController controller = movement.GetComponent<CharacterController>();
        controller.enabled = false;
        movement.transform.SetPositionAndRotation(position, Quaternion.identity);
        controller.enabled = true;
    }
    private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
    private void Check(bool result, string message, bool log = true)
    {
        if (!result) throw new InvalidOperationException(message);
        if (runtimeError != null) throw new InvalidOperationException(runtimeError);
        if (log) report.AppendLine("PASS " + message);
    }
    private void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace;
    }
    private void Finish(Exception failure)
    {
        if (finished) return;
        finished = true;
        try
        {
            if (input != null && input.actions != null) input.actions.devices = originalDevices;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        }
        catch (Exception exception) { failure = failure ?? exception; }
        Application.targetFrameRate = originalFps;
        QualitySettings.vSyncCount = originalVSync;
        Application.runInBackground = originalBackground;
        Application.logMessageReceived -= OnLog;
        report.AppendLine(failure == null ? "ALL CHECKS PASSED" : "FAIL " + failure);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ShadowDashVfxRegression.txt", report.ToString());
        if (failure == null) Debug.Log("Shadow Trail regression: ALL CHECKS PASSED. Logs/ShadowDashVfxRegression.txt");
        else Debug.LogError("Shadow Trail regression failed: " + failure.Message);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { if (!finished) Finish(new InvalidOperationException("Regression interrupted")); }
}
#endif
