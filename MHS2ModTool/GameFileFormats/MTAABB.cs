using System.Numerics;

namespace MHS2ModTool.GameFileFormats
{
    readonly struct MTAABB
    {
        public static MTAABB Empty => new MTAABB(Vector4.Zero, Vector4.Zero);

        public readonly Vector4 Min;
        public readonly Vector4 Max;

        public MTAABB(Vector4 min, Vector4 max)
        {
            Min = min;
            Max = max;
        }

        public static MTAABB? Expand(MTAABB? boundingBox, ReadOnlySpan<Vector3> vertices)
        {
            if (vertices.Length == 0)
            {
                return null;
            }

            Vector3 min = boundingBox?.Min.Xyz() ?? vertices[0];
            Vector3 max = boundingBox?.Max.Xyz() ?? vertices[0];

            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            return new(new(min, 0f), new(max, 0f));
        }
    }
}
