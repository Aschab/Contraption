using System;
using UnityEngine;

namespace Contraption.Game
{
    // Player volume settings (0..1), saved in PlayerPrefs. Master scales everything through the AudioListener.
    public static class SoundSettings
    {
        public static event Action Changed;

        public static float Master { get => Get("master"); set => Set("master", value); }
        public static float Effects { get => Get("effects"); set => Set("effects", value); }
        public static float Ambience { get => Get("ambience"); set => Set("ambience", value); }
        public static float Music { get => Get("music"); set => Set("music", value); }

        public static float Gain(SoundChannel c) => c switch
        {
            SoundChannel.Ambience => Ambience,
            SoundChannel.Music => Music,
            _ => Effects,
        };

        private static float Get(string key) => PlayerPrefs.GetFloat("contraption.volume." + key, 1f);

        private static void Set(string key, float value)
        {
            value = Mathf.Clamp01(Mathf.Round(value * 100f) / 100f);
            if (Mathf.Approximately(Get(key), value)) return;
            PlayerPrefs.SetFloat("contraption.volume." + key, value);
            AudioListener.volume = Master;
            Changed?.Invoke();
        }
    }
}
