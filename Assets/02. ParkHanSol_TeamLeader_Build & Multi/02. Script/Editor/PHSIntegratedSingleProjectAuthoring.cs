using System;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSIntegratedSingleProjectAuthoring
    {
        [MenuItem("Tools/ParkHanSol/BEAVER/Run Integrated Single Project Authoring")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "PHS_INTEGRATED_AUTHORING_FAILED reason=play_mode_active");
            }

            PHSUtilityItemVisualPrefabAuthoring.Build();
            PHSUpgradeVisualPrefabAuthoring.Build();
            PHSRangeCastGrappleEndpointAuthoring.Author();
            PHSShopStockAuthoring.Author();
            PHSNetworkTutorialAuthoring.MigrateTutorialPlayerToCanonicalVariant();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            PHSUpgradeVisualPrefabAuthoring.ValidateOrThrow();
            PHSUtilityItemVisualPrefabValidator.Validate();
            PHSRangeCastGrappleEndpointValidator.Validate();
            PHSShopStockAuthoring.ValidateOrThrow();
            PHS0715IntegrationValidator.ValidateTutorialPlayerVariantFromMenu();

            Debug.Log(
                "PHS_INTEGRATED_SINGLE_PROJECT_AUTHORING_OK " +
                "upgradeVisuals=5 tutorialVariant=true");
        }
    }
}
