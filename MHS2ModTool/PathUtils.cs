namespace MHS2ModTool
{
    static class PathUtils
    {
        public static string ReplaceExtension(string path, string expect, string want)
        {
            if (path.EndsWith(expect, StringComparison.InvariantCultureIgnoreCase))
            {
                return path[..^expect.Length] + want;
            }

            return path;
        }
    }
}
