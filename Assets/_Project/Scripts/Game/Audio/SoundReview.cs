using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Contraption.Game
{
    // Playtest marks on individual sound files ("keep" / "purge"), saved to ArtSource/Audio/packs/review.txt so the
    // purge can be applied to the project later. Purged clips stop playing at once. Editor only; builds have no marks.
    public static class SoundReview
    {
        private static Dictionary<string, string> _marks;
        private static Dictionary<string, string> _sources;

        public static string MarkOf(string clip) => Marks().TryGetValue(clip, out var m) ? m : null;
        public static bool IsPurged(string clip) => MarkOf(clip) == "purge";

        // Sets the mark, or clears it when the clip already has that mark.
        public static void Toggle(string clip, string mark)
        {
            var marks = Marks();
            if (marks.TryGetValue(clip, out var m) && m == mark) marks.Remove(clip);
            else marks[clip] = mark;
            Save();
        }

        // Original pack file of an imported clip (from the packs' SOURCES.md), or "".
        public static string Source(string clip)
        {
#if UNITY_EDITOR
            if (_sources == null)
            {
                _sources = new Dictionary<string, string>();
                foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "ThirdParty"), "SOURCES.md", SearchOption.AllDirectories))
                    foreach (var line in File.ReadAllLines(file))
                    {
                        var cells = line.Split('`');
                        if (cells.Length >= 4) _sources[Path.GetFileNameWithoutExtension(cells[1])] = Path.GetFileName(cells[3]);
                    }
            }
#endif
            return _sources != null && _sources.TryGetValue(clip, out var s) ? s : "";
        }

        private static string FilePath => Path.Combine(Application.dataPath, "../ArtSource/Audio/packs/review.txt");

        private static Dictionary<string, string> Marks()
        {
            if (_marks != null) return _marks;
            _marks = new Dictionary<string, string>();
#if UNITY_EDITOR
            if (File.Exists(FilePath))
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var parts = line.Split(' ');
                    if (parts.Length == 2) _marks[parts[1]] = parts[0];
                }
#endif
            return _marks;
        }

        private static void Save()
        {
#if UNITY_EDITOR
            var lines = new List<string>();
            foreach (var kv in _marks) lines.Add(kv.Value + " " + kv.Key);
            lines.Sort();
            File.WriteAllLines(FilePath, lines);
#endif
        }
    }
}
