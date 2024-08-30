using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace MHS2ModTool.CommonFileFormats
{
    static class PNGTexture
    {
        private const int ChunkOverheadSize = 12;
        private const int MaxIdatChunkSize = 0x2000;

        private static readonly uint[] s_CrcTable;

        static PNGTexture()
        {
            s_CrcTable = new uint[256];

            uint c;

            for (int n = 0; n < s_CrcTable.Length; n++)
            {
                c = (uint)n;

                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) != 0)
                    {
                        c = 0xedb88320 ^ (c >> 1);
                    }
                    else
                    {
                        c >>= 1;
                    }
                }

                s_CrcTable[n] = c;
            }
        }

        private ref struct PngChunk
        {
            public uint ChunkType;
            public ReadOnlySpan<byte> Data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PngHeader
        {
            public int Width;
            public int Height;
            public byte BitDepth;
            public byte ColorType;
            public byte CompressionMethod;
            public byte FilterMethod;
            public byte InterlaceMethod;
        }

        private enum FilterType : byte
        {
            None = 0,
            Sub = 1,
            Up = 2,
            Average = 3,
            Paeth = 4,
        }

        private const uint IhdrMagic = ((byte)'I' << 24) | ((byte)'H' << 16) | ((byte)'D' << 8) | (byte)'R';
        private const uint PlteMagic = ((byte)'P' << 24) | ((byte)'L' << 16) | ((byte)'T' << 8) | (byte)'E';
        private const uint IdatMagic = ((byte)'I' << 24) | ((byte)'D' << 16) | ((byte)'A' << 8) | (byte)'T';
        private const uint IendMagic = ((byte)'I' << 24) | ((byte)'E' << 16) | ((byte)'N' << 8) | (byte)'D';

        private static readonly byte[] s_PngSignature =
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
        ];

        public static void Save(Stream output, ImageParameters parameters, ReadOnlySpan<byte> data, bool fastMode = false)
        {
            output.Write(s_PngSignature);

            WriteChunk(output, IhdrMagic, new PngHeader()
            {
                Width = ReverseEndianness(parameters.Width),
                Height = ReverseEndianness(parameters.Height),
                BitDepth = 8,
                ColorType = 6,
            });

            byte[] encoded = EncodeImageData(data, parameters.Width, parameters.Height, fastMode);

            for (int encodedOffset = 0; encodedOffset < encoded.Length; encodedOffset += MaxIdatChunkSize)
            {
                int length = Math.Min(MaxIdatChunkSize, encoded.Length - encodedOffset);

                WriteChunk(output, IdatMagic, encoded.AsSpan().Slice(encodedOffset, length));
            }

            WriteChunk(output, IendMagic, ReadOnlySpan<byte>.Empty);
        }

        private static byte[] EncodeImageData(ReadOnlySpan<byte> input, int width, int height, bool fastMode)
        {
            int bpp = 4;
            int stride = width * bpp;
            byte[] tempLine = new byte[stride];

            using MemoryStream ms = new();

            using (ZLibStream zLibStream = new(ms, fastMode ? CompressionLevel.Fastest : CompressionLevel.SmallestSize))
            {
                for (int y = 0; y < height; y++)
                {
                    ReadOnlySpan<byte> scanline = input.Slice(y * stride, stride);
                    FilterType filterType = fastMode ? FilterType.None : SelectFilterType(input, scanline, y, width, bpp);

                    zLibStream.WriteByte((byte)filterType);

                    switch (filterType)
                    {
                        case FilterType.None:
                            zLibStream.Write(scanline);
                            break;
                        case FilterType.Sub:
                            for (int x = 0; x < scanline.Length; x++)
                            {
                                byte left = x < bpp ? (byte)0 : scanline[x - bpp];
                                tempLine[x] = (byte)(scanline[x] - left);
                            }
                            zLibStream.Write(tempLine);
                            break;
                        case FilterType.Up:
                            for (int x = 0; x < scanline.Length; x++)
                            {
                                byte above = y == 0 ? (byte)0 : input[y * stride + x - stride];
                                tempLine[x] = (byte)(scanline[x] - above);
                            }
                            zLibStream.Write(tempLine);
                            break;
                        case FilterType.Average:
                            for (int x = 0; x < scanline.Length; x++)
                            {
                                byte left = x < bpp ? (byte)0 : scanline[x - bpp];
                                byte above = y == 0 ? (byte)0 : input[y * stride + x - stride];
                                tempLine[x] = (byte)(scanline[x] - ((left + above) >> 1));
                            }
                            zLibStream.Write(tempLine);
                            break;
                        case FilterType.Paeth:
                            for (int x = 0; x < scanline.Length; x++)
                            {
                                byte left = x < bpp ? (byte)0 : scanline[x - bpp];
                                byte above = y == 0 ? (byte)0 : input[y * stride + x - stride];
                                byte leftAbove = y == 0 || x < bpp ? (byte)0 : input[y * stride + x - bpp - stride];
                                tempLine[x] = (byte)(scanline[x] - PaethPredict(left, above, leftAbove));
                            }
                            zLibStream.Write(tempLine);
                            break;
                    }

                }
            }

            return ms.ToArray();
        }

        private static FilterType SelectFilterType(ReadOnlySpan<byte> input, ReadOnlySpan<byte> scanline, int y, int width, int bpp)
        {
            int stride = width * bpp;

            Span<int> deltas = stackalloc int[4];

            for (int x = 0; x < scanline.Length; x++)
            {
                byte left = x < bpp ? (byte)0 : scanline[x - bpp];
                byte above = y == 0 ? (byte)0 : input[y * stride + x - stride];
                byte leftAbove = y == 0 || x < bpp ? (byte)0 : input[y * stride + x - bpp - stride];

                int value = scanline[x];
                int valueSub = value - left;
                int valueUp = value - above;
                int valueAverage = value - ((left + above) >> 1);
                int valuePaeth = value - PaethPredict(left, above, leftAbove);

                deltas[0] += Math.Abs(valueSub);
                deltas[1] += Math.Abs(valueUp);
                deltas[2] += Math.Abs(valueAverage);
                deltas[3] += Math.Abs(valuePaeth);
            }

            int lowestDelta = int.MaxValue;
            FilterType bestCandidate = FilterType.None;

            for (int i = 0; i < deltas.Length; i++)
            {
                if (deltas[i] < lowestDelta)
                {
                    lowestDelta = deltas[i];
                    bestCandidate = (FilterType)(i + 1);
                }
            }

            return bestCandidate;
        }

        private static void WriteChunk<T>(Stream output, uint chunkType, T data) where T : unmanaged
        {
            WriteChunk(output, chunkType, MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref data, 1)));
        }

        private static void WriteChunk(Stream output, uint chunkType, ReadOnlySpan<byte> data)
        {
            WriteUInt32BE(output, (uint)data.Length);
            WriteUInt32BE(output, chunkType);
            output.Write(data);
            WriteUInt32BE(output, ComputeCrc(chunkType, data));
        }

        private static void WriteUInt32BE(Stream output, uint value)
        {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static int PaethPredict(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);

            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            else if (pb <= pc)
            {
                return b;
            }
            else
            {
                return c;
            }
        }

        private static uint ComputeCrc(uint chunkType, ReadOnlySpan<byte> input)
        {
            uint crc = UpdateCrc(uint.MaxValue, (byte)(chunkType >> 24));
            crc = UpdateCrc(crc, (byte)(chunkType >> 16));
            crc = UpdateCrc(crc, (byte)(chunkType >> 8));
            crc = UpdateCrc(crc, (byte)chunkType);
            crc = UpdateCrc(crc, input);

            return ~crc;
        }

        private static uint UpdateCrc(uint crc, byte input)
        {
            return s_CrcTable[(byte)(crc ^ input)] ^ (crc >> 8);
        }

        private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> input)
        {
            uint c = crc;

            for (int n = 0; n < input.Length; n++)
            {
                c = s_CrcTable[(byte)(c ^ input[n])] ^ (c >> 8);
            }

            return c;
        }

        private static int ReverseEndianness(int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(value);
            }

            return value;
        }
    }
}
