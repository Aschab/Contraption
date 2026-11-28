using UnityEngine;

namespace Contraption.Game
{
    // Player options that make the game easier to read or play; will move to the settings menu.
    public static class Accessibility
    {
        private const string MagnifyKey = "contraption.access.magnify";
        private const string QuickKey = "contraption.access.quick";
        private static bool? _magnify, _quick;

        // The focused or hovered part comes toward the camera so its symbols are easy to read.
        public static bool MagnifyFocus
        {
            get => _magnify ??= PlayerPrefs.GetInt(MagnifyKey, 1) == 1;
            set
            {
                _magnify = value;
                PlayerPrefs.SetInt(MagnifyKey, value ? 1 : 0);
            }
        }

        // Displays update all at once and silently after a move, instead of one by one with their sounds.
        public static bool Quick
        {
            get => _quick ??= PlayerPrefs.GetInt(QuickKey, 0) == 1;
            set
            {
                _quick = value;
                PlayerPrefs.SetInt(QuickKey, value ? 1 : 0);
            }
        }
    }
}
