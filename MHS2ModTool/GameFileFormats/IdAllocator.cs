using SharpGLTF.Schema2;

namespace MHS2ModTool.GameFileFormats
{
    internal class IdAllocator
    {
        private readonly Dictionary<LogicalChildOfRoot, uint> _objToIdMap;
        private readonly HashSet<uint> _usedIds;

        public IdAllocator()
        {
            _objToIdMap = new();
            _usedIds = new();
        }

        public IdAllocator(IEnumerable<LogicalChildOfRoot> objs, string prefix) : this()
        {
            Parse(objs, prefix);
        }

        public void Parse(IEnumerable<LogicalChildOfRoot> objs, string prefix)
        {
            foreach (var obj in objs)
            {
                
                Parse(obj, prefix);
            }
        }

        public void Parse(LogicalChildOfRoot obj, string prefix)
        {
            if (TryParse(obj.Name, prefix, out uint id))
            {
                _usedIds.Add(id);
                _objToIdMap[obj] = id;
            }
        }

        public uint GetIdByName(LogicalChildOfRoot obj)
        {
            if (_objToIdMap.TryGetValue(obj, out uint id))
            {
                return id;
            }

            id = 1;

            while (!_usedIds.Add(id))
            {
                id++;
            }

            _objToIdMap.Add(obj, id);

            return id;
        }

        public uint GetIdByName(LogicalChildOfRoot obj, uint fallbackId)
        {
            if (_objToIdMap.TryGetValue(obj, out uint id))
            {
                return id;
            }

            return fallbackId;
        }

        private static bool TryParse(string name, string prefix, out uint value)
        {
            value = 0;
            int prefixIndex = -1;

            do
            {
                prefixIndex = name.IndexOf(prefix, prefixIndex + 1, StringComparison.InvariantCultureIgnoreCase);
                if (prefixIndex < 0)
                {
                    return false;
                }
            } while (prefixIndex + prefix.Length < name.Length && !char.IsAsciiDigit(name[prefixIndex + prefix.Length]));

            int numberIndex = prefixIndex + prefix.Length;
            int numberLength = 0;

            while (numberIndex + numberLength < name.Length && char.IsAsciiDigit(name[numberIndex + numberLength]))
            {
                numberLength++;
            }

            if (numberLength == 0)
            {
                return false;
            }

            return uint.TryParse(name.AsSpan(numberIndex, numberLength), out value);
        }

    }
}
