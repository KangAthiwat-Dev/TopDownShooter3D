using System;
using UnityEngine;

// Save this value with the future character/profile save, not renderer instances.
[Serializable]
public struct PlayerAppearanceData
{
    public Color skinColor;
    public Color hairColor;
    public string hairStyleId;

    public static PlayerAppearanceData Default => new PlayerAppearanceData
    {
        skinColor = new Color(1f, 0.627f, 0.478f, 1f),
        hairColor = Color.white,
        hairStyleId = "topknot"
    };
}
