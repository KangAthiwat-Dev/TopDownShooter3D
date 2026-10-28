using System;
using UnityEngine;

/// <summary>Appearance has its own profile; changing a look never resets game progress.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAppearanceProfile : MonoBehaviour
{
    public const string SaveKey="TopDownShooter3D.Appearance.v1";
    [Serializable] private sealed class Profile { public int version=1;public PlayerAppearanceData appearance; }
    public static bool TryRead(out PlayerAppearanceData appearance)
    {
        appearance=PlayerAppearanceData.Default;
        if(!PlayerPrefs.HasKey(SaveKey))return false;
        try
        {
            var data=JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(SaveKey));
            if(data==null||data.version!=1||string.IsNullOrWhiteSpace(data.appearance.hairStyleId)||!Valid(data.appearance.skinColor)||!Valid(data.appearance.hairColor))return false;
            appearance=data.appearance;return true;
        }
        catch(ArgumentException){return false;}
    }
    private static bool Valid(Color c)=>Channel(c.r)&&Channel(c.g)&&Channel(c.b);
    private static bool Channel(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f)&&f>=0&&f<=1;
    public static void Save(PlayerAppearanceData appearance)
    {
        if(!Valid(appearance.skinColor)||!Valid(appearance.hairColor)||string.IsNullOrWhiteSpace(appearance.hairStyleId))return;
        PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(new Profile{appearance=appearance}));PlayerPrefs.Save();
    }
    private void Start()
    {
        var visual=GetComponentInChildren<PlayerAppearance>(true);
        if(visual!=null&&TryRead(out var appearance))visual.ApplyAppearance(appearance);
    }
}
