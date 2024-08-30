using System.Text;

namespace MHS2ModTool.GameFileFormats
{
    unsafe struct MTName
    {
        private const int Length = 128;

        public fixed byte Name[Length];

        public MTName(string name)
        {
            fixed (byte* pName = Name)
            {
                var nameSpan = new Span<byte>(pName, 128);

                int bytesCount = Encoding.UTF8.GetBytes(name, nameSpan);
                if (bytesCount == Length)
                {
                    Name[Length - 1] = 0;
                }
            }
        }

        public string GetString()
        {
            fixed (byte* pName = Name)
            {
                var nameSpan = new Span<byte>(pName, 128);

                int nullTermIndex = nameSpan.IndexOf(new byte[] { 0 });

                if (nullTermIndex >= 0)
                {
                    nameSpan = nameSpan.Slice(0, nullTermIndex);
                }

                return Encoding.UTF8.GetString(nameSpan);
            }
        }

        public override string ToString()
        {
            return GetString();
        }
    }
}
