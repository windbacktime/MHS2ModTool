using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace MHS2ModTool.CommonFileFormats
{
    readonly struct BC7ModeInfo
    {
        public readonly int SubsetCount;
        public readonly int PartitionBitCount;
        public readonly int PBits;
        public readonly int RotationBitCount;
        public readonly int IndexModeBitCount;
        public readonly int ColorIndexBitCount;
        public readonly int AlphaIndexBitCount;
        public readonly int ColorDepth;
        public readonly int AlphaDepth;

        public BC7ModeInfo(
            int subsetCount,
            int partitionBitsCount,
            int pBits,
            int rotationBitCount,
            int indexModeBitCount,
            int colorIndexBitCount,
            int alphaIndexBitCount,
            int colorDepth,
            int alphaDepth)
        {
            SubsetCount = subsetCount;
            PartitionBitCount = partitionBitsCount;
            PBits = pBits;
            RotationBitCount = rotationBitCount;
            IndexModeBitCount = indexModeBitCount;
            ColorIndexBitCount = colorIndexBitCount;
            AlphaIndexBitCount = alphaIndexBitCount;
            ColorDepth = colorDepth;
            AlphaDepth = alphaDepth;
        }
    }

    static class BC67Tables
    {
        public static readonly BC7ModeInfo[] BC7ModeInfos = new BC7ModeInfo[]
        {
                new BC7ModeInfo(3, 4, 6, 0, 0, 3, 0, 4, 0),
                new BC7ModeInfo(2, 6, 2, 0, 0, 3, 0, 6, 0),
                new BC7ModeInfo(3, 6, 0, 0, 0, 2, 0, 5, 0),
                new BC7ModeInfo(2, 6, 4, 0, 0, 2, 0, 7, 0),
                new BC7ModeInfo(1, 0, 0, 2, 1, 2, 3, 5, 6),
                new BC7ModeInfo(1, 0, 0, 2, 0, 2, 2, 7, 8),
                new BC7ModeInfo(1, 0, 2, 0, 0, 4, 0, 7, 7),
                new BC7ModeInfo(2, 6, 4, 0, 0, 2, 0, 5, 5),
        };

        public static readonly byte[][] Weights =
        {
            new byte[] { 0, 21, 43, 64 },
            new byte[] { 0, 9, 18, 27, 37, 46, 55, 64 },
            new byte[] { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 },
        };

        public static readonly byte[][] InverseWeights =
        {
            new byte[] { 64, 43, 21, 0 },
            new byte[] { 64, 55, 46, 37, 27, 18, 9, 0 },
            new byte[] { 64, 60, 55, 51, 47, 43, 38, 34, 30, 26, 21, 17, 13, 9, 4, 0 },
        };

        public static readonly byte[][][] FixUpIndices = new byte[3][][]
        {
            new byte[64][]
            {
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
                new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 }, new byte[] {  0,  0,  0 },
            },
            new byte[64][]
            {
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0,  2,  0 },
                new byte[] {  0,  2,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0,  2,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  2,  0 },
                new byte[] {  0,  8,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  2,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0,  6,  0 }, new byte[] {  0,  8,  0 },
                new byte[] {  0,  2,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0,  2,  0 }, new byte[] {  0,  8,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  2,  0 },
                new byte[] {  0,  2,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0,  6,  0 },
                new byte[] {  0,  6,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  6,  0 }, new byte[] {  0,  8,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  2,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 }, new byte[] {  0, 15,  0 },
                new byte[] {  0, 15,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0,  2,  0 }, new byte[] {  0, 15,  0 },
            },
            new byte[64][]
            {
                new byte[] {  0,  3, 15 }, new byte[] {  0,  3,  8 }, new byte[] {  0, 15,  8 }, new byte[] {  0, 15,  3 },
                new byte[] {  0,  8, 15 }, new byte[] {  0,  3, 15 }, new byte[] {  0, 15,  3 }, new byte[] {  0, 15,  8 },
                new byte[] {  0,  8, 15 }, new byte[] {  0,  8, 15 }, new byte[] {  0,  6, 15 }, new byte[] {  0,  6, 15 },
                new byte[] {  0,  6, 15 }, new byte[] {  0,  5, 15 }, new byte[] {  0,  3, 15 }, new byte[] {  0,  3,  8 },
                new byte[] {  0,  3, 15 }, new byte[] {  0,  3,  8 }, new byte[] {  0,  8, 15 }, new byte[] {  0, 15,  3 },
                new byte[] {  0,  3, 15 }, new byte[] {  0,  3,  8 }, new byte[] {  0,  6, 15 }, new byte[] {  0, 10,  8 },
                new byte[] {  0,  5,  3 }, new byte[] {  0,  8, 15 }, new byte[] {  0,  8,  6 }, new byte[] {  0,  6, 10 },
                new byte[] {  0,  8, 15 }, new byte[] {  0,  5, 15 }, new byte[] {  0, 15, 10 }, new byte[] {  0, 15,  8 },
                new byte[] {  0,  8, 15 }, new byte[] {  0, 15,  3 }, new byte[] {  0,  3, 15 }, new byte[] {  0,  5, 10 },
                new byte[] {  0,  6, 10 }, new byte[] {  0, 10,  8 }, new byte[] {  0,  8,  9 }, new byte[] {  0, 15, 10 },
                new byte[] {  0, 15,  6 }, new byte[] {  0,  3, 15 }, new byte[] {  0, 15,  8 }, new byte[] {  0,  5, 15 },
                new byte[] {  0, 15,  3 }, new byte[] {  0, 15,  6 }, new byte[] {  0, 15,  6 }, new byte[] {  0, 15,  8 },
                new byte[] {  0,  3, 15 }, new byte[] {  0, 15,  3 }, new byte[] {  0,  5, 15 }, new byte[] {  0,  5, 15 },
                new byte[] {  0,  5, 15 }, new byte[] {  0,  8, 15 }, new byte[] {  0,  5, 15 }, new byte[] {  0, 10, 15 },
                new byte[] {  0,  5, 15 }, new byte[] {  0, 10, 15 }, new byte[] {  0,  8, 15 }, new byte[] {  0, 13, 15 },
                new byte[] {  0, 15,  3 }, new byte[] {  0, 12, 15 }, new byte[] {  0,  3, 15 }, new byte[] {  0,  3,  8 },
            },
        };

        public static readonly byte[][][] PartitionTable = new byte[3][][]
        {
            new byte[64][]
            {
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 0
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 1
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 2
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 3
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 4
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 5
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 6
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 7
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 8
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 9
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 10
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 11
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 12
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 13
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 14
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 15
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 16
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 17
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 18
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 19
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 20
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 21
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 22
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 23
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 24
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 25
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 26
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 27
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 28
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 29
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 30
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 31
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 32
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 33
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 34
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 35
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 36
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 37
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 38
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 39
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 40
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 41
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 42
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 43
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 44
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 45
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 46
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 47
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 48
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 49
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 50
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 51
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 52
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 53
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 54
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 55
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 56
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 57
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 58
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 59
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 60
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 61
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 62
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 63
            },
            new byte[64][]
            {
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 }, // 0
                new byte[16] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 }, // 1
                new byte[16] { 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1 }, // 2
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 1 }, // 3
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1 }, // 4
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1 }, // 5
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1 }, // 6
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1 }, // 7
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1 }, // 8
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, // 9
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1 }, // 10
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1 }, // 11
                new byte[16] { 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, // 12
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 }, // 13
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, // 14
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 }, // 15
                new byte[16] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1 }, // 16
                new byte[16] { 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, // 17
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0 }, // 18
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0 }, // 19
                new byte[16] { 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, // 20
                new byte[16] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0 }, // 21
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0 }, // 22
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1 }, // 23
                new byte[16] { 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0 }, // 24
                new byte[16] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0 }, // 25
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0 }, // 26
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0 }, // 27
                new byte[16] { 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0 }, // 28
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, // 29
                new byte[16] { 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0 }, // 30
                new byte[16] { 0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0 }, // 31
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 }, // 32
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1 }, // 33
                new byte[16] { 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0 }, // 34
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0 }, // 35
                new byte[16] { 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0 }, // 36
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0 }, // 37
                new byte[16] { 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1 }, // 38
                new byte[16] { 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1 }, // 39
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0 }, // 40
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0 }, // 41
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0 }, // 42
                new byte[16] { 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 0 }, // 43
                new byte[16] { 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0 }, // 44
                new byte[16] { 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1 }, // 45
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1 }, // 46
                new byte[16] { 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0 }, // 47
                new byte[16] { 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, // 48
                new byte[16] { 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0 }, // 49
                new byte[16] { 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0 }, // 50
                new byte[16] { 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0 }, // 51
                new byte[16] { 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1 }, // 52
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1 }, // 53
                new byte[16] { 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0 }, // 54
                new byte[16] { 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0 }, // 55
                new byte[16] { 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1 }, // 56
                new byte[16] { 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1 }, // 57
                new byte[16] { 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1 }, // 58
                new byte[16] { 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1 }, // 59
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 }, // 60
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, // 61
                new byte[16] { 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0 }, // 62
                new byte[16] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1 }, // 63
            },
            new byte[64][]
            {
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 1, 2, 2, 2, 2 }, // 0
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 2, 1 }, // 1
                new byte[16] { 0, 0, 0, 0, 2, 0, 0, 1, 2, 2, 1, 1, 2, 2, 1, 1 }, // 2
                new byte[16] { 0, 2, 2, 2, 0, 0, 2, 2, 0, 0, 1, 1, 0, 1, 1, 1 }, // 3
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2 }, // 4
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 2, 2 }, // 5
                new byte[16] { 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1 }, // 6
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1 }, // 7
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2 }, // 8
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2 }, // 9
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2 }, // 10
                new byte[16] { 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2 }, // 11
                new byte[16] { 0, 1, 1, 2, 0, 1, 1, 2, 0, 1, 1, 2, 0, 1, 1, 2 }, // 12
                new byte[16] { 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 2 }, // 13
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 2, 1, 2, 2, 2 }, // 14
                new byte[16] { 0, 0, 1, 1, 2, 0, 0, 1, 2, 2, 0, 0, 2, 2, 2, 0 }, // 15
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 2 }, // 16
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 2, 0, 0, 1, 2, 2, 0, 0 }, // 17
                new byte[16] { 0, 0, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1, 2, 2 }, // 18
                new byte[16] { 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 1 }, // 19
                new byte[16] { 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2 }, // 20
                new byte[16] { 0, 0, 0, 1, 0, 0, 0, 1, 2, 2, 2, 1, 2, 2, 2, 1 }, // 21
                new byte[16] { 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 2, 2, 0, 1, 2, 2 }, // 22
                new byte[16] { 0, 0, 0, 0, 1, 1, 0, 0, 2, 2, 1, 0, 2, 2, 1, 0 }, // 23
                new byte[16] { 0, 1, 2, 2, 0, 1, 2, 2, 0, 0, 1, 1, 0, 0, 0, 0 }, // 24
                new byte[16] { 0, 0, 1, 2, 0, 0, 1, 2, 1, 1, 2, 2, 2, 2, 2, 2 }, // 25
                new byte[16] { 0, 1, 1, 0, 1, 2, 2, 1, 1, 2, 2, 1, 0, 1, 1, 0 }, // 26
                new byte[16] { 0, 0, 0, 0, 0, 1, 1, 0, 1, 2, 2, 1, 1, 2, 2, 1 }, // 27
                new byte[16] { 0, 0, 2, 2, 1, 1, 0, 2, 1, 1, 0, 2, 0, 0, 2, 2 }, // 28
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 2, 0, 0, 2, 2, 2, 2, 2 }, // 29
                new byte[16] { 0, 0, 1, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 0, 1, 1 }, // 30
                new byte[16] { 0, 0, 0, 0, 2, 0, 0, 0, 2, 2, 1, 1, 2, 2, 2, 1 }, // 31
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 2, 2, 2 }, // 32
                new byte[16] { 0, 2, 2, 2, 0, 0, 2, 2, 0, 0, 1, 2, 0, 0, 1, 1 }, // 33
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 2, 0, 0, 2, 2, 0, 2, 2, 2 }, // 34
                new byte[16] { 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0 }, // 35
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0 }, // 36
                new byte[16] { 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0 }, // 37
                new byte[16] { 0, 1, 2, 0, 2, 0, 1, 2, 1, 2, 0, 1, 0, 1, 2, 0 }, // 38
                new byte[16] { 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1 }, // 39
                new byte[16] { 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0, 1, 1 }, // 40
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2 }, // 41
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 2, 1, 2, 1, 2, 1 }, // 42
                new byte[16] { 0, 0, 2, 2, 1, 1, 2, 2, 0, 0, 2, 2, 1, 1, 2, 2 }, // 43
                new byte[16] { 0, 0, 2, 2, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 1, 1 }, // 44
                new byte[16] { 0, 2, 2, 0, 1, 2, 2, 1, 0, 2, 2, 0, 1, 2, 2, 1 }, // 45
                new byte[16] { 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 0, 1, 0, 1 }, // 46
                new byte[16] { 0, 0, 0, 0, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1 }, // 47
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2 }, // 48
                new byte[16] { 0, 2, 2, 2, 0, 1, 1, 1, 0, 2, 2, 2, 0, 1, 1, 1 }, // 49
                new byte[16] { 0, 0, 0, 2, 1, 1, 1, 2, 0, 0, 0, 2, 1, 1, 1, 2 }, // 50
                new byte[16] { 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1, 2 }, // 51
                new byte[16] { 0, 2, 2, 2, 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2 }, // 52
                new byte[16] { 0, 0, 0, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2 }, // 53
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 2, 2 }, // 54
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 1, 2 }, // 55
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 2, 2, 2, 2, 2, 2 }, // 56
                new byte[16] { 0, 0, 2, 2, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 2, 2 }, // 57
                new byte[16] { 0, 0, 2, 2, 1, 1, 2, 2, 1, 1, 2, 2, 0, 0, 2, 2 }, // 58
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2 }, // 59
                new byte[16] { 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1 }, // 60
                new byte[16] { 0, 2, 2, 2, 1, 2, 2, 2, 0, 2, 2, 2, 1, 2, 2, 2 }, // 61
                new byte[16] { 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, // 62
                new byte[16] { 0, 1, 1, 1, 2, 0, 1, 1, 2, 2, 0, 1, 2, 2, 2, 0 }, // 63
            },
        };
    }

    static class BC7Decoder
    {
        struct Block
        {
            public ulong Low;
            public ulong High;

            public void Encode(ulong value, ref int offset, int bits)
            {
                if (offset >= 64)
                {
                    High |= value << (offset - 64);
                }
                else
                {
                    Low |= value << offset;

                    if (offset + bits > 64)
                    {
                        int remainder = 64 - offset;
                        High |= value >> remainder;
                    }
                }

                offset += bits;
            }

            public readonly ulong Decode(ref int offset, int bits)
            {
                ulong value;
                ulong mask = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;

                if (offset >= 64)
                {
                    value = (High >> (offset - 64)) & mask;
                }
                else
                {
                    value = Low >> offset;

                    if (offset + bits > 64)
                    {
                        int remainder = 64 - offset;
                        value |= High << remainder;
                    }

                    value &= mask;
                }

                offset += bits;

                return value;
            }
        }

        struct RgbaColor32 : IEquatable<RgbaColor32>
        {
            private Vector128<int> _color;

            public int R
            {
                readonly get => _color.GetElement(0);
                set => _color = _color.WithElement(0, value);
            }

            public int G
            {
                readonly get => _color.GetElement(1);
                set => _color = _color.WithElement(1, value);
            }

            public int B
            {
                readonly get => _color.GetElement(2);
                set => _color = _color.WithElement(2, value);
            }

            public int A
            {
                readonly get => _color.GetElement(3);
                set => _color = _color.WithElement(3, value);
            }

            public RgbaColor32(Vector128<int> color)
            {
                _color = color;
            }

            public RgbaColor32(int r, int g, int b, int a)
            {
                _color = Vector128.Create(r, g, b, a);
            }

            public RgbaColor32(int scalar)
            {
                _color = Vector128.Create(scalar);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RgbaColor32 operator +(RgbaColor32 x, RgbaColor32 y)
            {
                return new RgbaColor32(x.R + y.R, x.G + y.G, x.B + y.B, x.A + y.A);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RgbaColor32 operator -(RgbaColor32 x, RgbaColor32 y)
            {
                return new RgbaColor32(x.R - y.R, x.G - y.G, x.B - y.B, x.A - y.A);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RgbaColor32 operator *(RgbaColor32 x, RgbaColor32 y)
            {
                return new RgbaColor32(x.R * y.R, x.G * y.G, x.B * y.B, x.A * y.A);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RgbaColor32 operator <<(RgbaColor32 x, [ConstantExpected] byte shift)
            {
                return new RgbaColor32(x.R << shift, x.G << shift, x.B << shift, x.A << shift);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RgbaColor32 operator >>(RgbaColor32 x, [ConstantExpected] byte shift)
            {
                return new RgbaColor32(x.R >> shift, x.G >> shift, x.B >> shift, x.A >> shift);
            }

            public static bool operator ==(RgbaColor32 x, RgbaColor32 y)
            {
                return x.Equals(y);
            }

            public static bool operator !=(RgbaColor32 x, RgbaColor32 y)
            {
                return !x.Equals(y);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly RgbaColor8 GetColor8()
            {
                return new RgbaColor8(ClampByte(R), ClampByte(G), ClampByte(B), ClampByte(A));
            }

            private static byte ClampByte(int value)
            {
                return (byte)Math.Clamp(value, 0, 255);
            }

            public readonly override int GetHashCode()
            {
                return HashCode.Combine(R, G, B, A);
            }

            public readonly override bool Equals(object? obj)
            {
                return obj is RgbaColor32 other && Equals(other);
            }

            public readonly bool Equals(RgbaColor32 other)
            {
                return _color.Equals(other._color);
            }
        }

        struct RgbaColor8 : IEquatable<RgbaColor8>
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;

            public RgbaColor8(byte r, byte g, byte b, byte a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public uint ToUInt32()
            {
                return Unsafe.As<RgbaColor8, uint>(ref this);
            }

            public readonly override int GetHashCode()
            {
                return HashCode.Combine(R, G, B, A);
            }

            public readonly override bool Equals(object? obj)
            {
                return obj is RgbaColor8 other && Equals(other);
            }

            public readonly bool Equals(RgbaColor8 other)
            {
                return R == other.R && G == other.G && B == other.B && A == other.A;
            }

            public readonly byte GetComponent(int index)
            {
                return index switch
                {
                    0 => R,
                    1 => G,
                    2 => B,
                    3 => A,
                    _ => throw new ArgumentOutOfRangeException(nameof(index)),
                };
            }
        }

        public static void Decode(Span<byte> output, ReadOnlySpan<byte> data, int width, int height)
        {
            ReadOnlySpan<Block> blocks = MemoryMarshal.Cast<byte, Block>(data);

            Span<uint> output32 = MemoryMarshal.Cast<byte, uint>(output);

            int wInBlocks = (width + 3) / 4;
            int hInBlocks = (height + 3) / 4;

            for (int y = 0; y < hInBlocks; y++)
            {
                int y2 = y * 4;
                int bh = Math.Min(4, height - y2);

                for (int x = 0; x < wInBlocks; x++)
                {
                    int x2 = x * 4;
                    int bw = Math.Min(4, width - x2);

                    DecodeBlock(blocks[y * wInBlocks + x], output32[(y2 * width + x2)..], bw, bh, width);
                }
            }
        }

        private static void DecodeBlock(Block block, Span<uint> output, int w, int h, int width)
        {
            int mode = BitOperations.TrailingZeroCount((byte)block.Low | 0x100);
            if (mode == 8)
            {
                // Mode is invalid, the spec mandates that hardware fills the block with
                // a transparent black color.
                for (int ty = 0; ty < h; ty++)
                {
                    int baseOffs = ty * width;

                    for (int tx = 0; tx < w; tx++)
                    {
                        int offs = baseOffs + tx;

                        output[offs] = 0;
                    }
                }

                return;
            }

            BC7ModeInfo modeInfo = BC67Tables.BC7ModeInfos[mode];

            int offset = mode + 1;
            int partition = (int)block.Decode(ref offset, modeInfo.PartitionBitCount);
            int rotation = (int)block.Decode(ref offset, modeInfo.RotationBitCount);
            int indexMode = (int)block.Decode(ref offset, modeInfo.IndexModeBitCount);

            Debug.Assert(partition < 64);
            Debug.Assert(rotation < 4);
            Debug.Assert(indexMode < 2);

            int endPointCount = modeInfo.SubsetCount * 2;

            Span<RgbaColor32> endPoints = stackalloc RgbaColor32[endPointCount];
            Span<byte> pValues = stackalloc byte[modeInfo.PBits];

            endPoints.Fill(new RgbaColor32(0, 0, 0, 255));

            for (int i = 0; i < endPointCount; i++)
            {
                endPoints[i].R = (int)block.Decode(ref offset, modeInfo.ColorDepth);
            }

            for (int i = 0; i < endPointCount; i++)
            {
                endPoints[i].G = (int)block.Decode(ref offset, modeInfo.ColorDepth);
            }

            for (int i = 0; i < endPointCount; i++)
            {
                endPoints[i].B = (int)block.Decode(ref offset, modeInfo.ColorDepth);
            }

            if (modeInfo.AlphaDepth != 0)
            {
                for (int i = 0; i < endPointCount; i++)
                {
                    endPoints[i].A = (int)block.Decode(ref offset, modeInfo.AlphaDepth);
                }
            }

            for (int i = 0; i < modeInfo.PBits; i++)
            {
                pValues[i] = (byte)block.Decode(ref offset, 1);
            }

            for (int i = 0; i < endPointCount; i++)
            {
                int pBit = -1;

                if (modeInfo.PBits != 0)
                {
                    int pIndex = (i * modeInfo.PBits) / endPointCount;
                    pBit = pValues[pIndex];
                }

                Unquantize(ref endPoints[i], modeInfo.ColorDepth, modeInfo.AlphaDepth, pBit);
            }

            byte[] partitionTable = BC67Tables.PartitionTable[modeInfo.SubsetCount - 1][partition];
            byte[] fixUpTable = BC67Tables.FixUpIndices[modeInfo.SubsetCount - 1][partition];

            Span<byte> colorIndices = stackalloc byte[16];

            for (int i = 0; i < 16; i++)
            {
                byte subset = partitionTable[i];
                int bitCount = i == fixUpTable[subset] ? modeInfo.ColorIndexBitCount - 1 : modeInfo.ColorIndexBitCount;

                colorIndices[i] = (byte)block.Decode(ref offset, bitCount);
                Debug.Assert(colorIndices[i] < 16);
            }

            Span<byte> alphaIndices = stackalloc byte[16];

            if (modeInfo.AlphaIndexBitCount != 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    int bitCount = i != 0 ? modeInfo.AlphaIndexBitCount : modeInfo.AlphaIndexBitCount - 1;

                    alphaIndices[i] = (byte)block.Decode(ref offset, bitCount);
                    Debug.Assert(alphaIndices[i] < 16);
                }
            }

            for (int ty = 0; ty < h; ty++)
            {
                int baseOffs = ty * width;

                for (int tx = 0; tx < w; tx++)
                {
                    int i = ty * 4 + tx;

                    RgbaColor32 color;

                    byte subset = partitionTable[i];

                    RgbaColor32 color1 = endPoints[subset * 2];
                    RgbaColor32 color2 = endPoints[subset * 2 + 1];

                    if (modeInfo.AlphaIndexBitCount != 0)
                    {
                        if (indexMode == 0)
                        {
                            color = Interpolate(color1, color2, colorIndices[i], alphaIndices[i], modeInfo.ColorIndexBitCount, modeInfo.AlphaIndexBitCount);
                        }
                        else
                        {
                            color = Interpolate(color1, color2, alphaIndices[i], colorIndices[i], modeInfo.AlphaIndexBitCount, modeInfo.ColorIndexBitCount);
                        }
                    }
                    else
                    {
                        color = Interpolate(color1, color2, colorIndices[i], colorIndices[i], modeInfo.ColorIndexBitCount, modeInfo.ColorIndexBitCount);
                    }

                    if (rotation != 0)
                    {
                        int a = color.A;

                        switch (rotation)
                        {
                            case 1:
                                color.A = color.R;
                                color.R = a;
                                break;
                            case 2:
                                color.A = color.G;
                                color.G = a;
                                break;
                            case 3:
                                color.A = color.B;
                                color.B = a;
                                break;
                        }
                    }

                    RgbaColor8 color8 = color.GetColor8();

                    output[baseOffs + tx] = color8.ToUInt32();
                }
            }
        }

        private static RgbaColor32 Interpolate(
            RgbaColor32 color1,
            RgbaColor32 color2,
            int colorWeightIndex,
            int alphaWeightIndex,
            int colorIndexBitCount,
            int alphaIndexBitCount)
        {
            Debug.Assert(colorIndexBitCount >= 2 && colorIndexBitCount <= 4);
            Debug.Assert(alphaIndexBitCount >= 2 && alphaIndexBitCount <= 4);

            int colorWeight = BC67Tables.Weights[colorIndexBitCount - 2][colorWeightIndex];
            int alphaWeight = BC67Tables.Weights[alphaIndexBitCount - 2][alphaWeightIndex];

            RgbaColor32 weightV = new(colorWeight)
            {
                A = alphaWeight,
            };
            RgbaColor32 invWeightV = new RgbaColor32(64) - weightV;

            return (color1 * invWeightV + color2 * weightV + new RgbaColor32(32)) >> 6;
        }

        private static void Unquantize(ref RgbaColor32 color, int colorDepth, int alphaDepth, int pBit)
        {
            color.R = UnquantizeComponent(color.R, colorDepth, pBit);
            color.G = UnquantizeComponent(color.G, colorDepth, pBit);
            color.B = UnquantizeComponent(color.B, colorDepth, pBit);
            color.A = alphaDepth != 0 ? UnquantizeComponent(color.A, alphaDepth, pBit) : 255;
        }

        private static int UnquantizeComponent(int component, int bits, int pBit)
        {
            int shift = 8 - bits;
            int value = component << shift;

            if (pBit >= 0)
            {
                Debug.Assert(pBit <= 1);
                value |= value >> (bits + 1);
                value |= pBit << (shift - 1);
            }
            else
            {
                value |= value >> bits;
            }

            return value;
        }
    }
}
