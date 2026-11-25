using System;
using System.Collections.Generic;

namespace Contraption.Core
{
    public enum LiquidProperty { Color, Viscosity, Temperature, Flammability, Refraction }

    public struct Liquid : IEquatable<Liquid>
    {
        public const int PropertyCount = 5;

        public int Color;
        public int Viscosity;
        public int Temperature;
        public int Flammability;
        public int Refraction;

        public int this[LiquidProperty p]
        {
            get => p switch
            {
                LiquidProperty.Color => Color,
                LiquidProperty.Viscosity => Viscosity,
                LiquidProperty.Temperature => Temperature,
                LiquidProperty.Flammability => Flammability,
                _ => Refraction,
            };
            set
            {
                switch (p)
                {
                    case LiquidProperty.Color: Color = value; break;
                    case LiquidProperty.Viscosity: Viscosity = value; break;
                    case LiquidProperty.Temperature: Temperature = value; break;
                    case LiquidProperty.Flammability: Flammability = value; break;
                    default: Refraction = value; break;
                }
            }
        }

        public static Liquid Random(Rng rng)
        {
            var l = new Liquid();
            for (int i = 0; i < PropertyCount; i++) l[(LiquidProperty)i] = rng.RangeInclusive(Values.Min, Values.Max);
            return l;
        }

        // Rounded (half up) average per property.
        public static Liquid Mix(IReadOnlyList<Liquid> inputs)
        {
            if (inputs.Count == 1) return inputs[0];
            var result = new Liquid();
            int n = inputs.Count;
            for (int p = 0; p < PropertyCount; p++)
            {
                int sum = 0;
                foreach (var l in inputs) sum += l[(LiquidProperty)p];
                result[(LiquidProperty)p] = (sum * 2 + n) / (2 * n);
            }
            return result;
        }

        public bool Equals(Liquid o) =>
            Color == o.Color && Viscosity == o.Viscosity && Temperature == o.Temperature &&
            Flammability == o.Flammability && Refraction == o.Refraction;

        public override bool Equals(object obj) => obj is Liquid l && Equals(l);

        public override int GetHashCode() =>
            (((Color * 7 + Viscosity) * 7 + Temperature) * 7 + Flammability) * 7 + Refraction;

        public override string ToString() => $"C{Color} V{Viscosity} T{Temperature} F{Flammability} R{Refraction}";
    }
}
