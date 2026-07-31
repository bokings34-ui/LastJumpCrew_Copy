using System;
using TMPro;
using UnityEditor;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public enum PHSUIFontRole
    {
        Body,
        Control,
        Emphasis,
        Heading,
        AlertHeading
    }

    public static class PHSUIFontPaths
    {
        private const string FontRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/_ThirdParty/Fonts";

        public const string SuitRegular =
            FontRoot + "/SUIT/TMP/SUIT Regular SDF.asset";
        public const string SuitMedium =
            FontRoot + "/SUIT/TMP/SUIT Medium SDF.asset";
        public const string SuitSemiBold =
            FontRoot + "/SUIT/TMP/SUIT SemiBold SDF.asset";
        public const string SuitBold =
            FontRoot + "/SUIT/TMP/SUIT Bold SDF.asset";
        public const string SuiteSemiBold =
            FontRoot + "/SUITE/TMP/SUITE SemiBold SDF.asset";
        public const string SuiteBold =
            FontRoot + "/SUITE/TMP/SUITE Bold SDF.asset";
        public static TMP_FontAsset Load(PHSUIFontRole role)
        {
            var path = role switch
            {
                PHSUIFontRole.Control => SuitMedium,
                PHSUIFontRole.Emphasis => SuitSemiBold,
                PHSUIFontRole.Heading => SuiteSemiBold,
                PHSUIFontRole.AlertHeading => SuiteBold,
                _ => SuitRegular
            };
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
                ?? throw new InvalidOperationException(
                    $"PHS_UI_FONT_MISSING role={role} path={path}");
        }

        public static PHSUIFontRole ResolveRole(TMP_Text text)
        {
            var nameIdentity = text.name.ToLowerInvariant();
            var valueIdentity = (text.text ?? string.Empty).ToLowerInvariant();
            var isHeading = ContainsAny(
                nameIdentity,
                "title",
                "header",
                "heading");
            var isAlert = ContainsAny(
                nameIdentity,
                "warning",
                "alert",
                "critical",
                "emergency") ||
                isHeading && ContainsAny(
                    valueIdentity,
                    "warning",
                    "alert",
                    "critical",
                    "emergency");
            if (isHeading && isAlert)
            {
                return PHSUIFontRole.AlertHeading;
            }

            if (isHeading)
            {
                return PHSUIFontRole.Heading;
            }

            if (ContainsAny(
                    nameIdentity,
                    "button",
                    "prompt",
                    "interact",
                    "action",
                    "control",
                    "price",
                    "cost",
                    "purchase",
                    "buy",
                    "credit",
                    "currency"))
            {
                return PHSUIFontRole.Control;
            }

            if (ContainsAny(
                    nameIdentity,
                    "value",
                    "status",
                    "gauge",
                    "timer",
                    "health",
                    "ship hp",
                    "warp",
                    "amount",
                    "count",
                    "percent",
                    "score"))
            {
                return PHSUIFontRole.Emphasis;
            }

            return PHSUIFontRole.Body;
        }

        public static void Apply(TMP_Text text, PHSUIFontRole role)
        {
            var font = Load(role);
            var currentFont = text.font;
            var currentMaterial = text.fontSharedMaterial;
            var preserveCustomMaterial = currentMaterial != null &&
                currentFont != null &&
                currentMaterial != currentFont.material &&
                currentMaterial.mainTexture == font.atlasTexture;
            text.font = font;
            text.fontSharedMaterial = preserveCustomMaterial
                ? currentMaterial
                : font.material;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            EditorUtility.SetDirty(text);
        }

        public static void ApplyResolved(TMP_Text text)
        {
            Apply(text, ResolveRole(text));
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (var token in tokens)
            {
                if (value.Contains(token, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

