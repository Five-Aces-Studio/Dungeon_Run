using System;
using UnityEngine;

/// <summary>Explicit lab-camera opt-in. Targets reference existing mesh slots; source materials stay intact.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class DungeonRunSelectiveOutlineSettings : MonoBehaviour
{
    public enum Candidate { O0, O1, O2 }

    [Serializable]
    public sealed class Target
    {
        public MeshRenderer renderer;
        public int submesh;
        public bool restrictLocalX, restrictLocalY;
        public float maximumLocalX, maximumLocalY;
    }

    public Candidate candidate = Candidate.O0;
    public Color o1Color = new Color(.065f, .08f, .09f, 1);
    public Color o2Color = new Color(.105f, .125f, .14f, 1);
    public Target[] targets = Array.Empty<Target>();
    public Color OutlineColor => candidate == Candidate.O2 ? o2Color : o1Color;
}
