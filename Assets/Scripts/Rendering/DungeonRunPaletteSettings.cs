using System;
using UnityEngine;

/// <summary>Lab world-camera palette opt-in; authored colors are sRGB, processing is linear Rec.709.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class DungeonRunPaletteSettings : MonoBehaviour
{
    public enum Candidate { P0, P1, P2 }
    [Serializable]
    public sealed class Treatment
    {
        [Range(0, 1)] public float hueGrouping = .2f;
        [Range(0, 1)] public float chromaRetention = .96f;
        [Range(0, 1)] public float backgroundChromaRetention = .68f;
        [Range(1, 20)] public float familySharpness = 7;
    }

    public Candidate candidate = Candidate.P0;
    public Treatment subtle = new Treatment();
    public Treatment grouped = new Treatment { hueGrouping = .42f, chromaRetention = .88f,
        backgroundChromaRetention = .48f, familySharpness = 9 };
    [Header("Eye-space background distance")]
    public float backgroundStart = 35;
    public float backgroundEnd = 55;
    [Header("High-luminance accent protection (linear Rec.709)")]
    public float accentStart = .15f;
    public float accentEnd = .65f;
    [Range(0, 1)] public float accentProtection = .8f;
    [Header("Family directions (sRGB; luminance is never transferred)")]
    public Color coldStone = new Color(.19f, .27f, .32f);
    public Color amber = new Color(.66f, .43f, .17f);
    public Color jade = new Color(.28f, .44f, .31f);
    public Color terracotta = new Color(.48f, .27f, .19f);
    public Color bone = new Color(.57f, .54f, .42f);
    public Color cyan = new Color(.18f, .57f, .65f);
    public Treatment ActiveTreatment => candidate == Candidate.P2 ? grouped : subtle;
}
