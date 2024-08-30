using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MHS2ModTool.GameFileFormats
{
    unsafe struct Vertex
    {
        public Vector4 Position;
        public Vector4 Normal;
        public Vector4 Tangent;
        public Vector2 TexCoord0;
        public Vector2 TexCoord1;
        public Vector4 Color;
        public fixed byte Joints0[4];
        public fixed byte Joints1[4];
        public fixed float Weights0[4];
        public fixed float Weights1[4];

        public bool IsValidBone(int index)
        {
            if ((uint)index >= 4)
            {
                return false;
            }

            return (Joints0[index] != 0 && Weights0[index] > 0f)|| Weights0[index] >= (1f / byte.MaxValue);
        }
    }

    readonly struct EncodedVertexBufferSpan
    {
        public readonly uint Format;
        public readonly uint Stride;
        public readonly uint Offset;
        public readonly uint First;

        public EncodedVertexBufferSpan(uint format, uint stride, uint offset, uint first)
        {
            Format = format;
            Stride = stride;
            Offset = offset;
            First = first;
        }
    }

    internal class MTVertexBuffer
    {
        private byte[] _data;

        private uint _lastVertexFormat;
        private uint _lastVertexOffset;
        private uint _accumulatedVertexCount;

        public ReadOnlySpan<byte> Data => _data;

        public MTVertexBuffer()
        {
            _data = [];
        }

        public MTVertexBuffer(byte[] data)
        {
            _data = data;
        }

        public unsafe Vertex[] ReadVertices(
            VertexFormatAttributes attributes,
            uint baseOffset,
            uint count,
            uint stride,
            MTAABB modelAABB,
            int weightsPerVertex,
            int boneFirst)
        {
            using var ms = new MemoryStream(_data);
            using var reader = new BinaryReader(ms);

            Vertex[] output = new Vertex[count];

            bool positionQuantized = attributes.IsVertexPositionQuantized();

            Vector4 positionDelta = modelAABB.Max - modelAABB.Min;
            Vector4 positionBase = modelAABB.Min;

            for (int i = 0; i < count; i++)
            {
                ms.Seek(baseOffset + i * stride, SeekOrigin.Begin);

                Vertex vertex = new();

                int posIndex = 0;
                int normIndex = 0;
                int tanIndex = 0;
                int uv0Index = 0;
                int uv1Index = 0;
                int colorIndex = 0;
                int joints0Index = 0;
                int joints1Index = 0;
                int weights0Index = 0;
                int weights1Index = 0;
                float weightSum = 0f;

                foreach (VertexAttribute attr in attributes.Attributes)
                {
                    if (attr.Type == VertexComponentType.UnormRgb10A2)
                    {
                        uint packed = reader.ReadUInt32();

                        uint r = packed & 0x3ff;
                        uint g = (packed >> 10) & 0x3ff;
                        uint b = (packed >> 20) & 0x3ff;

                        float rFloat = r / 1023f;
                        float gFloat = g / 1023f;
                        float bFloat = b / 1023f;

                        rFloat = (rFloat * 2f) - 1f;
                        gFloat = (gFloat * 2f) - 1f;
                        bFloat = (bFloat * 2f) - 1f;

                        switch (attr.Name)
                        {
                            case VertexAttributeName.Normal:
                                vertex.Normal.X = rFloat;
                                vertex.Normal.Y = gFloat;
                                vertex.Normal.Z = bFloat;
                                break;
                            case VertexAttributeName.Tangent:
                                vertex.Tangent.X = rFloat;
                                vertex.Tangent.Y = gFloat;
                                vertex.Tangent.Z = bFloat;
                                break;
                        }
                    }
                    else
                    {
                        int ec = attr.ElementCount;

                        for (int j = 0; j < ec; j++)
                        {
                            switch (attr.Name)
                            {
                                case VertexAttributeName.Position:
                                    vertex.Position[posIndex++] = Read(reader, attr.Type);
                                    break;
                                case VertexAttributeName.Normal:
                                    vertex.Normal[normIndex++] = Read(reader, attr.Type);
                                    break;
                                case VertexAttributeName.Tangent:
                                    vertex.Tangent[tanIndex++] = Read(reader, attr.Type);
                                    break;
                                case VertexAttributeName.TexCoord0:
                                    vertex.TexCoord0[uv0Index++] = Read(reader, attr.Type);
                                    break;
                                case VertexAttributeName.TexCoord1:
                                    vertex.TexCoord1[uv1Index++] = Read(reader, attr.Type);
                                    break;
                                case VertexAttributeName.Color:
                                    vertex.Color[colorIndex++] = Read(reader, attr.Type);
                                    break;
                                case VertexAttributeName.Joints0:
                                    vertex.Joints0[joints0Index++] = ReadInteger(reader, attr.Type);
                                    break;
                                case VertexAttributeName.Joints1:
                                    vertex.Joints1[joints1Index++] = ReadInteger(reader, attr.Type);
                                    break;
                                case VertexAttributeName.Weights0:
                                    float weight0 = Read(reader, attr.Type);
                                    vertex.Weights0[weights0Index++] = weight0;
                                    weightSum += weight0;
                                    break;
                                case VertexAttributeName.Weights1:
                                    float weight1 = Read(reader, attr.Type);
                                    vertex.Weights1[weights1Index++] = weight1;
                                    weightSum += weight1;
                                    break;
                                default:
                                    Skip(ms, attr.Type);
                                    break;
                            }
                        }
                    }
                }

                if (weights0Index < joints0Index)
                {
                    vertex.Weights0[weights0Index] = 1f - weightSum;
                }
                else if (weights1Index < joints1Index)
                {
                    vertex.Weights1[weights1Index] = 1f - weightSum;
                }

                for (int j = 0; j < weightsPerVertex; j++)
                {
                    vertex.Joints0[j] = (byte)(vertex.Joints0[j] + boneFirst);
                }

                if (positionQuantized)
                {
                    vertex.Position = (vertex.Position * positionDelta) + positionBase;
                }

                output[i] = vertex;
            }

            return output;
        }

        private static float Read(BinaryReader reader, VertexComponentType type)
        {
            return type switch
            {
                VertexComponentType.UInt8 => reader.ReadByte(),
                VertexComponentType.UInt16 => reader.ReadUInt16(),
                VertexComponentType.UInt32 => reader.ReadUInt32(),
                VertexComponentType.SInt8 => reader.ReadSByte(),
                VertexComponentType.SInt16 => reader.ReadInt16(),
                VertexComponentType.SInt32 => reader.ReadInt32(),
                VertexComponentType.Float16 => (float)reader.ReadHalf(),
                VertexComponentType.Float32 => reader.ReadSingle(),
                VertexComponentType.UNorm8 => reader.ReadByte() * (1f / byte.MaxValue),
                VertexComponentType.UNorm16 => reader.ReadUInt16() * (1f / ushort.MaxValue),
                VertexComponentType.SNorm8 => MathF.Max(reader.ReadSByte() * (1f / sbyte.MaxValue), -1f),
                VertexComponentType.SNorm16 => MathF.Max(reader.ReadInt16() * (1f / short.MaxValue), -1f),
                _ => throw new ArgumentException("Invalid type " + type),
            };
        }

        private static byte ReadInteger(BinaryReader reader, VertexComponentType type)
        {
            return type switch
            {
                VertexComponentType.UInt8 => reader.ReadByte(),
                VertexComponentType.UInt16 => (byte)reader.ReadUInt16(),
                VertexComponentType.UInt32 => (byte)reader.ReadUInt32(),
                VertexComponentType.SInt8 => (byte)reader.ReadSByte(),
                VertexComponentType.SInt16 => (byte)reader.ReadInt16(),
                VertexComponentType.SInt32 => (byte)reader.ReadInt32(),
                VertexComponentType.Float16 => (byte)reader.ReadHalf(),
                _ => (byte)Read(reader, type),
            };
        }

        private static void Skip(Stream stream, VertexComponentType type)
        {
            int length = type switch
            {
                VertexComponentType.UInt8 or VertexComponentType.UNorm8 => sizeof(byte),
                VertexComponentType.UInt16 or VertexComponentType.UNorm16 => sizeof(ushort),
                VertexComponentType.UInt32 or VertexComponentType.UnormRgb10A2 => sizeof(uint),
                VertexComponentType.SInt8 or VertexComponentType.SNorm8 => sizeof(sbyte),
                VertexComponentType.SInt16 or VertexComponentType.SNorm16 => sizeof(short),
                VertexComponentType.SInt32 => sizeof(int),
                VertexComponentType.Float16 => Unsafe.SizeOf<Half>(),
                VertexComponentType.Float32 => sizeof(float),
                _ => throw new ArgumentException("Invalid type " + type),
            };

            stream.Seek(length, SeekOrigin.Current);
        }

        public EncodedVertexBufferSpan WriteVertices(ReadOnlySpan<Vertex> input, VertexFormatFlags formatFlags, byte weightsPerVertex, bool isBridge, MTAABB modelAABB)
        {
            uint vertexFormat = MTVertexFormatTable.SelectOptimalVertexFormat(formatFlags, weightsPerVertex, isBridge, out uint vertexStride);

            bool success = MTVertexFormatTable.TryGetVertexAttributes(vertexFormat, out var attributes);
            Debug.Assert(success);

            byte[] encoded = Encode(input, attributes, modelAABB);
            int oldLength = _data.Length;

            Array.Resize(ref _data, oldLength + encoded.Length);
            Array.Copy(encoded, 0, _data, oldLength, encoded.Length);
            
            if (vertexFormat == _lastVertexFormat)
            {
                uint first = _accumulatedVertexCount;
                _accumulatedVertexCount += (uint)input.Length;

                return new EncodedVertexBufferSpan(vertexFormat, vertexStride, _lastVertexOffset, first);
            }
            else
            {
                _accumulatedVertexCount = (uint)input.Length;
                _lastVertexOffset = (uint)oldLength;
                _lastVertexFormat = vertexFormat;

                return new EncodedVertexBufferSpan(vertexFormat, vertexStride, (uint)oldLength, 0);
            }
        }

        private static unsafe uint GetWeightsPerVertex(ReadOnlySpan<Vertex> input)
        {
            uint weightsCount = 0;

            for (int i = 0; i < input.Length; i++)
            {
                Vertex vertex = input[i];

                for (uint j = 0; j < 4; j++)
                {
                    if (vertex.Weights0[j] > 0f)
                    {
                        weightsCount = Math.Max(weightsCount, j + 1);
                    }

                    if (vertex.Weights1[j] > 0f)
                    {
                        weightsCount = Math.Max(weightsCount, j + 5);
                    }
                }
            }

            return weightsCount;
        }

        public unsafe byte[] Encode(ReadOnlySpan<Vertex> input, VertexFormatAttributes attributes, MTAABB modelAABB)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            bool positionQuantized = attributes.IsVertexPositionQuantized();

            Vector4 positionDeltaInv = (modelAABB.Max - modelAABB.Min).Reciprocal();
            Vector4 positionBase = modelAABB.Min;

            for (int i = 0; i < input.Length; i++)
            {
                Vertex vertex = input[i];

                Vector4 position = vertex.Position;

                if (positionQuantized)
                {
                    position = (position - positionBase) * positionDeltaInv;
                }

                int posIndex = 0;
                int normIndex = 0;
                int tanIndex = 0;
                int uv0Index = 0;
                int uv1Index = 0;
                int colorIndex = 0;
                int joints0Index = 0;
                int joints1Index = 0;
                int weights0Index = 0;
                int weights1Index = 0;

                foreach (VertexAttribute attr in attributes.Attributes)
                {
                    if (attr.Type == VertexComponentType.UnormRgb10A2)
                    {
                        float rFloat = 0f;
                        float gFloat = 0f;
                        float bFloat = 0f;

                        switch (attr.Name)
                        {
                            case VertexAttributeName.Normal:
                                rFloat = vertex.Normal.X;
                                gFloat = vertex.Normal.Y;
                                bFloat = vertex.Normal.Z;
                                break;
                            case VertexAttributeName.Tangent:
                                rFloat = vertex.Tangent.X;
                                gFloat = vertex.Tangent.Y;
                                bFloat = vertex.Tangent.Z;
                                break;
                        }

                        rFloat = (rFloat + 1f) * 0.5f;
                        gFloat = (gFloat + 1f) * 0.5f;
                        bFloat = (bFloat + 1f) * 0.5f;

                        uint r = Math.Clamp((uint)MathF.Round(rFloat * 1023), 0, 1023);
                        uint g = Math.Clamp((uint)MathF.Round(gFloat * 1023), 0, 1023);
                        uint b = Math.Clamp((uint)MathF.Round(bFloat * 1023), 0, 1023);

                        uint packed = r | (g << 10) | (b << 20) | (3u << 30);

                        writer.Write(packed);
                    }
                    else
                    {
                        int ec = attr.ElementCount;

                        for (int j = 0; j < ec; j++)
                        {
                            switch (attr.Name)
                            {
                                case VertexAttributeName.Position:
                                    Write(writer, attr.Type, position[posIndex++]);
                                    break;
                                case VertexAttributeName.Normal:
                                    Write(writer, attr.Type, vertex.Normal[normIndex++]);
                                    break;
                                case VertexAttributeName.Tangent:
                                    Write(writer, attr.Type, vertex.Tangent[tanIndex++]);
                                    break;
                                case VertexAttributeName.TexCoord0:
                                    Write(writer, attr.Type, vertex.TexCoord0[uv0Index++]);
                                    break;
                                case VertexAttributeName.TexCoord1:
                                    Write(writer, attr.Type, vertex.TexCoord1[uv1Index++]);
                                    break;
                                case VertexAttributeName.Color:
                                    Write(writer, attr.Type, vertex.Color[colorIndex++]);
                                    break;
                                case VertexAttributeName.Joints0:
                                    WriteInteger(writer, attr.Type, vertex.Joints0[joints0Index++]);
                                    break;
                                case VertexAttributeName.Joints1:
                                    WriteInteger(writer, attr.Type, vertex.Joints1[joints1Index++]);
                                    break;
                                case VertexAttributeName.Weights0:
                                    Write(writer, attr.Type, vertex.Weights0[weights0Index++]);
                                    break;
                                case VertexAttributeName.Weights1:
                                    
                                    Write(writer, attr.Type, vertex.Weights1[weights1Index++]);
                                    break;
                                default:
                                    Skip(ms, attr.Type);
                                    break;
                            }
                        }
                    }
                }
            }

            return ms.ToArray();
        }

        private static void Write(BinaryWriter writer, VertexComponentType type, float value)
        {
            switch (type)
            {
                case VertexComponentType.UInt8:
                    writer.Write((byte)value);
                    break;
                case VertexComponentType.UInt16:
                    writer.Write((ushort)value);
                    break;
                case VertexComponentType.UInt32:
                    writer.Write((uint)value);
                    break;
                case VertexComponentType.SInt8:
                    writer.Write((sbyte)value);
                    break;
                case VertexComponentType.SInt16:
                    writer.Write((short)value);
                    break;
                case VertexComponentType.SInt32:
                    writer.Write((int)value);
                    break;
                case VertexComponentType.Float16:
                    writer.Write((Half)value);
                    break;
                case VertexComponentType.Float32:
                    writer.Write(value);
                    break;
                case VertexComponentType.UNorm8:
                    writer.Write((byte)Math.Clamp(MathF.Round(value * byte.MaxValue), byte.MinValue, byte.MaxValue));
                    break;
                case VertexComponentType.UNorm16:
                    writer.Write((ushort)Math.Clamp(MathF.Round(value * ushort.MaxValue), ushort.MinValue, ushort.MaxValue));
                    break;
                case VertexComponentType.SNorm8:
                    writer.Write((sbyte)Math.Clamp(MathF.Round(value * sbyte.MaxValue), sbyte.MinValue, sbyte.MaxValue));
                    break;
                case VertexComponentType.SNorm16:
                    writer.Write((short)Math.Clamp(MathF.Round(value * short.MaxValue), short.MinValue, short.MaxValue));
                    break;
                default:
                    throw new ArgumentException("Invalid type " + type);
            }
        }

        private static void WriteInteger(BinaryWriter writer, VertexComponentType type, byte value)
        {
            switch (type)
            {
                case VertexComponentType.UInt8:
                    writer.Write(value);
                    break;
                case VertexComponentType.UInt16:
                    writer.Write((ushort)value);
                    break;
                case VertexComponentType.UInt32:
                    writer.Write((uint)value);
                    break;
                case VertexComponentType.SInt8:
                    writer.Write((sbyte)value);
                    break;
                case VertexComponentType.SInt16:
                    writer.Write((short)value);
                    break;
                case VertexComponentType.SInt32:
                    writer.Write((int)value);
                    break;
                case VertexComponentType.Float16:
                    writer.Write((Half)value);
                    break;
                default:
                    Write(writer, type, value);
                    break;
            }
        }
    }
}
