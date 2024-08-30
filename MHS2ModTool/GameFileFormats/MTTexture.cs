using MHS2ModTool.CommonFileFormats;
using MHS2ModTool.GameFileFormats.Switch;
using System.Runtime.CompilerServices;

namespace MHS2ModTool.GameFileFormats
{
    internal class MTTexture
    {
        private const uint Magic = 0x584554;
        private const ushort Version = 0x30a3; // 0xb0a3 is also possible?

        private enum TextureDims
        {
            T2D = 2,
            T3D = 3,
            TCube = 6
        }

        private enum TextureFormat : byte
        {
            Invalid,
            Rgba32Float,
            Rgba16Float,
            Rgba16Unorm,
            Rgba16Snorm,
            Rg32Float,
            Rgb10A2Unorm,
            Rgba8Unorm,
            Rgba8Snorm,
            Rgba8Srgb,
            Rgba4Unorm,
            Rg16Float,
            Rg16Unorm,
            Rg16Snorm,
            R32Float,
            D24UnormS8,
            R16Float,
            R16Unorm,
            R8Unorm,
            Bc1,
            Bc1Srgb,
            Bc2,
            Bc2Srgb,
            Bc3,
            Bc3Srgb,
            Bc4Unorm,
            Bc4Snorm,
            Bc5Snorm,
            Bgr565,
            Bgr5A1,
            Bc1_2,
            Bc5Unorm,
            Bc7,
            Bc7_2,
            Bgrx8Unorm,
            Bc7Srgb,
            Bc7Srgb_2,
            Bc7_3,
            R11G11B10Float,
            Bgra8Unorm,
            Bgra8Srgb,
            Bc1_3,
            Bc7_4,
            Bc7_5,
            R8Unorm_2,
            Rgba8Unorm_2,
            Rgb10A2Unorm_2,
            Bc7Srgb_3,
            Invalid_2,
            Invalid_3,
            Invalid_4,
            Rg8Unorm,
            R8Unorm_3,
            Rg8Unorm_2,
            Bc7_6,
            Bc7Srgb_4
        }

        private readonly struct Bitfield0
        {
            private readonly uint _word;

            public Bitfield0(ushort version, uint baseLevel, TextureDims textureDims)
            {
                _word = version | (baseLevel << 24) | ((uint)textureDims << 28);
            }

            public ushort UnpackVersion()
            {
                return (ushort)_word;
            }

            public uint UnpackBaseLevel()
            {
                return (_word >> 24) & 0xf;
            }

            public TextureDims UnpackTextureDims()
            {
                return (TextureDims)((_word >> 28) & 0xf);
            }
        }

        private readonly struct Bitfield1
        {
            private readonly uint _word;

            public Bitfield1(uint mipCount, uint width, uint height)
            {
                _word = mipCount | (width << 6) | (height << 19);
            }

            public uint UnpackMipCount()
            {
                return _word & 0x1f;
            }

            public uint UnpackWidth()
            {
                return (_word >> 6) & 0x1fff;
            }

            public uint UnpackHeight()
            {
                return _word >> 19;
            }
        }

        private readonly struct Bitfield2
        {
            private readonly uint _word;

            public Bitfield2(byte layers, TextureFormat format, ushort depth)
            {
                _word = layers | ((uint)format << 8) | ((uint)depth << 16);
            }

            public byte UnpackLayers()
            {
                return (byte)_word;
            }

            public TextureFormat UnpackFormat()
            {
                return (TextureFormat)(_word >> 8);
            }

            public ushort UnpackDepth()
            {
                return (ushort)(_word >> 16);
            }
        }

        private struct Header
        {
            public uint Magic;
            public Bitfield0 Bitfield0;
            public Bitfield1 Bitfield1;
            public Bitfield2 Bitfield2;
        }

        private TextureDims _textureDims;
        private uint _baseLevel;
        private uint _mipCount;
        private uint _width;
        private uint _height;
        private byte _layers;
        private TextureFormat _format;
        private ushort _depth;
        private byte[]? _data;

        public static MTTexture Load(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            return Load(fs);
        }

