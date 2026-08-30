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

        public float Volume { get; private set; } = 0.62f;

        private void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            SetVolume(PlayerProfile.LoadSfxVolume());
        }

        public void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
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
    }
}
