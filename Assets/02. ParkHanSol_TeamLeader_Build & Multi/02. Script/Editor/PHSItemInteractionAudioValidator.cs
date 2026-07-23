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
        private const int PositionedVoiceLimit = 3;
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string AudioRoot = Root + "/06. Audio/NetworkGenerated";
        private static readonly string[] Files =
        {
            "PHS_Item_Wrench_Impact.wav",
            "PHS_Item_Repair_Complete.wav",
            "PHS_Item_Extinguisher_Spray.wav",
            "PHS_Item_Extinguish_Complete.wav",
            "PHS_Item_Battery_Install.wav",
            "PHS_Item_Battery_Shock.wav",
            "PHS_Item_Foam_Shot.wav",
            "PHS_Item_Foam_Attach.wav",
            "PHS_Item_Foam_Harden.wav",
            "PHS_Item_Foam_Seal_Complete.wav",
            "PHS_Item_Foam_Fire_Complete.wav"
        };
        private static readonly string[] PlayerPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };
        private static readonly BindingContract[] OwnerBindings =
        {
            new(NetworkAudioCue.ExtinguisherSpray, "PHS_Item_Extinguisher_Spray.wav", 0.55f, 0.12f),
            new(NetworkAudioCue.FoamShot, "PHS_Item_Foam_Shot.wav", 0.65f, 0.08f)
        };
        private static readonly BindingContract[] WorldBindings =
        {
            new(NetworkAudioCue.WrenchImpact, "PHS_Item_Wrench_Impact.wav", 0.75f, 0.08f),
            new(NetworkAudioCue.RepairComplete, "PHS_Item_Repair_Complete.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.ExtinguishComplete, "PHS_Item_Extinguish_Complete.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.BatteryInstall, "PHS_Item_Battery_Install.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.FoamAttach, "PHS_Item_Foam_Attach.wav", 0.65f, 0.06f),
            new(NetworkAudioCue.FoamHarden, "PHS_Item_Foam_Harden.wav", 0.65f, 0.12f),
            new(NetworkAudioCue.FoamSealComplete, "PHS_Item_Foam_Seal_Complete.wav", 0.8f, 0.20f),
            new(NetworkAudioCue.FoamFireComplete, "PHS_Item_Foam_Fire_Complete.wav", 0.8f, 0.20f)
        };

        private readonly struct BindingContract
        {
            public BindingContract(
                NetworkAudioCue cue,
                string file,
                float volume,
                float cooldown)
            {
                Cue = cue;
                File = file;
                Volume = volume;
                Cooldown = cooldown;
            }

            public NetworkAudioCue Cue { get; }
            public string File { get; }
            public float Volume { get; }
            public float Cooldown { get; }
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Item Interaction Audio")]
        public static void Validate()
        {
            var errors = new List<string>();
            foreach (var file in Files)
            {
                ValidateWave($"{AudioRoot}/{file}", errors);
            }

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

            Debug.Log("PHS_ITEM_INTERACTION_AUDIO_VALIDATION_PASSED waves=11 players=2 owner2D=true world3D=true shock3D=true");
        }

        private static void ValidateWave(string path, ICollection<string> errors)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.channels != 1 || clip.frequency != 44100)
            {
                errors.Add($"wave_import path={path}");
                return;
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44
                || BitConverter.ToInt16(bytes, 22) != 1
                || BitConverter.ToInt32(bytes, 24) != 44100
                || BitConverter.ToInt16(bytes, 34) != 16)
            {
                errors.Add($"wave_pcm_contract path={path}");
            }
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
                || worldEmitter is not IPositionedNetworkAudioCuePlayer
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
                $"{AudioRoot}/PHS_Item_Battery_Shock.wav");
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

            var positionedVoiceLimit =
                serialized.FindProperty("positionedVoiceLimit").intValue;
            if (positionedVoiceLimit != PositionedVoiceLimit)
            {
                errors.Add(
                    $"emitter_positioned_voice_limit path={path} name={name} actual={positionedVoiceLimit} expected={PositionedVoiceLimit}");
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
                    $"{AudioRoot}/{expected.File}");
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
                "TryPlayOwnerPredicted(",
                "NetworkAudioCue.ExtinguisherSpray");
            RequireOrdered(
                Root + "/02. Script/Multiplayer/PHSNetworkUtilityFamilyActionController.cs",
                "family_server_commit_terminal_sequence",
                errors,
                "if (!IsServer",
                "lastServerSequence = requestSequence;",
                "if (candidate.TryResolve(itemRecord, requestSequence, gameObject))",
                "NetworkAudioCue.WrenchImpact",
                "candidate.AimPosition",
                "if (candidate.IsRepairComplete)",
                "NetworkAudioCue.RepairComplete",
                "candidate.AimPosition",
                "== PHSUtilityFamilyActionKind.FireExtinguisher",
                "&& candidate.IsRepairComplete)",
                "NetworkAudioCue.ExtinguishComplete",
                "candidate.AimPosition");
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
                "attachPosition",
                "var wasHardened = accumulator.State",
                "accumulator.State = NetworkFoamTargetState.Hardened;",
                "if (!wasHardened)",
                "NetworkAudioCue.FoamHarden",
                "blob.ShotSequence",
                "attachPosition");
            RequireOrdered(
                Root + "/02. Script/Items/PHSNetworkFoamCoordinator.cs",
                "foam_terminal_sequence",
                errors,
                "utilityTarget.TryResolveUtilityAttack(",
                "accumulator.State = NetworkFoamTargetState.Completed;",
                "PublishCompletionFeedback(shooter, accumulator);",
                "NetworkAudioCue.FoamFireComplete",
                "NetworkAudioCue.FoamSealComplete",
                "blob.ShotSequence",
                "attachPosition");
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
                "expectedRevision",
                "transform.position");
            RequireOrdered(
                Root + "/02. Script/Multiplayer/Audio/PHSNetworkItemInteractionAudioRelay.cs",
                "confirmed_dedupe_store_separation",
                errors,
                "private const int MaxRememberedKeys = 256;",
                "HashSet<ulong> broadcastKeys",
                "HashSet<ulong> playedKeys",
                "RememberKey(key, broadcastKeys, broadcastKeyOrder)",
                "PlayConfirmedClientRpc(cue, key, confirmedPosition);",
                "RememberKey(key, playedKeys, playedKeyOrder)",
                "worldCuePlayer.TryPlayAt(cue, confirmedPosition");
            RequireOrdered(
                Root + "/02. Script/Multiplayer/Audio/PHSNetworkItemInteractionAudioRelay.cs",
                "confirmed_position_rpc_sequence",
                errors,
                "TryBroadcastConfirmedServer(",
                "Vector3 confirmedPosition",
                "PlayConfirmedClientRpc(cue, key, confirmedPosition);",
                "[ClientRpc]",
                "Vector3 confirmedPosition)",
                "worldCuePlayer.TryPlayAt(cue, confirmedPosition");
            RequireOrdered(
                Root + "/02. Script/Multiplayer/Audio/NetworkAudioCueEmitter.cs",
                "positioned_emitter_pool_sequence",
                errors,
                "private const int DefaultPositionedVoiceLimit = 3;",
                "private int positionedVoiceLimit = DefaultPositionedVoiceLimit;",
                "public bool TryPlayAt(",
                "EnsurePositionedVoicePool();",
                "var voice = SelectPositionedVoice();",
                "voice.Source.Stop();",
                "CopyPlaybackSettings(audioSource, voice.Source);",
                "voice.Root.transform.position = position;",
                "voice.Source.PlayOneShot(",
                "private void EnsurePositionedVoicePool()",
                "positionedVoices.Capacity = voiceLimit;",
                "private PositionedVoice SelectPositionedVoice()",
                "if (!voice.Source.isPlaying)",
                "return oldest;",
                "private void OnDestroy()",
                "Destroy(positionedVoiceRoot);");
            RequireOrdered(
                Root + "/02. Script/Editor/PHSItemInteractionAudioAuthoring.cs",
                "positioned_voice_authoring",
                errors,
                "private const int PositionedVoiceLimit = 3;",
                "serialized.FindProperty(\"positionedVoiceLimit\").intValue =",
                "PositionedVoiceLimit;");
            RequireAbsentBetween(
                Root + "/02. Script/Multiplayer/Audio/NetworkAudioCueEmitter.cs",
                "positioned_emitter_no_per_shot_allocation",
                errors,
                "public bool TryPlayAt(",
                "private void EnsurePositionedVoicePool()",
                "new GameObject(",
                "AddComponent<AudioSource>()",
                "Destroy(");
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

        private static void RequireAbsentBetween(
            string path,
            string contract,
            ICollection<string> errors,
            string startMarker,
            string endMarker,
            params string[] forbiddenMarkers)
        {
            if (!File.Exists(path))
            {
                errors.Add($"hook_source_missing contract={contract} path={path}");
                return;
            }

            var source = File.ReadAllText(path);
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = start < 0
                ? -1
                : source.IndexOf(endMarker, start, StringComparison.Ordinal);
            if (start < 0 || end <= start)
            {
                errors.Add(
                    $"hook_range_contract contract={contract} start={startMarker} end={endMarker}");
                return;
            }

            var body = source.Substring(start, end - start);
            foreach (var marker in forbiddenMarkers)
            {
                if (body.IndexOf(marker, StringComparison.Ordinal) >= 0)
                {
                    errors.Add(
                        $"hook_forbidden_contract contract={contract} marker={marker}");
                }
            }
        }
    }
}
