using System;

namespace DungeonRun.Rendering
{
    /// <summary>Plain 4-float value; mirrors a shader float4 without any engine dependency.</summary>
    public readonly struct Float4 : IEquatable<Float4>
    {
        public readonly float X, Y, Z, W;
        public Float4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        public bool Equals(Float4 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        public override bool Equals(object obj) => obj is Float4 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(Float4 a, Float4 b) => a.Equals(b);
        public static bool operator !=(Float4 a, Float4 b) => !a.Equals(b);
    }

    /// <summary>Immutable, zero-neutral material/lighting globals shared by every acrylic-lit material and the fullscreen pass.</summary>
    public readonly struct PainterlyGlobals : IEquatable<PainterlyGlobals>
    {
        public readonly Float4 Painterly;
        public readonly Float4 Brush;

        public PainterlyGlobals(Float4 painterly, Float4 brush) { Painterly = painterly; Brush = brush; }

        public static readonly PainterlyGlobals Neutral = new PainterlyGlobals(new Float4(0, 0, 0, 0), new Float4(0, 0, 0, 0));

        public static PainterlyGlobals Create(float influence, float lightingSmoothness, float warmLightInfluence,
            float ambientCoolness, float albedoVariation, float colorVariation, float terminatorBreakup,
            float shadowEdgeBreakupMetres)
        {
            float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
            float Clamp(float v, float max) => v < 0f ? 0f : (v > max ? max : v);

            float clampedInfluence = Clamp01(influence);
            float smoothness = Clamp01(lightingSmoothness);
            float warm = Clamp01(warmLightInfluence);
            float coolness = Clamp01(ambientCoolness);
            float albedo = Clamp(albedoVariation, 0.5f);
            float color = Clamp01(colorVariation);
            float terminator = Clamp01(terminatorBreakup);
            float shadowEdge = Clamp(shadowEdgeBreakupMetres, 0.5f);

            var painterly = new Float4(clampedInfluence, smoothness * clampedInfluence, warm * clampedInfluence,
                coolness * clampedInfluence);
            var brush = new Float4(albedo * clampedInfluence, color * clampedInfluence,
                terminator * clampedInfluence, shadowEdge * clampedInfluence);
            return new PainterlyGlobals(painterly, brush);
        }

        public bool Equals(PainterlyGlobals other) => Painterly.Equals(other.Painterly) && Brush.Equals(other.Brush);
        public override bool Equals(object obj) => obj is PainterlyGlobals other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Painterly, Brush);
        public static bool operator ==(PainterlyGlobals a, PainterlyGlobals b) => a.Equals(b);
        public static bool operator !=(PainterlyGlobals a, PainterlyGlobals b) => !a.Equals(b);
    }
}
