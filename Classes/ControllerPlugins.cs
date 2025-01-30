using System.Diagnostics;
using System.IO.Compression;
using PluginManagerObs.Models;
using Tomlyn;

namespace PluginManagerObs.Classes
{
    internal class ControllerPlugins
    {
        private OBSPath _obsPath;
        public string PluginsPath;
        public readonly List<Plugin> ListPlugins;

        private readonly List<Plugin> _listPluginsFull;

        private readonly PluginsContext _dbHandler;
        public ControllerPlugins()
        {
            _obsPath = new OBSPath
            {
                Path = string.Empty
            };
            PluginsPath = string.Empty;

            ListPlugins = [];
            _listPluginsFull = [];

            _dbHandler = new PluginsContext();
            _dbHandler.Database.EnsureCreatedAsync().Wait();
        }

        public bool SetObsPath(string obsPath)
        {
            var query = _dbHandler.OBSPaths.Where(o => o.Path == obsPath);
            Debug.WriteLine($"Query contents: {query.Count()}");
            if(!query.Any() )
            {
                _obsPath = new OBSPath { Path = obsPath };
                _dbHandler.OBSPaths.Add(_obsPath);
                _dbHandler.SaveChanges();
            }
            else
            {
                _obsPath = query.First();
            }
            return true;
        }

        public bool PopulatePlugins()
        {
            if (PluginsPath == string.Empty) return false;
            ListPlugins.Clear();
            _listPluginsFull.Clear();

            foreach (var file in Directory.EnumerateFiles(PluginsPath))
            {
                var extension = file.Substring(file.Length - 3, 3);
                if (extension == "zip")
                {
                    // Validate plugin zips
                    if (ValidateZip(file))
                    {
                        var splitName = file.Split('\\');
                        var simpleName = splitName[^1];
                        simpleName = simpleName[..^4];
                        // Add validated zips
                        Plugin p = new()
                        {
                            Name = simpleName
                        };
                        _listPluginsFull.Add(p);
                    }
                }
            }

            var query = _dbHandler.Plugins.Where(p => p.OBSPathId == _obsPath.OBSPathId);
            for (var i = 0;i<_listPluginsFull.Count;i++)
            {
                foreach (var plu in query)
                {
                    if (_listPluginsFull[i].Name == plu.Name)
                    {
                        _listPluginsFull[i] = plu;
                        break;
                    }
                }
                ListPlugins.Add(_listPluginsFull[i]);
            }
            return true;
        }

        public bool AddPlugins(string pluginName)
        {
            try
            {
                if (_obsPath.Path == string.Empty && PluginsPath == string.Empty) return false;
                // Check if already exists
                var name = $"{pluginName}.zip";
                using (var zip = ZipFile.Open(PluginsPath + name, ZipArchiveMode.Read))
                {
                    foreach (var zipEntry in zip.Entries)
                    {
                        var zipWin = zipEntry.FullName.Replace('/', '\\');

                        if (zipEntry.FullName.Last() == '/')
                        {
                            if (!Directory.Exists(_obsPath.Path + zipWin))
                            {
                                Directory.CreateDirectory(_obsPath.Path + zipWin);
                            }
                        }
                        else
                        {
                            // benchmark split vs substring
                            var path = zipWin.Split('\\');
                            var justPath = string.Empty;
                            for (var i = 0; i < path.Length - 1; i++)
                            {
                                justPath += path[i] + '\\';
                            }
                            if (!Directory.Exists(_obsPath.Path + justPath))
                            {
                                Directory.CreateDirectory(_obsPath.Path + justPath);
                            }
                            zipEntry.ExtractToFile(_obsPath.Path + zipWin);
                        }
                    }
                }
                Plugin plugin = new();
                foreach (var t in ListPlugins.Where(t => t.Name == pluginName))
                {
                    plugin = t;
                    plugin.IsInstalled = true;
                    plugin.IsInstalled = true;
                    plugin.InstalledDate = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
                    plugin.OBSPathId = _obsPath.OBSPathId;
                    break;
                }
                _dbHandler.Add(plugin);
                _dbHandler.SaveChanges();
            }catch (Exception e)
            {
                Debug.Write("IO Exception, while adding plugin "+ pluginName + e);
                return false;
            }
            return true;
        }

