using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using System.Numerics;

namespace MHS2ModTool.GameFileFormats
{
    internal class GltfVertexReader
    {
        private Vertex[] _vertices;
        private VertexFormatFlags _formatFlags;

        public GltfVertexReader()
        {
            _vertices = [];
        }

        public static uint GetStride(MeshPrimitive primitive, bool isBridge)
        {
            VertexFormatFlags formatFlags = VertexFormatFlags.None;
            byte weightsPerVertex = 0;

            foreach ((var attributeKey, var accessor) in primitive.VertexAccessors)
            {
                switch (attributeKey)
                {
                    case "POSITION":
                        formatFlags |= VertexFormatFlags.HasPosition;
                        break;
                    case "NORMAL":
                        formatFlags |= VertexFormatFlags.HasNormal;
                        break;
                    case "TANGENT":
                        formatFlags |= VertexFormatFlags.HasTangent;
                        break;
                    case "TEXCOORD_0":
                        formatFlags |= VertexFormatFlags.HasTexCoord0;
                        break;
                    case "TEXCOORD_1":
                        formatFlags |= VertexFormatFlags.HasTexCoord1;
                        break;
                    case "COLOR":
                        formatFlags |= VertexFormatFlags.HasColor;
                        break;
                    case "JOINTS_0":
                        formatFlags |= VertexFormatFlags.HasJoints0;
                        weightsPerVertex = accessor.Dimensions switch
                        {
                            DimensionType.SCALAR => 1,
                            DimensionType.VEC2 => 2,
                            DimensionType.VEC3 => 3,
                            _ => 4
                        };
                        break;
                    case "JOINTS_1":
                        formatFlags |= VertexFormatFlags.HasJoints1;
                        break;
                }
            }

            MTVertexFormatTable.SelectOptimalVertexFormat(formatFlags, weightsPerVertex, isBridge, out uint stride);

            return stride;
        }

        public unsafe void Read(string attributeKey, MemoryAccessor accessor)
        {
            switch (attributeKey)
            {
                case "POSITION":
                    SetVertexDataVec3(accessor, VertexFormatFlags.HasPosition, (i, p) => _vertices[i].Position = p);
                    break;
                case "NORMAL":
                    SetVertexDataVec3(accessor, VertexFormatFlags.HasNormal, (i, n) => _vertices[i].Normal = n);
                    break;
                case "TANGENT":
                    SetVertexDataVec3(accessor, VertexFormatFlags.HasTangent, (i, t) => _vertices[i].Tangent = t);
                    break;
                case "TEXCOORD_0":
                    SetVertexDataVec2(accessor, VertexFormatFlags.HasTexCoord0, (i, tc) => _vertices[i].TexCoord0 = tc);
                    break;
                case "TEXCOORD_1":
                    SetVertexDataVec2(accessor, VertexFormatFlags.HasTexCoord1, (i, tc) => _vertices[i].TexCoord1 = tc);
                    break;
                case "COLOR":
                    SetVertexDataVec4(accessor, VertexFormatFlags.HasColor, (i, c) => _vertices[i].Color = c);
                    break;
                case "JOINTS_0":
                    SetVertexDataJoints(accessor, VertexFormatFlags.HasJoints0, (i, j) =>
                    {
                        _vertices[i].Joints0[0] = (byte)j.Item1;
                        _vertices[i].Joints0[1] = (byte)j.Item2;
                        _vertices[i].Joints0[2] = (byte)j.Item3;
                        _vertices[i].Joints0[3] = (byte)j.Item4;
                    });
                    break;
                case "JOINTS_1":
                    SetVertexDataJoints(accessor, VertexFormatFlags.HasJoints1, (i, j) =>
                    {
                        _vertices[i].Joints1[0] = (byte)j.Item1;
                        _vertices[i].Joints1[1] = (byte)j.Item2;
                        _vertices[i].Joints1[2] = (byte)j.Item3;
                        _vertices[i].Joints1[3] = (byte)j.Item4;
                    });
                    break;
                case "WEIGHTS_0":
                    SetVertexDataWeights(accessor, VertexFormatFlags.None, (i, w) =>
                    {
                        _vertices[i].Weights0[0] = w.X;
                        _vertices[i].Weights0[1] = w.Y;
                        _vertices[i].Weights0[2] = w.Z;
                        _vertices[i].Weights0[3] = w.W;
                    });
                    break;
                case "WEIGHTS_1":
                    SetVertexDataWeights(accessor, VertexFormatFlags.None, (i, w) =>
                    {
                        _vertices[i].Weights1[0] = w.X;
                        _vertices[i].Weights1[1] = w.Y;
                        _vertices[i].Weights1[2] = w.Z;
                        _vertices[i].Weights1[3] = w.W;
                    });
                    break;
            }
        }

