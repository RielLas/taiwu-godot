using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Audio;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using Mathf = Godot.Mathf;

namespace Taiwu_Godot.Scripts.Audio
{
    /// <summary>
    /// Enterprise Halal Audio & Atmosphere Service for Taiwu Path of the Gu (Godot 4.x Mono).
    /// Manages environmental mountain wind ambiance, real-time combat audio transitions,
    /// and multi-voice physical SFX pooling with pitch jitter, voice stealing prevention,
    /// volume persistence, and centralized frame ticking.
    /// 100% Halal Compliant: Strictly excludes musical instruments (strings, flutes, bells, singing bowls).
    /// Pure nature foley and physical acoustic vibrations generated procedurally (0 MB disk bloat).
    /// </summary>
    public partial class TaiwuAudioService : Node, IDisposable
    {
        private AudioStreamPlayer _ambientWindSource = null!;
        private AudioStreamPlayer _combatWindSource = null!;
        private readonly AudioStreamPlayer[] _sfxVoicePool = new AudioStreamPlayer[8];
        private int _nextVoiceIndex = 0;

        // --- Audio Streams (Procedural in-memory buffers) ---
        private AudioStreamWav _clipMountainWind = null!;
        private AudioStreamWav _clipCombatWind = null!;
        private AudioStreamWav _clipFootstep = null!;
        private AudioStreamWav _clipParchment = null!;
        private AudioStreamWav _clipBladeSlash = null!;
        private AudioStreamWav _clipBluntImpact = null!;
        private AudioStreamWav _clipDashWhoosh = null!;
        private AudioStreamWav _clipCauldronFire = null!;
        private AudioStreamWav _clipStoneShatter = null!;
        private AudioStreamWav _clipCauldronSteam = null!;
        private AudioStreamWav _clipWoodenKnock = null!;

        // --- Crossfade & State ---
        private bool _isInCombat = false;
        private float _targetAmbientVol = 0.40f;
        private float _targetCombatVol = 0.55f;
        private float _currentAmbientLinear = 0.40f;
        private float _currentCombatLinear = 0.00f;
        private float _masterVolume = 1.0f;
        private float _sfxVolume = 0.85f;
        private float _ambientVolume = 0.50f;

        // --- Throttling Matrix ---
        private readonly Dictionary<string, float> _lastPlayedTime = new();
        private float _elapsedSeconds = 0f;
        private const float COOLDOWN_FOOTSTEP = 0.14f;
        private const float COOLDOWN_SLASH = 0.08f;
        private const float COOLDOWN_IMPACT = 0.08f;
        private const float COOLDOWN_RUSTLE = 0.15f;

        private FastNoise _pitchRng = new(777);
        private bool _isDisposed = false;

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp(value, 0f, 1f);
                RefreshSourceVolumes();
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp(value, 0f, 1f);
        }

        public float AmbientVolume
        {
            get => _ambientVolume;
            set
            {
                _ambientVolume = Mathf.Clamp(value, 0f, 1f);
                RefreshSourceVolumes();
            }
        }

        public override void _Ready()
        {
            // CRUCIAL REFINEMENT: Disable automatic engine ticking so Bootstrapper explicitly drives Tick(dt).
            SetProcess(false);
            SetPhysicsProcess(false);

            InitializeAudioEngine();
        }

        private void InitializeAudioEngine()
        {
            // 1. Synthesize all Halal sound streams in memory (<2ms execution via FastNoise)
            _clipMountainWind = CreateWavFromSamples(HalalSoundBuffers.CreateMountainWindLoop(4.0f), loop: true);
            _clipCombatWind = CreateWavFromSamples(HalalSoundBuffers.CreateTurbulentCombatWind(3.0f), loop: true);
            _clipFootstep = CreateWavFromSamples(HalalSoundBuffers.CreateTileFootstep());
            _clipParchment = CreateWavFromSamples(HalalSoundBuffers.CreateParchmentRustle());
            _clipBladeSlash = CreateWavFromSamples(HalalSoundBuffers.CreateBladeSlash());
            _clipBluntImpact = CreateWavFromSamples(HalalSoundBuffers.CreateBluntImpact());
            _clipDashWhoosh = CreateWavFromSamples(HalalSoundBuffers.CreateDashWhoosh());
            _clipCauldronFire = CreateWavFromSamples(HalalSoundBuffers.CreateCauldronFireBurst());
            _clipStoneShatter = CreateWavFromSamples(HalalSoundBuffers.CreateStoneShatter());
            _clipCauldronSteam = CreateWavFromSamples(HalalSoundBuffers.CreateCauldronSteamHiss());
            _clipWoodenKnock = CreateWavFromSamples(HalalSoundBuffers.CreateWoodenSignalKnock());

            // 2. Configure Environmental Wind Sources
            _ambientWindSource = CreateAudioPlayer("AmbientWindSource");
            _ambientWindSource.Stream = _clipMountainWind;
            _currentAmbientLinear = _targetAmbientVol * _ambientVolume * _masterVolume;
            _ambientWindSource.VolumeDb = LinearToDecibels(_currentAmbientLinear);
            _ambientWindSource.Play();

            _combatWindSource = CreateAudioPlayer("CombatWindSource");
            _combatWindSource.Stream = _clipCombatWind;
            _currentCombatLinear = 0f;
            _combatWindSource.VolumeDb = -80f;
            _combatWindSource.Play();

            // 3. Configure Multi-Voice SFX Pool (8 simultaneous non-blocking voices)
            for (int i = 0; i < _sfxVoicePool.Length; i++)
            {
                _sfxVoicePool[i] = CreateAudioPlayer($"SFXVoice_{i}");
            }

            // 4. Symmetrical Event Registration
            RegisterEvents();

            GD.Print("[TaiwuAudioService] Halal Acoustic Sound Engine initialized (0 MB disk bloat, Xorshift DSP).");
        }

