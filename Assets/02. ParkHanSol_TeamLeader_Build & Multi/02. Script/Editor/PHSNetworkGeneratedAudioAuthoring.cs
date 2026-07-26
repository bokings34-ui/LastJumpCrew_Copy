using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkGeneratedAudioAuthoring
    {
        private const string OutputFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/06. Audio/NetworkGenerated";
        private const int SampleRate = 44100;
        private const int FixedSeed = 20260723;
        private const double MaximumPeak = 0.7079457843841379;

        private enum RecipeKind : byte
        {
            UiClick,
            ItemPickup,
            ItemDrop,
            ItemSwap,
            ShopSuccess,
            ShopFail,
            Warning,
            Clear,
            GameOver,
            RestartSuccess,
            RestartFail,
            TutorialComplete,
            WarpSafeZoneEnter,
            WarpSafeZoneExit
        }

        private readonly struct Recipe
        {
            public Recipe(string fileName, double durationSeconds, RecipeKind kind)
            {
                FileName = fileName;
                DurationSeconds = durationSeconds;
                Kind = kind;
            }

            public string FileName { get; }
            public double DurationSeconds { get; }
            public RecipeKind Kind { get; }
        }

        private static readonly IReadOnlyList<Recipe> Recipes = new[]
        {
            new Recipe("PHS_Network_UI_Click.wav", 0.08d, RecipeKind.UiClick),
            new Recipe("PHS_Network_Item_Pickup.wav", 0.22d, RecipeKind.ItemPickup),
            new Recipe("PHS_Network_Item_Drop.wav", 0.24d, RecipeKind.ItemDrop),
            new Recipe("PHS_Network_Item_Swap.wav", 0.30d, RecipeKind.ItemSwap),
            new Recipe("PHS_Network_Shop_Success.wav", 0.64d, RecipeKind.ShopSuccess),
            new Recipe("PHS_Network_Shop_Fail.wav", 0.42d, RecipeKind.ShopFail),
            new Recipe("PHS_Network_Warning.wav", 0.90d, RecipeKind.Warning),
            new Recipe("PHS_Network_Clear.wav", 1.45d, RecipeKind.Clear),
            new Recipe("PHS_Network_GameOver.wav", 1.35d, RecipeKind.GameOver),
            new Recipe("PHS_Network_Restart_Success.wav", 0.72d, RecipeKind.RestartSuccess),
            new Recipe("PHS_Network_Restart_Fail.wav", 0.48d, RecipeKind.RestartFail),
            new Recipe("PHS_Network_TutorialComplete.wav", 0.92d, RecipeKind.TutorialComplete),
            new Recipe("PHS_Warp_SafeZone_Enter.wav", 0.58d, RecipeKind.WarpSafeZoneEnter),
            new Recipe("PHS_Warp_SafeZone_Exit.wav", 0.34d, RecipeKind.WarpSafeZoneExit)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Generated Audio")]
        public static void Author()
        {
            Directory.CreateDirectory(OutputFolder);
            for (var index = 0; index < Recipes.Count; index++)
            {
                var recipe = Recipes[index];
                var samples = Render(recipe, FixedSeed + index * 7919);
                WritePcm16Wave(
                    Path.Combine(OutputFolder, recipe.FileName),
                    samples);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"PHS_NETWORK_GENERATED_AUDIO_OK count={Recipes.Count} sampleRate={SampleRate} seed={FixedSeed} folder={OutputFolder}");
        }

        private static float[] Render(Recipe recipe, int seed)
        {
            var sampleCount = Math.Max(
                1,
                (int)Math.Round(recipe.DurationSeconds * SampleRate));
            var samples = new float[sampleCount];
            var random = new System.Random(seed);
            var maximumAbsoluteSample = 0d;
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (double)SampleRate;
                var normalizedTime = time / recipe.DurationSeconds;
                var sample = RenderSample(
                    recipe.Kind,
                    time,
                    normalizedTime,
                    random);
                samples[index] = (float)sample;
                maximumAbsoluteSample = Math.Max(
                    maximumAbsoluteSample,
                    Math.Abs(sample));
            }

            var scale = maximumAbsoluteSample <= double.Epsilon
                ? 0d
                : MaximumPeak / maximumAbsoluteSample;
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = (float)Math.Clamp(
                    samples[index] * scale,
                    -MaximumPeak,
                    MaximumPeak);
            }

            return samples;
        }

        private static double RenderSample(
            RecipeKind kind,
            double time,
            double normalizedTime,
            System.Random random)
        {
            var noise = random.NextDouble() * 2d - 1d;
            var release = Math.Pow(Math.Max(0d, 1d - normalizedTime), 2d);
            return kind switch
            {
                RecipeKind.UiClick =>
                    Chirp(time, normalizedTime, 920d, 620d) * release
                    + noise * 0.08d * Math.Exp(-time * 80d),
                RecipeKind.ItemPickup =>
                    Arpeggio(time, normalizedTime, 660d, 880d, 1100d)
                    * release,
                RecipeKind.ItemDrop =>
                    Chirp(time, normalizedTime, 145d, 72d) * release
                    + noise * 0.18d * release,
                RecipeKind.ItemSwap =>
                    (normalizedTime < 0.45d
                        ? Chirp(time, normalizedTime / 0.45d, 520d, 360d)
                        : Chirp(time, (normalizedTime - 0.45d) / 0.55d, 620d, 930d))
                    * release,
                RecipeKind.ShopSuccess =>
                    Arpeggio(time, normalizedTime, 1046.5d, 1318.5d, 1568d)
                    * release,
                RecipeKind.ShopFail =>
                    Arpeggio(time, normalizedTime, 310d, 205d, 155d)
                    * release,
                RecipeKind.Warning =>
                    Math.Sin(2d * Math.PI *
                        (Math.Floor(time * 6d) % 2d == 0d ? 760d : 570d) * time)
                    * Pulse(time, 3d, 0.9d),
                RecipeKind.Clear =>
                    Arpeggio(time, normalizedTime, 523.25d, 659.25d, 783.99d)
                    * Math.Sqrt(release),
                RecipeKind.GameOver =>
                    Arpeggio(time, normalizedTime, 392d, 311.13d, 261.63d)
                    * Math.Sqrt(release),
                RecipeKind.RestartSuccess =>
                    Chirp(time, normalizedTime, 180d, 760d) * Math.Sqrt(release)
                    + noise * 0.04d * release,
                RecipeKind.RestartFail =>
                    (Math.Sin(2d * Math.PI * 220d * time)
                        + 0.5d * Math.Sin(2d * Math.PI * 146.83d * time))
                    * Pulse(time, 2d, 0.48d),
                RecipeKind.TutorialComplete =>
                    Arpeggio(time, normalizedTime, 783.99d, 1046.5d, 1318.5d)
                    * release,
                RecipeKind.WarpSafeZoneEnter =>
                    (Chirp(time, normalizedTime, 95d, 920d)
                        + 0.38d * Chirp(time, normalizedTime, 190d, 1380d)
                        + noise * 0.12d)
                    * Math.Sin(Math.PI * Math.Clamp(normalizedTime, 0d, 1d)),
                RecipeKind.WarpSafeZoneExit =>
                    (Chirp(time, normalizedTime, 980d, 120d)
                        + 0.3d * Chirp(time, normalizedTime, 1460d, 240d)
                        + noise * 0.08d)
                    * Math.Pow(Math.Max(0d, 1d - normalizedTime), 4d),
                _ => 0d
            };
        }

        private static double Chirp(
            double time,
            double normalizedTime,
            double startFrequency,
            double endFrequency)
        {
            var frequency = startFrequency
                + (endFrequency - startFrequency)
                * Math.Clamp(normalizedTime, 0d, 1d);
            return Math.Sin(2d * Math.PI * frequency * time);
        }

        private static double Arpeggio(
            double time,
            double normalizedTime,
            double first,
            double second,
            double third)
        {
            var frequency = normalizedTime < 1d / 3d
                ? first
                : normalizedTime < 2d / 3d
                    ? second
                    : third;
            return Math.Sin(2d * Math.PI * frequency * time);
        }

        private static double Pulse(
            double time,
            double pulseCount,
            double duration)
        {
            var pulsePosition = time * pulseCount / duration;
            var local = pulsePosition - Math.Floor(pulsePosition);
            return local < 0.55d
                ? Math.Sin(Math.PI * local / 0.55d)
                : 0d;
        }

        private static void WritePcm16Wave(
            string path,
            IReadOnlyList<float> samples)
        {
            const short channelCount = 1;
            const short bitsPerSample = 16;
            var dataByteCount = samples.Count * sizeof(short);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            WriteFourCc(writer, "RIFF");
            writer.Write(36 + dataByteCount);
            WriteFourCc(writer, "WAVE");
            WriteFourCc(writer, "fmt ");
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channelCount);
            writer.Write(SampleRate);
            writer.Write(SampleRate * channelCount * bitsPerSample / 8);
            writer.Write((short)(channelCount * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            WriteFourCc(writer, "data");
            writer.Write(dataByteCount);
            foreach (var sample in samples)
            {
                writer.Write((short)Math.Round(
                    Math.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        private static void WriteFourCc(BinaryWriter writer, string value)
        {
            writer.Write(Encoding.ASCII.GetBytes(value));
        }
    }
}