        private void SetVertexDataVec2(MemoryAccessor accessor, VertexFormatFlags formatFlags, Action<int, Vector2> setter)
        {
            if (accessor.Attribute.Dimensions == DimensionType.VEC2)
            {
                _formatFlags |= formatFlags;
                var array = accessor.AsVector2Array();

                EnsureVerticesCapacity(array.Count);

                for (int i = 0; i < array.Count; i++)
                {
                    setter(i, array[i]);
                }
            }
        }

        private void SetVertexDataVec3(MemoryAccessor accessor, VertexFormatFlags formatFlags, Action<int, Vector4> setter)
        {
            if (accessor.Attribute.Dimensions == DimensionType.VEC3)
            {
                _formatFlags |= formatFlags;
                var array = accessor.AsVector3Array();

                EnsureVerticesCapacity(array.Count);

                for (int i = 0; i < array.Count; i++)
                {
                    setter(i, new(array[i], 1f));
                }
            }
        }

        private void SetVertexDataVec4(MemoryAccessor accessor, VertexFormatFlags formatFlags, Action<int, Vector4> setter)
        {
            if (accessor.Attribute.Dimensions == DimensionType.VEC4)
            {
                _formatFlags |= formatFlags;
                var array = accessor.AsVector4Array();

                EnsureVerticesCapacity(array.Count);

                for (int i = 0; i < array.Count; i++)
                {
                    setter(i, array[i]);
                }
            }
        }

        private void SetVertexDataJoints(MemoryAccessor accessor, VertexFormatFlags formatFlags, Action<int, (uint, uint, uint, uint)> setter)
        {
            _formatFlags |= formatFlags;

            switch (accessor.Attribute.Dimensions)
            {
                case DimensionType.SCALAR:
                    var array = accessor.AsIntegerArray();

                    EnsureVerticesCapacity(array.Count);

                    for (int i = 0; i < array.Count; i++)
                    {
                        setter(i, (array[i], 0, 0, 0));
                    }
                    break;
                case DimensionType.VEC2:
                    var array2 = accessor.AsVector2Array();

                    EnsureVerticesCapacity(array2.Count);

                    for (int i = 0; i < array2.Count; i++)
                    {
                        setter(i, ((uint)array2[i].X, (uint)array2[i].Y, 0, 0));
                    }
                    break;
                case DimensionType.VEC3:
                    var array3 = accessor.AsVector3Array();

                    EnsureVerticesCapacity(array3.Count);

                    for (int i = 0; i < array3.Count; i++)
                    {
                        setter(i, ((uint)array3[i].X, (uint)array3[i].Y, (uint)array3[i].Z, 0));
                    }
                    break;
                case DimensionType.VEC4:
                    var array4 = accessor.AsVector4Array();

                    EnsureVerticesCapacity(array4.Count);

                    for (int i = 0; i < array4.Count; i++)
                    {
                        setter(i, ((uint)array4[i].X, (uint)array4[i].Y, (uint)array4[i].Z, (uint)array4[i].W));
                    }
                    break;
            }
        }

        private void SetVertexDataWeights(MemoryAccessor accessor, VertexFormatFlags formatFlags, Action<int, Vector4> setter)
        {
            _formatFlags |= formatFlags;

            switch (accessor.Attribute.Dimensions)
            {
                case DimensionType.SCALAR:
                    var array = accessor.AsScalarArray();

                    EnsureVerticesCapacity(array.Count);

                    for (int i = 0; i < array.Count; i++)
                    {
                        setter(i, new(array[i], 0f, 0f, 0f));
                    }
                    break;
                case DimensionType.VEC2:
                    var array2 = accessor.AsVector2Array();

                    EnsureVerticesCapacity(array2.Count);

                    for (int i = 0; i < array2.Count; i++)
                    {
                        setter(i, new(array2[i], 0f, 0f));
                    }
                    break;
                case DimensionType.VEC3:
                    var array3 = accessor.AsVector3Array();

                    EnsureVerticesCapacity(array3.Count);

                    for (int i = 0; i < array3.Count; i++)
                    {
                        setter(i, new(array3[i], 0f));
                    }
                    break;
                case DimensionType.VEC4:
                    var array4 = accessor.AsVector4Array();

                    EnsureVerticesCapacity(array4.Count);

                    for (int i = 0; i < array4.Count; i++)
                    {
                        setter(i, array4[i]);
                    }
                    break;
            }
        }

        private void EnsureVerticesCapacity(int count)
        {
            if (_vertices.Length < count)
            {
                Array.Resize(ref _vertices, count);
            }
        }

        public (Vertex[], VertexFormatFlags) GetVertices()
        {
            return (_vertices, _formatFlags);
        }
    }
}
