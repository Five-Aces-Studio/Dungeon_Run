using System;
using System.Collections.Generic;

namespace DungeonRun.Rendering
{
    /// <summary>
    /// Two painter's "colour touches" per AcrylicV3 material: a fraction of the brush dabs (selected by the stroke
    /// map hue id) is pulled toward touch colour A or B, luminance-matched in the shader so only the hue shifts.
    /// Colours are sRGB-authored 0..1.
    /// </summary>
    public readonly struct TouchPalette : IEquatable<TouchPalette>
    {
        public readonly float ARed, AGreen, ABlue;
        public readonly float BRed, BGreen, BBlue;
        public readonly float AmountA, AmountB;
        public readonly float Strength;

        public TouchPalette(float aRed, float aGreen, float aBlue, float bRed, float bGreen, float bBlue,
            float amountA, float amountB, float strength)
        {
            ARed = aRed;
            AGreen = aGreen;
            ABlue = aBlue;
            BRed = bRed;
            BGreen = bGreen;
            BBlue = bBlue;
            AmountA = amountA;
            AmountB = amountB;
            Strength = strength;
        }

        public bool Equals(TouchPalette other) =>
            ARed.Equals(other.ARed) && AGreen.Equals(other.AGreen) && ABlue.Equals(other.ABlue) &&
            BRed.Equals(other.BRed) && BGreen.Equals(other.BGreen) && BBlue.Equals(other.BBlue) &&
            AmountA.Equals(other.AmountA) && AmountB.Equals(other.AmountB) && Strength.Equals(other.Strength);
        public override bool Equals(object obj) => obj is TouchPalette other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ARed, AGreen, ABlue, BRed, BGreen, BBlue,
            HashCode.Combine(AmountA, AmountB, Strength));
    }

    public static class AcrylicTouchPalettes
    {
        // Emissive materials: no touches. Colours match the shader property defaults so an untouched material and a
        // neutral palette author identical values.
        private static readonly TouchPalette Neutral = new TouchPalette(.55f, .47f, .26f, .26f, .30f, .42f, 0f, 0f, 0f);

        // A rgb, B rgb, amountA, amountB, strength
        private static readonly Dictionary<string, TouchPalette> Palettes = new Dictionary<string, TouchPalette>
        {
            // Environment: olive/ochre and rust inside cool stone, rust and verdigris in metal.
            ["StoneFloor_AcrylicV3"] = new TouchPalette(.50f, .50f, .25f, .52f, .33f, .24f, .20f, .08f, .75f),
            ["StoneDark_AcrylicV3"] = new TouchPalette(.55f, .47f, .26f, .26f, .30f, .42f, .15f, .12f, .70f),
            ["StoneWorn_AcrylicV3"] = new TouchPalette(.55f, .47f, .26f, .26f, .30f, .42f, .15f, .12f, .70f),
            ["StoneBackground_AcrylicV3"] = new TouchPalette(.20f, .36f, .40f, .40f, .30f, .22f, .15f, .08f, .50f),
            ["MetalDark_AcrylicV3"] = new TouchPalette(.45f, .25f, .15f, .25f, .42f, .38f, .15f, .10f, .60f),

            // Emissive
            ["ArcaneWarm_AcrylicV3"] = Neutral,
            ["ArcaneCool_AcrylicV3"] = Neutral,
            ["ArcaneWarmTorch_AcrylicV3"] = Neutral,
            ["ArcaneDistant_AcrylicV3"] = Neutral,

            // Characters: complementary warm/cool touches, weaker than the environment so silhouettes stay readable.
            ["TravelerCloth_AcrylicV3"] = new TouchPalette(.60f, .66f, .40f, .28f, .45f, .50f, .12f, .12f, .50f),
            ["TravelerArmor_AcrylicV3"] = new TouchPalette(.62f, .50f, .30f, .40f, .45f, .52f, .10f, .10f, .45f),
            ["EnemyBone_AcrylicV3"] = new TouchPalette(.66f, .55f, .35f, .45f, .50f, .58f, .10f, .10f, .45f),
            ["EnemyClay_AcrylicV3"] = new TouchPalette(.45f, .20f, .22f, .75f, .45f, .20f, .12f, .10f, .50f),
            ["HoundClay_AcrylicV3"] = new TouchPalette(.45f, .20f, .22f, .75f, .45f, .20f, .12f, .10f, .50f),
            ["EnemyJade_AcrylicV3"] = new TouchPalette(.55f, .62f, .30f, .20f, .45f, .45f, .10f, .10f, .45f),
            ["CharacterMetal_AcrylicV3"] = new TouchPalette(.45f, .28f, .18f, .28f, .42f, .40f, .08f, .08f, .40f),
            ["GuardianShield_AcrylicV3"] = new TouchPalette(.45f, .28f, .18f, .28f, .42f, .40f, .08f, .08f, .40f),
            ["CreatureStone_AcrylicV3"] = new TouchPalette(.55f, .47f, .26f, .26f, .30f, .42f, .15f, .12f, .55f),
        };

        public static bool TryGet(string acrylicName, out TouchPalette palette)
        {
            if (acrylicName == null)
            {
                palette = default;
                return false;
            }
            return Palettes.TryGetValue(acrylicName, out palette);
        }

        public static TouchPalette Get(string acrylicName)
        {
            if (TryGet(acrylicName, out var palette)) return palette;
            throw new KeyNotFoundException($"No colour touch palette for '{acrylicName}'.");
        }
    }
}
