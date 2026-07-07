using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

#if IS_LINUX
using SingleFileExtractor.Core;
#endif

namespace StarMap.Types
{
    public class LoaderConfig
    {

        public bool TryLoadConfig()
        {
            if (!File.Exists("./StarMapConfig.json"))
            {
                Console.WriteLine("StarMap - Please fill the StarMapConfig.json and restart the program");
                File.WriteAllText("./StarMapConfig.json", JsonSerializer.Serialize(new LoaderConfig(), new JsonSerializerOptions { WriteIndented = true }));
                return false;
            }

            var jsonString = File.ReadAllText("./StarMapConfig.json");
            var config = JsonSerializer.Deserialize<LoaderConfig>(jsonString);

            if (config is null) return false;

            if (string.IsNullOrEmpty(config.GameLocation))
            {
                Console.WriteLine("StarMap - The 'GameLocation' property in StarMapConfig.json is either empty or points to a non-existing file.");
                return false;
            }

            var path = config.GameLocation;
            
            if (Directory.Exists(path))
            {
                var dllPath = Path.Combine(path, "KSA.dll");
                
                // The Linux build is distributed as a single-file executable without separate DLLs.
                // The DLLs need to be extracted for StarMap to work.
                #if IS_LINUX
                // There could be an existing DLL in the game directory from a previous run of StarMap (or extracted manually).
                // However, this could be outdated if the user updated their game after the bundle was last extracted, so we need
                // to check the version of any existing KSA.dll against the version in the bundle and re-extract if they don't match.
                var existingDllVersion = File.Exists(dllPath) ? AssemblyName.GetAssemblyName(dllPath).Version : null;

                var binPath = Path.Combine(path, "KSA");
                if (File.Exists(binPath))
                {
                    var reader = new ExecutableReader(binPath);
                    if (reader.IsSingleFile)
                    {
                        if (existingDllVersion is null)
                        {
                            Console.WriteLine("StarMap - Extracting DLLs from Linux executable bundle...");
                            reader.ExtractToDirectory(path);
                        }
                        else
                        {
                            var bundleStream = reader.Bundle.Files
                                .FirstOrDefault(entry => entry.RelativePath == "KSA.dll")?.AsStream();
                            if (bundleStream is not null)
                            {
                                using var peReader = new PEReader(bundleStream);
                                var metadataReader = peReader.GetMetadataReader();
                                var bundleVersion = metadataReader.GetAssemblyDefinition().Version;

                                if (bundleVersion != existingDllVersion)
                                {
                                    Console.WriteLine("StarMap - Extracting DLLs from Linux executable bundle...");
                                    reader.ExtractToDirectory(path);
                                }
                            }   
                        }
                    }
                }
                
                // The Linux version also ships without executable permissions set on the game executables, so set them here
                var newMode = File.GetUnixFileMode(binPath) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                File.SetUnixFileMode(binPath, newMode);
                File.SetUnixFileMode(Path.Join(path, "Brutal.Monitor.Subprocess"), newMode);
                #endif
                
                path = dllPath;
            }

            if (!File.Exists(path))
            {
                Console.WriteLine("StarMap - Could not find KSA.dll. Make sure the folder or file path is correct:");
                Console.WriteLine(path);
                return false;
            }

            GameLocation = path;

            GameArguments = config.GameArguments;
            
            return true;
        }

        public string GameLocation { get; set; } = "";
        public string RepositoryLocation { get; set; } = "";
        public string[] GameArguments { get; set; } = [];
    }
}
