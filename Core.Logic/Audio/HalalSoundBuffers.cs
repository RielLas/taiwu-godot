using System;

namespace TaiwuGu.Audio
{
    /// <summary>
    /// Lightweight Xorshift32 Pseudo-Random Number Generator.
    /// Zero allocation, pure C# inlined execution.
    /// Generates uniform white noise without OS/library overhead.
    /// </summary>
    public struct FastNoise
    {
        private uint _state;

        public FastNoise(uint seed = 2463534242)
        {
            _state = (seed == 0) ? 1u : seed;
        }

        public float NextRange(float min, float max)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            float normalized = (_state & 0x00FFFFFF) / 16777216.0f; // [0.0, 1.0)
            return min + normalized * (max - min);
        }

        public float NextSigned() => NextRange(-1.0f, 1.0f);

        public float NextValue() => NextRange(0.0f, 1.0f);
    }

    /// <summary>
    /// 100% Halal Acoustic Foley & Nature Ambience DSP Buffer Generator.
    /// Strictly avoids all musical instruments (strings, flutes, bells, singing bowls).
    /// Synthesizes pure organic nature wind, gravel footsteps, bamboo friction, blade slices,
    /// and alchemical fire/stone physics directly into memory (0 MB disk bloat).
    /// Pure C# POCO layer with zero engine dependencies.
    /// </summary>
    public static class HalalSoundBuffers
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Converts normalized float samples [-1.0f, 1.0f] into 16-bit little-endian PCM byte array.
        /// </summary>
        public static byte[] ConvertTo16BitPcm(float[] samples)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<byte>();

            byte[] pcmBytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short pcm = (short)Math.Clamp((int)(samples[i] * 32767f), -32768, 32767);
                pcmBytes[i * 2] = (byte)(pcm & 0xFF);
                pcmBytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }
            return pcmBytes;
        }

        /// <summary>
        /// Creates a seamless 4-second looping mountain wind ambience.
        /// Low-pass filtered brownian noise with gentle atmospheric wind gusts.
        /// </summary>
        public static float[] CreateMountainWindLoop(float duration = 4.0f)
        {
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(1001);

            float lastValue = 0f;
            float twoPi = MathF.PI * 2f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // Slow atmospheric gust modulation (0.25 Hz & 0.41 Hz)
                float gust = 0.55f + 0.35f * MathF.Sin(twoPi * 0.25f * t) + 0.10f * MathF.Sin(twoPi * 0.41f * t);

                // Brownian noise integration (soft low-pass noise)
                float white = rng.NextSigned();
                lastValue = (lastValue + (0.04f * white)) / 1.04f;

                // Subtle high-altitude air whisper
                float whisper = rng.NextRange(-0.02f, 0.02f);

                samples[i] = Math.Clamp((lastValue * 3.2f + whisper) * gust * 0.45f, -0.95f, 0.95f);
            }

            // Crossfade ends for 100% seamless loop
            int fadeSamples = (int)MathF.Round(SampleRate * 0.35f);
            for (int i = 0; i < fadeSamples; i++)
            {
                float blend = (float)i / fadeSamples;
                int tailIdx = totalSamples - fadeSamples + i;
                samples[i] = (1f - blend) * samples[tailIdx] + blend * samples[i];
            }

            return samples;
        }

        /// <summary>
        /// Creates a 3-second turbulent combat wind loop with deeper low-end atmospheric pressure.
        /// </summary>
        public static float[] CreateTurbulentCombatWind(float duration = 3.0f)
        {
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(2002);

            float lastValue = 0f;
            float twoPi = MathF.PI * 2f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // Restless, agitated wind speed oscillation
                float gust = 0.65f + 0.25f * MathF.Sin(twoPi * 0.6f * t) + 0.10f * MathF.Cos(twoPi * 1.1f * t);

                // Sub-bass atmospheric pressure rumble (42 Hz)
                float subPressure = MathF.Sin(twoPi * 42f * t) * 0.12f;

                float white = rng.NextSigned();
                lastValue = (lastValue + (0.06f * white)) / 1.06f;

                samples[i] = Math.Clamp((lastValue * 2.8f + subPressure) * gust * 0.55f, -0.95f, 0.95f);
            }

            int fadeSamples = (int)MathF.Round(SampleRate * 0.3f);
            for (int i = 0; i < fadeSamples; i++)
            {
                float blend = (float)i / fadeSamples;
                int tailIdx = totalSamples - fadeSamples + i;
                samples[i] = (1f - blend) * samples[tailIdx] + blend * samples[i];
            }

            return samples;
        }

        /// <summary>
        /// Muffled acoustic gravel / stone footstep for tile movement.
        /// </summary>
        public static float[] CreateTileFootstep()
        {
            float duration = 0.09f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(3003);

            float lastNoise = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                // Fast attack, exponential decay
                float env = MathF.Exp(-progress * 18f);

                // Low thud (75 Hz) + muffled noise
                float thud = MathF.Sin(2f * MathF.PI * 75f * t) * 0.4f;
                float white = rng.NextRange(-0.35f, 0.35f);
                lastNoise = (lastNoise + white * 0.2f) * 0.85f;

                samples[i] = (thud + lastNoise) * env * 0.7f;
            }

            return samples;
        }

        /// <summary>
        /// Crisp acoustic parchment / bamboo slip friction for opening menus and ledgers.
        /// </summary>
        public static float[] CreateParchmentRustle()
        {
            float duration = 0.16f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(4004);

            float filterState = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                // Dual-hump envelope simulating sliding strips
                float env = MathF.Sin(progress * MathF.PI) * (1.0f + 0.35f * MathF.Sin(progress * MathF.PI * 3f));

                // Bandpassed textured friction
                float noise = rng.NextSigned();
                filterState += (noise - filterState) * 0.32f;
                float bandpassed = noise - filterState;

                samples[i] = bandpassed * env * 0.42f;
            }

            return samples;
        }

        /// <summary>
        /// High-velocity aerodynamic blade slice through air.
        /// </summary>
        public static float[] CreateBladeSlash()
        {
            float duration = 0.18f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(5005);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                // Sharp whip-crack envelope
                float env = MathF.Pow(MathF.Sin(progress * MathF.PI), 1.8f);

                // Sweeping center frequency for air shear (3200 Hz down to 600 Hz)
                float sweepFreq = 3200f + (600f - 3200f) * progress;
                float carrier = MathF.Sin(2f * MathF.PI * sweepFreq * t);
                float hiss = rng.NextRange(-0.4f, 0.4f);

                samples[i] = (carrier * 0.35f + hiss * 0.65f) * env * 0.85f;
            }

            return samples;
        }

        /// <summary>
        /// Heavy kinetic martial strike impact with sub-bass weight.
        /// </summary>
        public static float[] CreateBluntImpact()
        {
            float duration = 0.22f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(6006);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                float env = MathF.Exp(-progress * 14f);

                // Pitch-diving bass body (130 Hz down to 38 Hz)
                float freq = 130f + (38f - 130f) * MathF.Pow(progress, 0.5f);
                float subBass = MathF.Sin(2f * MathF.PI * freq * t);

                // Initial impact crack transient (first 15ms)
                float crack = (progress < 0.1f) ? rng.NextRange(-0.8f, 0.8f) * (1f - progress * 10f) : 0f;

                samples[i] = Math.Clamp((subBass * 0.75f + crack * 0.45f) * env, -0.95f, 0.95f);
            }

            return samples;
        }

        /// <summary>
        /// Swift air displacement whoosh for combat dash and evasive maneuvers.
        /// </summary>
        public static float[] CreateDashWhoosh()
        {
            float duration = 0.25f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(7007);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                // Smooth bell envelope
                float env = MathF.Sin(progress * MathF.PI);

                // Low-mid rushing air (420 Hz swept down to 140 Hz)
                float freq = 420f + (140f - 420f) * progress;
                float airTone = MathF.Sin(2f * MathF.PI * freq * t) * 0.3f;
                float noise = rng.NextRange(-0.5f, 0.5f);

                samples[i] = (airTone + noise * 0.7f) * env * 0.75f;
            }

            return samples;
        }

        /// <summary>
        /// Resonant roaring flame surge for Cauldron Pulse Heat (+12°C).
        /// </summary>
        public static float[] CreateCauldronFireBurst()
        {
            float duration = 0.38f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(8008);

            float filter = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                float env = MathF.Sin(progress * MathF.PI);

                // Low flame rumble (65 Hz)
                float rumble = MathF.Sin(2f * MathF.PI * 65f * t) * 0.4f;

                // Fire crackle micro-bursts
                float white = rng.NextSigned();
                filter += (white - filter) * 0.15f;

                samples[i] = (rumble + filter * 0.6f) * env * 0.8f;
            }

            return samples;
        }

        /// <summary>
        /// Crystalline stone fracture and crushing noise for Primeval Stone Crushing.
        /// </summary>
        public static float[] CreateStoneShatter()
        {
            float duration = 0.28f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(9009);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                float env = MathF.Exp(-progress * 12f);

                // Multi-fracture grit
                float noise = rng.NextSigned();
                float crackle = (rng.NextValue() > 0.88f) ? rng.NextRange(-0.9f, 0.9f) : 0f;

                // Mid clink (1200 Hz fast decaying)
                float clink = MathF.Sin(2f * MathF.PI * 1200f * t) * MathF.Exp(-progress * 25f) * 0.35f;

                samples[i] = Math.Clamp((noise * 0.45f + crackle * 0.4f + clink) * env, -0.95f, 0.95f);
            }

            return samples;
        }

        /// <summary>
        /// Escaping high-pressure steam hiss for Cauldron Will Suppression.
        /// </summary>
        public static float[] CreateCauldronSteamHiss()
        {
            float duration = 0.32f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(10010);

            float highpassFilter = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                float env = MathF.Sin(progress * MathF.PI * 0.5f) * MathF.Exp(-progress * 4f);

                float white = rng.NextSigned();
                highpassFilter += (white - highpassFilter) * 0.6f;
                float hiss = white - highpassFilter;

                samples[i] = hiss * env * 0.65f;
            }

            return samples;
        }

        /// <summary>
        /// Traditional Chinese hardwood clapper (Bangzi / 夜梆) single tap.
        /// Pure functional wooden percussion used for ancient timekeeping / night-watch signals.
        /// </summary>
        public static float[] CreateWoodenSignalKnock()
        {
            float duration = 0.12f;
            int totalSamples = (int)MathF.Round(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new FastNoise(11011);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                float env = MathF.Exp(-progress * 32f);

                // Dual resonant hardwood frequencies (780 Hz & 1560 Hz harmonic)
                float woodFundamental = MathF.Sin(2f * MathF.PI * 780f * t) * 0.65f;
                float woodOvertone = MathF.Sin(2f * MathF.PI * 1560f * t) * 0.25f;
                float tapTransient = (progress < 0.05f) ? rng.NextRange(-0.3f, 0.3f) : 0f;

                samples[i] = (woodFundamental + woodOvertone + tapTransient) * env * 0.9f;
            }

            return samples;
        }
    }
}
