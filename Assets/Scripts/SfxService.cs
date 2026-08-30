using System.Collections.Generic;
using UnityEngine;

namespace VerdantBlade
{
    public enum SoundCue
    {
        Attack,
        Dash,
        PlayerHit,
        EnemyHit,
        EnemyDefeat,
        EnemyShot,
        CollectShard,
        CollectHeart,
        PotBreak,
        GateOpen,
        Victory,
        MenuConfirm
    }

    public sealed class SfxService : MonoBehaviour
    {
        public static SfxService Instance { get; private set; }

        private readonly Dictionary<SoundCue, AudioClip> clips = new Dictionary<SoundCue, AudioClip>();
        private AudioSource source;
        private AudioSource musicSource;
        private AudioClip musicClip;

        public float Volume { get; private set; } = 0.62f;
        public float MusicVolume { get; private set; } = 0.38f;

        private void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.loop = true;
            SetVolume(PlayerProfile.LoadSfxVolume());
            SetMusicVolume(PlayerProfile.LoadMusicVolume());
            musicClip = BuildMusicLoop();
            musicSource.clip = musicClip;
            musicSource.Play();
        }

        public void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            if (musicSource != null)
            {
                musicSource.volume = MusicVolume;
            }
        }

        public void SetMusicPaused(bool paused)
        {
            if (musicSource == null)
            {
                return;
            }
            if (paused)
            {
                musicSource.Pause();
            }
            else
            {
                musicSource.UnPause();
            }
        }

        public void Play(SoundCue cue)
        {
            if (Volume <= 0f)
            {
                return;
            }

            if (!clips.TryGetValue(cue, out var clip))
            {
                clip = CreateClip(cue);
                clips.Add(cue, clip);
            }
            source.PlayOneShot(clip, Volume);
        }

        private static AudioClip CreateClip(SoundCue cue)
        {
            var duration = 0.11f;
            var startFrequency = 330f;
            var endFrequency = 480f;
            var volume = 0.28f;
            switch (cue)
            {
                case SoundCue.Attack: startFrequency = 240f; endFrequency = 740f; duration = 0.1f; break;
                case SoundCue.Dash: startFrequency = 180f; endFrequency = 430f; duration = 0.13f; break;
                case SoundCue.PlayerHit: startFrequency = 210f; endFrequency = 85f; duration = 0.18f; break;
                case SoundCue.EnemyHit: startFrequency = 280f; endFrequency = 190f; duration = 0.08f; break;
                case SoundCue.EnemyDefeat: startFrequency = 420f; endFrequency = 120f; duration = 0.25f; break;
                case SoundCue.EnemyShot: startFrequency = 470f; endFrequency = 650f; duration = 0.12f; break;
                case SoundCue.CollectShard: startFrequency = 590f; endFrequency = 960f; duration = 0.18f; break;
                case SoundCue.CollectHeart: startFrequency = 380f; endFrequency = 610f; duration = 0.2f; break;
                case SoundCue.PotBreak: startFrequency = 160f; endFrequency = 90f; duration = 0.13f; break;
                case SoundCue.GateOpen: startFrequency = 380f; endFrequency = 760f; duration = 0.35f; break;
                case SoundCue.Victory: startFrequency = 520f; endFrequency = 1040f; duration = 0.45f; break;
                case SoundCue.MenuConfirm: startFrequency = 480f; endFrequency = 680f; duration = 0.07f; volume = 0.2f; break;
            }
            return BuildTone(cue.ToString(), startFrequency, endFrequency, duration, volume);
        }

        private static AudioClip BuildTone(string name, float startFrequency, float endFrequency, float duration, float amplitude)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var phase = 0f;
            for (var index = 0; index < sampleCount; index++)
            {
                var progress = (float)index / sampleCount;
                var frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += frequency * Mathf.PI * 2f / sampleRate;
                var envelope = Mathf.Sin(progress * Mathf.PI);
                samples[index] = Mathf.Sin(phase) * envelope * amplitude;
            }
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildMusicLoop()
        {
            const int sampleRate = 22050;
            const float duration = 12f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var chordRoots = new[] { 146.83f, 174.61f, 196f, 164.81f };
            for (var index = 0; index < sampleCount; index++)
            {
                var time = (float)index / sampleRate;
                var chord = Mathf.FloorToInt(time / 3f) % chordRoots.Length;
                var root = chordRoots[chord];
                var phase = (time % 3f) / 3f;
                var envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(phase * 7f, (1f - phase) * 7f));
                var pad = Mathf.Sin(time * root * Mathf.PI * 2f) * 0.035f;
                pad += Mathf.Sin(time * root * 1.5f * Mathf.PI * 2f) * 0.022f;
                pad += Mathf.Sin(time * root * 2f * Mathf.PI * 2f) * 0.012f;
                var bell = Mathf.Sin(time * (root * 4f) * Mathf.PI * 2f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * Mathf.PI / 1.5f)), 12f) * 0.018f;
                samples[index] = (pad + bell) * envelope;
            }
            var clip = AudioClip.Create("Verdant Blade Ambient Loop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
