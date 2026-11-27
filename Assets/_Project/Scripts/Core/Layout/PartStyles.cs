using System.Collections.Generic;

namespace Contraption.Core
{
    // Visual variants and their grid footprints. Names are model names in Art/Models/Kit (one FBX each).
    // A style changes the look only; the control/display rules are the same for every style of a kind.
    public sealed class PartStyle
    {
        public readonly string Name;
        public readonly int W, H;

        public PartStyle(string name, int w, int h)
        {
            Name = name;
            W = w;
            H = h;
        }

        public bool IsSingle => W == 1 && H == 1;
    }

    public static class PartStyles
    {
        public static readonly string[] Fillers = { "Fill_Vent", "Fill_Grille", "Fill_Plate", "Fill_Pipes", "Fill_Porthole" };
        public static readonly string[] BigFillers = { "Fill_Fan_2x2", "Fill_Hatch_2x2" };
        public static readonly string[] InputContainers = { "In_Flask", "In_Tank", "In_Hopper", "In_Sphere" };
        public static readonly string[] OutputContainers = { "Out_Tank", "Out_Vat", "Out_Flask" };
        public static readonly string[] TopStyles = { "Crest", "Chimney", "Dome", "Antenna" };
        public static readonly string[] BaseStyles = { "Foot", "Plinth", "Skid" };

        private static readonly Dictionary<ControlKind, PartStyle[]> ControlStyles = new()
        {
            [ControlKind.Button] = Styles("Ctl_Button", (1, 1), (2, 1), (2, 2)),
            [ControlKind.Lever] = Styles("Ctl_Lever", (1, 1), (1, 2), (1, 3)),
            [ControlKind.Dial] = Styles("Ctl_Dial", (1, 1), (2, 2)),
            [ControlKind.Knob] = Concat(Styles("Ctl_Knob", (1, 1), (2, 2)),
                Styles("Ctl_Slider", (2, 1), (3, 1), (4, 1), (1, 2), (1, 3), (1, 4))),
            [ControlKind.Joystick] = Styles("Ctl_Joystick", (1, 1), (2, 2)),
        };

        private static readonly Dictionary<DisplayKind, PartStyle[]> DisplayStyles = new()
        {
            [DisplayKind.Light] = Styles("Disp_Light", (1, 1), (1, 2), (2, 1), (2, 2)),
            [DisplayKind.Bar] = Styles("Disp_Bar", (1, 1), (1, 2), (1, 3), (1, 4), (2, 1), (3, 1), (4, 1)),
            [DisplayKind.Gauge] = Styles("Disp_Gauge", (1, 1), (2, 2), (2, 1), (3, 1)),
            [DisplayKind.Glyph] = Styles("Disp_Glyph", (1, 1), (2, 1), (2, 2)),
        };

        public static IReadOnlyList<PartStyle> For(ControlKind k) => ControlStyles[k];
        public static IReadOnlyList<PartStyle> For(DisplayKind k) => DisplayStyles[k];

        public static PartStyle Find(string name)
        {
            foreach (var list in ControlStyles.Values)
                foreach (var s in list) if (s.Name == name) return s;
            foreach (var list in DisplayStyles.Values)
                foreach (var s in list) if (s.Name == name) return s;
            return null;
        }

        // "Ctl_Lever" for 1x1, "Ctl_Lever_1x3" otherwise.
        private static PartStyle[] Styles(string baseName, params (int w, int h)[] sizes)
        {
            var r = new PartStyle[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                var (w, h) = sizes[i];
                bool single = w == 1 && h == 1 && baseName != "Ctl_Slider";
                r[i] = new PartStyle(single ? baseName : $"{baseName}_{w}x{h}", w, h);
            }
            return r;
        }

        private static PartStyle[] Concat(PartStyle[] a, PartStyle[] b)
        {
            var r = new PartStyle[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }
    }
}
