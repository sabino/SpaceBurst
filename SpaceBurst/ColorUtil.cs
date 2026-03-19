using Microsoft.Xna.Framework;
using System;

namespace SpaceBurst
{
    static class ColorUtil
    {
        public static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            string value = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (value.Length != 6)
                return fallback;

            return new Color(
                Convert.ToByte(value.Substring(0, 2), 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16));
        }

        public static Color Scale(Color color, float factor)
        {
            return new Color(
                (byte)Math.Clamp((int)MathF.Round(color.R * factor), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.G * factor), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.B * factor), 0, 255),
                color.A);
        }
    }
}
