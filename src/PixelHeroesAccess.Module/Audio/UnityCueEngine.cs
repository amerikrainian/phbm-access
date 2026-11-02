using System;
using System.Collections.Generic;
using System.IO;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Audio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PixelHeroesAccess.Module.Audio
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The cue engine over Unity's audio, which this game plays everything through (its
    /// <c>AudioControl</c>: plain AudioSources, no mixer): a hidden object of ours holding a few
    /// AudioSources, silent while the game's sound-effects switch is off (<c>Options.pSFX</c>), one
    /// clip per cue decoded from assets/audio on first play and kept. Our own sources, not the game's pool, so a
    /// cue's pan and pitch never stay behind on a source the game plays its sounds through. An
    /// AudioSource cannot amplify: a volume above 1 plays at 1. A missing or unreadable file logs once
    /// and stays silent. Released on module teardown, clips and object both: a reload builds its own.
    /// </summary>
    internal sealed class UnityCueEngine : IAudioEngine, IDisposable
    {
        private const int Voices = 4;

        private readonly string _assetRoot;
        private readonly Dictionary<AudioCue, AudioClip> _clips = new Dictionary<AudioCue, AudioClip>();
        private readonly HashSet<AudioCue> _warned = new HashSet<AudioCue>();
        private GameObject _host;
        private AudioSource[] _sources;
        private int _next;
        private bool _failed;

        public UnityCueEngine(string assetRoot) => _assetRoot = assetRoot;

        public bool Available => !_failed;

        private bool EnsureStarted()
        {
            if (_sources != null) return true;
            if (_failed) return false;
            try
            {
                _host = new GameObject("PixelHeroesAccess.Cues") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(_host);
                _sources = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    var source = _host.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    _sources[i] = source;
                }
                CoreLog.Info("audio: cue sources opened");
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                CoreLog.Warning("audio: Unity audio unavailable; cues disabled: " + e.Message);
                return false;
            }
        }

        public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f)
        {
            if (volume <= 0f || !EnsureStarted()) return;
            // The game's own sound-effects switch (its options popup) silences the cues too.
            if (!Options.pSFX) return;
            try
            {
                if (!TryLoad(cue, out var clip)) return;
                var source = NextSource();
                source.Stop();
                source.clip = clip;
                source.volume = Math.Min(1f, volume);
                source.panStereo = Math.Max(-1f, Math.Min(1f, pan));
                source.pitch = pitch;
                source.Play();
            }
            catch (Exception e)
            {
                CoreLog.Warning("audio: cue " + cue + " failed: " + e.Message);
            }
        }

        // The first idle source, else the one after the last used (the oldest sound gives way).
        private AudioSource NextSource()
        {
            for (int i = 0; i < Voices; i++)
            {
                var source = _sources[(_next + i) % Voices];
                if (!source.isPlaying) { _next = (_next + i + 1) % Voices; return source; }
            }
            var oldest = _sources[_next];
            _next = (_next + 1) % Voices;
            return oldest;
        }

        private bool TryLoad(AudioCue cue, out AudioClip clip)
        {
            if (_clips.TryGetValue(cue, out clip)) return true;
            string path = Path.Combine(_assetRoot, AudioCues.RelativePath(cue));
            if (!File.Exists(path))
            {
                if (_warned.Add(cue)) CoreLog.Warning("audio: no file for " + cue + " at " + path);
                return false;
            }
            WavFile wav;
            try { wav = WavFile.Parse(File.ReadAllBytes(path)); }
            catch (Exception e)
            {
                if (_warned.Add(cue)) CoreLog.Warning("audio: cannot read " + path + ": " + e.Message);
                return false;
            }
            clip = AudioClip.Create("PixelHeroesAccess." + AudioCues.FileName(cue), Math.Max(1, wav.Frames), wav.Channels, wav.SampleRate, false);
            clip.SetData(wav.Samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            _clips[cue] = clip;
            return true;
        }

        public void Dispose()
        {
            try
            {
                if (_host != null) Object.Destroy(_host);
                foreach (var clip in _clips.Values) if (clip != null) Object.Destroy(clip);
            }
            catch (Exception e) { CoreLog.Warning("audio: release failed: " + e.Message); }
            _clips.Clear();
            _host = null;
            _sources = null;
        }
    }
}
