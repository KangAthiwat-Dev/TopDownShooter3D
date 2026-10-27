using System;
using UnityEngine;

public enum CameraViewMode { TopDown = 0, HighThirdPerson = 1 }

[Serializable]
public sealed class CameraViewPreset
{
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 rigEulerAngles;
    public Vector3 targetOffset;
    [Min(1f)] public float fieldOfView = 45f;
    [Min(.01f)] public float nearClipPlane = .3f;
    public bool followMovementHeading;
    public bool alignBehindPlayerOnStart;
    public bool cameraRelativeMovement;
    public bool avoidSolidObstacles;
}

/// <summary>Two camera presets ready for a settings screen. Does not own aiming or player rotation.</summary>
[DefaultExecutionOrder(50), RequireComponent(typeof(CameraFollow)), DisallowMultipleComponent]
public sealed class CameraViewSettings : MonoBehaviour
{
    public const string PreferenceKey = "TopDownShooter3D.CameraView";
    [SerializeField] private CameraFollow follow;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private CameraViewMode defaultView = CameraViewMode.TopDown;
    [SerializeField] private bool rememberSelection = true;
    [SerializeField] private CameraViewPreset topDown = new CameraViewPreset {
        localPosition = new Vector3(0,12,-10), localEulerAngles = new Vector3(50,0,0),
        targetOffset = Vector3.up, fieldOfView = 45f, nearClipPlane = .3f
    };
    [SerializeField] private CameraViewPreset highThirdPerson = new CameraViewPreset {
        localPosition = new Vector3(0,6,-8), localEulerAngles = new Vector3(36.8699f,0,0),
        targetOffset = new Vector3(0,1.35f,0), fieldOfView = 55f, nearClipPlane = .1f,
        followMovementHeading = true, alignBehindPlayerOnStart = true,
        cameraRelativeMovement = true, avoidSolidObstacles = true
    };
    public CameraViewMode CurrentView { get; private set; }
    public CameraViewMode DefaultView => defaultView;
    public CameraViewPreset TopDownPreset => topDown;
    public CameraViewPreset HighThirdPersonPreset => highThirdPerson;
    public event Action<CameraViewMode> ViewChanged;

    private void Awake() => ApplyView(ReadPreferredView(), false);

    public CameraViewMode ReadPreferredView()
    {
        int saved = rememberSelection ? PlayerPrefs.GetInt(PreferenceKey,(int)defaultView) : (int)defaultView;
        return saved == (int)CameraViewMode.HighThirdPerson ? CameraViewMode.HighThirdPerson : CameraViewMode.TopDown;
    }

    // Dropdown indices intentionally match CameraViewMode; this can be wired to UnityEvents.
    public void SelectView(int index)
    {
        if(index == 0 || index == 1) ApplyView((CameraViewMode)index);
    }

    public void ApplyView(CameraViewMode mode, bool savePreference = true)
    {
        if(mode != CameraViewMode.TopDown && mode != CameraViewMode.HighThirdPerson) return;
        if(follow == null) follow = GetComponent<CameraFollow>();
        if(movement == null) movement = FindFirstObjectByType<PlayerMovement>();
        var view = follow.FollowCamera;
        if(view == null) view = GetComponentInChildren<Camera>();
        if(view == null) return;
        var preset = mode == CameraViewMode.TopDown ? topDown : highThirdPerson;
        var boom = GetComponent<CameraBoomCollision>();
        // Restore the old boom before writing a new rest pose; otherwise OnDisable
        // would put the previous view's offset back over the newly selected one.
        if(boom != null) boom.enabled = false;
        var fade = GetComponent<CameraOcclusionFade>();
        if(fade != null) fade.enabled = false;
        follow.ApplyViewPose(preset.localPosition,Quaternion.Euler(preset.localEulerAngles),
            Quaternion.Euler(preset.rigEulerAngles),preset.targetOffset,
            preset.followMovementHeading,preset.alignBehindPlayerOnStart);
        view.fieldOfView = preset.fieldOfView; view.nearClipPlane = preset.nearClipPlane;
        if(movement != null) movement.ConfigureCameraRelativeMovement(preset.cameraRelativeMovement,view.transform);
        var dash = GetComponent<CameraDashEffect>();
        if(dash != null) dash.SetBaseFieldOfView(preset.fieldOfView);
        if(boom != null) { boom.ResetForView(); boom.enabled = preset.avoidSolidObstacles; }
        CurrentView = mode;
        if(savePreference && rememberSelection)
        { PlayerPrefs.SetInt(PreferenceKey,(int)mode); PlayerPrefs.Save(); }
        ViewChanged?.Invoke(mode);
    }

    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(PreferenceKey); PlayerPrefs.Save();
        ApplyView(defaultView,false);
    }
}
