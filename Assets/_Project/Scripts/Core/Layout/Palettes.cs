namespace Contraption.Core
{
    // Curated schemes from the concept art (Share/t1–t4) plus a few extra.
    public static class Palettes
    {
        public static readonly MachineColors[] All =
        {
            P("Ochre", "2A2A2E", "C8862E", "E8D9B5", "D08A2E", "8A5A3A", "9A9A9E", "3FFFE0"),
            P("Navy Violet", "23283A", "C27A3A", "E6D6B0", "8A3F2E", "6E7B6A", "A8A8AE", "9B5CFF", "B8F23A"),
            P("Forest Coral", "2E3B2E", "B8863A", "EADDBE", "C9B48A", "7A8A6A", "B0AFAA", "FF7A6A", "5CC8FF"),
            P("Brass Teal", "1E1E22", "D0923A", "E9DCB8", "C9853A", "5A4A3A", "A0A0A4", "4FFFD8"),
            P("Crucible", "33615A", "A8452E", "E8D9B5", "B85A3A", "C9A86A", "C28F42", "FFB84F", "3FFFD9"),
            P("Ember", "3A2422", "C9A050", "EEE0C0", "9E3B2C", "D9A441", "B5B0A8", "FF5A3C", "FFD24A"),
            P("Slate Sky", "2B3440", "B57B3E", "E4DCC8", "4F7FA0", "C7B07A", "ADB2B8", "5CE1FF", "FF6AD5"),
        };

        private static MachineColors P(string name, string frame, string trim, string panel, string alt, string alt2, string metal, params string[] glows)
        {
            var c = new MachineColors { Name = name, Frame = frame, Trim = trim, Panel = panel, PanelAlt = alt, PanelAlt2 = alt2, Metal = metal };
            c.Glows.AddRange(glows);
            return c;
        }
    }
}
