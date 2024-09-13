using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MHS2ModTool.GameFileFormats
{
    internal struct RotationDecomposed
    {
        private const float RadsToDegs = 180f / MathF.PI;
        private const float DegsToRads = MathF.PI/ 180f;

        public Vector3 Rotation;

        public RotationDecomposed(Matrix4x4 matrix)
        {
            if (Matrix4x4.Decompose(matrix, out _, out Quaternion rotation, out _))
            {
                Rotation = rotation.ToYawPitchRoll() * RadsToDegs;
            }
            else
            {
                Rotation = Vector3.Zero;
            }
        }

        public readonly Matrix4x4 ToMatrix4x4()
        {
            Vector3 r = Rotation * DegsToRads;

            return Matrix4x4.CreateFromYawPitchRoll(r.Z, r.Y, r.X);
        }
    }

    internal struct Chain
    {
        public byte Collision;
        public byte Weightiness;
        public Vector3 Gravity;
        public float Damping;
        public float ConeLimit;
        public float Tension;
        public float WindMultiplier;
        public float UnknownFloatTwo;
        public float UnknownFloatThree;
        public float UnknownFloatFour;
        public ChainNode[] ChainNodes;

        public Chain()
        {
            ChainNodes = [];
        }

        public Chain(int chainLength)
        {
            ChainNodes = new ChainNode[chainLength];
        }
    }

    internal struct ChainNode
    {
        public RotationDecomposed Orientation;
        public bool FixedEnd;
        public byte Unknown2;
        public byte Unknown3;
        public byte Unknown4;
        public byte Unknown5;
        public byte Unknown6;
        public int BoneFunctionId;
        public byte Unknown7;
        public float Radius;
        public float MotionScale;
        public float Unknown11;
        public float Unknown12;
    }

    internal class TinyChain
    {
        public float StepTime;
        public float SpringScaling;
        public float GlobalDamping;
        public float GlobalTransmissiveForceCoefficient;
        public float GravityScaling;
        public float WindScale;
        public Chain[] Chains;

        public TinyChain()
        {
            Chains = [];
        }

        public TinyChain(int chainsCount)
        {
            Chains = new Chain[chainsCount];
        }
    }

    [JsonSourceGenerationOptions(IncludeFields = true, WriteIndented = true)]
    [JsonSerializable(typeof(TinyChain))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }

    internal class MTCnsTinyChain
    {
        private const uint Magic = 0x435443;
        private const uint Version = 23;
        private const uint PaddingValue = 0xCDCDCDCD;

        private struct Header
        {
            public uint Magic;
            public uint Version;
            public uint Unknown0; // 0
            public uint Unknown1; // 1000
            public int ARecordsCount;
            public int BRecordsCount;
            public int UnknownConstantInt;
            public float StepTime;
            public float SpringScaling;
            public float GlobalDamping;
            public float GlobalTransmissiveForceCoefficient;
            public float GravityScaling;
            public float WindScale;
            public byte CollisionFilterHit0;
            public byte CollisionFilterHit1;
            public byte CollisionFilterHit2;
            public byte CollisionFilterHit3;
            public byte CollisionFilterHit4;
            public byte CollisionFilterHit5;
            public byte CollisionFilterHit6;
            public byte CollisionFilterHit7;
        }

        private struct ARecord
        {
            public int ChainLength;
            public byte Collision;
            public byte Weightiness;
            public byte Unknown0;
            public byte Unknown1;
            public uint Unknown2; // 0xFFFFFFFF
            public uint Unknown3; // 1
            public uint Unknown4; // 1
            public uint Padding0;
            public uint Padding1;
            public uint Padding2;
            public Vector4 Gravity;
            public float Damping;
            public float ConeLimit;
            public float Tension;
            public float WindMultiplier;
            public float UnknownFloatTwo;
            public float UnknownFloatThree;
            public float UnknownFloatFour;
            public uint Padding3;
        }

        private struct BRecord
        {
            public Matrix4x4 MotionLimit;
            public byte Unknown0;
            public byte Unknown1;
            public bool FixedEnd;
            public byte Unknown2;
            public byte Unknown3;
            public byte Unknown4;
            public byte Unknown5;
            public byte Unknown6;
            public int BoneFunctionId;
            public byte Unknown7;
            public byte Unknown8;
            public byte Unknown9;
            public byte Unknown10;
            public float Radius;
            public float MotionScale;
            public float Unknown11;
            public float Unknown12;
        }

        private TinyChain _content;

        public static MTCnsTinyChain Load(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            return Load(fs);
        }

        public static MTCnsTinyChain Load(Stream stream)
        {
            using var reader = new BinaryReader(stream);

            var header = reader.Read<Header>();

            var content = new TinyChain(header.ARecordsCount)
            {
                StepTime = header.StepTime,
                SpringScaling = header.SpringScaling,
                GlobalDamping = header.GlobalDamping,
                GlobalTransmissiveForceCoefficient = header.GlobalTransmissiveForceCoefficient,
                GravityScaling = header.GravityScaling,
                WindScale = header.WindScale,
            };

            for (int i = 0; i < header.ARecordsCount; i++)
            {
                var aRecord = reader.Read<ARecord>();

                content.Chains[i] = new Chain(aRecord.ChainLength)
                {
                    Collision = aRecord.Collision,
                    Weightiness = aRecord.Weightiness,
                    Gravity = aRecord.Gravity.Xyz(),
                    Damping = aRecord.Damping,
                    ConeLimit = aRecord.ConeLimit,
                    Tension = aRecord.Tension,
                    WindMultiplier = aRecord.WindMultiplier,
                    UnknownFloatTwo = aRecord.UnknownFloatTwo,
                    UnknownFloatThree = aRecord.UnknownFloatThree,
                    UnknownFloatFour = aRecord.UnknownFloatFour,
                };
            }

            int chainIndex = 0;
            int chainCount = 0;

            for (int i = 0; i < header.BRecordsCount; i++)
            {
                var bRecord = reader.Read<BRecord>();

                if (chainCount == content.Chains[chainIndex].ChainNodes.Length)
                {
                    chainIndex++;
                    chainCount = 0;
                }

                content.Chains[chainIndex].ChainNodes[chainCount++] = new ChainNode()
                {
                    Orientation = new RotationDecomposed(bRecord.MotionLimit),
                    FixedEnd = bRecord.FixedEnd,
                    Unknown2 = bRecord.Unknown2,
                    Unknown3 = bRecord.Unknown3,
                    Unknown4 = bRecord.Unknown4,
                    Unknown5 = bRecord.Unknown5,
                    Unknown6 = bRecord.Unknown6,
                    BoneFunctionId = bRecord.BoneFunctionId,
                    Unknown7 = bRecord.Unknown7,
                    Radius = bRecord.Radius,
                    MotionScale = bRecord.MotionScale,
                    Unknown11 = bRecord.Unknown11,
                    Unknown12 = bRecord.Unknown12,
                };
            }

            return new MTCnsTinyChain() { _content = content };
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

            writer.Write(new Header()
            {
                Magic = Magic,
                Version = Version,
                Unknown0 = 0,
                Unknown1 = 1000,
                ARecordsCount = _content.Chains.Length,
                BRecordsCount = _content.Chains.Sum(c => c.ChainNodes.Length),
                UnknownConstantInt = 4096,
                StepTime = _content.StepTime,
                SpringScaling = _content.SpringScaling,
                GlobalDamping = _content.GlobalDamping,
                GlobalTransmissiveForceCoefficient = _content.GlobalTransmissiveForceCoefficient,
                GravityScaling = _content.GravityScaling,
                WindScale = _content.WindScale,
                CollisionFilterHit0 = 1,
                CollisionFilterHit1 = 1,
                CollisionFilterHit2 = 1,
                CollisionFilterHit3 = 1,
                CollisionFilterHit4 = 1,
                CollisionFilterHit5 = 1,
                CollisionFilterHit6 = 0,
                CollisionFilterHit7 = 0,
            });

            foreach (var chain in _content.Chains)
            {
                writer.Write(new ARecord()
                {
                    ChainLength = chain.ChainNodes.Length,
                    Collision = chain.Collision,
                    Weightiness = chain.Weightiness,
                    Unknown0 = 0,
                    Unknown1 = 0,
                    Unknown2 = uint.MaxValue,
                    Unknown3 = 1,
                    Unknown4 = 1,
                    Padding0 = PaddingValue,
                    Padding1 = PaddingValue,
                    Padding2 = PaddingValue,
                    Gravity = new Vector4(chain.Gravity, 0f),
                    Damping = chain.Damping,
                    ConeLimit = chain.ConeLimit,
                    Tension = chain.Tension,
                    WindMultiplier = chain.WindMultiplier,
                    UnknownFloatTwo = chain.UnknownFloatTwo,
                    UnknownFloatThree = chain.UnknownFloatThree,
                    UnknownFloatFour = chain.UnknownFloatFour,
                    Padding3 = PaddingValue,
                });
            }

            foreach (var chain in _content.Chains)
            {
                foreach (var node in chain.ChainNodes)
                {
                    writer.Write(new BRecord()
                    {
                        MotionLimit = node.Orientation.ToMatrix4x4(),
                        Unknown0 = 0,
                        Unknown1 = 0,
                        FixedEnd = node.FixedEnd,
                        Unknown2 = node.Unknown2,
                        Unknown3 = node.Unknown3,
                        Unknown4 = node.Unknown4,
                        Unknown5 = node.Unknown5,
                        Unknown6 = node.Unknown6,
                        BoneFunctionId = node.BoneFunctionId,
                        Unknown7 = node.Unknown7,
                        Unknown8 = 0,
                        Unknown9 = 0,
                        Unknown10 = 0,
                        Radius = node.Radius,
                        MotionScale = node.MotionScale,
                        Unknown11 = node.Unknown11,
                        Unknown12 = node.Unknown12,
                    });
                }
            }

        }

        public static MTCnsTinyChain ImportJson(string inputJsonFileName)
        {
            var content = (TinyChain?)JsonSerializer.Deserialize(File.ReadAllText(inputJsonFileName), typeof(TinyChain), SourceGenerationContext.Default);

            return new MTCnsTinyChain() { _content = content ?? new TinyChain(0) };
        }

        public void ExportJson(string outputJsonFileName)
        {
            File.WriteAllText(outputJsonFileName, JsonSerializer.Serialize(_content, typeof(TinyChain), SourceGenerationContext.Default));
        }
    }
}
