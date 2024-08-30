using System.Numerics;

namespace MHS2ModTool.GameFileFormats
{
    readonly struct MTBoundingSphere
    {
        public readonly Vector3 CenterPosition;
        public readonly float Radius;

        public MTBoundingSphere(Vector3 centerPosition, float radius)
        {
            CenterPosition = centerPosition;
            Radius = radius;
        }

        public static MTBoundingSphere FromAABB(MTAABB boundingBox, float radius)
        {
            return new MTBoundingSphere((boundingBox.Min.Xyz() + boundingBox.Max.Xyz()) / 2f, radius);
        }

        public MTBoundingSphere ExpandRadius(ReadOnlySpan<Vector3> vertices)
        {
            var center = CenterPosition;
            float maxLen = 0f;

            foreach (var vertex in vertices)
            {
                maxLen = MathF.Max(maxLen, (vertex - center).LengthSquared());
            }

            return new(center, MathF.Max(Radius, MathF.Sqrt(maxLen)));
        }
    }
}
