using System.Collections.Generic;

namespace Contraption.Core
{
    // A machine's colour scheme, stored as hex "RRGGBB" so a curated level keeps its look.
    public sealed class MachineColors
    {
        public string Name = "";
        public string Frame = "", Trim = "", Panel = "", PanelAlt = "", PanelAlt2 = "", Metal = "";
        // Display/indicator glow colours; each placement picks one by index.
        public List<string> Glows = new();

        public MachineColors Clone()
        {
            var c = (MachineColors)MemberwiseClone();
            c.Glows = new List<string>(Glows);
            return c;
        }
    }
}