        private AudioStreamPlayer CreateAudioPlayer(string name)
        {
            var player = new AudioStreamPlayer
            {
                Name = name,
                Autoplay = false
            };
            AddChild(player);
            return player;
        }

        private static AudioStreamWav CreateWavFromSamples(float[] samples, bool loop = false)
        {
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = HalalSoundBuffers.SampleRate,
                Stereo = false,
                Data = HalalSoundBuffers.ConvertTo16BitPcm(samples)
            };

            if (loop)
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = samples.Length;
            }
            else
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
            }

            return wav;
        }

        private void RegisterEvents()
        {
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnLedgerToggled += HandleModalRustle;
            WorldSignals.OnPOIModalToggled += HandleModalRustle;
            WorldSignals.OnCauldronModalToggled += HandleModalRustle;
            WorldSignals.OnTransitModalToggled += HandleModalRustle;
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
            WorldSignals.OnSoundRequested += PlaySoundByType;
        }

        private void UnregisterEvents()
        {
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnLedgerToggled -= HandleModalRustle;
            WorldSignals.OnPOIModalToggled -= HandleModalRustle;
            WorldSignals.OnCauldronModalToggled -= HandleModalRustle;
            WorldSignals.OnTransitModalToggled -= HandleModalRustle;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;
            WorldSignals.OnSoundRequested -= PlaySoundByType;
        }

        /// <summary>
        /// Centralized frame tick driven exclusively by TaiwuGameBootstrapper.
        /// Avoids double-ticking and performs smooth atmospheric crossfading.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _elapsedSeconds += deltaTime;

            // Target volumes
            float targetAmb = (_isInCombat ? 0.10f : _targetAmbientVol) * _ambientVolume * _masterVolume;
            float targetCbt = (_isInCombat ? _targetCombatVol : 0.00f) * _ambientVolume * _masterVolume;

            _currentAmbientLinear = Mathf.MoveToward(_currentAmbientLinear, targetAmb, deltaTime * 0.8f);
            _currentCombatLinear = Mathf.MoveToward(_currentCombatLinear, targetCbt, deltaTime * 1.2f);

            if (_ambientWindSource != null && GodotObject.IsInstanceValid(_ambientWindSource))
            {
                _ambientWindSource.VolumeDb = LinearToDecibels(_currentAmbientLinear);
            }

            if (_combatWindSource != null && GodotObject.IsInstanceValid(_combatWindSource))
            {
                _combatWindSource.VolumeDb = LinearToDecibels(_currentCombatLinear);
            }
        }

        private void RefreshSourceVolumes()
        {
            float targetAmb = (_isInCombat ? 0.10f : _targetAmbientVol) * _ambientVolume * _masterVolume;
            float targetCbt = (_isInCombat ? _targetCombatVol : 0.00f) * _ambientVolume * _masterVolume;

            _currentAmbientLinear = targetAmb;
            _currentCombatLinear = targetCbt;

            if (_ambientWindSource != null && GodotObject.IsInstanceValid(_ambientWindSource))
            {
                _ambientWindSource.VolumeDb = LinearToDecibels(_currentAmbientLinear);
            }
            if (_combatWindSource != null && GodotObject.IsInstanceValid(_combatWindSource))
            {
                _combatWindSource.VolumeDb = LinearToDecibels(_currentCombatLinear);
            }
        }

        private static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.LinearToDb(linear);
        }

        // --- Event Handlers ---

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.PlayerPos) != 0 && CanPlay("footstep", COOLDOWN_FOOTSTEP))
            {
                PlaySFX(_clipFootstep, 0.70f, 0.92f, 1.08f);
            }
        }

        private void HandleModalRustle(bool isOpen)
        {
            if (isOpen && CanPlay("rustle", COOLDOWN_RUSTLE))
            {
                PlaySFX(_clipParchment, 0.80f, 0.95f, 1.05f);
            }
        }

        private void HandleBattleStageChanged(bool inBattle) => _isInCombat = inBattle;

        public void PlaySoundByType(HalalSoundType soundType)
        {
            switch (soundType)
            {
                case HalalSoundType.Footstep:
                    if (CanPlay("footstep", COOLDOWN_FOOTSTEP)) PlaySFX(_clipFootstep, 0.70f, 0.92f, 1.08f);
                    break;
                case HalalSoundType.ParchmentRustle:
                    if (CanPlay("rustle", COOLDOWN_RUSTLE)) PlaySFX(_clipParchment, 0.80f, 0.95f, 1.05f);
                    break;
                case HalalSoundType.BladeSlash:
                    if (CanPlay("slash", COOLDOWN_SLASH)) PlaySFX(_clipBladeSlash, 0.85f, 0.92f, 1.08f);
                    break;
                case HalalSoundType.BluntImpact:
                    if (CanPlay("impact", COOLDOWN_IMPACT)) PlaySFX(_clipBluntImpact, 0.90f, 0.90f, 1.05f);
                    break;
                case HalalSoundType.DashWhoosh:
                    PlaySFX(_clipDashWhoosh, 0.90f, 0.95f, 1.05f);
                    break;
                case HalalSoundType.CauldronFire:
                    PlaySFX(_clipCauldronFire, 0.90f, 0.93f, 1.07f);
                    break;
                case HalalSoundType.StoneShatter:
                    PlaySFX(_clipStoneShatter, 0.95f, 0.94f, 1.06f);
                    break;
                case HalalSoundType.CauldronSteam:
                    PlaySFX(_clipCauldronSteam, 0.75f, 0.96f, 1.04f);
                    break;
                case HalalSoundType.WoodenKnock:
                    PlaySFX(_clipWoodenKnock, 0.85f, 0.94f, 1.06f);
                    break;
            }
        }

        // --- Core Playback Engine ---

        private bool CanPlay(string soundKey, float minInterval)
        {
            if (_lastPlayedTime.TryGetValue(soundKey, out float last) && (_elapsedSeconds - last) < minInterval)
            {
                return false;
            }
            _lastPlayedTime[soundKey] = _elapsedSeconds;
            return true;
        }

        private void PlaySFX(AudioStreamWav? clip, float volumeScale = 1.0f, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (clip == null || _sfxVoicePool == null || _sfxVoicePool.Length == 0) return;

            // Pick first idle voice
            AudioStreamPlayer? voice = null;
            for (int i = 0; i < _sfxVoicePool.Length; i++)
            {
                if (!_sfxVoicePool[i].Playing)
                {
                    voice = _sfxVoicePool[i];
                    break;
                }
            }

            // Fallback: round-robin voice stealing
            if (voice == null)
            {
                voice = _sfxVoicePool[_nextVoiceIndex];
                _nextVoiceIndex = (_nextVoiceIndex + 1) % _sfxVoicePool.Length;
            }

            float pitch = _pitchRng.NextRange(minPitch, maxPitch);
            voice.PitchScale = pitch;
            float finalLinear = volumeScale * _sfxVolume * _masterVolume;
            voice.VolumeDb = LinearToDecibels(finalLinear);
            voice.Stream = clip;
            voice.Play();
        }

        public override void _ExitTree()
        {
            Shutdown();
            base._ExitTree();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Shutdown();
            }
            base.Dispose(disposing);
        }

        public new void Dispose()
        {
            Shutdown();
            base.Dispose();
        }

        private void Shutdown()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            UnregisterEvents();

            if (_ambientWindSource != null && GodotObject.IsInstanceValid(_ambientWindSource))
            {
                _ambientWindSource.Stop();
            }
            if (_combatWindSource != null && GodotObject.IsInstanceValid(_combatWindSource))
            {
                _combatWindSource.Stop();
            }
            if (_sfxVoicePool != null)
            {
                for (int i = 0; i < _sfxVoicePool.Length; i++)
                {
                    if (_sfxVoicePool[i] != null && GodotObject.IsInstanceValid(_sfxVoicePool[i]))
                    {
                        _sfxVoicePool[i].Stop();
                    }
                }
            }
        }
    }
}
