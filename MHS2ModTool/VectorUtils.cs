using System.Numerics;

namespace MHS2ModTool
{
    internal static class VectorUtils
    {
        public static Vector3 Xyz(this Vector4 vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }

        public static Vector3 Reciprocal(this Vector3 vector)
        {
            return new Vector3(ReciprocalScalar(vector.X), ReciprocalScalar(vector.Y), ReciprocalScalar(vector.Z));
        }

        public static Vector4 Reciprocal(this Vector4 vector)
        {
            return new Vector4(ReciprocalScalar(vector.X), ReciprocalScalar(vector.Y), ReciprocalScalar(vector.Z), ReciprocalScalar(vector.W));
        }

        private static float ReciprocalScalar(float x)
        {
            return x == 0f ? 0f : 1f / x;
        }
    }
}
