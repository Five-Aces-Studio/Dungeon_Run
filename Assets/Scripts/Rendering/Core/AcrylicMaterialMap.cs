using System;
using System.Collections.Generic;

namespace DungeonRun.Rendering
{
    /// <summary>Declarative (renderer, source material) -&gt; AcrylicV3 material name map. V1_* sources are superseded and unmapped.</summary>
    public static class AcrylicMaterialMap
    {
        private static readonly string[] StylizedV1Families =
        {
            "StoneFloor", "StoneDark", "StoneWorn", "StoneBackground", "MetalDark",
            "TravelerCloth", "TravelerArmor", "EnemyBone", "EnemyClay", "EnemyJade",
            "ArcaneWarm", "ArcaneCool", "ArcaneDistant"
        };

        private static readonly string[] V2Families =
        {
            "CharacterMetal", "CreatureStone", "GuardianShield", "HoundClay"
        };

        public static readonly IReadOnlyList<string> SourceNames = BuildSourceNames();

        public static readonly IReadOnlyList<string> AllAcrylicNames = BuildAllAcrylicNames();

        private static string[] BuildSourceNames()
        {
            var names = new List<string>(StylizedV1Families.Length + V2Families.Length);
            foreach (var family in StylizedV1Families) names.Add(family + "_StylizedV1");
            foreach (var family in V2Families) names.Add("V2_" + family);
            return names.ToArray();
        }

        private static string[] BuildAllAcrylicNames()
        {
            var names = new List<string>(StylizedV1Families.Length + V2Families.Length + 1);
            foreach (var family in StylizedV1Families) names.Add(family + "_AcrylicV3");
            foreach (var family in V2Families) names.Add(family + "_AcrylicV3");
            names.Add("ArcaneWarmTorch_AcrylicV3");
            return names.ToArray();
        }

        private static string FamilyOf(string sourceMaterialName)
        {
            if (sourceMaterialName.StartsWith("V2_", StringComparison.Ordinal))
                return sourceMaterialName.Substring(3);
            if (sourceMaterialName.EndsWith("_StylizedV1", StringComparison.Ordinal))
                return sourceMaterialName.Substring(0, sourceMaterialName.Length - "_StylizedV1".Length);
            return null;
        }

        public static string Resolve(string rendererName, string sourceMaterialName)
        {
            if (string.IsNullOrEmpty(sourceMaterialName)) return null;
            if ((rendererName == "ENV_ArcaneFixture_1" || rendererName == "ENV_ArcaneFixture_2") &&
                sourceMaterialName == "ArcaneWarm_StylizedV1")
                return "ArcaneWarmTorch_AcrylicV3";

            bool known = false;
            foreach (var source in SourceNames)
            {
                if (source != sourceMaterialName) continue;
                known = true;
                break;
            }
            if (!known) return null;

            string family = FamilyOf(sourceMaterialName);
            return family == null ? null : family + "_AcrylicV3";
        }

        public static bool IsAcrylic(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var acrylic in AllAcrylicNames)
                if (acrylic == name) return true;
            return false;
        }

        public static string Inverse(string acrylicName)
        {
            const string suffix = "_AcrylicV3";
            if (string.IsNullOrEmpty(acrylicName) || !acrylicName.EndsWith(suffix, StringComparison.Ordinal))
                return null;
            string family = acrylicName.Substring(0, acrylicName.Length - suffix.Length);
            if (family == "ArcaneWarmTorch") return "ArcaneWarm_StylizedV1";
            foreach (var v2Family in V2Families)
                if (v2Family == family) return "V2_" + family;
            foreach (var v1Family in StylizedV1Families)
                if (v1Family == family) return family + "_StylizedV1";
            return null;
        }
    }
}
