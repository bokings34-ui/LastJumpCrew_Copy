using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSItemInteractionAudioGenerator
    {
        private const string OutputFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/06. Audio/NetworkGenerated";
        private const int SampleRate = 44100;
        private const int Seed = 2026072302;
        private const double Peak = 0.68d;

        private readonly struct Recipe
        {
            public Recipe(string file, double duration, double low, double high, double noise)
            {
                File = file;
                Duration = duration;
                Low = low;
                High = high;
                Noise = noise;
            }

            public string File { get; }
            public double Duration { get; }
            public double Low { get; }
            public double High { get; }
            public double Noise { get; }
        }

        private static readonly IReadOnlyList<Recipe> Recipes = new[]
        {
            new Recipe("PHS_Item_Wrench_Impact.wav", 0.16d, 180d, 760d, 0.20d),
            new Recipe("PHS_Item_Repair_Complete.wav", 0.48d, 520d, 1040d, 0.03d),
            new Recipe("PHS_Item_Extinguisher_Spray.wav", 0.18d, 90d, 210d, 0.72d),
            new Recipe("PHS_Item_Extinguish_Complete.wav", 0.44d, 260d, 880d, 0.18d),
            new Recipe("PHS_Item_Battery_Install.wav", 0.34d, 110d, 620d, 0.14d),
            new Recipe("PHS_Item_Foam_Shot.wav", 0.14d, 130d, 430d, 0.50d),
            new Recipe("PHS_Item_Foam_Attach.wav", 0.18d, 90d, 240d, 0.38d),
            new Recipe("PHS_Item_Foam_Harden.wav", 0.28d, 210d, 720d, 0.12d),
            new Recipe("PHS_Item_Foam_Seal_Complete.wav", 0.46d, 380d, 980d, 0.06d),
            new Recipe("PHS_Item_Foam_Fire_Complete.wav", 0.52d, 170d, 840d, 0.24d),
            new Recipe("PHS_Item_Battery_Shock.wav", 0.62d, 170d, 1380d, 0.34d)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Generate Item Interaction Audio")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            for (var recipeIndex = 0; recipeIndex < Recipes.Count; recipeIndex++)
            {
                var recipe = Recipes[recipeIndex];
                WriteWave(
                    Path.Combine(OutputFolder, recipe.File),
                    Render(recipe, Seed + recipeIndex * 3571));
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"PHS_ITEM_INTERACTION_AUDIO_GENERATED count={Recipes.Count} rate={SampleRate} channels=1 bits=16 seed={Seed}");
        }

        private static float[] Render(Recipe recipe, int seed)
        {
            var count = Math.Max(1, (int)Math.Round(recipe.Duration * SampleRate));
            var samples = new float[count];
            var random = new System.Random(seed);
            var maximum = 0d;
            var phase = 0d;
            for (var index = 0; index < count; index++)
            {
                var t = index / (double)SampleRate;
                var normalized = t / recipe.Duration;
                var frequency = recipe.Low
                    + (recipe.High - recipe.Low) * Smooth(normalized);
                phase += 2d * Math.PI * frequency / SampleRate;
                var attack = Math.Min(1d, normalized * 35d);
                var release = Math.Pow(Math.Max(0d, 1d - normalized), 2d);
                var noise = random.NextDouble() * 2d - 1d;
                var pulse = 0.72d * Math.Sin(phase)
                    + 0.28d * Math.Sin(phase * 2.01d);
                var sample = attack * release
                    * (pulse * (1d - recipe.Noise) + noise * recipe.Noise);
                samples[index] = (float)sample;
                maximum = Math.Max(maximum, Math.Abs(sample));
            }

            var scale = maximum <= double.Epsilon ? 0d : Peak / maximum;
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = (float)Math.Clamp(samples[index] * scale, -Peak, Peak);
            }

            return samples;
        }

        private static double Smooth(double value)
        {
            value = Math.Clamp(value, 0d, 1d);
            return value * value * (3d - 2d * value);
        }

        private static void WriteWave(string path, IReadOnlyList<float> samples)
        {
            const short channels = 1;
            const short bits = 16;
            var dataBytes = samples.Count * sizeof(short);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            FourCc(writer, "RIFF");
            writer.Write(36 + dataBytes);
            FourCc(writer, "WAVE");
            FourCc(writer, "fmt ");
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * channels * bits / 8);
            writer.Write((short)(channels * bits / 8));
            writer.Write(bits);
            FourCc(writer, "data");
            writer.Write(dataBytes);
            foreach (var sample in samples)
            {
                writer.Write((short)Math.Round(
                    Math.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        private static void FourCc(BinaryWriter writer, string value)
        {
            writer.Write(Encoding.ASCII.GetBytes(value));
        }
    }
}
