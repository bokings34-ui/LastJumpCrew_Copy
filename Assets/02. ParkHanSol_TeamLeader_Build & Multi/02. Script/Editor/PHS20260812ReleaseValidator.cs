using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>Single release contract for the 2026-08-12 integrated build.</summary>
    public static class PHS20260812ReleaseValidator
    {
        [MenuItem("Tools/ParkHanSol/20260812/Validate Current Release")]
        public static void Validate()
        {
            PHSRuntimeEditorOnlyComponentCleanup.Validate();
            PHS0715IntegrationValidator.ValidateFromMenu();
            PHSMapSpawnNavigationValidator.ValidateOrThrow();
            PHSIntegratedReleaseValidator.Validate();

            Debug.Log(
                "PHS_20260812_RELEASE_VALIDATION_PASS " +
                "static=true integration=true tutorial=true map=true " +
                "current_contracts=toolbox+shop_cursor+gravity_look " +
                "runtime_proof_required=true");
        }
    }
}