        public bool UninstallPlugin(string pluginName)
        {
            var name = $"{pluginName}.zip";
            var pluginFolder = string.Empty;
            const string dpPath = "data/obs-plugins/";

            using (var zip = ZipFile.Open(PluginsPath + name, ZipArchiveMode.Read))
            {
                foreach (var zipEntry in zip.Entries)
                {
                    var len = zipEntry.FullName.Length-1;
                    var fullName = zipEntry.FullName;
                    var end=0;

                    if (zipEntry.FullName.Last() != '/')
                    {
                        if (pluginFolder == string.Empty && fullName.Contains(dpPath) && len > 16)
                        {
                            for(var i = 18; i < len; i++)
                            {
                                if (fullName[i] == '/')
                                {
                                    end = i;
                                    break;
                                }
                            }
                            var ss = fullName.Substring(17, end - 17);
                            Debug.WriteLine($"Substring plugin name: {ss}");
                            pluginFolder = ss;
                        }
                        var zipWin = zipEntry.FullName.Replace('/', '\\');
                        if (File.Exists(_obsPath.Path + zipWin))
                            try
                            {
                                File.Delete(_obsPath.Path + zipWin);
                            }catch (Exception e) when (e is IOException ||
                                                       e is UnauthorizedAccessException)
                            {
                                Debug.WriteLine($"Error deleting file {zipWin}: {e}");
                                return false;
                            }
                    }
                }
                if (Directory.Exists(_obsPath.Path + dpPath + pluginFolder))
                    try
                    {
                        Directory.Delete(_obsPath.Path + dpPath + pluginFolder, true);
                    }
                    catch (IOException e)
                    {
                        Debug.WriteLine($"Error deleting directory {pluginFolder}: {e}");
                        return false;
                    }
            }
            foreach (var plugin in ListPlugins)
            {
                if (plugin.Name == pluginName)
                {
                    plugin.IsInstalled = false;
                    plugin.InstalledDate = 0;
                    var query = _dbHandler.Plugins.Where(plugin => plugin.Name == pluginName && plugin.OBSPathId == _obsPath.OBSPathId);
                    if (query.Any())
                    {
                        _dbHandler.Plugins.Remove(plugin);
                        _dbHandler.SaveChanges();
                    }
                    break;
                }
            }

            return true;
        }

        public bool CopyPluginZip(string file)
        {
            var extension = file.Substring(file.Length - 3, 3);
            if (extension == "zip")
            {
                var separatorPos = file.LastIndexOf('\\') + 1;
                var nameAndExtension = file.Substring(separatorPos, file.Length - separatorPos);
                // TODO Check valid plugin before copy
                try
                {
                    if (ValidateZip(file))
                        File.Copy(file, PluginsPath + nameAndExtension);
                    else
                        return false;
                }catch (IOException e)
                {
                    Debug.WriteLine($"Could not copy file {nameAndExtension} : " + e);
                    return false;
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        // TODO, remove? Private, later
        public void VanityRemoval()
        {
            VanityCheck(_obsPath.Path,0);
        }

        private static void VanityCheck(string path,int tabs)
        {
            var space = "";
            for(int i = 0; i < tabs; i++) { space += "   "; }
            // If no file && no dir, DELETE
            // else
            var files = Directory.EnumerateFiles(path);
            var directories = Directory.EnumerateDirectories(path);
            var dirs = directories as string[] ?? directories.ToArray();
            if (files.Any() && dirs.Length != 0)
            {
                foreach (var dir in dirs)
                {
                    Debug.WriteLine($"Vanity check dir: {space}{dir}");
                    VanityCheck(dir, tabs + 1);
                }
                var entries = Directory.EnumerateFileSystemEntries(path);
                if (entries.Any()) return;
            }

            Directory.Delete(path,false);
            Debug.WriteLine($"Vanity {path} DELETED!");
        }

        private static bool ValidateZip(string file)
        {
            bool data = false, plugins = false;
            using (var zip = ZipFile.Open(file, ZipArchiveMode.Read))
            {
                foreach (var zipEntry in zip.Entries)
                {
                    if (zipEntry.ToString().Contains("data/")) data = true;
                    if (zipEntry.ToString().Contains("obs-plugins/")) plugins = true;
                }
            }
            return (data && plugins);
        }

        public void FilterPlugins(string text)
        {
            ListPlugins.Clear();
            text = text.ToLower();
            foreach (var plugin in _listPluginsFull)
            {
                if (plugin.Name.ToLower().Contains(text))
                {
                    ListPlugins.Add(plugin);
                }
            }
        }

        public bool LoadPaths()
        {
            const string settings = "settings.tml";
            string toml;
            if (!File.Exists(settings)) return false;
            using (var sr = new StreamReader(settings))
            {
                toml = sr.ReadToEnd();
            }
            var model = Toml.ToModel(toml);

            var obsPath = (string)model["obspath"];
            obsPath = obsPath.Replace('/', '\\');

            SetObsPath(obsPath);

            PluginsPath = (string)model["pluginspath"];
            PluginsPath = PluginsPath.Replace('/', '\\');
            return true;
        }

        public bool SavePaths()
        {
            const string settings = "settings.tml";
            var toml = $"obspath = \"{_obsPath.Path.Replace('\\', '/')}\"\n";
            toml += $"pluginspath = \"{PluginsPath.Replace('\\', '/')}\"";

            using var sw = new StreamWriter(settings);
            sw.Write(toml);
            return true;
        }

        public static bool ValidateObsPath(string obsPath)
        {
            var exists = File.Exists(obsPath + @"bin\64bit\obs64.exe") || File.Exists(obsPath + @"bin\32bit\obs.exe");
            return exists;
        }

        public string GetObsPath() => _obsPath.Path;
    }
}

