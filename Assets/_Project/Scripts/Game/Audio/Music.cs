using System.Collections;
using UnityEngine;

namespace Contraption.Game
{
    // Background music from Resources/Audio/Music: a different looping track for each new machine, crossfaded,
    // on the Music volume channel.
    public static class Music
    {
        private const float Level = 0.6f, FadeTime = 2.5f;
        private static AudioSource[] _decks;
        private static AudioClip[] _tracks;
        private static int _active, _last = -1;
        private static float[] _fade = { 0f, 0f };
        private static Coroutine _crossfade;

        public static AudioClip Current => _decks != null && _decks[_active] != null ? _decks[_active].clip : null;

        public static void Next(MonoBehaviour host)
        {
            if (!Init()) return;
            var usable = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _tracks.Length; i++) if (!SoundReview.IsPurged(_tracks[i].name)) usable.Add(i);
            if (usable.Count > 1) usable.Remove(_last);
            if (usable.Count == 0) return;
            int pick = usable[Random.Range(0, usable.Count)];
            _last = pick;
            _active = 1 - _active;
            var deck = _decks[_active];
            deck.clip = _tracks[pick];
            deck.time = 0f;
            deck.Play();
            if (_crossfade != null) host.StopCoroutine(_crossfade);
            _crossfade = host.StartCoroutine(Crossfade());
        }

        // Unscaled time, so the pause menu doesn't freeze a fade.
        private static IEnumerator Crossfade()
        {
            while (_fade[_active] < 1f || _fade[1 - _active] > 0f)
            {
                float step = Time.unscaledDeltaTime / FadeTime;
                _fade[_active] = Mathf.Min(1f, _fade[_active] + step);
                _fade[1 - _active] = Mathf.Max(0f, _fade[1 - _active] - step);
                Apply();
                yield return null;
            }
            _decks[1 - _active].Stop();
            _crossfade = null;
        }

        private static void Apply()
        {
            for (int i = 0; i < 2; i++) _decks[i].volume = Level * SoundSettings.Music * _fade[i];
        }

        private static bool Init()
        {
            if (_decks != null && _decks[0] != null) return true;
            _tracks = Resources.LoadAll<AudioClip>("Audio/Music");
            var host = new GameObject("Music");
            Object.DontDestroyOnLoad(host);
            _decks = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                _decks[i] = src;
            }
            _fade = new[] { 0f, 0f };
            SoundSettings.Changed -= Apply;
            SoundSettings.Changed += Apply;
            return true;
        }
    }
}
