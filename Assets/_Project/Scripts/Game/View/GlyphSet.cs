using Contraption.Core;

namespace Contraption.Game
{
    // One machine's alien alphabets. Every glyph screen of a machine maps the same value to the same symbol,
    // so players can learn a machine's numbers; the three screen styles use different alphabets.
    public sealed class GlyphSet
    {
        public const int WordLength = 3;

        public int MinValue;
        public int[] Dots;      // 3x3 dot masks, index = value - MinValue
        public int[] Runes;     // stroke masks, index = value - MinValue
        public int[] Letters;   // small stroke masks used to spell words
        public int[][] Words;   // letter indices, index = value - MinValue
        public int[] Labels;    // dot masks under each control

        public static GlyphSet Create(MachineDefinition def)
        {
            int min = 0, max = Values.Max;
            foreach (var d in def.Displays)
            {
                MachineRules.DisplayRange(d, out int lo, out int hi);
                if (lo < min) min = lo;
                if (hi > max) max = hi;
            }
            int count = max - min + 1;
            var dots = Glyphs.Make(def.Seed, count + def.Controls.Count);
            var set = new GlyphSet
            {
                MinValue = min,
                Dots = dots[..count],
                Labels = dots[count..],
                Runes = Glyphs.MakeRunes(def.Seed ^ 0x2B1E5UL, count, 3, 6),
                Letters = Glyphs.MakeRunes(def.Seed ^ 0x1E77E5UL, 7, 2, 4),
            };
            var rng = new Rng(def.Seed ^ 0x3070D5UL);
            set.Words = new int[count][];
            for (int v = 0; v < count; v++)
            {
                int[] word;
                do
                {
                    word = new int[WordLength];
                    for (int i = 0; i < WordLength; i++) word[i] = rng.Range(0, set.Letters.Length);
                } while (Taken(set.Words, v, word));
                set.Words[v] = word;
            }
            return set;
        }

        private static bool Taken(int[][] words, int upTo, int[] word)
        {
            for (int i = 0; i < upTo; i++)
                if (words[i][0] == word[0] && words[i][1] == word[1] && words[i][2] == word[2]) return true;
            return false;
        }
    }
}
