using System.Runtime.Intrinsics;
using static MHS2ModTool.GameFileFormats.Switch.BlockLinearConstants;

namespace MHS2ModTool.GameFileFormats.Switch
{
    public static class LayoutConverter
    {
        public static byte[] ConvertBlockLinearToLinear(
            int width,
            int height,
            int depth,
            int sliceDepth,
            int levels,
            int layers,
            int blockWidth,
            int blockHeight,
            int bytesPerPixel,
            int gobBlocksInY,
            int gobBlocksInZ,
            int gobBlocksInTileX,
            SizeInfo sizeInfo,
            ReadOnlySpan<byte> data)
        {
            int outSize = GetTextureSize(
                width,
                height,
                sliceDepth,
                levels,
                layers,
                blockWidth,
                blockHeight,
                bytesPerPixel);

            byte[] outputArray = new byte[outSize];
            Span<byte> output = outputArray;

            int outOffs = 0;

            int mipGobBlocksInY = gobBlocksInY;
            int mipGobBlocksInZ = gobBlocksInZ;

            int gobWidth = (GobStride / bytesPerPixel) * gobBlocksInTileX;
            int gobHeight = gobBlocksInY * GobHeight;

            for (int level = 0; level < levels; level++)
            {
                int w = Math.Max(1, width >> level);
                int h = Math.Max(1, height >> level);
                int d = Math.Max(1, depth >> level);

                w = BitUtils.DivRoundUp(w, blockWidth);
                h = BitUtils.DivRoundUp(h, blockHeight);

                while (h <= (mipGobBlocksInY >> 1) * GobHeight && mipGobBlocksInY != 1)
                {
                    mipGobBlocksInY >>= 1;
                }

                if (level > 0 && d <= (mipGobBlocksInZ >> 1) && mipGobBlocksInZ != 1)
                {
                    mipGobBlocksInZ >>= 1;
                }

                int strideTrunc = BitUtils.AlignDown(w * bytesPerPixel, 16);
                int strideTrunc64 = BitUtils.AlignDown(w * bytesPerPixel, 64);

                int xStart = strideTrunc / bytesPerPixel;

                int stride = w * bytesPerPixel;

                int outStrideGap = stride - w * bytesPerPixel;

                int alignment = gobWidth;

                if (d < gobBlocksInZ || w <= gobWidth || h <= gobHeight)
                {
                    alignment = GobStride / bytesPerPixel;
                }

                int wAligned = BitUtils.AlignUp(w, alignment);

                BlockLinearLayout layoutConverter = new(
                    wAligned,
                    h,
                    mipGobBlocksInY,
                    mipGobBlocksInZ,
                    bytesPerPixel);

                int sd = Math.Max(1, sliceDepth >> level);

                unsafe bool Convert<T>(Span<byte> output, ReadOnlySpan<byte> data) where T : unmanaged
                {
                    fixed (byte* outputPtr = output, dataPtr = data)
                    {
                        byte* outPtr = outputPtr + outOffs;
                        for (int layer = 0; layer < layers; layer++)
                        {
                            byte* inBaseOffset = dataPtr + (layer * sizeInfo.LayerSize + sizeInfo.GetMipOffset(level));

                            for (int z = 0; z < sd; z++)
                            {
                                layoutConverter.SetZ(z);
                                for (int y = 0; y < h; y++)
                                {
                                    layoutConverter.SetY(y);

                                    for (int x = 0; x < strideTrunc64; x += 64, outPtr += 64)
                                    {
                                        byte* offset = inBaseOffset + layoutConverter.GetOffsetWithLineOffset64(x);
                                        byte* offset2 = offset + 0x20;
                                        byte* offset3 = offset + 0x100;
                                        byte* offset4 = offset + 0x120;

                                        Vector128<byte> value = *(Vector128<byte>*)offset;
                                        Vector128<byte> value2 = *(Vector128<byte>*)offset2;
                                        Vector128<byte> value3 = *(Vector128<byte>*)offset3;
                                        Vector128<byte> value4 = *(Vector128<byte>*)offset4;

                                        *(Vector128<byte>*)outPtr = value;
                                        *(Vector128<byte>*)(outPtr + 16) = value2;
                                        *(Vector128<byte>*)(outPtr + 32) = value3;
                                        *(Vector128<byte>*)(outPtr + 48) = value4;
                                    }

                                    for (int x = strideTrunc64; x < strideTrunc; x += 16, outPtr += 16)
                                    {
                                        byte* offset = inBaseOffset + layoutConverter.GetOffsetWithLineOffset16(x);

                                        *(Vector128<byte>*)outPtr = *(Vector128<byte>*)offset;
                                    }

                                    for (int x = xStart; x < w; x++, outPtr += bytesPerPixel)
                                    {
                                        byte* offset = inBaseOffset + layoutConverter.GetOffset(x);

                                        *(T*)outPtr = *(T*)offset;
                                    }

                                    outPtr += outStrideGap;
                                }
                            }
                        }
                        outOffs += stride * h * d * layers;
                    }
                    return true;
                }

                bool _ = bytesPerPixel switch
                {
                    1 => Convert<byte>(output, data),
                    2 => Convert<ushort>(output, data),
                    4 => Convert<uint>(output, data),
                    8 => Convert<ulong>(output, data),
                    12 => Convert<Bpp12Pixel>(output, data),
                    16 => Convert<Vector128<byte>>(output, data),
                    _ => throw new NotSupportedException($"Unable to convert ${bytesPerPixel} bpp pixel format."),
                };
            }
            return outputArray;
        }

