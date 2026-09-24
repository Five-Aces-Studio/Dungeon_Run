using System;
using System.Collections.Generic;

namespace DungeonRun.Rendering
{
    public enum AcrylicBrushMapping { WorldTriplanar = 0, ObjectTriplanar = 1, UV0 = 2 }

    /// <summary>Per-family AcrylicV3 material tuning defaults used by the installer to author material assets.</summary>
    public readonly struct AcrylicFamilyDefault : IEquatable<AcrylicFamilyDefault>
    {
        public readonly AcrylicBrushMapping BrushMapping;
        public readonly float BrushStrength;
        public readonly float BrushScale;
        public readonly float LightWrap;
        public readonly float ShadowBreakup;
        public readonly float RimStrength;
        public readonly float MinBandLevel0;
        public readonly float AmbientScale;
        public readonly float EmissionScale;

        public AcrylicFamilyDefault(AcrylicBrushMapping brushMapping, float brushStrength, float brushScale,
            float lightWrap, float shadowBreakup, float rimStrength, float minBandLevel0, float ambientScale,
            float emissionScale)
        {
            BrushMapping = brushMapping;
            BrushStrength = brushStrength;
            BrushScale = brushScale;
            LightWrap = lightWrap;
            ShadowBreakup = shadowBreakup;
            RimStrength = rimStrength;
            MinBandLevel0 = minBandLevel0;
            AmbientScale = ambientScale;
            EmissionScale = emissionScale;
        }

        public bool Equals(AcrylicFamilyDefault other) =>
            BrushMapping == other.BrushMapping && BrushStrength.Equals(other.BrushStrength) &&
            BrushScale.Equals(other.BrushScale) && LightWrap.Equals(other.LightWrap) &&
            ShadowBreakup.Equals(other.ShadowBreakup) && RimStrength.Equals(other.RimStrength) &&
            MinBandLevel0.Equals(other.MinBandLevel0) && AmbientScale.Equals(other.AmbientScale) &&
            EmissionScale.Equals(other.EmissionScale);
        public override bool Equals(object obj) => obj is AcrylicFamilyDefault other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BrushMapping, BrushStrength, BrushScale, LightWrap,
            HashCode.Combine(ShadowBreakup, RimStrength, MinBandLevel0, AmbientScale, EmissionScale));
    }

    public static class AcrylicFamilyDefaults
    {
        // strength, scale, wrap, shadowBreakup, rim, minBandLevel0, ambientScale, emissionScale
        private static readonly Dictionary<string, AcrylicFamilyDefault> Defaults = new Dictionary<string, AcrylicFamilyDefault>
        {
            // Environment (World Triplanar)
            ["StoneFloor_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .65f, .35f, .15f, 1f, 0f, .16f, 1.25f, 1f),
            ["StoneDark_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .6f, .4f, .15f, 1f, 0f, .16f, 1.25f, 1f),
            ["StoneWorn_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .65f, .4f, .15f, 1f, 0f, .16f, 1.25f, 1f),
            ["StoneBackground_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .4f, .25f, .15f, .8f, 0f, .14f, 1.15f, 1f),
            ["MetalDark_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .18f, .6f, 0f, .5f, 0f, .12f, 1f, 1f),
            ["ArcaneDistant_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f),

            // Emissive (small), Object except the fixture torch (World)
            ["ArcaneWarm_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f),
            ["ArcaneCool_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f),
            ["ArcaneWarmTorch_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.WorldTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1.6f),

            // Characters (Object Triplanar, no shadow breakup)
            ["TravelerCloth_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .45f, 1.2f, .5f, 0f, .35f, .16f, 1.1f, 1f),
            ["TravelerArmor_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .2f, 0f, .35f, .16f, 1.1f, 1f),
            ["EnemyBone_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .25f, 0f, .3f, .16f, 1.1f, 1f),
            ["EnemyClay_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .5f, 1.2f, .25f, 0f, .3f, .16f, 1.1f, 1f),
            ["EnemyJade_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .2f, 0f, .3f, .16f, 1.1f, 1f),
            ["CharacterMetal_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .18f, 1.5f, 0f, 0f, .3f, .16f, 1f, 1f),
            ["CreatureStone_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .15f, 0f, .3f, .16f, 1.1f, 1f),
            ["GuardianShield_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .15f, 0f, .3f, .16f, 1.1f, 1f),
            ["HoundClay_AcrylicV3"] = new AcrylicFamilyDefault(AcrylicBrushMapping.ObjectTriplanar, .5f, 1.2f, .25f, 0f, .3f, .16f, 1.1f, 1f),
        };

        public static bool TryGet(string acrylicName, out AcrylicFamilyDefault value)
        {
            if (acrylicName == null)
            {
                value = default;
                return false;
            }
            return Defaults.TryGetValue(acrylicName, out value);
        }

        public static AcrylicFamilyDefault Get(string acrylicName) => Defaults[acrylicName];
    }
}