        public static MTTexture Load(Stream stream)
        {
            MTTexture output = new();

            long start = stream.Position;

            using var reader = new BinaryReader(stream);

            var header = reader.Read<Header>();

            var textureDims = header.Bitfield0.UnpackTextureDims();
            uint mipCount = header.Bitfield1.UnpackMipCount();
            uint width = header.Bitfield1.UnpackWidth();
            uint height = header.Bitfield1.UnpackHeight();
            byte layers = header.Bitfield2.UnpackLayers();
            TextureFormat format = header.Bitfield2.UnpackFormat();
            ushort depth = header.Bitfield2.UnpackDepth();

            (uint bw, uint bh) = GetCompressionBlockSize(format);
            uint bpp = GetBytesPerPixel(format);

            uint linearSize = CalculateTextureSize(width, height, depth, layers, mipCount, bw, bh, bpp);

            // PC textures uses 8 bytes per mip offset, doesn't have the file length after the header,
            // and uses linear texture data. We can guess which one it is from the file size.
            bool isPCTexture = (stream.Length - start) == Unsafe.SizeOf<Header>() + mipCount * sizeof(ulong) + linearSize;

            byte[] data;

            if (isPCTexture)
            {
                ulong[] mipOffsets = new ulong[mipCount];

                for (uint i = 0; i < mipCount; i++)
                {
                    mipOffsets[i] = reader.ReadUInt64();
                }

                data = reader.ReadBytes((int)linearSize);
            }
            else
            {
                uint totalLength = reader.ReadUInt32();
                uint[] mipOffsets = new uint[mipCount];

                for (uint i = 0; i < mipCount; i++)
                {
                    mipOffsets[i] = reader.ReadUInt32();
                }

                SizeInfo sizeInfo = SizeCalculator.GetBlockLinearTextureSize(
                    (int)width,
                    (int)height,
                    depth,
                    (int)mipCount,
                    layers,
                    (int)bw,
                    (int)bh,
                    (int)bpp,
                    16,
                    1,
                    1);

                data = reader.ReadBytes(sizeInfo.TotalSize);

                data = LayoutConverter.ConvertBlockLinearToLinear(
                    (int)width,
                    (int)height,
                    depth,
                    depth,
                    (int)mipCount,
                    layers,
                    (int)bw,
                    (int)bh,
                    (int)bpp,
                    16,
                    1,
                    1,
                    sizeInfo,
                    data);
            }

            output._textureDims = textureDims;
            output._baseLevel = header.Bitfield0.UnpackBaseLevel();
            output._mipCount = mipCount;
            output._width = width;
            output._height = height;
            output._layers = layers;
            output._format = format;
            output._depth = depth;
            output._data = data;

            return output;
        }

        public void Save(string fileName, TargetPlatform targetPlatform)
        {
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            Save(fs, targetPlatform);
        }

        public void Save(Stream stream, TargetPlatform targetPlatform)
        {
            if (_data == null)
            {
                return;
            }

            using var writer = new BinaryWriter(stream);

            writer.Write(new Header()
            {
                Magic = Magic,
                Bitfield0 = new(Version, _baseLevel, _textureDims),
                Bitfield1 = new(_mipCount, _width, _height),
                Bitfield2 = new(_layers, _format, _depth)
            });

            (uint bw, uint bh) = GetCompressionBlockSize(_format);
            uint bpp = GetBytesPerPixel(_format);

            if (targetPlatform == TargetPlatform.PC)
            {
                ulong mipOffset = (uint)Unsafe.SizeOf<Header>() + _mipCount * sizeof(ulong);

                for (uint l = 0; l < _mipCount; l++)
                {
                    uint w = Math.Max(_width >> (int)l, 1);
                    uint h = Math.Max(_height >> (int)l, 1);
                    uint d = Math.Max((uint)_depth >> (int)l, 1);

                    writer.Write(mipOffset);

                    mipOffset += CalculateTextureSize(w, h, d, _layers, 1, bw, bh, bpp);
                }

                writer.Write(_data);
            }
            else
            {
                SizeInfo sizeInfo = SizeCalculator.GetBlockLinearTextureSize(
                    (int)_width,
                    (int)_height,
                    _depth,
                    (int)_mipCount,
                    _layers,
                    (int)bw,
                    (int)bh,
                    (int)bpp,
                    16,
                    1,
                    1);

                writer.Write(sizeInfo.TotalSize);

                for (int l = 0; l < _mipCount; l++)
                {
                    writer.Write(sizeInfo.GetMipOffset(l));
                }

                byte[] encoded = new byte[sizeInfo.TotalSize];

                LayoutConverter.ConvertLinearToBlockLinear(
                    encoded,
                    (int)_width,
                    (int)_height,
                    _depth,
                    _depth,
                    (int)_mipCount,
                    _layers,
                    (int)bw,
                    (int)bh,
                    (int)bpp,
                    16,
                    1,
                    1,
                    sizeInfo,
                    _data);

                writer.Write(encoded);
            }
        }

