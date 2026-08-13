using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSDevelopmentEventHotkeyAuthoring
    {
        private const string RunRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/" +
            "PHS_NetworkRunSessionRoot.prefab";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Development Event Hotkeys")]
        public static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "PHS_DEV_EVENT_HOTKEY_AUTHOR_FAILED reason=play_mode_active");
            }

            var prefab = PrefabUtility.LoadPrefabContents(RunRootPrefabPath);
            try
            {
                var root = prefab.GetComponent<NetworkRunSessionRoot>()
                    ?? throw new InvalidOperationException(
                        "PHS_DEV_EVENT_HOTKEY_AUTHOR_FAILED reason=session_root_missing");
                var coordinator = prefab.GetComponent<NetworkEventCoordinator>()
                    ?? throw new InvalidOperationException(
                        "PHS_DEV_EVENT_HOTKEY_AUTHOR_FAILED reason=canonical_coordinator_missing");
                var dispatcher = prefab.GetComponent<PHSDevelopmentEventHotkeyDispatcher>()
                    ?? prefab.AddComponent<PHSDevelopmentEventHotkeyDispatcher>();

                SetReference(dispatcher, "sessionRoot", root);
                SetReference(dispatcher, "eventCoordinator", coordinator);
                PrefabUtility.SaveAsPrefabAsset(prefab, RunRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateOrThrow();
            Debug.Log(
                "PHS_DEV_EVENT_HOTKEY_AUTHOR_OK root=session_root keys=2-9 hull_7107=excluded");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Development Event Hotkeys")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunRootPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_DEV_EVENT_HOTKEY_VALIDATE_FAILED reason=session_root_prefab_missing");
            var root = prefab.GetComponent<NetworkRunSessionRoot>();
            var coordinator = prefab.GetComponent<NetworkEventCoordinator>();
            var dispatcher = prefab.GetComponent<PHSDevelopmentEventHotkeyDispatcher>();
            if (root == null || coordinator == null || dispatcher == null)
            {
                throw new InvalidOperationException(
                    "PHS_DEV_EVENT_HOTKEY_VALIDATE_FAILED reason=component_missing");
            }

            var data = new SerializedObject(dispatcher);
            if (data.FindProperty("sessionRoot")?.objectReferenceValue != root
                || data.FindProperty("eventCoordinator")?.objectReferenceValue != coordinator)
            {
                throw new InvalidOperationException(
                    "PHS_DEV_EVENT_HOTKEY_VALIDATE_FAILED reason=inspector_reference_invalid");
            }

            Debug.Log(
                "PHS_DEV_EVENT_HOTKEY_VALIDATE_OK root=session_root keys=2:Fire,3:EnemySpawn,4:PowerOff,5:OxygenLeak,6:MicDestroy,7:EnemyScout,8:MeteorAttack,9:EmpAttack");
        }

        private static void SetReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object value)
        {
            var data = new SerializedObject(owner);
            var property = data.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_DEV_EVENT_HOTKEY_AUTHOR_FAILED reason=property_missing property={propertyName}");
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }
    }
}