        public static void ConvertLinearToBlockLinear(
            Span<byte> output,
            int width,
            int height,
            int depth,
            int sliceDepth,
            int levels,
            int layers,
            int blockWidth,
            int blockHeight,
            int bytesPerPixel,
            int gobBlocksInY,
            int gobBlocksInZ,
            int gobBlocksInTileX,
            SizeInfo sizeInfo,
            ReadOnlySpan<byte> data)
        {
            int inOffs = 0;

            int mipGobBlocksInY = gobBlocksInY;
            int mipGobBlocksInZ = gobBlocksInZ;

            int gobWidth = (GobStride / bytesPerPixel) * gobBlocksInTileX;
            int gobHeight = gobBlocksInY * GobHeight;

            for (int level = 0; level < levels; level++)
            {
                int w = Math.Max(1, width >> level);
                int h = Math.Max(1, height >> level);
                int d = Math.Max(1, depth >> level);

                w = BitUtils.DivRoundUp(w, blockWidth);
                h = BitUtils.DivRoundUp(h, blockHeight);

                while (h <= (mipGobBlocksInY >> 1) * GobHeight && mipGobBlocksInY != 1)
                {
                    mipGobBlocksInY >>= 1;
                }

                if (level > 0 && d <= (mipGobBlocksInZ >> 1) && mipGobBlocksInZ != 1)
                {
                    mipGobBlocksInZ >>= 1;
                }

                int strideTrunc = BitUtils.AlignDown(w * bytesPerPixel, 16);
                int strideTrunc64 = BitUtils.AlignDown(w * bytesPerPixel, 64);

                int xStart = strideTrunc / bytesPerPixel;

                int stride = w * bytesPerPixel;

                int inStrideGap = stride - w * bytesPerPixel;

                int alignment = gobWidth;

                if (d < gobBlocksInZ || w <= gobWidth || h <= gobHeight)
                {
                    alignment = GobStride / bytesPerPixel;
                }

                int wAligned = BitUtils.AlignUp(w, alignment);

                BlockLinearLayout layoutConverter = new(
                    wAligned,
                    h,
                    mipGobBlocksInY,
                    mipGobBlocksInZ,
                    bytesPerPixel);

                int sd = Math.Max(1, sliceDepth >> level);

                unsafe bool Convert<T>(Span<byte> output, ReadOnlySpan<byte> data) where T : unmanaged
                {
                    fixed (byte* outputPtr = output, dataPtr = data)
                    {
                        byte* inPtr = dataPtr + inOffs;
                        for (int layer = 0; layer < layers; layer++)
                        {
                            byte* outBaseOffset = outputPtr + (layer * sizeInfo.LayerSize + sizeInfo.GetMipOffset(level));

                            for (int z = 0; z < sd; z++)
                            {
                                layoutConverter.SetZ(z);
                                for (int y = 0; y < h; y++)
                                {
                                    layoutConverter.SetY(y);

                                    for (int x = 0; x < strideTrunc64; x += 64, inPtr += 64)
                                    {
                                        byte* offset = outBaseOffset + layoutConverter.GetOffsetWithLineOffset64(x);
                                        byte* offset2 = offset + 0x20;
                                        byte* offset3 = offset + 0x100;
                                        byte* offset4 = offset + 0x120;

                                        Vector128<byte> value = *(Vector128<byte>*)inPtr;
                                        Vector128<byte> value2 = *(Vector128<byte>*)(inPtr + 16);
                                        Vector128<byte> value3 = *(Vector128<byte>*)(inPtr + 32);
                                        Vector128<byte> value4 = *(Vector128<byte>*)(inPtr + 48);

                                        *(Vector128<byte>*)offset = value;
                                        *(Vector128<byte>*)offset2 = value2;
                                        *(Vector128<byte>*)offset3 = value3;
                                        *(Vector128<byte>*)offset4 = value4;
                                    }

                                    for (int x = strideTrunc64; x < strideTrunc; x += 16, inPtr += 16)
                                    {
                                        byte* offset = outBaseOffset + layoutConverter.GetOffsetWithLineOffset16(x);

                                        *(Vector128<byte>*)offset = *(Vector128<byte>*)inPtr;
                                    }

                                    for (int x = xStart; x < w; x++, inPtr += bytesPerPixel)
                                    {
                                        byte* offset = outBaseOffset + layoutConverter.GetOffset(x);

                                        *(T*)offset = *(T*)inPtr;
                                    }

                                    inPtr += inStrideGap;
                                }
                            }
                        }
                        inOffs += stride * h * d * layers;
                    }
                    return true;
                }

                bool _ = bytesPerPixel switch
                {
                    1 => Convert<byte>(output, data),
                    2 => Convert<ushort>(output, data),
                    4 => Convert<uint>(output, data),
                    8 => Convert<ulong>(output, data),
                    12 => Convert<Bpp12Pixel>(output, data),
                    16 => Convert<Vector128<byte>>(output, data),
                    _ => throw new NotSupportedException($"Unable to convert ${bytesPerPixel} bpp pixel format."),
                };
            }
        }

        private static int GetTextureSize(
            int width,
            int height,
            int depth,
            int levels,
            int layers,
            int blockWidth,
            int blockHeight,
            int bytesPerPixel)
        {
            int layerSize = 0;

            for (int level = 0; level < levels; level++)
            {
                int w = Math.Max(1, width >> level);
                int h = Math.Max(1, height >> level);
                int d = Math.Max(1, depth >> level);

                w = BitUtils.DivRoundUp(w, blockWidth);
                h = BitUtils.DivRoundUp(h, blockHeight);

                int stride = w * bytesPerPixel;

                layerSize += stride * h * d;
            }

            return layerSize * layers;
        }
    }
}