        public static MTTexture ImportDds(string inputFileName, string? originalTexFileName = null)
        {
            var output = new MTTexture();

            byte[] ddsData = File.ReadAllBytes(inputFileName);

            DDSTexture.TryLoadHeader(ddsData, out ImageParameters parameters);

            output._data = new byte[DDSTexture.CalculateSize(parameters)];

            DDSTexture.TryLoadData(ddsData, output._data);

            output._width = (uint)parameters.Width;
            output._height = (uint)parameters.Height;
            output._depth = parameters.Dimensions == ImageDimensions.Dim3D ? (ushort)parameters.DepthOrLayers : (ushort)1;
            output._layers = parameters.Dimensions != ImageDimensions.Dim3D ? (byte)parameters.DepthOrLayers : (byte)1;
            output._mipCount = (uint)parameters.Levels;

            if (originalTexFileName != null && File.Exists(originalTexFileName))
            {
                output._baseLevel = Load(originalTexFileName)._baseLevel;
            }

            output._format = parameters.Format switch
            {
                ImageFormat.Bc1Unorm => TextureFormat.Bc1,
                ImageFormat.Bc1Srgb => TextureFormat.Bc1Srgb,
                ImageFormat.Bc2Unorm => TextureFormat.Bc2,
                ImageFormat.Bc2Srgb => TextureFormat.Bc2Srgb,
                ImageFormat.Bc3Unorm => TextureFormat.Bc3,
                ImageFormat.Bc3Srgb => TextureFormat.Bc3Srgb,
                ImageFormat.Bc4Unorm => TextureFormat.Bc4Unorm,
                ImageFormat.Bc4Snorm => TextureFormat.Bc4Snorm,
                ImageFormat.Bc5Unorm => TextureFormat.Bc5Unorm,
                ImageFormat.Bc5Snorm => TextureFormat.Bc5Snorm,
                ImageFormat.Bc7Unorm => TextureFormat.Bc7_6,
                ImageFormat.Bc7Srgb => TextureFormat.Bc7Srgb_4,
                ImageFormat.R8G8B8A8Srgb => TextureFormat.Rgba8Srgb,
                ImageFormat.R8G8B8A8Unorm => TextureFormat.Rgba8Unorm,
                ImageFormat.B8G8R8A8Srgb => TextureFormat.Bgra8Srgb,
                ImageFormat.B8G8R8A8Unorm => TextureFormat.Bgra8Unorm,
                ImageFormat.R5G6B5Unorm => TextureFormat.Bgr565,
                ImageFormat.R5G5B5A1Unorm => TextureFormat.Bgr5A1,
                ImageFormat.R4G4B4A4Unorm => TextureFormat.Rgba4Unorm,
                _ => TextureFormat.Rgba8Unorm
            };

            output._textureDims = parameters.Dimensions switch
            {
                ImageDimensions.Dim2D or ImageDimensions.Dim2DArray => TextureDims.T2D,
                ImageDimensions.Dim3D => TextureDims.T3D,
                ImageDimensions.DimCube or ImageDimensions.DimCubeArray => TextureDims.TCube,
                _ => TextureDims.T2D
            };

            return output;
        }

        public void Export(string outputFileName)
        {
            using var fs = new FileStream(outputFileName, FileMode.Create, FileAccess.Write);

            DDSTexture.Save(fs, CreateParameters(), _data);
        }

        public byte[] EncodeAsDds()
        {
            using var ms = new MemoryStream();

            DDSTexture.Save(ms, CreateParameters(), _data);

            return ms.ToArray();
        }

