using System;
using TaiwuGu.Audio;
using Xunit;

namespace Core.Logic.Tests.Verifications
{
    public class HalalAudioTests
    {
        [Fact]
        public void Test_AllSynthesizers_GenerateValidBuffers_AndExpectedLengths()
        {
            var wind = HalalSoundBuffers.CreateMountainWindLoop(4.0f);
            var combatWind = HalalSoundBuffers.CreateTurbulentCombatWind(3.0f);
            var footstep = HalalSoundBuffers.CreateTileFootstep();
            var parchment = HalalSoundBuffers.CreateParchmentRustle();
            var slash = HalalSoundBuffers.CreateBladeSlash();
            var impact = HalalSoundBuffers.CreateBluntImpact();
            var dash = HalalSoundBuffers.CreateDashWhoosh();
            var fire = HalalSoundBuffers.CreateCauldronFireBurst();
            var stone = HalalSoundBuffers.CreateStoneShatter();
            var steam = HalalSoundBuffers.CreateCauldronSteamHiss();
            var knock = HalalSoundBuffers.CreateWoodenSignalKnock();

            Assert.Equal(176400, wind.Length);
            Assert.Equal(132300, combatWind.Length);
            Assert.Equal(3969, footstep.Length);
            Assert.Equal(7056, parchment.Length);
            Assert.Equal(7938, slash.Length);
            Assert.Equal(9702, impact.Length);
            Assert.Equal(11025, dash.Length);
            Assert.Equal(16758, fire.Length);
            Assert.Equal(12348, stone.Length);
            Assert.Equal(14112, steam.Length);
            Assert.Equal(5292, knock.Length);
        }

        [Fact]
        public void Test_AudioBuffers_StayWithinAmplitudeBounds()
        {
            float[][] allBuffers = new float[][]
            {
                HalalSoundBuffers.CreateMountainWindLoop(1.0f),
                HalalSoundBuffers.CreateTurbulentCombatWind(1.0f),
                HalalSoundBuffers.CreateTileFootstep(),
                HalalSoundBuffers.CreateParchmentRustle(),
                HalalSoundBuffers.CreateBladeSlash(),
                HalalSoundBuffers.CreateBluntImpact(),
                HalalSoundBuffers.CreateDashWhoosh(),
                HalalSoundBuffers.CreateCauldronFireBurst(),
                HalalSoundBuffers.CreateStoneShatter(),
                HalalSoundBuffers.CreateCauldronSteamHiss(),
                HalalSoundBuffers.CreateWoodenSignalKnock()
            };

            foreach (var buffer in allBuffers)
            {
                Assert.NotEmpty(buffer);
                for (int i = 0; i < buffer.Length; i++)
                {
                    float s = buffer[i];
                    Assert.False(float.IsNaN(s), "Audio buffer contained NaN value.");
                    Assert.False(float.IsInfinity(s), "Audio buffer contained Infinity value.");
                    Assert.True(s >= -1.0f && s <= 1.0f, $"Audio buffer sample {s} exceeded [-1.0, 1.0] at index {i}");
                }
            }
        }

        [Fact]
        public void Test_AudioBuffers_HaveNonZeroEnergy()
        {
            float[][] allBuffers = new float[][]
            {
                HalalSoundBuffers.CreateMountainWindLoop(0.5f),
                HalalSoundBuffers.CreateTurbulentCombatWind(0.5f),
                HalalSoundBuffers.CreateTileFootstep(),
                HalalSoundBuffers.CreateParchmentRustle(),
                HalalSoundBuffers.CreateBladeSlash(),
                HalalSoundBuffers.CreateBluntImpact(),
                HalalSoundBuffers.CreateDashWhoosh(),
                HalalSoundBuffers.CreateCauldronFireBurst(),
                HalalSoundBuffers.CreateStoneShatter(),
                HalalSoundBuffers.CreateCauldronSteamHiss(),
                HalalSoundBuffers.CreateWoodenSignalKnock()
            };

            foreach (var buffer in allBuffers)
            {
                double sumSquares = 0.0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    sumSquares += buffer[i] * buffer[i];
                }
                double rms = Math.Sqrt(sumSquares / buffer.Length);
                Assert.True(rms > 0.005, $"Audio buffer RMS energy {rms} was too low (silence detected).");
            }
        }

        [Fact]
        public void Test_Pcm16Conversion_ExactByteLength_AndEdgeValues()
        {
            var empty = HalalSoundBuffers.ConvertTo16BitPcm(Array.Empty<float>());
            Assert.Empty(empty);

            float[] testSamples = new float[] { 0.0f, 1.0f, -1.0f, 0.5f, -0.5f };
            byte[] bytes = HalalSoundBuffers.ConvertTo16BitPcm(testSamples);

            Assert.Equal(testSamples.Length * 2, bytes.Length);

            // Verify sample 0 (0.0f)
            short pcm0 = (short)(bytes[0] | (bytes[1] << 8));
            Assert.Equal(0, pcm0);

            // Verify sample 1 (1.0f -> ~32767)
            short pcm1 = (short)(bytes[2] | (bytes[3] << 8));
            Assert.InRange(pcm1, 32700, 32767);

            // Verify sample 2 (-1.0f -> ~ -32768)
            short pcm2 = (short)(bytes[4] | (bytes[5] << 8));
            Assert.InRange(pcm2, -32768, -32700);
        }

        [Fact]
        public void Test_LoopCrossfades_AreContinuous()
        {
            var wind = HalalSoundBuffers.CreateMountainWindLoop(4.0f);
            var combatWind = HalalSoundBuffers.CreateTurbulentCombatWind(3.0f);

            // In seamless looping buffers, head sample (index 0) and tail sample (last index)
            // should not have an explosive jump across the loop seam.
            float windSeamDiff = MathF.Abs(wind[0] - wind[wind.Length - 1]);
            Assert.True(windSeamDiff < 0.35f, $"Mountain wind seam delta {windSeamDiff} was too abrupt.");

            float combatSeamDiff = MathF.Abs(combatWind[0] - combatWind[combatWind.Length - 1]);
            Assert.True(combatSeamDiff < 0.40f, $"Combat wind seam delta {combatSeamDiff} was too abrupt.");
        }
    }
}
