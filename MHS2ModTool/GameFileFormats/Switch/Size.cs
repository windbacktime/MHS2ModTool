namespace MHS2ModTool.GameFileFormats.Switch
{
    public readonly struct Size
    {
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        public Size(int width, int height, int depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }
    }
}
