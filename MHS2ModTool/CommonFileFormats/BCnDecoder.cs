using MHS2ModTool.GameFileFormats.Switch;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace MHS2ModTool.CommonFileFormats
{
    public static class BCnDecoder
    {
        private const int BlockWidth = 4;
        private const int BlockHeight = 4;

        public static byte[] DecodeBC1(ReadOnlySpan<byte> data, int width, int height, int depth, int levels, int layers)
        {
            int size = 0;

            for (int l = 0; l < levels; l++)
            {
                size += Math.Max(1, width >> l) * Math.Max(1, height >> l) * Math.Max(1, depth >> l) * layers * 4;
            }

            byte[] output = new byte[size];

            Span<byte> tile = stackalloc byte[BlockWidth * BlockHeight * 4];

            Span<uint> tileAsUint = MemoryMarshal.Cast<byte, uint>(tile);
            Span<uint> outputAsUint = MemoryMarshal.Cast<byte, uint>(output);

            Span<Vector128<byte>> tileAsVector128 = MemoryMarshal.Cast<byte, Vector128<byte>>(tile);

            Span<Vector128<byte>> outputLine0 = default;
            Span<Vector128<byte>> outputLine1 = default;
            Span<Vector128<byte>> outputLine2 = default;
            Span<Vector128<byte>> outputLine3 = default;

            int imageBaseOOffs = 0;

            for (int l = 0; l < levels; l++)
            {
                int w = BitUtils.DivRoundUp(width, BlockWidth);
                int h = BitUtils.DivRoundUp(height, BlockHeight);

                for (int l2 = 0; l2 < layers; l2++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            int baseY = y * BlockHeight;
                            int copyHeight = Math.Min(BlockHeight, height - baseY);
                            int lineBaseOOffs = imageBaseOOffs + baseY * width;

                            if (copyHeight == 4)
                            {
                                outputLine0 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[lineBaseOOffs..]);
                                outputLine1 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width)..]);
                                outputLine2 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width * 2)..]);
                                outputLine3 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width * 3)..]);
                            }

                            for (int x = 0; x < w; x++)
                            {
                                int baseX = x * BlockWidth;
                                int copyWidth = Math.Min(BlockWidth, width - baseX);

                                BC1DecodeTileRgb(tile, data);

                                if ((copyWidth | copyHeight) == 4)
                                {
                                    outputLine0[x] = tileAsVector128[0];
                                    outputLine1[x] = tileAsVector128[1];
                                    outputLine2[x] = tileAsVector128[2];
                                    outputLine3[x] = tileAsVector128[3];
                                }
                                else
                                {
                                    int pixelBaseOOffs = lineBaseOOffs + baseX;

                                    for (int tY = 0; tY < copyHeight; tY++)
                                    {
                                        tileAsUint.Slice(tY * 4, copyWidth).CopyTo(outputAsUint.Slice(pixelBaseOOffs + width * tY, copyWidth));
                                    }
                                }

                                data = data[8..];
                            }
                        }

                        imageBaseOOffs += width * height;
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
                depth = Math.Max(1, depth >> 1);
            }

            return output;
        }

        public static byte[] DecodeBC2(ReadOnlySpan<byte> data, int width, int height, int depth, int levels, int layers)
        {
            int size = 0;

            for (int l = 0; l < levels; l++)
            {
                size += Math.Max(1, width >> l) * Math.Max(1, height >> l) * Math.Max(1, depth >> l) * layers * 4;
            }

            byte[] output = new byte[size];

            Span<byte> tile = stackalloc byte[BlockWidth * BlockHeight * 4];

            Span<uint> tileAsUint = MemoryMarshal.Cast<byte, uint>(tile);
            Span<uint> outputAsUint = MemoryMarshal.Cast<byte, uint>(output);

            Span<Vector128<byte>> tileAsVector128 = MemoryMarshal.Cast<byte, Vector128<byte>>(tile);

            Span<Vector128<byte>> outputLine0 = default;
            Span<Vector128<byte>> outputLine1 = default;
            Span<Vector128<byte>> outputLine2 = default;
            Span<Vector128<byte>> outputLine3 = default;

            int imageBaseOOffs = 0;

            for (int l = 0; l < levels; l++)
            {
                int w = BitUtils.DivRoundUp(width, BlockWidth);
                int h = BitUtils.DivRoundUp(height, BlockHeight);

                for (int l2 = 0; l2 < layers; l2++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            int baseY = y * BlockHeight;
                            int copyHeight = Math.Min(BlockHeight, height - baseY);
                            int lineBaseOOffs = imageBaseOOffs + baseY * width;

                            if (copyHeight == 4)
                            {
                                outputLine0 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[lineBaseOOffs..]);
                                outputLine1 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width)..]);
                                outputLine2 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width * 2)..]);
                                outputLine3 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width * 3)..]);
                            }

                            for (int x = 0; x < w; x++)
                            {
                                int baseX = x * BlockWidth;
                                int copyWidth = Math.Min(BlockWidth, width - baseX);

                                BC23DecodeTileRgb(tile, data[8..]);

                                ulong block = BinaryPrimitives.ReadUInt64LittleEndian(data);

                                for (int i = 3; i < BlockWidth * BlockHeight * 4; i += 4, block >>= 4)
                                {
                                    tile[i] = (byte)((block & 0xf) | (block << 4));
                                }

                                if ((copyWidth | copyHeight) == 4)
                                {
                                    outputLine0[x] = tileAsVector128[0];
                                    outputLine1[x] = tileAsVector128[1];
                                    outputLine2[x] = tileAsVector128[2];
                                    outputLine3[x] = tileAsVector128[3];
                                }
                                else
                                {
                                    int pixelBaseOOffs = lineBaseOOffs + baseX;

                                    for (int tY = 0; tY < copyHeight; tY++)
                                    {
                                        tileAsUint.Slice(tY * 4, copyWidth).CopyTo(outputAsUint.Slice(pixelBaseOOffs + width * tY, copyWidth));
                                    }
                                }

                                data = data[16..];
                            }
                        }

                        imageBaseOOffs += width * height;
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
                depth = Math.Max(1, depth >> 1);
            }

            return output;
        }

        public static byte[] DecodeBC3(ReadOnlySpan<byte> data, int width, int height, int depth, int levels, int layers)
        {
            int size = 0;

            for (int l = 0; l < levels; l++)
            {
                size += Math.Max(1, width >> l) * Math.Max(1, height >> l) * Math.Max(1, depth >> l) * layers * 4;
            }

            byte[] output = new byte[size];

            Span<byte> tile = stackalloc byte[BlockWidth * BlockHeight * 4];
            Span<byte> rPal = stackalloc byte[8];

            Span<uint> tileAsUint = MemoryMarshal.Cast<byte, uint>(tile);
            Span<uint> outputAsUint = MemoryMarshal.Cast<byte, uint>(output);

            Span<Vector128<byte>> tileAsVector128 = MemoryMarshal.Cast<byte, Vector128<byte>>(tile);

            Span<Vector128<byte>> outputLine0 = default;
            Span<Vector128<byte>> outputLine1 = default;
            Span<Vector128<byte>> outputLine2 = default;
            Span<Vector128<byte>> outputLine3 = default;

            int imageBaseOOffs = 0;

            for (int l = 0; l < levels; l++)
            {
                int w = BitUtils.DivRoundUp(width, BlockWidth);
                int h = BitUtils.DivRoundUp(height, BlockHeight);

                for (int l2 = 0; l2 < layers; l2++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            int baseY = y * BlockHeight;
                            int copyHeight = Math.Min(BlockHeight, height - baseY);
                            int lineBaseOOffs = imageBaseOOffs + baseY * width;

                            if (copyHeight == 4)
                            {
                                outputLine0 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[lineBaseOOffs..]);
                                outputLine1 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width)..]);
                                outputLine2 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width * 2)..]);
                                outputLine3 = MemoryMarshal.Cast<uint, Vector128<byte>>(outputAsUint[(lineBaseOOffs + width * 3)..]);
                            }

                            for (int x = 0; x < w; x++)
                            {
                                int baseX = x * BlockWidth;
                                int copyWidth = Math.Min(BlockWidth, width - baseX);

                                BC23DecodeTileRgb(tile, data[8..]);

                                ulong block = BinaryPrimitives.ReadUInt64LittleEndian(data);

                                rPal[0] = (byte)block;
                                rPal[1] = (byte)(block >> 8);

                                BCnLerpAlphaUnorm(rPal);
                                BCnDecodeTileAlphaRgba(tile, rPal, block >> 16);

                                if ((copyWidth | copyHeight) == 4)
                                {
                                    outputLine0[x] = tileAsVector128[0];
                                    outputLine1[x] = tileAsVector128[1];
                                    outputLine2[x] = tileAsVector128[2];
                                    outputLine3[x] = tileAsVector128[3];
                                }
                                else
                                {
                                    int pixelBaseOOffs = lineBaseOOffs + baseX;

                                    for (int tY = 0; tY < copyHeight; tY++)
                                    {
                                        tileAsUint.Slice(tY * 4, copyWidth).CopyTo(outputAsUint.Slice(pixelBaseOOffs + width * tY, copyWidth));
                                    }
                                }

                                data = data[16..];
                            }
                        }

                        imageBaseOOffs += width * height;
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
                depth = Math.Max(1, depth >> 1);
            }

            return output;
        }

        public static byte[] DecodeBC7(ReadOnlySpan<byte> data, int width, int height, int depth, int levels, int layers)
        {
            int size = 0;

            for (int l = 0; l < levels; l++)
            {
                size += Math.Max(1, width >> l) * Math.Max(1, height >> l) * Math.Max(1, depth >> l) * layers * 4;
            }

            byte[] output = new byte[size];

            int inputOffset = 0;
            int outputOffset = 0;

            for (int l = 0; l < levels; l++)
            {
                int w = BitUtils.DivRoundUp(width, BlockWidth);
                int h = BitUtils.DivRoundUp(height, BlockHeight);

                for (int l2 = 0; l2 < layers; l2++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        BC7Decoder.Decode(output.AsSpan()[outputOffset..], data[inputOffset..], width, height);

                        inputOffset += w * h * 16;
                        outputOffset += width * height * 4;
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
                depth = Math.Max(1, depth >> 1);
            }

            return output;
        }

        private static void BCnLerpAlphaUnorm(Span<byte> alpha)
        {
            byte a0 = alpha[0];
            byte a1 = alpha[1];

            if (a0 > a1)
            {
                alpha[2] = (byte)((6 * a0 + 1 * a1) / 7);
                alpha[3] = (byte)((5 * a0 + 2 * a1) / 7);
                alpha[4] = (byte)((4 * a0 + 3 * a1) / 7);
                alpha[5] = (byte)((3 * a0 + 4 * a1) / 7);
                alpha[6] = (byte)((2 * a0 + 5 * a1) / 7);
                alpha[7] = (byte)((1 * a0 + 6 * a1) / 7);
            }
            else
            {
                alpha[2] = (byte)((4 * a0 + 1 * a1) / 5);
                alpha[3] = (byte)((3 * a0 + 2 * a1) / 5);
                alpha[4] = (byte)((2 * a0 + 3 * a1) / 5);
                alpha[5] = (byte)((1 * a0 + 4 * a1) / 5);
                alpha[6] = 0;
                alpha[7] = 0xff;
            }
        }

        private unsafe static void BCnDecodeTileAlphaRgba(Span<byte> output, Span<byte> rPal, ulong rI)
        {
            for (int i = 3; i < BlockWidth * BlockHeight * 4; i += 4, rI >>= 3)
            {
                output[i] = rPal[(int)(rI & 7)];
            }
        }

        private unsafe static void BC1DecodeTileRgb(Span<byte> output, ReadOnlySpan<byte> input)
        {
            Span<uint> clut = stackalloc uint[4];

            uint c0c1 = BinaryPrimitives.ReadUInt32LittleEndian(input);
            uint c0 = (ushort)c0c1;
            uint c1 = (ushort)(c0c1 >> 16);

            clut[0] = ConvertRgb565ToRgb888(c0) | 0xff000000;
            clut[1] = ConvertRgb565ToRgb888(c1) | 0xff000000;
            clut[2] = BC1LerpRgb2(clut[0], clut[1], c0, c1);
            clut[3] = BC1LerpRgb3(clut[0], clut[1], c0, c1);

            BCnDecodeTileRgb(clut, output, input);
        }

        private unsafe static void BC23DecodeTileRgb(Span<byte> output, ReadOnlySpan<byte> input)
        {
            Span<uint> clut = stackalloc uint[4];

            uint c0c1 = BinaryPrimitives.ReadUInt32LittleEndian(input);
            uint c0 = (ushort)c0c1;
            uint c1 = (ushort)(c0c1 >> 16);

            clut[0] = ConvertRgb565ToRgb888(c0);
            clut[1] = ConvertRgb565ToRgb888(c1);
            clut[2] = BC23LerpRgb2(clut[0], clut[1]);
            clut[3] = BC23LerpRgb3(clut[0], clut[1]);

            BCnDecodeTileRgb(clut, output, input);
        }

        private unsafe static void BCnDecodeTileRgb(Span<uint> clut, Span<byte> output, ReadOnlySpan<byte> input)
        {
            Span<uint> outputAsUint = MemoryMarshal.Cast<byte, uint>(output);

            uint indices = BinaryPrimitives.ReadUInt32LittleEndian(input[4..]);

            for (int i = 0; i < BlockWidth * BlockHeight; i++, indices >>= 2)
            {
                outputAsUint[i] = clut[(int)(indices & 3)];
            }
        }

        private static uint BC1LerpRgb2(uint color0, uint color1, uint c0, uint c1)
        {
            if (c0 > c1)
            {
                return BC23LerpRgb2(color0, color1) | 0xff000000;
            }

            uint carry = color0 & color1;
            uint addHalve = ((color0 ^ color1) >> 1) & 0x7f7f7f;
            return (addHalve + carry) | 0xff000000;
        }

        private static uint BC23LerpRgb2(uint color0, uint color1)
        {
            uint r0 = (byte)color0;
            uint g0 = color0 & 0xff00;
            uint b0 = color0 & 0xff0000;

            uint r1 = (byte)color1;
            uint g1 = color1 & 0xff00;
            uint b1 = color1 & 0xff0000;

            uint mixR = (2 * r0 + r1) / 3;
            uint mixG = (2 * g0 + g1) / 3;
            uint mixB = (2 * b0 + b1) / 3;

            return mixR | (mixG & 0xff00) | (mixB & 0xff0000);
        }

        private static uint BC1LerpRgb3(uint color0, uint color1, uint c0, uint c1)
        {
            if (c0 > c1)
            {
                return BC23LerpRgb3(color0, color1) | 0xff000000;
            }

            return 0;
        }

        private static uint BC23LerpRgb3(uint color0, uint color1)
        {
            uint r0 = (byte)color0;
            uint g0 = color0 & 0xff00;
            uint b0 = color0 & 0xff0000;

            uint r1 = (byte)color1;
            uint g1 = color1 & 0xff00;
            uint b1 = color1 & 0xff0000;

            uint mixR = (2 * r1 + r0) / 3;
            uint mixG = (2 * g1 + g0) / 3;
            uint mixB = (2 * b1 + b0) / 3;

            return mixR | (mixG & 0xff00) | (mixB & 0xff0000);
        }

        private static uint ConvertRgb565ToRgb888(uint value)
        {
            uint b = (value & 0x1f) << 19;
            uint g = (value << 5) & 0xfc00;
            uint r = (value >> 8) & 0xf8;

            b |= b >> 5;
            g |= g >> 6;
            r |= r >> 5;

            return r | (g & 0xff00) | (b & 0xff0000);
        }
    }
}
