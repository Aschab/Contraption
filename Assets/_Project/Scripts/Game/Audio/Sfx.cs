using System.Collections.Generic;
using UnityEngine;

namespace Contraption.Game
{
    // Sound effects from any Resources/Audio/Sfx folder (asset packs under Assets/ThirdParty, or
    // ArtSource/Audio/synth_sfx.py for sounds no pack provides) and ambience loops from Resources/Audio/Ambience. One-shots come as variations "<name>_<i>": each play picks a random one
    // (never the same twice in a row) with a slight pitch wobble. Loops are a single "<name>" clip.
    // Volumes follow SoundSettings: one-shots use the Effects channel, loops the channel they were made with.
    public static class Sfx
    {
        private sealed class LoopEntry
        {
            public AudioSource Source;
            public float Base;
            public SoundChannel Channel;
        }

        private const int Voices = 24;
        private static readonly List<LoopEntry> _loops = new();
        private const float Spatial = 0.6f;   // mostly panning; the table is small, so no distance falloff
        private static Dictionary<string, AudioClip[]> _sets;
        private static readonly Dictionary<string, int> _last = new();
        private static readonly Dictionary<string, float> _lastTime = new();
        private static readonly HashSet<string> _missing = new();
        private static AudioSource[] _voices;
        private static int _next;

        public static void Play(string name, Vector3? at = null, float volume = 1f, float pitch = 1f)
        {
            if (!TryGet(name, out var set)) return;
            // Many displays can change in one move: one sound per name per few frames.
            float now = Time.unscaledTime;
            if (_lastTime.TryGetValue(name, out float t) && now - t < 0.04f) return;
            _lastTime[name] = now;

            int i = Random.Range(0, set.Length);
            if (set.Length > 1 && _last.TryGetValue(name, out int prev) && i == prev) i = (i + 1 + Random.Range(0, set.Length - 1)) % set.Length;
            _last[name] = i;

            var src = _voices[_next++ % Voices];
            src.transform.position = at ?? Vector3.zero;
            src.spatialBlend = at.HasValue ? Spatial : 0f;
            src.clip = set[i];
            src.volume = volume * SoundSettings.Effects;
            src.pitch = pitch * Random.Range(0.96f, 1.04f);
            src.Play();
        }

        public static void PlayDelayed(MonoBehaviour host, float delay, string name, Vector3? at = null, float volume = 1f)
        {
            if (host != null && host.isActiveAndEnabled) host.StartCoroutine(Delayed(delay, name, at, volume));
        }

        private static System.Collections.IEnumerator Delayed(float delay, string name, Vector3? at, float volume)
        {
            yield return new WaitForSeconds(delay);
            Play(name, at, volume);
        }

        // A looping source on `parent` (not playing yet).
        public static AudioSource Loop(string name, Transform parent, Vector3 localPos, float volume = 1f, SoundChannel channel = SoundChannel.Effects)
        {
            if (!TryGet(name, out var set)) return null;
            var go = new GameObject("Loop " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var src = Configure(go.AddComponent<AudioSource>());
            src.clip = set[0];
            src.loop = true;
            src.spatialBlend = Spatial;
            _loops.Add(new LoopEntry { Source = src, Base = volume, Channel = channel });
            src.volume = volume * SoundSettings.Gain(channel);
            return src;
        }

        // Changes a loop's own volume; the channel volume still applies on top.
        public static void SetVolume(AudioSource src, float volume)
        {
            foreach (var e in _loops)
                if (e.Source == src) { e.Base = volume; src.volume = volume * SoundSettings.Gain(e.Channel); return; }
        }

        private static void ApplyVolumes()
        {
            AudioListener.volume = SoundSettings.Master;
            _loops.RemoveAll(e => e.Source == null);
            foreach (var e in _loops) e.Source.volume = e.Base * SoundSettings.Gain(e.Channel);
        }

        // Loops playing right now ("name · channel · volume"), for tracking down a sound.
        public static List<string> Playing()
        {
            var list = new List<string>();
            _loops.RemoveAll(e => e.Source == null);
            foreach (var e in _loops)
                if (e.Source.isPlaying && e.Source.isActiveAndEnabled)
                    list.Add($"{e.Source.clip.name} · {e.Channel} · {e.Source.volume:0.00}");
            list.Sort();
            return list;
        }

        // A non-positional loop that plays for the whole scene (room tone).
        public static AudioSource Ambient(string name, float volume)
        {
            Init();
            var src = Loop(name, _voices[0].transform.parent, Vector3.zero, volume, SoundChannel.Ambience);
            if (src == null) return null;
            src.spatialBlend = 0f;
            src.time = Random.Range(0f, src.clip.length);
            src.Play();
            return src;
        }

        private static bool TryGet(string name, out AudioClip[] set)
        {
            Init();
            if (_sets.TryGetValue(name, out set)) return true;
            if (_missing.Add(name)) Debug.LogWarning("Missing sound " + name);
            return false;
        }

        private static void Init()
        {
            if (_voices != null && _voices[0] != null) return;
            if (Object.FindAnyObjectByType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
            AudioListener.volume = SoundSettings.Master;
            SoundSettings.Changed -= ApplyVolumes;
            SoundSettings.Changed += ApplyVolumes;
            if (_sets == null)
            {
                var groups = new Dictionary<string, List<AudioClip>>();
                foreach (var clip in Resources.LoadAll<AudioClip>("Audio"))
                {
                    string n = clip.name;
                    int u = n.LastIndexOf('_');
                    if (u > 0 && int.TryParse(n[(u + 1)..], out _)) n = n[..u];
                    if (!groups.TryGetValue(n, out var list)) groups[n] = list = new List<AudioClip>();
                    list.Add(clip);
                }
                _sets = new Dictionary<string, AudioClip[]>();
                foreach (var kv in groups)
                {
                    kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    _sets[kv.Key] = kv.Value.ToArray();
                }
            }
            var host = new GameObject("Sfx");
            Object.DontDestroyOnLoad(host);
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) _voices[i] = Configure(new GameObject("Voice").AddComponent<AudioSource>(), host.transform);
        }

        private static AudioSource Configure(AudioSource src, Transform parent = null)
        {
            if (parent != null) src.transform.SetParent(parent, false);
            src.playOnAwake = false;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 20f;
            src.maxDistance = 60f;
            src.dopplerLevel = 0f;
            return src;
        }
    }
}
