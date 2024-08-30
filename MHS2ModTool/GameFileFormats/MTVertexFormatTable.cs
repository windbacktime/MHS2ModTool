using System.Runtime.CompilerServices;

namespace MHS2ModTool.GameFileFormats
{
    enum VertexAttributeName
    {
        DontCare,
        Position,
        Normal,
        Tangent,
        TexCoord0,
        TexCoord1,
        Color,
        Joints0,
        Joints1,
        Weights0,
        Weights1
    }

    enum VertexComponentType
    {
        Float32 = 1,
        Float16,
        SInt16,
        UInt16,
        SNorm16,
        UNorm16,
        SInt8,
        UInt8,
        SNorm8,
        UNorm8,
        UnormRgb10A2 = 0xc,
        SInt32 = 0xf,
        UInt32
    }

    readonly struct VertexAttribute
    {
        public readonly VertexAttributeName Name;
        public readonly VertexComponentType Type;
        public readonly byte ElementCount;
        public readonly byte RelativeOffset;

        public VertexAttribute(VertexAttributeName name, VertexComponentType type, byte elementCount, byte relativeOffset)
        {
            Type = type;
            Name = name;
            ElementCount = elementCount;
            RelativeOffset = relativeOffset;
        }

        public uint CalculateStride()
        {
            if (Type == VertexComponentType.UnormRgb10A2)
            {
                return sizeof(uint);
            }

            uint typeLength = Type switch
            {
                VertexComponentType.UInt8 or VertexComponentType.UNorm8 => sizeof(byte),
                VertexComponentType.UInt16 or VertexComponentType.UNorm16 => sizeof(ushort),
                VertexComponentType.UInt32 => sizeof(uint),
                VertexComponentType.SInt8 or VertexComponentType.SNorm8 => sizeof(sbyte),
                VertexComponentType.SInt16 or VertexComponentType.SNorm16 => sizeof(short),
                VertexComponentType.SInt32 => sizeof(int),
                VertexComponentType.Float16 => (uint)Unsafe.SizeOf<Half>(),
                VertexComponentType.Float32 => sizeof(float),
                _ => throw new InvalidOperationException("Invalid type " + Type),
            };

            return typeLength * ElementCount;
        }
    }

    [Flags]
    enum VertexFormatFlags
    {
        None = 0,
        HasPosition = 1 << 0,
        HasNormal = 1 << 1,
        HasTangent = 1 << 2,
        HasTexCoord0 = 1 << 3,
        HasTexCoord1 = 1 << 4,
        HasColor = 1 << 5,
        HasJoints0 = 1 << 6,
        HasJoints1 = 1 << 7,
    }

    readonly struct VertexFormatAttributes
    {
        public readonly string InputAssemblerName;
        public readonly IReadOnlyList<VertexAttribute> Attributes;

        public VertexFormatAttributes(string inputAssemblerName, params VertexAttribute[] attributes)
        {
            InputAssemblerName = inputAssemblerName;
            Attributes = attributes;
        }

        public uint CalculateStride()
        {
            return (uint)Attributes.Sum(attr => attr.CalculateStride());
        }

        public uint GetBonesCount()
        {
            return (uint)Attributes
                .Where(attr => attr.Name == VertexAttributeName.Joints0 || attr.Name == VertexAttributeName.Joints1)
                .Sum(attr => attr.ElementCount);
        }

        public VertexFormatFlags GetVertexFormatFlags()
        {
            VertexFormatFlags flags = VertexFormatFlags.None;

            foreach (VertexAttribute attr in Attributes)
            {
                switch (attr.Name)
                {
                    case VertexAttributeName.Position:
                        flags |= VertexFormatFlags.HasPosition;
                        break;
                    case VertexAttributeName.Normal:
                        flags |= VertexFormatFlags.HasNormal;
                        break;
                    case VertexAttributeName.Tangent:
                        flags |= VertexFormatFlags.HasTangent;
                        break;
                    case VertexAttributeName.TexCoord0:
                        flags |= VertexFormatFlags.HasTexCoord0;
                        break;
                    case VertexAttributeName.TexCoord1:
                        flags |= VertexFormatFlags.HasTexCoord1;
                        break;
                    case VertexAttributeName.Color:
                        flags |= VertexFormatFlags.HasColor;
                        break;
                    case VertexAttributeName.Joints0:
                        flags |= VertexFormatFlags.HasJoints0;
                        break;
                    case VertexAttributeName.Joints1:
                        flags |= VertexFormatFlags.HasJoints1;
                        break;
                }
            }

            return flags;
        }

        public bool IsVertexPositionQuantized()
        {
            return Attributes.Any(attr => attr.Name == VertexAttributeName.Position && attr.Type == VertexComponentType.SNorm16);
        }
    }

