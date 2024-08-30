using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MHS2ModTool.GameFileFormats
{
    internal class MTMaterial
    {
        private struct Header
        {
            public uint Magic;
            public uint Version;
            public uint MaterialCount;
            public uint TextureCount;
            public long Unknown;
            public long TexturesOffset;
            public long MaterialsOffset;
        }

        private unsafe struct TextureDescriptor
        {
            public uint FileTypeCode;
            public fixed byte Unknown[20];
            public MTName Name;
        }

        private unsafe struct MaterialDescriptor
        {
            public uint DescriptorId;
            public uint Unknown;
            public uint MaterialNameHash;
            public uint MaterialSize;
            public uint ShaderHash;
            public uint SkinId;
            public uint ResourceType;
            public byte PropertiesCount;
            public byte Padding1;
            public byte Padding2;
            public byte Padding3;
            public uint Unknown3;
            public Vector4 Unknown4;
            public uint SecondaryMaterialSize;
            public long DataOffset;
            public long Data2Offset;
        }

        private enum MaterialPropertyType : byte
        {
            TextureId = 0xc3,
        }

        private unsafe struct MaterialPropertyDescriptor
        {
            public MaterialPropertyType PropertyType;
            public byte Idk;
            public byte Idk2;
            public byte Idk3;
            public uint Padding;
            public ulong Value;
            public ulong Value2;
        }

        private class InternalMaterial
        {
            public readonly uint NameHash;
            public readonly Dictionary<string, ulong> Properties;

            public InternalMaterial(uint nameHash)
            {
                NameHash = nameHash;
                Properties = new();
            }
        }

        private readonly List<string> _textureNames = new();
        private readonly List<InternalMaterial> _materials = new();

        public static MTMaterial Load(string fileName)
        {
            using FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            return Load(fs);
        }

        public static MTMaterial Load(Stream stream)
        {
            MTMaterial output = new();

            long start = stream.Position;

            using var reader = new BinaryReader(stream);

            var header = reader.Read<Header>();

            stream.Seek(start + header.TexturesOffset, SeekOrigin.Begin);

            for (int i = 0; i < header.TextureCount; i++)
            {
                var textureDesc = reader.Read<TextureDescriptor>();

                output._textureNames.Add(textureDesc.Name.GetString());
            }

            stream.Seek(start + header.MaterialsOffset, SeekOrigin.Begin);

            List<MaterialDescriptor> materials = new();

            for (int i = 0; i < header.MaterialCount; i++)
            {
                var materialDesc = reader.Read<MaterialDescriptor>();

                materials.Add(materialDesc);
            }

            foreach (MaterialDescriptor material in materials)
            {
                stream.Seek(start + material.DataOffset, SeekOrigin.Begin);

                InternalMaterial m = new(material.MaterialNameHash);

                for (int j = 0; j < material.PropertiesCount; j++)
                {
                    MaterialPropertyDescriptor matPropDesc = reader.Read<MaterialPropertyDescriptor>();

                    // Console.WriteLine($"property type: 0x{matPropDesc.PropertyType:X} {matPropDesc.Value}");

                    switch (matPropDesc.PropertyType)
                    {
                        case MaterialPropertyType.TextureId:
                            if (matPropDesc.Value2 == 0xcd06f363)
                            {
                                m.Properties["tAlbedoMap"] = matPropDesc.Value;
                            }
                            break;
                    }
                }

                output._materials.Add(m);
            }

            return output;
        }

        public string[] GetTextureNames()
        {
            return _textureNames.ToArray();
        }

        public ulong GetProperty(string materialName, string propertyName)
        {
            uint nameHash = ~Crc32.HashToUInt32(Encoding.ASCII.GetBytes(materialName));

            foreach (var m in _materials)
            {
                if (m.NameHash == nameHash && m.Properties.TryGetValue(propertyName, out ulong value))
                {
                    return value;
                }
            }

            return default;
        }
    }
}