        public byte[]? EncodeAsPng()
        {
            byte[]? data = _data;

            var parameters = CreateParameters();

            switch (parameters.Format)
            {
                case ImageFormat.Bc1Srgb:
                case ImageFormat.Bc1Unorm:
                    data = BCnDecoder.DecodeBC1(data, (int)_width, (int)_height, _depth, (int)_mipCount, _layers);
                    break;
                case ImageFormat.Bc2Srgb:
                case ImageFormat.Bc2Unorm:
                    data = BCnDecoder.DecodeBC2(data, (int)_width, (int)_height, _depth, (int)_mipCount, _layers);
                    break;
                case ImageFormat.Bc3Srgb:
                case ImageFormat.Bc3Unorm:
                    data = BCnDecoder.DecodeBC3(data, (int)_width, (int)_height, _depth, (int)_mipCount, _layers);
                    break;
                case ImageFormat.Bc7Srgb:
                case ImageFormat.Bc7Unorm:
                    data = BCnDecoder.DecodeBC7(data, (int)_width, (int)_height, _depth, (int)_mipCount, _layers);
                    break;
                case ImageFormat.R8G8B8A8Srgb:
                case ImageFormat.R8G8B8A8Unorm:
                    break;
                default:
                    return null;
            }

            using var ms = new MemoryStream();

            PNGTexture.Save(ms, new((int)_width, (int)_height, _depth * _layers, (int)_mipCount, ImageFormat.R8G8B8A8Unorm, ImageDimensions.Dim2D), data);

            return ms.ToArray();
        }

        private ImageParameters CreateParameters()
        {
            var format = _format switch
            {
                TextureFormat.Rgba8Unorm or TextureFormat.Rgba8Unorm_2 => ImageFormat.R8G8B8A8Unorm,
                TextureFormat.Rgba8Srgb => ImageFormat.R8G8B8A8Srgb,
                TextureFormat.Rgba4Unorm => ImageFormat.R4G4B4A4Unorm,
                TextureFormat.Bc1 or TextureFormat.Bc1_2 or TextureFormat.Bc1_3 => ImageFormat.Bc1Unorm,
                TextureFormat.Bc1Srgb => ImageFormat.Bc1Srgb,
                TextureFormat.Bc2 => ImageFormat.Bc2Unorm,
                TextureFormat.Bc2Srgb => ImageFormat.Bc2Srgb,
                TextureFormat.Bc3 => ImageFormat.Bc3Unorm,
                TextureFormat.Bc3Srgb => ImageFormat.Bc3Srgb,
                TextureFormat.Bc4Unorm  => ImageFormat.Bc4Unorm,
                TextureFormat.Bc4Snorm => ImageFormat.Bc4Snorm,
                TextureFormat.Bc5Unorm => ImageFormat.Bc5Unorm,
                TextureFormat.Bc5Snorm => ImageFormat.Bc5Snorm,
                TextureFormat.Bgr565 => ImageFormat.R5G6B5Unorm,
                TextureFormat.Bgr5A1 => ImageFormat.R5G5B5A1Unorm,
                TextureFormat.Bc7 or
                TextureFormat.Bc7_2 or
                TextureFormat.Bc7_3 or
                TextureFormat.Bc7_4 or
                TextureFormat.Bc7_5 or
                TextureFormat.Bc7_6 => ImageFormat.Bc7Unorm,
                TextureFormat.Bc7Srgb or
                TextureFormat.Bc7Srgb_2 or
                TextureFormat.Bc7Srgb_3 or
                TextureFormat.Bc7Srgb_4 => ImageFormat.Bc7Srgb,
                TextureFormat.Bgra8Unorm => ImageFormat.B8G8R8A8Unorm,
                TextureFormat.Bgra8Srgb => ImageFormat.B8G8R8A8Srgb,
                _ => ImageFormat.R8G8B8A8Unorm
            };

            var dims = _textureDims switch
            {
                TextureDims.T2D => _layers > 1 ? ImageDimensions.Dim2DArray : ImageDimensions.Dim2D,
                TextureDims.T3D => ImageDimensions.Dim3D,
                TextureDims.TCube => _layers > 1 ? ImageDimensions.DimCubeArray : ImageDimensions.DimCube,
                _ => ImageDimensions.Dim2D
            };

            return new ImageParameters((int)_width, (int)_height, (int)(_depth * _layers), (int)_mipCount, format, dims);
        }

