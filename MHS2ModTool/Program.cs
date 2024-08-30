using MHS2ModTool.GameFileFormats;

namespace MHS2ModTool
{
    internal class Program
    {
        private enum Operation
        {
            Auto,
            ExtractArc,
            CreateArc,
            ConvertModToGlb,
            ConvertGlbToMod,
            ConvertTexToDds,
            ConvertDdsToTex
        }

        private struct Command
        {
            public TargetPlatform TargetPlatform;
            public Operation Operation;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("MHS2ModTool v0.1");

            Command command = new()
            {
                TargetPlatform = TargetPlatform.PC,
                Operation = Operation.Auto
            };

            List<string> paths = [];

            if (!ParseCommands(args, paths, ref command) || args.Length == 0)
            {
                PrintUsageGuide();
                return;
            }

            Console.WriteLine($"Target: {(command.TargetPlatform == TargetPlatform.PC ? "PC" : "Nintendo Switch")}");
            Console.WriteLine();

            switch (command.Operation)
            {
                case Operation.Auto:
                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            if (Path.GetExtension(path).Equals(".arc", StringComparison.InvariantCultureIgnoreCase))
                            {
                                Console.WriteLine($"Extracting archive '{path}'...");

                                ExtractAssets(path[..^4], path);
                            }
                            else
                            {
                                Console.WriteLine($"ERROR: Expected extension '.arc', for file '{path}'.");
                            }
                        }
                        else if (Directory.Exists(path))
                        {
                            if (MTArchive.IsValidArchiveDirectory(path))
                            {
                                Console.WriteLine($"Creating archive from '{path}'...");

                                RepackAssets(path, path + ".arc", command.TargetPlatform);
                            }
                            else
                            {
                                Console.WriteLine($"ERROR: Could not create '.arc' from path '{path}' because there is no '{MTArchive.OrderLogFileName}' file inside.");
                            }
                        }
                    }
                    break;
                case Operation.ExtractArc:
                    if (paths.Count != 2 || File.Exists(paths[0]) || !File.Exists(paths[1]))
                    {
                        Console.WriteLine("ERROR: Expected one output folder and one input '.arc' file, in that order.");
                        return;
                    }

                    Console.WriteLine($"Extracting archive '{paths[1]}' to '{paths[0]}'...");
                    MTArchive.Load(paths[1]).Extract(paths[0]);
                    break;
                case Operation.CreateArc:
                    if (paths.Count != 2 || Directory.Exists(paths[0]) || !Directory.Exists(paths[1]))
                    {
                        Console.WriteLine("ERROR: Expected one output '.arc' file and one input folder, in that order.");
                        return;
                    }

                    if (!MTArchive.IsValidArchiveDirectory(paths[1]))
                    {
                        Console.WriteLine($"ERROR: Could not create '.arc' because there is no '{MTArchive.OrderLogFileName}' file inside.");
                        return;
                    }

                    Console.WriteLine($"Creating archive '{paths[1]}' from '{paths[0]}'...");
                    MTArchive.CreateFromFolder(paths[1]).Save(paths[0], command.TargetPlatform == TargetPlatform.PC);
                    break;
                case Operation.ConvertModToGlb:
                    if (paths.Count != 2 || Directory.Exists(paths[0]) || !File.Exists(paths[1]))
                    {
                        Console.WriteLine("ERROR: Expected one output '.glb' file and one input '.mod' file, in that order.");
                        return;
                    }

                    string mrlPath = PathUtils.ReplaceExtension(paths[1], ".mod", ".mrl");

                    if (!File.Exists(mrlPath))
                    {
                        Console.WriteLine($"ERROR: Could not find materials file '{mrlPath}'.");
                        return;
                    }

                    Console.WriteLine($"Converting '{paths[1]}' to '{paths[0]}'...");
                    var mrl = MTMaterial.Load(mrlPath);
                    MTModel.Load(paths[1]).ExportGltf(paths[0], mrl);
                    break;
                case Operation.ConvertGlbToMod:
                    if (paths.Count != 2 || Directory.Exists(paths[0]) || !File.Exists(paths[1]))
                    {
                        Console.WriteLine("ERROR: Expected one output '.mod' file and one input '.glb' file, in that order.");
                        return;
                    }

                    if (!File.Exists(paths[0]))
                    {
                        Console.WriteLine("WARNING: The original model file was not found, groups will be lost which will likely result in errors.");
                    }

                    Console.WriteLine($"Converting '{paths[1]}' to '{paths[0]}'...");
                    MTModel.ImportGltf(paths[1], paths[0]).Save(paths[0]);
                    break;
                case Operation.ConvertTexToDds:
                    if (paths.Count != 2 || Directory.Exists(paths[0]) || !File.Exists(paths[1]))
                    {
                        Console.WriteLine("ERROR: Expected one output '.dds' file and one input '.tex' file, in that order.");
                        return;
                    }

