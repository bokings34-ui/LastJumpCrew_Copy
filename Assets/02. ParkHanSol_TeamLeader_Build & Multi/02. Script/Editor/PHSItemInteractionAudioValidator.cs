using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSItemInteractionAudioValidator
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private static readonly string[] PlayerPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };
        private static readonly BindingContract[] OwnerBindings =
        {
            new(NetworkAudioCue.WrenchImpact, 0.6f, 0.08f),
            new(NetworkAudioCue.ExtinguisherSpray, 0.55f, 0.12f),
            new(NetworkAudioCue.FoamShot, 0.65f, 0.08f)
        };
        private static readonly BindingContract[] WorldBindings =
        {
            new(NetworkAudioCue.WrenchImpact, 0.75f, 0.08f),
            new(NetworkAudioCue.ExtinguisherSpray, 0.7f, 0.12f),
            new(NetworkAudioCue.RepairComplete, 0.8f, 0.20f),
            new(NetworkAudioCue.ExtinguishComplete, 0.8f, 0.20f),
            new(NetworkAudioCue.BatteryInstall, 0.8f, 0.20f),
            new(NetworkAudioCue.FoamAttach, 0.65f, 0.06f),
            new(NetworkAudioCue.FoamHarden, 0.65f, 0.12f),
            new(NetworkAudioCue.FoamSealComplete, 0.8f, 0.20f),
            new(NetworkAudioCue.FoamFireComplete, 0.8f, 0.20f)
        };

        private readonly struct BindingContract
        {
            public BindingContract(
                NetworkAudioCue cue,
                float volume,
                float cooldown)
            {
                Cue = cue;
                Volume = volume;
                Cooldown = cooldown;
            }

            public NetworkAudioCue Cue { get; }
            public float Volume { get; }
            public float Cooldown { get; }
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Item Interaction Audio")]
        public static void Validate()
        {
            var errors = new List<string>();
            foreach (var cue in OwnerBindings.Concat(WorldBindings).Select(binding => binding.Cue))
            {
                ValidateWave(PHSCuratedAssetSfxAuthoring.GetCuePath(cue), errors);
            }
            ValidateWave(PHSCuratedAssetSfxAuthoring.BatteryShockPath, errors);

            foreach (var path in PlayerPaths)
            {
                ValidatePlayer(path, errors);
            }

            ValidateHookContracts(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_ITEM_INTERACTION_AUDIO_VALIDATION_FAILED\n" +
                    string.Join("\n", errors));
            }

            Debug.Log("PHS_ITEM_INTERACTION_AUDIO_VALIDATION_PASSED waves=12 players=2 owner2D=true world3D=true shock3D=true");
        }

        private static void ValidateWave(string path, ICollection<string> errors)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                errors.Add($"wave_import path={path}");
                return;
            }

            var bytes = File.ReadAllBytes(path);
            if (!TryReadWaveContract(
                    bytes,
                    out var channels,
                    out var sampleRate,
                    out var bitsPerSample)
                || channels < 1
                || channels > 2
                || sampleRate < 22050
                || bitsPerSample < 16
                || clip.channels != channels
                || clip.frequency != sampleRate)
            {
                errors.Add($"wave_pcm_contract path={path}");
            }
        }

        private static bool TryReadWaveContract(
            byte[] bytes,
            out short channels,
            out int sampleRate,
            out short bitsPerSample)
        {
            channels = 0;
            sampleRate = 0;
            bitsPerSample = 0;
            if (bytes.Length < 12
                || bytes[0] != 'R'
                || bytes[1] != 'I'
                || bytes[2] != 'F'
                || bytes[3] != 'F'
                || bytes[8] != 'W'
                || bytes[9] != 'A'
                || bytes[10] != 'V'
                || bytes[11] != 'E')
            {
                return false;
            }

            var foundFormat = false;
            var foundData = false;
            var offset = 12;
            while (offset + 8 <= bytes.Length)
            {
                var chunkSize = BitConverter.ToInt32(bytes, offset + 4);
                var dataOffset = offset + 8;
                if (chunkSize < 0 || dataOffset + chunkSize > bytes.Length)
                {
                    return false;
                }

                if (bytes[offset] == 'f'
                    && bytes[offset + 1] == 'm'
                    && bytes[offset + 2] == 't'
                    && bytes[offset + 3] == ' ')
                {
                    if (chunkSize < 16)
                    {
                        return false;
                    }

                    var format = BitConverter.ToInt16(bytes, dataOffset);
                    if (format != 1 && format != -2)
                    {
                        return false;
                    }

                    channels = BitConverter.ToInt16(bytes, dataOffset + 2);
                    sampleRate = BitConverter.ToInt32(bytes, dataOffset + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, dataOffset + 14);
                    foundFormat = true;
                }
                else if (bytes[offset] == 'd'
                    && bytes[offset + 1] == 'a'
                    && bytes[offset + 2] == 't'
                    && bytes[offset + 3] == 'a')
                {
                    foundData = chunkSize > 0;
                }

                offset = dataOffset + chunkSize + (chunkSize & 1);
            }

            return foundFormat && foundData;
        }

        private static void ValidatePlayer(string path, ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"player_missing path={path}");
                return;
            }

            var relays = prefab.GetComponents<PHSNetworkItemInteractionAudioRelay>();
            if (relays.Length != 1)
            {
                errors.Add($"relay_contract path={path} count={relays.Length}");
                return;
            }

            var ownerEmitter = ValidateEmitter(
                prefab,
                "PHS_ItemInteractionAudio_2D",
                0f,
                OwnerBindings,
                path,
                errors);
            var worldEmitter = ValidateEmitter(
                prefab,
                "PHS_ItemInteractionAudio_3D",
                1f,
                WorldBindings,
                path,
                errors);
            var relayObject = new SerializedObject(relays[0]);
            if (!relays[0].HasRequiredReferences
                || relayObject.FindProperty("ownerCuePlayerSource")
                    .objectReferenceValue != ownerEmitter
                || relayObject.FindProperty("worldCuePlayerSource")
                    .objectReferenceValue != worldEmitter)
            {
                errors.Add($"relay_reference_contract path={path}");
            }

            ValidateElectricShockAudio(prefab, path, errors);
        }

        private static void ValidateElectricShockAudio(
            GameObject prefab,
            string path,
            ICollection<string> errors)
        {
            var status = prefab.GetComponent<StatusEffectController>();
            var effectRoot = status == null
                ? null
                : new SerializedObject(status)
                    .FindProperty("electricShockEffectRoot")
                    ?.objectReferenceValue as GameObject;
            var sources = effectRoot == null
                ? Array.Empty<AudioSource>()
                : effectRoot.GetComponentsInChildren<AudioSource>(true);
            var source = sources.Length == 1 ? sources[0] : null;
            var expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                PHSCuratedAssetSfxAuthoring.BatteryShockPath);
            if (status == null
                || effectRoot == null
                || sources.Length != 1
                || source == null
                || source.gameObject != effectRoot
                || !source.enabled
                || source.clip != expectedClip
                || source.playOnAwake
                || source.loop
                || !Mathf.Approximately(source.volume, 0.65f)
                || !Mathf.Approximately(source.spatialBlend, 1f)
                || !Mathf.Approximately(source.dopplerLevel, 0f)
                || source.rolloffMode != AudioRolloffMode.Logarithmic
                || !Mathf.Approximately(source.minDistance, 1f)
                || !Mathf.Approximately(source.maxDistance, 15f))
            {
                errors.Add($"electric_shock_audio_contract path={path}");
            }
        }

        private static NetworkAudioCueEmitter ValidateEmitter(
            GameObject prefab,
            string name,
            float spatialBlend,
            IReadOnlyCollection<BindingContract> expectedBindings,
            string path,
            ICollection<string> errors)
        {
            var objects = prefab.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == name)
                .ToArray();
            var source = objects.Length == 1
                ? objects[0].GetComponent<AudioSource>()
                : null;
            var emitter = objects.Length == 1
                ? objects[0].GetComponent<NetworkAudioCueEmitter>()
                : null;
            if (objects.Length != 1
                || source == null
                || !Mathf.Approximately(source.spatialBlend, spatialBlend)
                || !Mathf.Approximately(source.dopplerLevel, 0f)
                || source.playOnAwake
                || source.loop
                || source.rolloffMode != AudioRolloffMode.Logarithmic
                || !Mathf.Approximately(source.minDistance, 1f)
                || !Mathf.Approximately(source.maxDistance, 20f)
                || emitter == null)
            {
                errors.Add($"emitter_contract path={path} name={name}");
                return emitter;
            }

            var serialized = new SerializedObject(emitter);
            if (serialized.FindProperty("audioSource").objectReferenceValue
                != source)
            {
                errors.Add($"emitter_source_contract path={path} name={name}");
            }

            var bindings = serialized.FindProperty("cueBindings");
            if (bindings.arraySize != expectedBindings.Count)
            {
                errors.Add(
                    $"emitter_binding_count path={path} name={name} actual={bindings.arraySize} expected={expectedBindings.Count}");
            }

            var expectedByCue = expectedBindings.ToDictionary(
                binding => binding.Cue);
            var observed = new HashSet<NetworkAudioCue>();
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                var cue = (NetworkAudioCue)binding
                    .FindPropertyRelative("cue").intValue;
                if (!observed.Add(cue)
                    || !expectedByCue.TryGetValue(cue, out var expected))
                {
                    errors.Add(
                        $"emitter_binding_unexpected path={path} name={name} cue={cue}");
                    continue;
                }

                var expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    PHSCuratedAssetSfxAuthoring.GetCuePath(expected.Cue));
                if (binding.FindPropertyRelative("clip").objectReferenceValue
                        != expectedClip
                    || !Mathf.Approximately(
                        binding.FindPropertyRelative("volumeScale").floatValue,
                        expected.Volume)
                    || !Mathf.Approximately(
                        binding.FindPropertyRelative("cooldownSeconds").floatValue,
                        expected.Cooldown))
                {
                    errors.Add(
                        $"emitter_binding_contract path={path} name={name} cue={cue}");
                }
            }

            foreach (var expected in expectedBindings)
            {
                if (!observed.Contains(expected.Cue))
                {
                    errors.Add(
                        $"emitter_binding_missing path={path} name={name} cue={expected.Cue}");
                }
            }

            return emitter;
        }

        private static void ValidateHookContracts(ICollection<string> errors)
        {
            RequireOrdered(
                Root + "/02. Script/Multiplayer/PHSNetworkUtilityFamilyActionController.cs",
                "family_owner_gate_sequence",
                errors,
                "if (itemLifecycle == null",
                "ownerSequence++;",
                "RequestActionServerRpc(",
                "if (familyKind == PHSUtilityFamilyActionKind.Wrench)",
                "TryPlayOwnerPredicted(",
                "NetworkAudioCue.WrenchImpact");
            RequireOrdered(
                Root + "/02. Script/Multiplayer/PHSNetworkUtilityFamilyActionController.cs",
                "family_server_confirmed_feedback_sequence",
                errors,
                "if (!IsServer",
                "lastServerSequence = requestSequence;",
                "if (resolvedCandidate.TryResolve(itemRecord, requestSequence, gameObject))",
                "if (resolvedCandidate.IsRepairTarget)",
                "NetworkAudioCue.RepairComplete",
                "== PHSUtilityFamilyActionKind.FireExtinguisher",
                "NetworkAudioCue.ExtinguisherSpray",
                "if (familyKind == PHSUtilityFamilyActionKind.Wrench)",
                "NetworkAudioCue.WrenchImpact",
                "if (resolvedCandidate.IsRepairComplete)",
                "NetworkAudioCue.RepairComplete",
                "== PHSUtilityFamilyActionKind.FireExtinguisher",
                "&& resolvedCandidate.IsRepairComplete)",
                "NetworkAudioCue.ExtinguishComplete");
            RequireOrdered(
                Root + "/02. Script/Items/PHSNetworkFoamGunController.cs",
                "foam_owner_gate_sequence",
                errors,
                "if (!CanRequestFire || Time.unscaledTime < nextLocalShotTime)",
                "localShotSequence++;",
                "RequestFireServerRpc(origin, direction, localShotSequence);",
                "TryPlayOwnerPredicted(NetworkAudioCue.FoamShot)");
            RequireOrdered(
                Root + "/02. Script/Items/PHSNetworkFoamCoordinator.cs",
                "foam_attach_harden_sequence",
                errors,
                "if (!IsServer",
                "blob.AttachServer(",
                "PublishAccumulator(accumulator);",
                "NetworkAudioCue.FoamAttach",
                "blob.ShotSequence",
                "var wasHardened = accumulator.State",
                "accumulator.State = NetworkFoamTargetState.Hardened;",
                "if (!wasHardened)",
                "NetworkAudioCue.FoamHarden",
                "blob.ShotSequence");
            RequireOrdered(
                Root + "/02. Script/Items/PHSNetworkFoamCoordinator.cs",
                "foam_terminal_sequence",
                errors,
                "utilityTarget.TryResolveUtilityAttack(",
                "accumulator.State = NetworkFoamTargetState.Completed;",
                "PublishCompletionFeedback(shooter, accumulator);",
                "NetworkAudioCue.FoamFireComplete",
                "NetworkAudioCue.FoamSealComplete",
                "blob.ShotSequence");
            RequireOrdered(
                Root + "/02. Script/Interaction/BatteryInsertPowerStationSocket.cs",
                "battery_commit_sequence",
                errors,
                "if (!IsSpawned || !IsServer)",
                "if (!completedRequests.Add(requestKey))",
                "if (!itemRecord.TryConsumeHeldItemServer(itemId, expectedRevision))",
                "if (!shipState.TryRestorePowerWithBattery(out reason))",
                "if (hasActivePowerFailureAccident",
                "if (!TryResolveBatteryFamilyItem(",
                "NetworkAudioCue.BatteryInstall",
                "expectedRevision");
        }

        private static void RequireOrdered(
            string path,
            string contract,
            ICollection<string> errors,
            params string[] markers)
        {
            if (!File.Exists(path))
            {
                errors.Add($"hook_source_missing contract={contract} path={path}");
                return;
            }

            var source = File.ReadAllText(path);
            var searchFrom = 0;
            foreach (var marker in markers)
            {
                var index = source.IndexOf(
                    marker,
                    searchFrom,
                    StringComparison.Ordinal);
                if (index < 0)
                {
                    errors.Add(
                        $"hook_order_contract contract={contract} marker={marker}");
                    return;
                }

                searchFrom = index + marker.Length;
            }
        }
    }
}
