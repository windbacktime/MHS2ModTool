using MHS2ModTool.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MHS2ModTool.GameFileFormats
{
    internal static class BinaryRWExtensions
    {
        public static T Read<T>(this BinaryReader reader) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(reader.ReadBytes(Unsafe.SizeOf<T>()).AsSpan())[0];
        }

        public static T Read<T>(this BinaryReader reader, Blowfish? crypto = null) where T : unmanaged
        {
            if (crypto != null)
            {
                byte[] data = reader.ReadBytes(Unsafe.SizeOf<T>());
                data = crypto.Decrypt(data);

                return MemoryMarshal.Cast<byte, T>(data.AsSpan())[0];
            }

            return Read<T>(reader);
        }

        public static byte[] ReadBytes(this BinaryReader reader, int length, Blowfish? crypto = null)
        {
            if (crypto != null)
            {
                byte[] data = reader.ReadBytes(length);
                data = crypto.Decrypt(data);

                return data;
            }

            return reader.ReadBytes(length);
        }

        public static void Write<T>(this BinaryWriter writer, T value) where T : unmanaged
        {
            writer.Write(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        public static void Write<T>(this BinaryWriter writer, T value, Blowfish? crypto = null) where T : unmanaged
        {
            if (crypto != null)
            {
                writer.Write(crypto.Encrypt(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1))));
            }
            else
            {
                Write(writer, value);
            }
        }

        public static void Write(this BinaryWriter writer, ReadOnlySpan<byte> data, Blowfish? crypto = null)
        {
            if (crypto != null)
            {
                writer.Write(crypto.Encrypt(data));
            }
            else
            {
                writer.Write(data);
            }
        }
    }
}
