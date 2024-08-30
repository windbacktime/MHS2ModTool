using System.IO.Hashing;
using System.Text;

namespace MHS2ModTool.GameFileFormats
{
    internal class HashUtils
    {
        public static uint CalculateCrc32(string data)
        {
            return CalculateCrc32(Encoding.UTF8.GetBytes(data));
        }

        public static uint CalculateCrc32NoSign(string data)
        {
            return CalculateCrc32NoSign(Encoding.UTF8.GetBytes(data));
        }

        public static uint CalculateCrc32(ReadOnlySpan<byte> data)
        {
            return ~Crc32.HashToUInt32(data);
        }

        public static uint CalculateCrc32NoSign(ReadOnlySpan<byte> data)
        {
            return ~Crc32.HashToUInt32(data) & int.MaxValue;
        }
    }
}
