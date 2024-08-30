using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace MHS2ModTool.GameFileFormats
{
    enum MTPrimitiveType : byte
    {
        Points,
        Lines,
        LineStrip,
        Triangles,
        TriangleStrip
    }

    enum NsPrimitiveType
    {
        Points,
        Lines,
        Triangles
    }

    readonly struct EncodedIndexBufferSpan
    {
        public readonly uint Offset;
        public readonly uint Count;

        public EncodedIndexBufferSpan(uint offset, uint count)
        {
            Offset = offset;
            Count = count;
        }
    }

    internal class MTIndexBuffer
    {
        readonly ref struct EncodedIndexBuffer
        {
            public readonly ReadOnlySpan<byte> Data;
            public readonly uint Count;

            public EncodedIndexBuffer(ReadOnlySpan<byte> data, uint count)
            {
                Data = data;
                Count = count;
            }
        }

        private byte[] _data;

        public ReadOnlySpan<byte> Data => _data;

        public uint IndexCount => (uint)(_data.Length / sizeof(ushort));

        public MTIndexBuffer()
        {
            _data = [];
        }

        public MTIndexBuffer(byte[] data)
        {
            _data = data;
        }

        public ushort[] ReadIndicesAsNonStrip(MTPrimitiveType primitiveType, uint baseOffset, uint count, uint vertexStart = 0)
        {
            return primitiveType switch
            {
                MTPrimitiveType.Points => ReadIndices(baseOffset, count, vertexStart),
                MTPrimitiveType.Lines => ReadIndices(baseOffset, count, vertexStart),
                MTPrimitiveType.LineStrip => ReadIndicesLineStripAsTriangles(baseOffset, count, vertexStart),
                MTPrimitiveType.Triangles => ReadIndices(baseOffset, count, vertexStart),
                _ => ReadIndicesTriangleStripAsTriangles(baseOffset, count, vertexStart),
            };
        }

        private ushort[] ReadIndices(uint baseOffset, uint count, uint vertexStart)
        {
            using var ms = new MemoryStream(_data);
            using var reader = new BinaryReader(ms);

            ms.Seek(baseOffset, SeekOrigin.Begin);

            var indices = new ushort[count];

            for (uint i = 0; i < count; i++)
            {
                ushort index = reader.ReadUInt16();
                ushort indexOffseted = (ushort)(index - vertexStart);

                indices[i] = indexOffseted;
            }

            return indices;
        }

        private ushort[] ReadIndicesLineStripAsTriangles(uint baseOffset, uint count, uint vertexStart)
        {
            using var ms = new MemoryStream(_data);
            using var reader = new BinaryReader(ms);

            ms.Seek(baseOffset, SeekOrigin.Begin);

            List<ushort> indices = [];

            ushort prevIndex = 0;

            for (uint i = 0, stripIndex = 0; i < count; i++)
            {
                ushort index = reader.ReadUInt16();
                ushort indexOffseted = (ushort)(index - vertexStart);

                if (index == ushort.MaxValue)
                {
                    stripIndex = 0;
                }
                else if (stripIndex > 1)
                {
                    uint lineIndex = stripIndex / 2;

                    indices.Add(prevIndex);
                    indices.Add(indexOffseted);

                    stripIndex += 2;
                }
                else
                {
                    indices.Add(indexOffseted);

                    stripIndex++;
                }

                prevIndex = indexOffseted;
            }

            return [.. indices];
        }

        private ushort[] ReadIndicesTriangleStripAsTriangles(uint baseOffset, uint count, uint vertexStart)
        {
            using var ms = new MemoryStream(_data);
            using var reader = new BinaryReader(ms);

            ms.Seek(baseOffset, SeekOrigin.Begin);

            List<ushort> indices = [];

            ushort prevIndex1 = 0;
            ushort prevIndex2 = 0;

            for (uint i = 0, stripIndex = 0; i < count; i++)
            {
                ushort index = reader.ReadUInt16();
                ushort indexOffseted = (ushort)(index - vertexStart);

                if (index == ushort.MaxValue)
                {
                    stripIndex = 0;
                }
                else if (stripIndex > 2)
                {
                    if (!IsDegenerateTriangle(prevIndex1, prevIndex2, indexOffseted))
                    {
                        uint triangleIndex = stripIndex / 3;

                        if ((triangleIndex & 1) != 0)
                        {
                            indices.Add(prevIndex1);
                            indices.Add(prevIndex2);
                            indices.Add(indexOffseted);
                        }
                        else
                        {
                            indices.Add(prevIndex2);
                            indices.Add(prevIndex1);
                            indices.Add(indexOffseted);
                        }
                    }

                    stripIndex += 3;

                    prevIndex2 = prevIndex1;
                    prevIndex1 = indexOffseted;
                }
                else
                {
                    ushort index2 = (ushort)(reader.ReadUInt16() - vertexStart);
                    ushort index3 = (ushort)(reader.ReadUInt16() - vertexStart);

                    if (!IsDegenerateTriangle(indexOffseted, index2, index3))
                    {
                        indices.Add(indexOffseted);
                        indices.Add(index2);
                        indices.Add(index3);
                    }

                    stripIndex += 3;
                    i += 2;

                    prevIndex2 = index2;
                    prevIndex1 = index3;
                }
            }

            return [.. indices];
        }

        public EncodedIndexBufferSpan WriteIndicesAsStrip(ReadOnlySpan<ushort> indices, NsPrimitiveType primitiveType, uint vertexStart = 0)
        {
            var encoded = primitiveType switch
            {
                NsPrimitiveType.Points => EncodeIndices(indices, vertexStart),
                NsPrimitiveType.Lines => EncodeIndicesLinesAsLineStrip(indices, vertexStart),
                NsPrimitiveType.Triangles => EncodeIndicesTrianglesAsTriangleStrip(indices, vertexStart),
                _ => throw new ArgumentOutOfRangeException(nameof(primitiveType))
            };

            int oldLength = _data.Length;
            Array.Resize(ref _data, oldLength + encoded.Data.Length);
            encoded.Data.CopyTo(_data.AsSpan()[oldLength..]);

            return new((uint)oldLength, encoded.Count);
        }

        private static EncodedIndexBuffer EncodeIndices(ReadOnlySpan<ushort> indices, uint vertexStart)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            for (int i = 0; i < indices.Length; i++)
            {
                ushort index = (ushort)(indices[i] + vertexStart);

                writer.Write(index);
            }

            return new(ms.ToArray(), (uint)indices.Length);
        }

        private static EncodedIndexBuffer EncodeIndicesLinesAsLineStrip(ReadOnlySpan<ushort> indices, uint vertexStart)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            ushort prevIndex = 0;
            uint stripIndex = 0;

            for (int i = 0; i < indices.Length; i++, stripIndex++)
            {
                ushort index = (ushort)(indices[i] + vertexStart);
                ushort newIndex = index;

                if (stripIndex > 1 && i + 1 < indices.Length)
                {
                    ushort index2 = (ushort)(indices[i + 1] + vertexStart);
                    uint lineIndex = stripIndex / 2;

                    if (prevIndex == index)
                    {
                        writer.Write(index2);
                        newIndex = index2;

                        stripIndex += 2;
                        i += 2;
                    }
                    else
                    {
                        writer.Write(ushort.MaxValue);
                        writer.Write(index);

                        stripIndex = 0;
                    }
                }
                else
                {
                    writer.Write(index);
                }

                prevIndex = newIndex;
            }

            uint indexCount = (uint)(ms.Length / sizeof(ushort));

            writer.Write(ushort.MaxValue);

            return new(ms.ToArray(), indexCount);
        }

        private static EncodedIndexBuffer EncodeIndicesTrianglesAsTriangleStrip(ReadOnlySpan<ushort> indices, uint vertexStart)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            ushort prevIndex1 = 0;
            ushort prevIndex2 = 0;
            uint stripIndex = 0;
            bool forceRestart = false;

            for (int i = 0; i < indices.Length; i++, stripIndex++)
            {
                ushort index = (ushort)(indices[i] + vertexStart);
                ushort newIndex = index;

                if (stripIndex > 2 && i + 2 < indices.Length)
                {
                    ushort index2 = (ushort)(indices[i + 1] + vertexStart);
                    ushort index3 = (ushort)(indices[i + 2] + vertexStart);
                    uint triangleIndex = stripIndex / 3;

                    bool validStrip;

                    if ((triangleIndex & 1) != 0)
                    {
                        validStrip = prevIndex1 == index && prevIndex2 == index2;
                    }
                    else
                    {
                        validStrip = prevIndex2 == index && prevIndex1 == index2;
                    }

                    if (validStrip & !forceRestart)
                    {
                        writer.Write(index3);
                        newIndex = index3;

                        stripIndex += 2;
                        i += 2;
                    }
                    else
                    {
                        if (i > 6 && stripIndex == 3)
                        {
                            ushort i1 = (ushort)(indices[i - 5] + vertexStart);
                            ushort i2 = (ushort)(indices[i - 4] + vertexStart);
                            ushort i3 = (ushort)(indices[i - 3] + vertexStart);
                            ushort i4 = (ushort)(indices[i - 2] + vertexStart);
                            ushort i5 = (ushort)(indices[i - 1] + vertexStart);
                            ushort i6 = index;
                            ushort i7 = index2;

                            if (i3 == i2 && i4 == i1 &&
                                i6 == i3 && i7 == i5)
                            {
                                // Avoid restarting the primitive twice by restarting it earlier.
                                // Rewind to the last index before the previous restart.

                                ms.Seek(-5 * sizeof(ushort), SeekOrigin.Current);
                                stripIndex = 3;
                                i -= 6 + 1;
                                forceRestart = true;

                                continue;
                            }
                        }

                        writer.Write(ushort.MaxValue);
                        writer.Write(index);

                        stripIndex = 0;
                        forceRestart = false;
                    }
                }
                else
                {
                    writer.Write(index);
                }

                prevIndex2 = prevIndex1;
                prevIndex1 = newIndex;
            }

            uint indexCount = (uint)(ms.Length / sizeof(ushort));

            writer.Write(ushort.MaxValue);

            return new(ms.ToArray(), indexCount);
        }

        public static uint CountEdges(ushort[] indices)
        {
            HashSet<uint> edges = [];

            for (int triIndex = 0; triIndex < indices.Length; triIndex += 3)
            {
                ushort i0 = indices[triIndex];
                ushort i1 = indices[triIndex + 1];
                ushort i2 = indices[triIndex + 2];

                edges.Add(CreateEdge(i0, i1));
                edges.Add(CreateEdge(i1, i2));
                edges.Add(CreateEdge(i2, i0));
            }

            return (uint)edges.Count;
        }

        private static uint CreateEdge(ushort a, ushort b)
        {
            return Math.Min(a, b) | ((uint)Math.Max(a, b) << 16);
        }

        private static bool IsDegenerateTriangle(ushort i0, ushort i1, ushort i2)
        {
            return i0 == i1 || i1 == i2 || i0 == i2;
        }
    }
}