    static class MTVertexFormatTable
    {
        private const int MinOptimalSearchId = 0x17;
        private const int MaxOptimalSearchId = 0x3D;

        private const int MinBridgeOptimalSearchId = 0x12;
        private const int MaxBridgeOptimalSearchId = 0x16;

        private static VertexFormatAttributes[] s_VertexFormats =
        [
            new("IASystemCopy", // 0x1
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 4, 0)),
            new("IASystemClear", // 0x2
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 3, 16)),
            new("IAFilter", // 0x3
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float32, 2, 16),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 24)),
            new("IAFilter0", // 0x4
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 2, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 8)),
            new("IAFilter1", // 0x5
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 16)),
            new("IAFilter2", // 0x6
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 4, 0)),
            new("IADevelopPrim2D", // 0x7
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 2, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 2, 12)),
            new("IADevelopPrim3D", // 0x8
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 2, 16)),
            new("IACollisionInput", // 0x9
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12)),
            new("IASwing", // 0xA
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.UNorm8, 4, 0)),
            new("IASwing2", // 0xB
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.UNorm8, 4, 0)),
            new("IASwingHighPrecision", // 0xC
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0)),
            new("IASwing2HighPrecision", // 0xD
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0)),
            new("IAInstancing", // 0xE
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 32),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 48)),
            new("IALatticeDeform", // 0xF
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 32),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 48)),
            new("IATetraDeform", // 0x10
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16)),
            new("IATetraDeform2", // 0x11
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 8),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 16)),
            new("IASkinBridge1wt", // 0x12
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 8)),
            new("IASkinBridge2wt", // 0x13
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 8),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt16, 2, 12)),
            new("IASkinBridge4wt", // 0x14
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 8),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 16)),
            new("IASkinBridge8wt", // 0x15
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 8),
                new VertexAttribute(VertexAttributeName.Joints1, VertexComponentType.UInt8, 4, 12),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.UNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.Weights1, VertexComponentType.UNorm8, 4, 20),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 24)),
            new("IASkinBridge4wt4M", // 0x16
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 8),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 28),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 36),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 44),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 48),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 52)),
            new("IASkinTB1wt", // 0x17
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16)),
            new("IASkinTBN1wt", // 0x18
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20)),
            new("IASkinTBC1wt", // 0x19
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 20)),
            new("IASkinTBNLA1wt", // 0x1A
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 24)),
            new("IASkinTB2wt", // 0x1B
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.Float16, 2, 20)),
            new("IASkinTBN2wt", // 0x1C
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.Float16, 2, 24)),
            new("IASkinTBC2wt", // 0x1D
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 24)),
            new("IASkinTBNLA2wt", // 0x1E
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 24),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.Float16, 2, 32)),
            new("IASkinTB4wt", // 0x1F
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 24)),
            new("IASkinTBN4wt", // 0x20
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 28)),
            new("IASkinTBC4wt", // 0x21
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 28)),
            new("IASkinTBNLA4wt", // 0x22
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 28),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 36)),
            new("IASkinTB8wt", // 0x23
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Weights1, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.Joints1, VertexComponentType.UInt8, 4, 20),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 28),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 32)),
            new("IASkinTBN8wt", // 0x24
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Weights1, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.Joints1, VertexComponentType.UInt8, 4, 20),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 28),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 32),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 36)),
            new("IASkinTBC8wt", // 0x25
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Weights1, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.Joints1, VertexComponentType.UInt8, 4, 20),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 28),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 32),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 36)),
            new("IASkinTBNLA8wt", // 0x26
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 3, 0),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.SNorm16, 1, 6),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Weights1, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Joints0, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.Joints1, VertexComponentType.UInt8, 4, 20),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 28),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 32),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 36),
                new VertexAttribute(VertexAttributeName.Weights0, VertexComponentType.Float16, 2, 44)),
            new("IANonSkinTB", // 0x27
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20)),
            new("IANonSkinTBC", // 0x28
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 24)),
            new("IANonSkinTBL", // 0x29
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 28)),
            new("IANonSkinTBL_LA", // 0x2A
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 28)),
            new("IANonSkinTBN", // 0x2B
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24)),
            new("IANonSkinTBA", // 0x2C
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24)),
            new("IANonSkinTBNC", // 0x2D
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 28)),
            new("IANonSkinTBNL", // 0x2E
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 28),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 32)),
            new("IANonSkinTBNL_LA", // 0x2F
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 28),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 32)),
            new("IANonSkinTBNA", // 0x30
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 28)),
            new("IANonSkinTBLA", // 0x31
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 28)),
            new("IANonSkinTBCA", // 0x32
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 28)),
            new("IANonSkinTBNCA", // 0x33
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 28),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 32)),
            new("IANonSkinTBNLA", // 0x34
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 28)),
            new("IANonSkinB", // 0x35
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16)),
            new("IANonSkinBC", // 0x36
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 20)),
            new("IANonSkinBL", // 0x37
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20)),
            new("IANonSkinBL_LA", // 0x38
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20)),
            new("IANonSkinBA", // 0x39
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20)),
            new("IANonSkinBLA", // 0x3A
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 24)),
            new("IANonSkinBCA", // 0x3B
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 12),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 16),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 24)),
            new("IASkinOTB_4WT_4M", // 0x3C
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 28),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 36),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 44),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 52),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 56),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 60)),
            new("IANonSkinTBN_4M", // 0x3D
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.Tangent, VertexComponentType.SNorm8, 4, 16),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 20),
                new VertexAttribute(VertexAttributeName.TexCoord1, VertexComponentType.Float16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 28),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 36),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 44),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 52),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 56),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm8, 4, 60)),
            new("IASkinVelocytyEdge", // 0x3E
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.UnormRgb10A2, 3, 8),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 4, 12)),
            new("IADualParaboloid", // 0x3F
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 16)),
            new("IA_DeferredLighting_LightVolume", // 0x40
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0)),
            new("IAPrimitiveCloudBillboard", // 0x41
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 4, 24)),
            new("IAPrimitiveCloud", // 0x42
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 3, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 28)),
            new("IAPrimitiveSprite", // 0x43
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 4, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt8, 4, 28)),
            new("IAPrimitiveNT", // 0x44
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 4, 24)),
            new("IAPrimitivePolyline", // 0x45
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt8, 4, 28)),
            new("IAPrimitivePolygon", // 0x46
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt8, 4, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt8, 4, 28)),
            new("IACubeMapFilter", // 0x47
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 16)),
            new("IAGSDOFFilter", // 0x48
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 0)),
            new("IABokeh", // 0x49
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 0)),
            new("IAWater", // 0x4A
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.TexCoord0, VertexComponentType.Float16, 2, 12)),
            new("IAWaterRipple", // 0x4B
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 16)),
            new("IAGUI", // 0x4C
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 16)),
            new("IATextureBlend", // 0x4D
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 3, 16)),
            new("IAGPUParticle", // 0x4E
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SNorm16, 2, 28),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 32)),
            new("IAGPULineParticle", // 0x4F
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 32)),
            new("IAGPUPolylineParticle", // 0x50
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt8, 4, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 28)),
            new("IALightShaftInput", // 0x51
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0)),
            new("IAGrass", // 0x52
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 0),
                new VertexAttribute(VertexAttributeName.Normal, VertexComponentType.SNorm8, 4, 4),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UNorm8, 4, 8)),
            new("IAGrassHicomp", // 0x53
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UNorm8, 4, 4)),
            new("IAGrassHicomp2", // 0x54
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UNorm8, 4, 4)),
            new("IAGrassPoint", // 0x55
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0)),
            new("IAGrassLowest", // 0x56
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 0)),
            new("IAGrassSPU", // 0x57
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 32),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 48),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 64),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 80),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 96)),
            new("IAGrassOutsourcing", // 0x58
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 0)),
            new("IAGrassOutsourcingF32", // 0x59
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0)),
            new("IAMirage", // 0x5A
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 16)),
            new("IASimWaterForViewInput", // 0x5B
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 32)),
            new("IASoftBodyQuad", // 0x5C
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 0)),
            new("IASoftBodyVertex", // 0x5D
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 0)),
            new("IASoftBodyDecouple", // 0x5E
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 8),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 16)),
            new("IASoftBodyVertexNoVTF", // 0x5F
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 12)),
            new("IASoftBodyVertexPS3", // 0x60
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16)),
            new("IASoftBodyDecoupleNoVTF", // 0x61
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 32),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 36),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 44),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 52),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 60),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 68)),
            new("IATattooBlend2D", // 0x62
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 3, 16)),
            new("IABuilder", // 0x63
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.Float32, 3, 0),
                new VertexAttribute(VertexAttributeName.Color, VertexComponentType.UNorm8, 4, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16)),
            new("IASky", // 0x64
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 0)),
            new("IAAstralBody", // 0x65
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 1, 16)),
            new("IASkyStar", // 0x66
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 16)),
            new("IAAmbientShadow", // 0x67
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0)),
            new("IAVertexIndexF32", // 0x68
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 0)),
            new("IAVertexIndexF16", // 0x69
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 0)),
            new("IATriangleF32", // 0x6A
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 16)),
            new("IATriangleF16", // 0x6B
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float16, 2, 8)),
            new("IAInfParticle", // 0x6C
                new VertexAttribute(VertexAttributeName.Position, VertexComponentType.SNorm16, 4, 0),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UNorm8, 4, 8),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.Float32, 2, 12),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.SInt16, 2, 20),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt8, 4, 24),
                new VertexAttribute(VertexAttributeName.DontCare, VertexComponentType.UInt16, 2, 28)),
        ];

        public static uint SelectOptimalVertexFormat(VertexFormatFlags formatFlags, uint weightsPerVertex, bool isBridge, out uint stride)
        {
            int bestCandidateId = 0;
            uint bestCandidateStride = uint.MaxValue;

            int minId, maxId;

            if (isBridge)
            {
                minId = MinBridgeOptimalSearchId;
                maxId = MaxBridgeOptimalSearchId;
            }
            else
            {
                minId = MinOptimalSearchId;
                maxId = MaxOptimalSearchId;
            }

            for (int id = minId - 1; id <= maxId - 1; id++)
            {
                VertexFormatAttributes attrs = s_VertexFormats[id];

                VertexFormatFlags candidateFormatFlags = attrs.GetVertexFormatFlags();
                uint bonesCount = attrs.GetBonesCount();

                if ((formatFlags & candidateFormatFlags) == formatFlags &&
                    bonesCount >= weightsPerVertex &&
                    (weightsPerVertex > 0 || bonesCount == 0))
                {
                    uint currentStride = attrs.CalculateStride();

                    if (currentStride < bestCandidateStride)
                    {
                        bestCandidateId = id;
                        bestCandidateStride = currentStride;
                    }
                }
            }

            stride = bestCandidateStride;

            return (HashUtils.CalculateCrc32(s_VertexFormats[bestCandidateId].InputAssemblerName) << 12) | ((uint)bestCandidateId + 1);
        }

        public static bool TryGetVertexAttributes(uint vertexFormat, out VertexFormatAttributes attributes)
        {
            uint index = vertexFormat & 0xfff;
            index--;

            if (index >= s_VertexFormats.Length)
            {
                attributes = default;
                return false;
            }

            attributes = s_VertexFormats[index];
            return true;
        }

        public static bool TryGetVertexFormatInputAssembler(uint vertexFormat, out string? inputAssembler)
        {
            uint index = vertexFormat & 0xfff;
            index--;

            if (index >= s_VertexFormats.Length)
            {
                inputAssembler = null;
                return false;
            }

            inputAssembler = s_VertexFormats[index].InputAssemblerName;
            return true;
        }
    }
}
