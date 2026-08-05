using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSCuratedAssetSfxAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        public const string AudioRoot = Root + "/06. Audio/CuratedAssetSfx";
        public const string BatteryShockPath = AudioRoot + "/Item_BatteryShock.wav";
        public const string DoorOpenPath = AudioRoot + "/Tutorial_DoorOpen.wav";
        public const string UiConfirmPath = AudioRoot + "/Tutorial_UiConfirm.wav";
        public const string SpaceEngineLoopPath = AudioRoot + "/SpaceEngine_RunningLoop.wav";

        private readonly struct AssetCopy
        {
            public AssetCopy(string source, string destination)
            {
                Source = source;
                Destination = destination;
            }

            public string Source { get; }
            public string Destination { get; }
        }

        private static readonly IReadOnlyDictionary<NetworkAudioCue, AssetCopy>
            CueCopies = new Dictionary<NetworkAudioCue, AssetCopy>
            {
                { NetworkAudioCue.ItemPickup, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Click_Item_Pick_Up.wav", "Network_ItemPickup.wav") },
                { NetworkAudioCue.ItemSwap, Copy("Assets/Casual Game UI Sound/MOTION/CARTOON_MOTION_SFX_12.wav", "Network_ItemSwap.wav") },
                { NetworkAudioCue.ItemDrop, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Click_Item_Put.wav", "Network_ItemDrop.wav") },
                { NetworkAudioCue.ShopSuccess, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Coin Buy.wav", "Network_ShopSuccess.wav") },
                { NetworkAudioCue.ShopFailure, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Menu_Fail.wav", "Network_ShopFailure.wav") },
                { NetworkAudioCue.Warning, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Timer_01.wav", "Network_Warning.wav") },
                { NetworkAudioCue.RunClear, Copy("Assets/Casual Game UI Sound/NOTIFICATION/NOTIFICATION_Positive_Notification_01.wav", "Network_RunClear.wav") },
                { NetworkAudioCue.RunGameOver, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Digital_Fail_03.wav", "Network_RunGameOver.wav") },
                { NetworkAudioCue.RestartRequested, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Click_01.wav", "Network_RestartRequested.wav") },
                { NetworkAudioCue.RestartSucceeded, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Digital_Success_01.wav", "Network_RestartSucceeded.wav") },
                { NetworkAudioCue.RestartFailed, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Digital_Fail_01.wav", "Network_RestartFailed.wav") },
                { NetworkAudioCue.TutorialComplete, Copy("Assets/Electric Sfx/Wav/Jingle_Win_Synth/Jingle_Win_Synth_01.wav", "Tutorial_Complete.wav") },
                { NetworkAudioCue.WrenchImpact, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Tool_Metal_Put.wav", "Item_WrenchImpact.wav") },
                { NetworkAudioCue.RepairComplete, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Digital_Success_03.wav", "Item_RepairComplete.wav") },
                { NetworkAudioCue.ExtinguisherSpray, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Liquid_Put.wav", "Item_ExtinguisherSpray.wav") },
                { NetworkAudioCue.ExtinguishComplete, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Digital_Success_04.wav", "Item_ExtinguishComplete.wav") },
                { NetworkAudioCue.BatteryInstall, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Gear_Metal_Put_01.wav", "Item_BatteryInstall.wav") },
                { NetworkAudioCue.FoamShot, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Click_Bubble_01.wav", "Item_FoamShot.wav") },
                { NetworkAudioCue.FoamAttach, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Click_Bubble_02.wav", "Item_FoamAttach.wav") },
                { NetworkAudioCue.FoamHarden, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Stoen_Put_02.wav", "Item_FoamHarden.wav") },
                { NetworkAudioCue.FoamSealComplete, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Digital_Success_02.wav", "Item_FoamSealComplete.wav") },
                { NetworkAudioCue.FoamFireComplete, Copy("Assets/Electric Sfx/Wav/Jingle_Win_Synth/Jingle_Win_Synth_02.wav", "Item_FoamFireComplete.wav") },
                { NetworkAudioCue.DebrisDeposit, Copy("Assets/Casual Game UI Sound/ITEM/ITEM_Bow_Metal_Put.wav", "Gameplay_DebrisDeposit.wav") },
                { NetworkAudioCue.FootstepWalk, Copy("Assets/Casual Game UI Sound/MOTION/CARTOON_MOTION_SFX_05.wav", "Player_FootstepWalk.wav") },
                { NetworkAudioCue.FootstepRun, Copy("Assets/Casual Game UI Sound/MOTION/CARTOON_MOTION_SFX_04.wav", "Player_FootstepRun.wav") },
                { NetworkAudioCue.PlayerJump, Copy("Assets/Casual Game UI Sound/MOTION/CARTOON_MOTION_SFX_09.wav", "Player_Jump.wav") },
                { NetworkAudioCue.MissionSuccess, Copy("Assets/Casual Game UI Sound/NOTIFICATION/NOTIFICATION_Positive_Notification_01.wav", "Gameplay_MissionSuccess.wav") },
                { NetworkAudioCue.VendingInteraction, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Button_Touch_02.wav", "Gameplay_VendingInteraction.wav") },
                { NetworkAudioCue.InteractionFocus, Copy("Assets/Electric Sfx/Wav/UI_Electric/UI_Electric_07.wav", "UI_InteractionFocus.wav") },
                { NetworkAudioCue.OptionsSaved, Copy("Assets/Electric Sfx/Wav/UI_Electric/UI_Electric_03.wav", "UI_OptionsSaved.wav") },
                { NetworkAudioCue.WarpStart, Copy("Assets/Electric Sfx/Wav/SpaceEngine/SpaceEngine_Start_00.wav", "Gameplay_WarpStart.wav") },
                { NetworkAudioCue.WarpEnd, Copy("Assets/Electric Sfx/Wav/SpaceEngine/SpaceEngine_Stop_00.wav", "Gameplay_WarpEnd.wav") },
                { NetworkAudioCue.AccidentAppeared, Copy("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Error_02.wav", "Gameplay_AccidentAppeared.wav") }
            };

        private static readonly AssetCopy[] ExtraCopies =
        {
            new("Assets/Electric Sfx/Wav/UI_Electric/UI_Electric_11.wav", BatteryShockPath),
            new("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Synth_Zoom_06.wav", DoorOpenPath),
            new("Assets/Casual Game UI Sound/USER_INTERFACE/USER_INTERFACE_Button_Click_01.wav", UiConfirmPath),
            new("Assets/Electric Sfx/Wav/SpaceEngine/SpaceEngine_Running_Loop_00.wav", SpaceEngineLoopPath)
        };

        public static IReadOnlyCollection<string> AllRuntimePaths => CueCopies
            .Values
            .Select(copy => copy.Destination)
            .Concat(ExtraCopies.Select(copy => copy.Destination))
            .ToArray();

        public static string GetCuePath(NetworkAudioCue cue)
        {
            if (!CueCopies.TryGetValue(cue, out var copy))
            {
                throw new InvalidOperationException(
                    $"PHS_CURATED_AUDIO_FAILED reason=cue_mapping_missing cue={cue}");
            }

            return copy.Destination;
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Curated Asset SFX")]
        public static void Author()
        {
            EnsureDestinationFolder();
            foreach (var copy in CueCopies.Values.Concat(ExtraCopies))
            {
                CopyOwnedAsset(copy);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"PHS_CURATED_AUDIO_AUTHORED cues={CueCopies.Count} extras={ExtraCopies.Length} root={AudioRoot}");
        }

        private static AssetCopy Copy(string source, string fileName)
        {
            return new AssetCopy(source, $"{AudioRoot}/{fileName}");
        }

        private static void EnsureDestinationFolder()
        {
            if (AssetDatabase.IsValidFolder(AudioRoot))
            {
                return;
            }

            var guid = AssetDatabase.CreateFolder(Root + "/06. Audio", "CuratedAssetSfx");
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new InvalidOperationException(
                    $"PHS_CURATED_AUDIO_FAILED reason=destination_folder_create_failed path={AudioRoot}");
            }
        }

        private static void CopyOwnedAsset(AssetCopy copy)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(copy.Destination);
            if (existing != null)
            {
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<AudioClip>(copy.Source);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"PHS_CURATED_AUDIO_FAILED reason=source_clip_missing path={copy.Source}");
            }

            if (AssetDatabase.LoadMainAssetAtPath(copy.Destination) != null)
            {
                throw new InvalidOperationException(
                    $"PHS_CURATED_AUDIO_FAILED reason=destination_type_invalid path={copy.Destination}");
            }

            if (!AssetDatabase.CopyAsset(copy.Source, copy.Destination))
            {
                throw new InvalidOperationException(
                    $"PHS_CURATED_AUDIO_FAILED reason=copy_failed source={copy.Source} destination={copy.Destination}");
            }

            AssetDatabase.ImportAsset(copy.Destination, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(copy.Destination) == null)
            {
                throw new InvalidOperationException(
                    $"PHS_CURATED_AUDIO_FAILED reason=copied_clip_import_failed path={copy.Destination}");
            }
        }
    }
}
