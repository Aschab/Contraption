using System;
using System.IO;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Saved levels as JSON under Assets/_Project/Data/Levels (editor workflow; runtime loading comes with Worlds).
    public static class LevelStore
    {
        public static string Folder => Path.Combine(Application.dataPath, "_Project", "Data", "Levels");

        public static string[] List()
        {
            if (!Directory.Exists(Folder)) return Array.Empty<string>();
            var files = Directory.GetFiles(Folder, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public static string Save(MachineDefinition def)
        {
            Directory.CreateDirectory(Folder);
            string name = string.Join("_", def.Name.Split(Path.GetInvalidFileNameChars())).Trim();
            string path = Path.Combine(Folder, (name.Length > 0 ? name : "Machine") + ".json");
            File.WriteAllText(path, MachineJson.ToJson(def));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            return path;
        }

        public static MachineDefinition Load(string path) => MachineJson.FromJson(File.ReadAllText(path));
    }
}
