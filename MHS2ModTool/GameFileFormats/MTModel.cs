using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MHS2ModTool.GameFileFormats
{
    internal class MTModel
    {
        private const uint ModMagic = 0x444F4D;
        private const ushort Version = 214;

        private const string BoneIdPrefix = "Bone";
        private const string MeshIdPrefix = "Mesh";
        private const string GroupIdPrefix = "_g";
        private const string EdgeSuffix = "_e";
        private const string HeadwearSuffix = "_hw";

        private struct MTMetaData
        {
            public uint MiddleDistance;
            public uint LowDistance;
            public uint LightGroup;
            public uint BoundaryJoint;
            public uint EnvelopesCount;
        }

        [StructLayout(LayoutKind.Sequential, Size = 0xA4)]
        private struct MTModHeader
        {
            public uint Magic;
            public ushort Version;
            public ushort BonesCount;
            public ushort MeshesCount;
            public ushort MaterialsCount;
            public uint VertexCount;
            public uint IndexCount;
            public uint EdgesCount;
            public uint VertexBufferSize;
            public uint TexturesCount;
            public uint GroupsCount;
            public long SkeletonOffset;
            public long GroupsOffset;
            public long MaterialNamesOffset;
            public long MeshesOffset;
            public long VertexBufferOffset;
            public long IndexBufferOffset;
            public long FileLength;
            public MTBoundingSphere BoundingSphere;
            public MTAABB BoundingBox;
            public MTMetaData MetaData;
        }

        private struct MTBone
        {
            public ushort Id;
            public byte ParentIndex;
            public byte MirrorIndex;
            public float FurthestVertexDistance;
            public float ParentDistance;
            public Vector3 Position;
        }

        private struct MTGroup
        {
            public uint Id;
            public uint Padding0;
            public uint Padding1;
            public uint Padding2;
            public MTBoundingSphere BoundingSphere;
        }

        private readonly record struct OffsetAndSize(long Offset, long Size);
        private readonly record struct VertexDataOffsets(
            OffsetAndSize PositionOffset,
            OffsetAndSize NormalOffset,
            OffsetAndSize TangentOffset,
            OffsetAndSize UV0Offset,
            OffsetAndSize UV1Offset,
            OffsetAndSize ColorOffset,
            OffsetAndSize Joints0Offset,
            OffsetAndSize Joints1Offset,
            OffsetAndSize Weights0Offset,
            OffsetAndSize Weights1Offset);
        
        private enum MTMeshType : ushort
        {
            Edge = 0x1020,
            Headwear = 0xFDF7,
            General = 0xFFFF
        }

        private readonly struct MeshBitField0
        {
            private readonly uint _packed;

            public MeshBitField0(uint groupIndex, uint materialIndex, byte visibleLod)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(groupIndex, 0xfffu, nameof(groupIndex));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(materialIndex, 0xfffu, nameof(materialIndex));

                _packed = groupIndex | (materialIndex << 12) | ((uint)visibleLod << 24);
            }

            public uint UnpackGroupId()
            {
                return _packed & 0xfff;
            }

            public uint UnpackMaterialIndex()
            {
                return (_packed >> 12) & 0xfff;
            }

            public byte UnpackVisibleLod()
            {
                return (byte)(_packed >> 24);
            }
        }

        private readonly struct MeshBitField1
        {
            private readonly uint _packed;

            public MeshBitField1(bool visible, bool flag0, bool flag1, byte weightsPerVertex, byte unk1, byte vertexBufferStride, MTPrimitiveType primitiveType, uint unk2)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(weightsPerVertex, (byte)4, nameof(weightsPerVertex));
                ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)primitiveType, (uint)MTPrimitiveType.TriangleStrip, nameof(primitiveType));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(unk2, 3u, nameof(unk2));

                _packed = (visible ? 1u : 0u) |
                    (flag0 ? 2u : 0u) |
                    (flag1 ? 4u : 0u) |
                    ((uint)weightsPerVertex << 3) |
                    ((uint)unk1 << 8) |
                    ((uint)vertexBufferStride << 16) |
                    ((uint)primitiveType << 24) |
                    (unk2 << 30);
            }

            public bool UnpackVisible()
            {
                return (_packed & (1u << 0)) != 0;
            }

            public bool UnpackFlag0()
            {
                return (_packed & (1u << 1)) != 0;
            }

            public bool UnpackFlag1()
            {
                return (_packed & (1u << 2)) != 0;
            }

            public byte UnpackWeightsPerVertex()
            {
                return (byte)((_packed >> 3) & 0x1f);
            }

            public byte UnpackUnk1()
            {
                return (byte)(_packed >> 8);
            }

            public byte UnpackVertexBufferStride()
            {
                return (byte)(_packed >> 16);
            }

            public MTPrimitiveType UnpackPrimitiveType()
            {
                return (MTPrimitiveType)((_packed >> 24) & 0x3f);
            }

            public uint UnpackUnk2()
            {
                return (_packed >> 30) & 3;
            }
        }

        private struct MTMesh
        {
            public MTMeshType MeshType;
            public ushort VertexCount;
            public MeshBitField0 BitField0;
            public MeshBitField1 BitField1;
            public uint VertexFirst;
            public uint VertexOffset;
            public uint VertexFormat;
            public uint IndexFirst;
            public uint IndexCount;
            public uint IndexValueOffset;
            public byte BoneFirst;
            public byte UniqueBoneIds;
            public ushort MeshId;
            public ushort MinVertexIndex;
            public ushort MaxVertexIndex;
            public uint Hash;
            public ulong Unknown;
        }

        private struct MTEnvelope
        {
            public uint BoneIndex;
            public uint Padding0;
            public uint Padding1;
            public uint Padding2;
            public MTBoundingSphere BoundingSphere;
            public MTAABB BoundingBox;
            public Matrix4x4 LocalTransform;
            public Vector4 AbsolutePosition;
        }

        private readonly struct InternalBone
        {
            public readonly ushort Id;
            public readonly byte ParentIndex;
            public readonly Matrix4x4 LocalTransform;
            public readonly Matrix4x4 InvBindPoseTransform;

            public InternalBone(ushort id, byte parentIndex, Matrix4x4 localTransform, Matrix4x4 invBindPoseTransform)
            {
                Id = id;
                ParentIndex = parentIndex;
                LocalTransform = localTransform;
                InvBindPoseTransform = invBindPoseTransform;
            }

            public Matrix4x4 CalculateTransform(IReadOnlyList<InternalBone> skeleton)
            {
                var visited = new BitArray(skeleton.Count);
                var mtx = LocalTransform;
                int pIndex = ParentIndex;

                while (pIndex < skeleton.Count && !visited.Get(pIndex))
                {
                    visited.Set(pIndex, true);
                    mtx *= skeleton[pIndex].LocalTransform;
                    pIndex = skeleton[pIndex].ParentIndex;
                }

                return mtx;
            }

            public Vector3 CalculateAbsolutePosition(IReadOnlyList<InternalBone> skeleton)
            {
                return CalculateTransform(skeleton).Translation;
            }

            public Vector3 GetLocalPosition()
            {
                return LocalTransform.Translation;
            }
        }

        private unsafe struct MTBoneRemap
        {
            private const int Length = 0x1000;

            private fixed byte _map[Length];

            public void Reset()
            {
                fixed (byte* pMap = _map)
                {
                    new Span<byte>(pMap, Length).Fill(byte.MaxValue);
                }
            }

            public byte Get(int index)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));

                return _map[index];
            }

            public void Set(int index, byte value)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));

                _map[index] = value;
            }
        }

        private readonly struct InternalMesh
        {
            public readonly Vertex[] Vertices;
            public readonly VertexFormatFlags VertexFormatFlags;
            public readonly NsPrimitiveType PrimitiveType;
            public readonly ushort[] Indices;
            public readonly MTMeshType Type;
            public readonly uint Id;
            public readonly uint GroupId;
            public readonly uint MaterialIndex;
            public readonly byte WeightsPerVertex;

            public InternalMesh(
                Vertex[] vertices,
                VertexFormatFlags vertexFormatFlags,
                NsPrimitiveType primitiveType,
                ushort[] indices,
                MTMeshType type,
                uint id,
                uint groupId,
                uint materialIndex,
                byte weightsPerVertex)
            {
                Vertices = vertices;
                VertexFormatFlags = vertexFormatFlags;
                PrimitiveType = primitiveType;
                Indices = indices;
                Type = type;
                Id = id;
                GroupId = groupId;
                MaterialIndex = materialIndex;
                WeightsPerVertex = weightsPerVertex;
            }

            public unsafe uint CalculateUniqueBoneIds(int maxBonesCount)
            {
                var usedBones = new BitArray(maxBonesCount);

                for (int vtx = 0; vtx < Vertices.Length; vtx++)
                {
                    var vertex = Vertices[vtx];

                    for (int i = 0; i < WeightsPerVertex; i++)
                    {
                        if (vertex.IsValidBone(i))
                        {
                            int boneIndex = vertex.Joints0[i];

                            if (boneIndex < maxBonesCount)
                            {
                                usedBones.Set(boneIndex, true);
                            }
                        }
                    }
                }

                uint count = 0;

                for (int i = 0; i < usedBones.Count; i++)
                {
                    if (usedBones.Get(i))
                    {
                        count++;
                    }
                }

                return count;
            }

            public unsafe Vector3[] Transform(IReadOnlyList<InternalBone> skeleton)
            {
                Vector3[] output = new Vector3[Vertices.Length];

                if (WeightsPerVertex == 0)
                {
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        output[i] = Vertices[i].Position.Xyz();
                    }
                }
                else
                {
                    var transforms = new Matrix4x4[skeleton.Count];

                    for (int i = 0; i < skeleton.Count; i++)
                    {
                        transforms[i] = skeleton[i].InvBindPoseTransform * skeleton[i].CalculateTransform(skeleton);
                    }

                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        var vertex = Vertices[i];
                        Vector3 pSum = Vector3.Zero;

                        for (int j = 0; j < WeightsPerVertex; j++)
                        {
                            Vector3 p = Vector3.Transform(vertex.Position.Xyz(), transforms[vertex.Joints0[j]]);
                            pSum += p * vertex.Weights0[j];
                        }

                        output[i] = pSum;
                    }
                }
                
                return output;
            }
        }

        private readonly List<InternalBone> _skeleton = [];
        private MTBoneRemap _boneRemap;
        private readonly List<MTGroup> _groups = [];
        private readonly List<string> _materialNames = [];
        private readonly List<InternalMesh> _meshes = [];
        private readonly List<MTEnvelope> _envelopes = [];
        private bool _calculateEnvelopesUponSave;

        public static MTModel Load(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            return Load(fs);
        }

        public static MTModel Load(Stream stream)
        {
            MTModel output = new();

            long start = stream.Position;

            using var reader = new BinaryReader(stream);

            var header = reader.Read<MTModHeader>();

            stream.Seek(start + header.SkeletonOffset, SeekOrigin.Begin);

            var bones = new MTBone[header.BonesCount];
            var boneLocalMatrices = new Matrix4x4[header.BonesCount];
            var boneAbsoluteMatrices = new Matrix4x4[header.BonesCount];

            for (int i = 0; i < header.BonesCount; i++)
            {
                var boneDesc = reader.Read<MTBone>();

                bones[i] = boneDesc;
            }

            for (int i = 0; i < header.BonesCount; i++)
            {
                var mtx = reader.Read<Matrix4x4>();

                boneLocalMatrices[i] = mtx;
            }

            Vector4 aabbDelta = header.BoundingBox.Max - header.BoundingBox.Min;
            Matrix4x4 invScaleMatrix = Matrix4x4.CreateTranslation(-header.BoundingBox.Min.Xyz()) * Matrix4x4.CreateScale(aabbDelta.Xyz().Reciprocal());

            for (int i = 0; i < header.BonesCount; i++)
            {
                var mtx = reader.Read<Matrix4x4>();

                boneAbsoluteMatrices[i] = invScaleMatrix * mtx;
            }

            for (int i = 0; i < header.BonesCount; i++)
            {
                output._skeleton.Add(new InternalBone(bones[i].Id, bones[i].ParentIndex, boneLocalMatrices[i], boneAbsoluteMatrices[i]));
            }

            output._boneRemap = reader.Read<MTBoneRemap>();

            stream.Seek(start + header.GroupsOffset, SeekOrigin.Begin);

            for (int i = 0; i < header.GroupsCount; i++)
            {
                output._groups.Add(reader.Read<MTGroup>());
            }

            stream.Seek(start + header.MaterialNamesOffset, SeekOrigin.Begin);

            for (int i = 0; i < header.MaterialsCount; i++)
            {
                output._materialNames.Add(reader.Read<MTName>().GetString());
            }

            stream.Seek(start + header.VertexBufferOffset, SeekOrigin.Begin);

            var vertexBuffer = new MTVertexBuffer(reader.ReadBytes((int)header.VertexBufferSize));

            stream.Seek(start + header.IndexBufferOffset, SeekOrigin.Begin);

            var indexBuffer = new MTIndexBuffer(reader.ReadBytes((int)header.IndexCount * sizeof(ushort)));

            stream.Seek(start + header.MeshesOffset, SeekOrigin.Begin);

            var meshes = new List<MTMesh>();

            for (int i = 0; i < header.MeshesCount; i++)
            {
                var meshDesc = reader.Read<MTMesh>();

                meshes.Add(meshDesc);
            }

            for (int i = 0; i < header.MetaData.EnvelopesCount; i++)
            {
                output._envelopes.Add(reader.Read<MTEnvelope>());
            }

            foreach (MTMesh mesh in meshes)
            {
                uint vbStride = mesh.BitField1.UnpackVertexBufferStride();

                if (!MTVertexFormatTable.TryGetVertexAttributes(mesh.VertexFormat, out var attrs))
                {
                    continue;
                }

                MTVertexFormatTable.TryGetVertexFormatInputAssembler(mesh.VertexFormat, out string? iaName);

                Vertex[] vertices = vertexBuffer.ReadVertices(
                    attrs,
                    mesh.VertexOffset + (mesh.VertexFirst + mesh.IndexValueOffset) * vbStride,
                    mesh.VertexCount,
                    vbStride,
                    header.BoundingBox,
                    mesh.BitField1.UnpackWeightsPerVertex(),
                    mesh.BoneFirst);
                
                MTPrimitiveType primitiveType = mesh.BitField1.UnpackPrimitiveType();
                NsPrimitiveType nsPrimitiveType = primitiveType switch
                {
                    MTPrimitiveType.Points => NsPrimitiveType.Points,
                    MTPrimitiveType.Lines or MTPrimitiveType.LineStrip => NsPrimitiveType.Lines,
                    _ => NsPrimitiveType.Triangles
                };

                ushort[] indices = indexBuffer.ReadIndicesAsNonStrip(
                    primitiveType,
                    mesh.IndexFirst * sizeof(ushort),
                    mesh.IndexCount,
                    mesh.VertexFirst);

                VertexFormatFlags formatFlags = attrs.GetVertexFormatFlags();

                output._meshes.Add(new InternalMesh(
                    vertices,
                    formatFlags,
                    nsPrimitiveType,
                    indices,
                    mesh.MeshType,
                    mesh.MeshId,
                    mesh.BitField0.UnpackGroupId(),
                    mesh.BitField0.UnpackMaterialIndex(),
                    mesh.BitField1.UnpackWeightsPerVertex()));
            }

            return output;
        }

        public void Save(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            Save(fs);
        }

        public void Save(Stream stream)
        {
            using var writer = new BinaryWriter(stream);

            long start = stream.Position;

            stream.Seek(Unsafe.SizeOf<MTModHeader>(), SeekOrigin.Current);

            long skeletonOffset = stream.Position - start;

            Vector3[][] worldSpaceMeshes = new Vector3[_meshes.Count][];

            for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
            {
                worldSpaceMeshes[meshIndex] = _meshes[meshIndex].Transform(_skeleton);
            }

            if (_calculateEnvelopesUponSave)
            {
                if (_envelopes.Count == 0)
                {
                    CalculateBoneEnvelopes(worldSpaceMeshes);
                }

                _calculateEnvelopesUponSave = false;
            }

            float[] furthestVertexDistances = CalculateFurthestVertexDistances(worldSpaceMeshes);

            for (int boneIndex = 0; boneIndex < _skeleton.Count; boneIndex++)
            {
                var bone = _skeleton[boneIndex];

                writer.Write(new MTBone()
                {
                    Id = bone.Id,
                    ParentIndex = bone.ParentIndex,
                    MirrorIndex = (byte)FindMirrorXIndex(boneIndex),
                    FurthestVertexDistance = furthestVertexDistances[boneIndex],
                    ParentDistance = CalculateParentDistance(boneIndex),
                    Position = bone.GetLocalPosition()
                });
            }

            foreach (var bone in _skeleton)
            {
                writer.Write(bone.LocalTransform);
            }

            MTAABB? bbox = null;

            for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
            {
                bbox = MTAABB.Expand(bbox, worldSpaceMeshes[meshIndex]);
            }

            var boundingBox = bbox ?? MTAABB.Empty;
            var boundingSphere = MTBoundingSphere.FromAABB(boundingBox, 0f);

            for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
            {
                boundingSphere = boundingSphere.ExpandRadius(worldSpaceMeshes[meshIndex]);
            }

            Vector4 aabbDelta = boundingBox.Max - boundingBox.Min;
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(aabbDelta.Xyz()) * Matrix4x4.CreateTranslation(boundingBox.Min.Xyz());

            foreach (var bone in _skeleton)
            {
                writer.Write(scaleMatrix * bone.InvBindPoseTransform);
            }

            writer.Write(_boneRemap);

            long groupsOffset = stream.Position - start;

            foreach (var group in _groups)
            {
                writer.Write(group);
            }

            long materialNamesOffset = stream.Position - start;

            foreach (string materialName in _materialNames)
            {
                writer.Write(new MTName(materialName));
            }

            long meshesOffset = stream.Position - start;

            var vertexBuffer = new MTVertexBuffer();
            var indexBuffer = new MTIndexBuffer();

            uint vertexCount = 0;
            uint edgesCount = 0;

            for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
            {
                var mesh = _meshes[meshIndex];

                var vbInfo = vertexBuffer.WriteVertices(mesh.Vertices, mesh.VertexFormatFlags, mesh.WeightsPerVertex, mesh.Type == MTMeshType.Edge, boundingBox);
                var ibInfo = indexBuffer.WriteIndicesAsStrip(mesh.Indices, mesh.PrimitiveType, vbInfo.First);

                vertexCount += (uint)mesh.Vertices.Length;
                edgesCount += MTIndexBuffer.CountEdges(mesh.Indices);

                MTPrimitiveType primitiveType = mesh.PrimitiveType switch
                {
                    NsPrimitiveType.Points => MTPrimitiveType.Points,
                    NsPrimitiveType.Lines => MTPrimitiveType.LineStrip,
                    _ => MTPrimitiveType.TriangleStrip
                };

                writer.Write(new MTMesh()
                {
                    MeshType = mesh.Type,
                    VertexCount = (ushort)mesh.Vertices.Length,
                    BitField0 = new(mesh.GroupId, mesh.MaterialIndex, 0xFF),
                    BitField1 = new(
                        true,
                        false,
                        false,
                        mesh.WeightsPerVertex,
                        0,
                        (byte)vbInfo.Stride,
                        primitiveType,
                        mesh.Type == MTMeshType.Edge ? 3u : 0u),
                    VertexFirst = vbInfo.First,
                    VertexOffset = vbInfo.Offset,
                    VertexFormat = vbInfo.Format,
                    IndexFirst = ibInfo.Offset / sizeof(ushort),
                    IndexCount = ibInfo.Count,
                    IndexValueOffset = 0,
                    BoneFirst = 0,
                    UniqueBoneIds = (byte)mesh.CalculateUniqueBoneIds(_skeleton.Count),
                    MeshId = (ushort)mesh.Id,
                    MinVertexIndex = (ushort)vbInfo.First,
                    MaxVertexIndex = (ushort)(vbInfo.First + mesh.Vertices.Length - 1),
                    Hash = 0,
                    Unknown = 0
                });
            }

            foreach (var envelope in _envelopes)
            {
                writer.Write(envelope);
            }

            long vertexBufferOffset = stream.Position - start;

            writer.Write(vertexBuffer.Data);

            long indexBufferOffset = stream.Position - start;

            writer.Write(indexBuffer.Data);

            long end = stream.Position - start;

            stream.Seek(start, SeekOrigin.Begin);

            writer.Write(new MTModHeader()
            {
                Magic = ModMagic,
                Version = Version,
                BonesCount = (ushort)_skeleton.Count,
                MeshesCount = (ushort)_meshes.Count,
                MaterialsCount = (ushort)_materialNames.Count,
                VertexCount = vertexCount,
                IndexCount = indexBuffer.IndexCount,
                EdgesCount = edgesCount,
                VertexBufferSize = (uint)vertexBuffer.Data.Length,
                TexturesCount = 0,
                GroupsCount = (uint)_groups.Count,
                SkeletonOffset = skeletonOffset,
                GroupsOffset = groupsOffset,
                MaterialNamesOffset = materialNamesOffset,
                MeshesOffset = meshesOffset,
                VertexBufferOffset = vertexBufferOffset,
                IndexBufferOffset = indexBufferOffset,
                FileLength = end,
                BoundingSphere = boundingSphere,
                BoundingBox = boundingBox,
                MetaData = new MTMetaData()
                {
                    MiddleDistance = 1000,
                    LowDistance = 3000,
                    LightGroup = 0,
                    BoundaryJoint = 0,
                    EnvelopesCount = (uint)_envelopes.Count
                }
            });

            stream.Seek(start + end, SeekOrigin.Begin);

            writer.Write(0u);
        }

        private unsafe float[] CalculateFurthestVertexDistances(Vector3[][] worldSpaceMeshes)
        {
            float[] distances = new float[_skeleton.Count];
            Vector3[] positions = new Vector3[_skeleton.Count];

            for (int i = 0; i < _skeleton.Count; i++)
            {
                positions[i] = _skeleton[i].CalculateAbsolutePosition(_skeleton);
            }

            for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
            {
                var mesh = _meshes[meshIndex];
                var vertices = worldSpaceMeshes[meshIndex];

                for (int vtx = 0; vtx < mesh.Vertices.Length; vtx++)
                {
                    var vertex = mesh.Vertices[vtx];
                    var p = vertices[vtx];

                    for (int i = 0; i < mesh.WeightsPerVertex; i++)
                    {
                        if (vertex.IsValidBone(i))
                        {
                            int boneIndex = vertex.Joints0[i];
                            float distance = (p - positions[boneIndex]).LengthSquared();

                            distances[boneIndex] = MathF.Max(distances[boneIndex], distance);
                        }
                    }
                }
            }

            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = MathF.Sqrt(distances[i]);
            }

            return distances;
        }

        private unsafe void CalculateBoneEnvelopes(Vector3[][] worldSpaceMeshes)
        {
            for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
            {
                Vector3?[] bbMin = new Vector3?[_skeleton.Count];
                Vector3?[] bbMax = new Vector3?[_skeleton.Count];

                var mesh = _meshes[meshIndex];
                var vertices = worldSpaceMeshes[meshIndex];

                for (int vtx = 0; vtx < mesh.Vertices.Length; vtx++)
                {
                    var vertex = mesh.Vertices[vtx];
                    var p = vertices[vtx];

                    for (int i = 0; i < mesh.WeightsPerVertex; i++)
                    {
                        if (vertex.IsValidBone(i))
                        {
                            int boneIndex = vertex.Joints0[i];

                            bbMin[boneIndex] = Vector3.Min(bbMin[boneIndex] ?? p, p);
                            bbMax[boneIndex] = Vector3.Max(bbMax[boneIndex] ?? p, p);
                        }
                    }
                }

                float[] distances = new float[_skeleton.Count];

                for (int vtx = 0; vtx < mesh.Vertices.Length; vtx++)
                {
                    var vertex = mesh.Vertices[vtx];
                    var p = vertices[vtx];

                    for (int i = 0; i < mesh.WeightsPerVertex; i++)
                    {
                        if (vertex.IsValidBone(i))
                        {
                            int boneIndex = vertex.Joints0[i];

                            var min = bbMin[boneIndex] ?? Vector3.Zero;
                            var max = bbMax[boneIndex] ?? Vector3.Zero;
                            var center = (min + max) * 0.5f;
                            float distance = (p - center).LengthSquared();

                            distances[boneIndex] = MathF.Max(distances[boneIndex], distance);
                        }
                    }
                }

                for (int i = 0; i < _skeleton.Count; i++)
                {
                    if (bbMin[i].HasValue && bbMax[i].HasValue)
                    {
                        Matrix4x4.Invert(_skeleton[i].CalculateTransform(_skeleton), out var invTransform);

                        var boundingBox = new MTAABB(
                            new(Vector3.Transform(bbMin[i] ?? Vector3.Zero, invTransform), 0f),
                            new(Vector3.Transform(bbMax[i] ?? Vector3.Zero, invTransform), 0f));

                        var boundingSphere = MTBoundingSphere.FromAABB(boundingBox, MathF.Sqrt(distances[i]));

                        // Note: The absolute position value does not match those of the original model.
                        // It is unknown how it is supposed to be calculated, maybe it is some information that has been lost from the original model.
                        // The local transform also sometimes does not match the original model.

                        _envelopes.Add(new MTEnvelope()
                        {
                            BoneIndex = (uint)i,
                            Padding0 = 0,
                            Padding1 = 0,
                            Padding2 = 0,
                            BoundingSphere = boundingSphere,
                            BoundingBox = boundingBox,
                            LocalTransform = Matrix4x4.CreateTranslation(boundingSphere.CenterPosition),
                            AbsolutePosition = boundingBox.Max
                        });
                    }
                }
            }
        }

        private float CalculateParentDistance(int boneIndex)
        {
            int parentIndex = _skeleton[boneIndex].ParentIndex;
            if (parentIndex >= _skeleton.Count)
            {
                return 0f;
            }

            return (_skeleton[boneIndex].CalculateAbsolutePosition(_skeleton) - _skeleton[parentIndex].CalculateAbsolutePosition(_skeleton)).Length();
        }

        private int FindMirrorXIndex(int selfIndex)
        {
            var selfBonePosition = _skeleton[selfIndex].CalculateAbsolutePosition(_skeleton);

            for (int boneIndex = 0; boneIndex < _skeleton.Count; boneIndex++)
            {
                if (boneIndex != selfIndex)
                {
                    var bone = _skeleton[boneIndex];
                    var bonePosition = bone.CalculateAbsolutePosition(_skeleton);

                    if (bonePosition.X == -selfBonePosition.X &&
                        bonePosition.Y == selfBonePosition.Y &&
                        bonePosition.Z == selfBonePosition.Z)
                    {
                        return boneIndex;
                    }
                }
            }

            return selfIndex;
        }

        public static MTModel ImportGltf(string inputFileName, string? originalModFileName = null)
        {
            MTModel output = new()
            {
                _calculateEnvelopesUponSave = true
            };

            if (originalModFileName != null && File.Exists(originalModFileName))
            {
                // If possible, import the information we can't store on the GLTF from the original model.

                MTModel originalModel = Load(originalModFileName);

                output._groups.AddRange(originalModel._groups);
            }
            else
            {
                output._groups.Add(new MTGroup()
                {
                    Id = 1,
                    Padding0 = 0,
                    Padding1 = 0,
                    Padding2 = 0,
                    BoundingSphere = new MTBoundingSphere(Vector3.Zero, 0f)
                });
            }

            var settings = new ReadSettings()
            {
                Validation = ValidationMode.Skip
            };

            var root = ModelRoot.Load(inputFileName, settings);

            output._boneRemap.Reset();

            if (root.LogicalSkins.Count > 0)
            {
                var skin = root.LogicalSkins[0];

                var boneIdAllocator = new IdAllocator();

                for (int i = 0; i < skin.JointsCount; i++)
                {
                    (var node, _) = skin.GetJoint(i);

                    boneIdAllocator.Parse(node, BoneIdPrefix);
                }

                int boneIndex = 0;

                for (int i = 0; i < skin.JointsCount; i++)
                {
                    (var node, var invBindPoseMatrix) = skin.GetJoint(i);

                    if (!node.IsSkinJoint)
                    {
                        continue;
                    }

                    int parentIndex = byte.MaxValue;
                    int parentIndexCandidate = 0;

                    for (int j = 0; j < skin.JointsCount; j++)
                    {
                        (var candidateNode, _) = skin.GetJoint(j);

                        if (!candidateNode.IsSkinJoint)
                        {
                            continue;
                        }

                        if (candidateNode.VisualChildren.Contains(node))
                        {
                            parentIndex = parentIndexCandidate;
                            break;
                        }

                        parentIndexCandidate++;
                    }

                    ushort boneId = (ushort)boneIdAllocator.GetIdByName(node);

                    output._boneRemap.Set(boneId, (byte)boneIndex);

                    output._skeleton.Add(new InternalBone(
                        boneId,
                        (byte)parentIndex,
                        node.LocalMatrix,
                        invBindPoseMatrix));

                    boneIndex++;
                }
            }

            var materialIndices = new Dictionary<Material, uint>();

            foreach (var material in root.LogicalMaterials.OrderBy(m => m.Name))
            {
                output._materialNames.Add(material.Name);
                materialIndices.Add(material, (uint)materialIndices.Count);
            }

            var meshIdAllocator = new IdAllocator(root.LogicalMeshes, MeshIdPrefix);
            var groupIdAllocator = new IdAllocator(root.LogicalMeshes, GroupIdPrefix);

            var primitives = new List<(ulong Key, uint MeshId, uint GroupId, MTMeshType MeshType, MeshPrimitive Primitive)>();

            foreach (var mesh in root.LogicalMeshes)
            {
                foreach (var primitive in mesh.Primitives)
                {
                    MTMeshType meshType = MTMeshType.General;

                    if (mesh.Name.EndsWith(EdgeSuffix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        meshType = MTMeshType.Edge;
                    }
                    else if (mesh.Name.EndsWith(HeadwearSuffix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        meshType = MTMeshType.Headwear;
                    }

                    uint meshId = meshIdAllocator.GetIdByName(mesh);
                    uint groupId = groupIdAllocator.GetIdByName(mesh, 0);

                    // Meshes are sorted first by material index, then by the vertex stride and then by mesh ID.
                    ulong key = (ulong)materialIndices[primitive.Material] << 32;
                    key |= GltfVertexReader.GetStride(primitive, meshType == MTMeshType.Edge) << 16;
                    key |= meshId;

                    primitives.Add((key, meshId, groupId, meshType, primitive));
                }
            }

            // Ensure meshes are in the correct order.
            primitives.Sort((x, y) => x.Key.CompareTo(y.Key));

            foreach ((_, uint meshId, uint groupId, var meshTpye, var primitive) in primitives)
            {
                var reader = new GltfVertexReader();
                byte weightsPerVertex = 0;

                foreach (var attributeKey in primitive.VertexAccessors.Keys)
                {
                    var accessor = primitive.GetVertices(attributeKey);
                    reader.Read(attributeKey, accessor);

                    if (attributeKey == "JOINTS_0")
                    {
                        weightsPerVertex = accessor.Attribute.Dimensions switch
                        {
                            DimensionType.SCALAR => 1,
                            DimensionType.VEC2 => 2,
                            DimensionType.VEC3 => 3,
                            _ => 4
                        };
                    }
                }

                (var vertices, var formatFlags) = reader.GetVertices();

                var indices = new List<ushort>();

                ReadIndices(indices, primitive);

                if (!materialIndices.TryGetValue(primitive.Material, out uint materialIndex))
                {
                    materialIndex = 0;
                }

                NsPrimitiveType primitiveType = primitive.DrawPrimitiveType switch
                {
                    PrimitiveType.POINTS => NsPrimitiveType.Points,
                    PrimitiveType.LINES or PrimitiveType.LINE_LOOP or PrimitiveType.LINE_STRIP => NsPrimitiveType.Lines,
                    _ => NsPrimitiveType.Triangles
                };

                output._meshes.Add(new InternalMesh(
                    vertices,
                    formatFlags,
                    primitiveType,
                    [.. indices],
                    meshTpye,
                    meshId,
                    groupId,
                    materialIndex,
                    weightsPerVertex));
            }

            return output;
        }

        private static void ReadIndices(List<ushort> indices, MeshPrimitive primitive)
        {
            switch (primitive.DrawPrimitiveType)
            {
                case PrimitiveType.POINTS:
                    foreach (var a in primitive.GetPointIndices())
                    {
                        indices.Add((ushort)a);
                    }
                    break;
                case PrimitiveType.LINES:
                case PrimitiveType.LINE_LOOP:
                case PrimitiveType.LINE_STRIP:
                    foreach ((var a, var b) in primitive.GetLineIndices())
                    {
                        indices.Add((ushort)a);
                        indices.Add((ushort)b);
                    }
                    break;
                case PrimitiveType.TRIANGLES:
                case PrimitiveType.TRIANGLE_STRIP:
                case PrimitiveType.TRIANGLE_FAN:
                    foreach ((var a, var b, var c) in primitive.GetTriangleIndices())
                    {
                        indices.Add((ushort)a);
                        indices.Add((ushort)b);
                        indices.Add((ushort)c);
                    }
                    break;
            }
        }

        public unsafe void ExportGltf(string outputBinaryFileName, MTMaterial materials, string? basePath = null)
        {
            var root = ModelRoot.CreateModel();
            var scene = root.UseScene("Default");
            var skin = root.CreateSkin();

            string[] textureNames = materials.GetTextureNames();

            Image[] ddsImages = new Image[textureNames.Length];
            Image[] pngImages = new Image[textureNames.Length];

            if (basePath != null)
            {
                for (int i = 0; i < textureNames.Length; i++)
                {
                    string texturePath = textureNames[i];

                    texturePath = Path.Combine(basePath, texturePath + ".tex");

                    if (!File.Exists(texturePath))
                    {
                        continue;
                    }

                    var texture = MTTexture.Load(texturePath);

                    string? parentOutputDirectory = Path.GetDirectoryName(outputBinaryFileName);
                    string relTexturePath = parentOutputDirectory != null ? Path.GetRelativePath(parentOutputDirectory, texturePath) : texturePath;

                    if (relTexturePath == texturePath)
                    {
                        relTexturePath = Path.GetFileNameWithoutExtension(texturePath);
                    }

                    ddsImages[i] = root.CreateImage(Path.GetFileNameWithoutExtension(textureNames[i]));
                    ddsImages[i].Content = new MemoryImage(texture.EncodeAsDds());
                    ddsImages[i].AlternateWriteFileName = PathUtils.ReplaceExtension(relTexturePath, ".tex", ".dds").Replace('\\', '/');

                    byte[]? pngData = texture.EncodeAsPng();

                    if (pngData != null)
                    {
                        pngImages[i] = root.CreateImage(Path.GetFileNameWithoutExtension(textureNames[i]));
                        pngImages[i].Content = new MemoryImage(pngData);
                        pngImages[i].AlternateWriteFileName = PathUtils.ReplaceExtension(relTexturePath, ".tex", ".png").Replace('\\', '/');
                    }
                }
            }

            Material[] outMaterials = new Material[_materialNames.Count];

            for (int i = 0; i < _materialNames.Count; i++)
            {
                string materialName = _materialNames[i];

                var material = root.CreateMaterial(materialName);
                material.InitializePBRMetallicRoughness([]);

                ulong albedoTexIndex = materials.GetProperty(materialName, "tAlbedoMap");

                if (albedoTexIndex != 0)
                {
                    material.FindChannel("BaseColor")?.SetTexture(0, ddsImages[(int)(albedoTexIndex - 1)], pngImages[(int)(albedoTexIndex - 1)]);
                }

                var mr = material.FindChannel("MetallicRoughness");
                if (mr != null)
                {
                    mr.Value.Parameters[0].Value = 0f;
                }

                outMaterials[i] = material;
            }

            using MemoryStream bufferData = new();
            BinaryWriter bufferDataWriter = new(bufferData);

            var vertexDataOffsets = new VertexDataOffsets[_meshes.Count];
            var indexDataOffsets = new OffsetAndSize[_meshes.Count];

            int meshIndex = 0;

            foreach (InternalMesh m in _meshes)
            {
                if (m.Indices.Length == 0)
                {
                    continue;
                }

                Vertex[] vertices = m.Vertices;
                VertexFormatFlags formatFlags = m.VertexFormatFlags;

                vertexDataOffsets[meshIndex] = WriteVertexData(bufferDataWriter, vertices, m.Transform(_skeleton), formatFlags);

                long indexStartOffset = bufferData.Position;
                bufferDataWriter.Write(MemoryMarshal.Cast<ushort, byte>(m.Indices));
                indexDataOffsets[meshIndex] = new OffsetAndSize(indexStartOffset, bufferData.Position - indexStartOffset);

                meshIndex++;
            }

            var vertexAndIndexBuffer = root.CreateBuffer((int)bufferData.Length);
            bufferData.Seek(0, SeekOrigin.Begin);
            bufferData.ReadExactly(vertexAndIndexBuffer.Content);

            meshIndex = 0;

            foreach (InternalMesh m in _meshes)
            {
                if (m.Indices.Length == 0)
                {
                    continue;
                }

                Vertex[] vertices = m.Vertices;
                VertexFormatFlags formatFlags = m.VertexFormatFlags;

                string suffix = string.Empty;

                if (m.Type == MTMeshType.Edge)
                {
                    suffix = EdgeSuffix;
                }
                else if (m.Type == MTMeshType.Headwear)
                {
                    suffix = HeadwearSuffix;
                }

                var mesh = root.CreateMesh($"{MeshIdPrefix}{m.Id}{GroupIdPrefix}{m.GroupId}{suffix}");
                var prim = mesh.CreatePrimitive();

                void AddVertexAttribute(string attributeKey, OffsetAndSize offsetAndSize, DimensionType dimensions, EncodingType encoding, bool normalized = false)
                {
                    BufferView bufferView = root.UseBufferView(
                        vertexAndIndexBuffer,
                        (int)offsetAndSize.Offset,
                        (int)offsetAndSize.Size,
                        0,
                        BufferMode.ARRAY_BUFFER);

                    var vertexAccessor = root.CreateAccessor();
                    vertexAccessor.SetVertexData(bufferView, 0, vertices.Length, dimensions, encoding, normalized);

                    prim.SetVertexAccessor(attributeKey, vertexAccessor);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasPosition))
                {
                    AddVertexAttribute("POSITION", vertexDataOffsets[meshIndex].PositionOffset, DimensionType.VEC3, EncodingType.FLOAT);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasNormal))
                {
                    AddVertexAttribute("NORMAL", vertexDataOffsets[meshIndex].NormalOffset, DimensionType.VEC3, EncodingType.FLOAT);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasTangent))
                {
                    AddVertexAttribute("TANGENT", vertexDataOffsets[meshIndex].TangentOffset, DimensionType.VEC3, EncodingType.FLOAT);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasTexCoord0))
                {
                    AddVertexAttribute("TEXCOORD_0", vertexDataOffsets[meshIndex].UV0Offset, DimensionType.VEC2, EncodingType.FLOAT);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasTexCoord1))
                {
                    AddVertexAttribute("TEXCOORD_1", vertexDataOffsets[meshIndex].UV1Offset, DimensionType.VEC2, EncodingType.FLOAT);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasColor))
                {
                    AddVertexAttribute("COLOR", vertexDataOffsets[meshIndex].ColorOffset, DimensionType.VEC4, EncodingType.FLOAT);
                }

                if (formatFlags.HasFlag(VertexFormatFlags.HasJoints0))
                {
                    AddVertexAttribute("JOINTS_0", vertexDataOffsets[meshIndex].Joints0Offset, DimensionType.VEC4, EncodingType.UNSIGNED_BYTE);
                    AddVertexAttribute("WEIGHTS_0", vertexDataOffsets[meshIndex].Weights0Offset, DimensionType.VEC4, EncodingType.FLOAT);

                    if (formatFlags.HasFlag(VertexFormatFlags.HasJoints1))
                    {
                        AddVertexAttribute("JOINTS_1", vertexDataOffsets[meshIndex].Joints1Offset, DimensionType.VEC4, EncodingType.UNSIGNED_BYTE);
                        AddVertexAttribute("WEIGHTS_1", vertexDataOffsets[meshIndex].Weights1Offset, DimensionType.VEC4, EncodingType.FLOAT);
                    }
                }

                BufferView indexBufferView = root.UseBufferView(
                    vertexAndIndexBuffer,
                    (int)indexDataOffsets[meshIndex].Offset,
                    (int)indexDataOffsets[meshIndex].Size,
                    0,
                    BufferMode.ELEMENT_ARRAY_BUFFER);

                var indexAccessor = root.CreateAccessor();
                indexAccessor.SetIndexData(indexBufferView, 0, m.Indices.Length, IndexEncodingType.UNSIGNED_SHORT);

                prim.SetIndexAccessor(indexAccessor);
                prim.Material = outMaterials[m.MaterialIndex];
                prim.DrawPrimitiveType = m.PrimitiveType switch
                {
                    NsPrimitiveType.Points => PrimitiveType.POINTS,
                    NsPrimitiveType.Lines => PrimitiveType.LINES,
                    _ => PrimitiveType.TRIANGLES
                };

                var node = scene.CreateNode();
                node.Mesh = mesh;

                if (formatFlags.HasFlag(VertexFormatFlags.HasJoints0))
                {
                    node.Skin = skin;
                }

                meshIndex++;
            }

            if (_skeleton.Count > 0)
            {
                var skeletonRootNode = scene.CreateNode();
                skin.Skeleton = skeletonRootNode;

                var skeletonNodes = new Node[_skeleton.Count];

                AddSkeletonChildNodes(skin, skeletonRootNode, skeletonNodes, byte.MaxValue);

                skin.BindJoints(skeletonNodes);
            }

            var settings = new WriteSettings
            {
                JsonIndented = true,
                Validation = ValidationMode.Skip,
                ImageWriting = ResourceWriteMode.SatelliteFile
            };

            root.SaveGLB(outputBinaryFileName, settings);
        }

        private void AddSkeletonChildNodes(Skin skin, Node parent, Node[] skeletonNodes, byte parentIndex)
        {
            int boneIndex = 0;

            foreach (var bone in _skeleton)
            {
                if (bone.ParentIndex == parentIndex && skeletonNodes[boneIndex] == null)
                {
                    var child = parent.CreateNode($"{BoneIdPrefix}{bone.Id}");

                    child.LocalMatrix = bone.LocalTransform;

                    skeletonNodes[boneIndex] = child;

                    AddSkeletonChildNodes(skin, child, skeletonNodes, (byte)boneIndex);
                }

                boneIndex++;
            }
        }

        private unsafe static VertexDataOffsets WriteVertexData(BinaryWriter writer, Vertex[] vertices, Vector3[] positions, VertexFormatFlags formatFlags)
        {
            long positionOffset = 0;
            long positionSize = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasPosition))
            {
                positionOffset = writer.BaseStream.Position;

                for (int i = 0; i < positions.Length; i++)
                {
                    writer.Write(positions[i]);
                }

                positionSize = writer.BaseStream.Position - positionOffset;
            }

            long normalOffset = 0;
            long normalSize = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasNormal))
            {
                normalOffset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    writer.Write(vertices[i].Normal.Xyz());
                }

                normalSize = writer.BaseStream.Position - normalOffset;
            }

            long tangentOffset = 0;
            long tangentSize = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasTangent))
            {
                tangentOffset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    writer.Write(vertices[i].Tangent.Xyz());
                }

                tangentSize = writer.BaseStream.Position - tangentOffset;
            }

            long uv0Offset = 0;
            long uv0Size = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasTexCoord0))
            {
                uv0Offset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    writer.Write(vertices[i].TexCoord0);
                }

                uv0Size = writer.BaseStream.Position - uv0Offset;
            }

            long uv1Offset = 0;
            long uv1Size = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasTexCoord1))
            {
                uv1Offset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    writer.Write(vertices[i].TexCoord1);
                }

                uv1Size = writer.BaseStream.Position - uv1Offset;
            }

            long colorOffset = 0;
            long colorSize = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasColor))
            {
                colorOffset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    writer.Write(vertices[i].Color);
                }

                colorSize = writer.BaseStream.Position - colorOffset;
            }

            long joints0Offset = 0, joints0Size = 0, weights0Offset = 0, weights0Size = 0;
            long joints1Offset = 0, joints1Size = 0, weights1Offset = 0, weights1Size = 0;

            if (formatFlags.HasFlag(VertexFormatFlags.HasJoints0))
            {
                joints0Offset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        writer.Write(vertices[i].Joints0[j]);
                    }
                }

                joints0Size = writer.BaseStream.Position - joints0Offset;

                weights0Offset = writer.BaseStream.Position;

                for (int i = 0; i < vertices.Length; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        writer.Write(vertices[i].Weights0[j]);
                    }
                }

                weights0Size = writer.BaseStream.Position - weights0Offset;

                if (formatFlags.HasFlag(VertexFormatFlags.HasJoints1))
                {
                    joints1Offset = writer.BaseStream.Position;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            writer.Write(vertices[i].Joints1[j]);
                        }
                    }

                    joints1Size = writer.BaseStream.Position - joints1Offset;

                    weights1Offset = writer.BaseStream.Position;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            writer.Write(vertices[i].Weights1[j]);
                        }
                    }

                    weights1Size = writer.BaseStream.Position - weights1Offset;
                }
            }

            return new VertexDataOffsets(
                new OffsetAndSize(positionOffset, positionSize),
                new OffsetAndSize(normalOffset, normalSize),
                new OffsetAndSize(tangentOffset, tangentSize),
                new OffsetAndSize(uv0Offset, uv0Size),
                new OffsetAndSize(uv1Offset, uv1Size),
                new OffsetAndSize(colorOffset, colorSize),
                new OffsetAndSize(joints0Offset, joints0Size),
                new OffsetAndSize(joints1Offset, joints1Size),
                new OffsetAndSize(weights0Offset, weights0Size),
                new OffsetAndSize(weights1Offset, weights1Size));
        }
    }
}