        private static uint GetBytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Rgba32Float:
                case TextureFormat.Bc2:
                case TextureFormat.Bc2Srgb:
                case TextureFormat.Bc3:
                case TextureFormat.Bc3Srgb:
                case TextureFormat.Bc5Snorm:
                case TextureFormat.Bc5Unorm:
                case TextureFormat.Bc7:
                case TextureFormat.Bc7_2:
                case TextureFormat.Bc7Srgb:
                case TextureFormat.Bc7Srgb_2:
                case TextureFormat.Bc7_3:
                case TextureFormat.Bc7_4:
                case TextureFormat.Bc7_5:
                case TextureFormat.Bc7Srgb_3:
                case TextureFormat.Bc7_6:
                case TextureFormat.Bc7Srgb_4:
                    return 16;
                case TextureFormat.Rgba16Float:
                case TextureFormat.Rgba16Unorm:
                case TextureFormat.Rgba16Snorm:
                case TextureFormat.Rg32Float:
                case TextureFormat.Bc1:
                case TextureFormat.Bc1Srgb:
                case TextureFormat.Bc4Unorm:
                case TextureFormat.Bc4Snorm:
                case TextureFormat.Bc1_2:
                case TextureFormat.Bc1_3:
                    return 8;
                case TextureFormat.Rgb10A2Unorm:
                case TextureFormat.Rgba8Unorm:
                case TextureFormat.Rgba8Snorm:
                case TextureFormat.Rgba8Srgb:
                case TextureFormat.Rg16Float:
                case TextureFormat.Rg16Unorm:
                case TextureFormat.Rg16Snorm:
                case TextureFormat.R32Float:
                case TextureFormat.D24UnormS8:
                case TextureFormat.Bgrx8Unorm:
                case TextureFormat.R11G11B10Float:
                case TextureFormat.Bgra8Unorm:
                case TextureFormat.Bgra8Srgb:
                case TextureFormat.Rgba8Unorm_2:
                case TextureFormat.Rgb10A2Unorm_2:
                    return 4;
                case TextureFormat.Rgba4Unorm:
                case TextureFormat.R16Float:
                case TextureFormat.R16Unorm:
                case TextureFormat.Bgr565:
                case TextureFormat.Bgr5A1:
                case TextureFormat.Rg8Unorm:
                case TextureFormat.Rg8Unorm_2:
                    return 2;
                case TextureFormat.R8Unorm:
                case TextureFormat.R8Unorm_2:
                case TextureFormat.R8Unorm_3:
                    return 1;                
            }

            return 0;
        }

        private static (uint, uint) GetCompressionBlockSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Bc1:
                case TextureFormat.Bc1Srgb:
                case TextureFormat.Bc2:
                case TextureFormat.Bc2Srgb:
                case TextureFormat.Bc3:
                case TextureFormat.Bc3Srgb:
                case TextureFormat.Bc4Unorm:
                case TextureFormat.Bc4Snorm:
                case TextureFormat.Bc5Snorm:
                case TextureFormat.Bc1_2:
                case TextureFormat.Bc5Unorm:
                case TextureFormat.Bc7:
                case TextureFormat.Bc7_2:
                case TextureFormat.Bc7Srgb:
                case TextureFormat.Bc7Srgb_2:
                case TextureFormat.Bc7_3:
                case TextureFormat.Bc1_3:
                case TextureFormat.Bc7_4:
                case TextureFormat.Bc7_5:
                case TextureFormat.Bc7Srgb_3:
                case TextureFormat.Bc7_6:
                case TextureFormat.Bc7Srgb_4:
                    return (4, 4);
            }

            return (1, 1);
        }

        private static uint CalculateTextureSize(uint width, uint height, uint depth, uint layers, uint mipCount, uint bw, uint bh, uint bpp)
        {
            uint size = 0;

            for (uint l = 0; l < mipCount; l++)
            {
                uint w = (Math.Max(width >> (int)l, 1) + (bw - 1)) / bw;
                uint h = (Math.Max(height >> (int)l, 1) + (bh - 1)) / bh;
                uint d = Math.Max(depth >> (int)l, 1);

                size += w * h * d * bpp;
            }

            return size * layers;
        }
    }
}
