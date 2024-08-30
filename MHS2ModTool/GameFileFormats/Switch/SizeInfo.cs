namespace MHS2ModTool.GameFileFormats.Switch
{
    public readonly struct SizeInfo
    {
        private readonly int[] _mipOffsets;

        public readonly int[] AllOffsets;
        public readonly int[] SliceSizes;
        public readonly int[] LevelSizes;
        public int LayerSize { get; }
        public int TotalSize { get; }

        public SizeInfo(int size)
        {
            _mipOffsets = [0];
            AllOffsets = [0];
            SliceSizes = [size];
            LevelSizes = [size];
            LayerSize = size;
            TotalSize = size;
        }

        internal SizeInfo(
            int[] mipOffsets,
            int[] allOffsets,
            int[] sliceSizes,
            int[] levelSizes,
            int layerSize,
            int totalSize)
        {
            _mipOffsets = mipOffsets;
            AllOffsets = allOffsets;
            SliceSizes = sliceSizes;
            LevelSizes = levelSizes;
            LayerSize = layerSize;
            TotalSize = totalSize;
        }

        public int GetMipOffset(int level)
        {
            if ((uint)level >= _mipOffsets.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            return _mipOffsets[level];
        }
    }
}