                    Console.WriteLine($"Converting '{paths[1]}' to '{paths[0]}'...");
                    MTTexture.Load(paths[1]).Export(paths[0]);
                    break;
                case Operation.ConvertDdsToTex:
                    if (paths.Count != 2 || Directory.Exists(paths[0]) || !File.Exists(paths[1]))
                    {
                        Console.WriteLine("ERROR: Expected one output '.tex' file and one input '.dds' file, in that order.");
                        return;
                    }

                    Console.WriteLine($"Converting '{paths[1]}' to '{paths[0]}'...");
                    MTTexture.ImportDds(paths[1], paths[0]).Save(paths[0], command.TargetPlatform);
                    break;
            }

            Console.WriteLine("Done!");
        }

        private static void PrintUsageGuide()
        {
            Console.WriteLine();
            Console.WriteLine("Arguments:");

            Console.WriteLine("--pc    Set target platform to PC.");
            Console.WriteLine("--nsw   Set target platform to Nintendo Switch.");
            Console.WriteLine("--xarc  Extract archive contents.");
            Console.WriteLine("--carc  Create archive from folder.");
            Console.WriteLine("--xmod  Convert MOD model to GLB model.");
            Console.WriteLine("--cmod  Create MOD model from GLB model.");
            Console.WriteLine("--xtex  Convert TEX texture to DDS texture.");
            Console.WriteLine("--ctex  Create TEX texture from DDS texture.");

            Console.WriteLine();
            Console.WriteLine("Note: If no command is specified, the tool will automatically extract or create archives from the paths.");
        }

        private static bool ParseCommands(string[] args, List<string> paths, ref Command command)
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "--pc":
                        command.TargetPlatform = TargetPlatform.PC;
                        break;
                    case "--nsw":
                        command.TargetPlatform = TargetPlatform.Switch;
                        break;
                    case "--xarc":
                        command.Operation = Operation.ExtractArc;
                        break;
                    case "--carc":
                        command.Operation = Operation.CreateArc;
                        break;
                    case "--xmod":
                        command.Operation = Operation.ConvertModToGlb;
                        break;
                    case "--cmod":
                        command.Operation = Operation.ConvertGlbToMod;
                        break;
                    case "--xtex":
                        command.Operation = Operation.ConvertTexToDds;
                        break;
                    case "--ctex":
                        command.Operation = Operation.ConvertDdsToTex;
                        break;
                    default:
                        if (arg.StartsWith('-') || arg.StartsWith('/'))
                        {
                            Console.WriteLine($"ERROR: Invalid argument: '{arg}'.");
                            return false;
                        }

                        paths.Add(arg);
                        break;
                }
            }

            return true;
        }

        private static void ExtractAssets(string baseFolder, string arcFileName)
        {
            var arc = MTArchive.Load(arcFileName);
            arc.Extract(baseFolder);
            var paths = arc.GetRelativePaths();

            foreach (var path in paths)
            {
                string fullPath = Path.Combine(baseFolder, path);

                switch (Path.GetExtension(path.ToLowerInvariant()))
                {
                    case ".mod":
                        string mrlPath = PathUtils.ReplaceExtension(fullPath, ".mod", ".mrl");

                        if (File.Exists(mrlPath))
                        {
                            string glbPath = PathUtils.ReplaceExtension(fullPath, ".mod", ".glb");

                            Console.WriteLine($"Converting '{fullPath}' to '{glbPath}'...");

                            var mrl = MTMaterial.Load(mrlPath);
                            var mod = MTModel.Load(fullPath);
                            
                            mod.ExportGltf(glbPath, mrl, baseFolder);
                        }
                        break;
                }
            }
        }

        private static void RepackAssets(string baseFolder, string arcFileName, TargetPlatform targetPlatform)
        {
            if (!MTArchive.IsValidArchiveDirectory(baseFolder))
            {
                return;
            }

            foreach (var path in Directory.GetFiles(baseFolder, "*.*", SearchOption.AllDirectories))
            {
                switch (Path.GetExtension(path.ToLowerInvariant()))
                {
                    case ".dds":
                        var texPath = PathUtils.ReplaceExtension(path, ".dds", ".tex");

                        Console.WriteLine($"Converting '{path}' to '{texPath}'...");

                        var tex = MTTexture.ImportDds(path, texPath);
                        tex.Save(texPath, targetPlatform);
                        break;
                    case ".glb":
                        var modPath = PathUtils.ReplaceExtension(path, ".glb", ".mod");

                        Console.WriteLine($"Converting '{path}' to '{modPath}'...");

                        var mod = MTModel.ImportGltf(path, modPath);
                        mod.Save(modPath);
                        break;
                }
            }

            var arc = MTArchive.CreateFromFolder(baseFolder);
            arc.Save(arcFileName, targetPlatform == TargetPlatform.PC);
        }
    }
}
