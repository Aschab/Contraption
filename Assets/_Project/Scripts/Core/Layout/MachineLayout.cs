using System.Collections.Generic;

namespace Contraption.Core
{
    // Visual arrangement of a machine, saved with the level so a curated look never changes.
    // Grid cells are Cell×Cell metres; x grows right, y grows up, (0,0) is the bottom-left cell.
    public sealed class MachineLayout
    {
        public int Columns, Rows;
        // Row-major, index = y * Columns + x. True where the chassis has a body.
        public bool[] Body = new bool[0];
        // Aligned with MachineDefinition.Controls / Displays.
        public List<Placement> Controls = new();
        public List<Placement> Displays = new();
        public List<Placement> Fillers = new();
        // Input containers stand on top of the given column; the output stands on the ground to the right.
        public List<Container> Inputs = new();
        public Container Output = new();
        public string TopStyle = "Crest";
        public string BaseStyle = "Foot";
        // Big trigger that runs a test: plunger on the chassis top or lever on its left side.
        public string TestStyle = "Test_Plunger";
        // Re-rolls small decoration (crest positions, rivet patterns) without moving parts.
        public ulong DecorSeed;
        // Null for layouts saved before colours existed (the kit's colours are used).
        public MachineColors Colors;
        // Shows the liquid after every pipeline step in glass vials (easier puzzles, tutorials).
        public bool ShowSteps;
        // Cornices along the row boundaries.
        public bool Ledges;

        public bool IsBody(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows && Body[y * Columns + x];

        // Highest body row of a column, or -1 if the column is empty.
        public int TopOf(int x)
        {
            for (int y = Rows - 1; y >= 0; y--) if (IsBody(x, y)) return y;
            return -1;
        }

        public IEnumerable<Placement> AllPlacements()
        {
            foreach (var p in Controls) if (p != null) yield return p;
            foreach (var p in Displays) if (p != null) yield return p;
            foreach (var p in Fillers) yield return p;
        }

        // True if the footprint lies on body cells not used by any placement except `ignore` (and fillers if asked).
        public bool Fits(int x, int y, int w, int h, Placement ignore = null, bool overFillers = false)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    if (!IsBody(i, j)) return false;
            foreach (var p in AllPlacements())
                if (p != ignore && p.Overlaps(x, y, w, h) && !(overFillers && Fillers.Contains(p))) return false;
            return true;
        }
    }

    public sealed class Placement
    {
        public string Style = "";
        public int X, Y, W = 1, H = 1;
        // Panel colour: 0 main, 1 alt, 2 alt2. Glow: index into MachineColors.Glows.
        public int Tint, Glow;
        // Relief: Flush on the body, Raised on a housing in front of it, Framed in a deep bezel.
        public const int Flush = 0, Raised = 1, Framed = 2;
        public int Depth;

        public bool Overlaps(int x, int y, int w, int h) => X < x + w && x < X + W && Y < y + h && y < Y + H;
    }

    public sealed class Container
    {
        public string Style = "";
        public int Column;
    }
}
