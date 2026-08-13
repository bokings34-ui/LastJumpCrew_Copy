using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Removes only PHS incident ownership that was superseded by the team event runtime.
    /// Inspector references are scanned before destruction so team components cannot retain
    /// a hidden dependency on a removed legacy component.
    /// </summary>
    public static class PHSTeamSingleTruthLegacyRemovalAuthoring
    {
        private const string RunRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string ShipRuntimePrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/PHS_ShipRuntime.prefab";

        [MenuItem("Tools/ParkHanSol/Team Events/Remove Legacy PHS Incident Ownership")]
        public static void Author()
        {
            var removedRunRoot = EditPrefab(
                RunRootPrefabPath,
                CollectRunRootLegacyComponents);
            var removedShipRuntime = EditPrefab(
                ShipRuntimePrefabPath,
                CollectShipRuntimeLegacyComponents);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"PHS_TEAM_SINGLE_TRUTH_LEGACY_REMOVAL_OK runRoot={removedRunRoot} shipRuntime={removedShipRuntime}");
        }

        private static int EditPrefab(
            string path,
            Func<GameObject, List<Component>> collectLegacyComponents)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_TEAM_SINGLE_TRUTH_LEGACY_REMOVAL_FAILED reason=prefab_missing path={path}");
            }

            try
            {
                var removals = collectLegacyComponents(root);
                var removalSet = new HashSet<UnityEngine.Object>(removals);
                var references = FindRetainedReferences(root, removalSet);
                if (references.Count > 0)
                {
                    throw new InvalidOperationException(
                        "PHS_TEAM_SINGLE_TRUTH_LEGACY_REMOVAL_FAILED " +
                        $"reason=retained_reference path={path} refs={string.Join(";", references)}");
                }

                // PHSNetworkIncidentDirector requires the ledger. Destroy its dependent
                // component first; otherwise Unity immediately recreates the ledger.
                foreach (var component in removals.OrderBy(
                             component => component is NetworkRunIncidentLedger ? 1 : 0))
                {
                    if (component != null)
                    {
                        UnityEngine.Object.DestroyImmediate(component, true);
                    }
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_TEAM_SINGLE_TRUTH_LEGACY_REMOVAL_FAILED reason=prefab_save_failed path={path}");
                }

                return removals.Count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static List<Component> CollectRunRootLegacyComponents(GameObject root)
        {
            var removals = new List<Component>();
            removals.AddRange(root.GetComponentsInChildren<NetworkRunIncidentLedger>(true));
            removals.AddRange(root.GetComponentsInChildren<PHSNetworkIncidentDirector>(true));
            removals.AddRange(root.GetComponentsInChildren<NetworkRunWarningAudioPresenter>(true));

            var warningRoot = root.transform.Find("PHS_NetworkWarningAudio");
            if (warningRoot != null)
            {
                removals.AddRange(warningRoot.GetComponentsInChildren<Component>(true)
                    .Where(component => component is not Transform));
            }

            return removals.Distinct().ToList();
        }

        private static List<Component> CollectShipRuntimeLegacyComponents(GameObject root)
        {
            var removals = new List<Component>();
            removals.AddRange(root.GetComponentsInChildren<PHSNetworkShipAccidentCoordinator>(true));
            removals.AddRange(root.GetComponentsInChildren<PHSShipAccidentAnchor>(true));
            removals.AddRange(root.GetComponentsInChildren<PHSShipAccidentHudBinder>(true));
            return removals.Distinct().ToList();
        }

        private static List<string> FindRetainedReferences(
            GameObject root,
            ISet<UnityEngine.Object> removals)
        {
            var references = new List<string>();
            foreach (var owner in root.GetComponentsInChildren<Component>(true))
            {
                if (owner == null || removals.Contains(owner))
                {
                    continue;
                }

                var serializedOwner = new SerializedObject(owner);
                var property = serializedOwner.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.objectReferenceValue == null
                        || !removals.Contains(property.objectReferenceValue))
                    {
                        continue;
                    }

                    references.Add(
                        $"{GetHierarchyPath(owner.transform)}:{owner.GetType().Name}:{property.propertyPath}->" +
                        property.objectReferenceValue.GetType().Name);
                }
            }

            return references;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }
    }
}
