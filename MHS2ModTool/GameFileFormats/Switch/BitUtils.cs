using System.Numerics;

namespace MHS2ModTool.GameFileFormats.Switch
{
    public static class BitUtils
    {
        public static T AlignUp<T>(T value, T size)
            where T : IBinaryInteger<T>
        {
            return (value + (size - T.One)) & -size;
        }

        public static T AlignDown<T>(T value, T size)
            where T : IBinaryInteger<T>
        {
            return value & -size;
        }

        public static T DivRoundUp<T>(T value, T dividend)
            where T : IBinaryInteger<T>
        {
            return (value + (dividend - T.One)) / dividend;
        }
    }
}
