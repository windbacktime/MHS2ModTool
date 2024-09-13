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

        public static string GetFullExtension(string path)
        {
            string ext = string.Empty;

            while (true)
            {
                string currentExt = Path.GetExtension(path);

                if (string.IsNullOrEmpty(currentExt))
                {
                    break;
                }

                path = path[..^currentExt.Length];
                ext = currentExt + ext;
            }

            return ext;
        }
    }
}
