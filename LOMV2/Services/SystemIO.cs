using LOM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.IO.Compression;
using SevenZipExtractor;

namespace LOM.Services
{
    public class SystemIO : ISystemIO
    {
        public List<FileInfo> ScanFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new();

            return DirSearch(path).Select(x => new FileInfo(x)).ToList();
        }

        private List<string> DirSearch(string sDir)
        {
            if(string.IsNullOrEmpty(sDir))
                return new List<String>();

            List<String> files = new List<String>();
            try
            {
                foreach (string f in Directory.GetFiles(sDir))
                {
                    files.Add(f);
                }
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    files.AddRange(DirSearch(d));
                }
            }
            catch (Exception ex)
            {
                return files;
            }

            return files;
        }

        public ModInfo? GetSingleMod(string path)
        {
            var files = ScanFolder(path);
            var mods = GetModInfoFromFilesInfo(files);

            if (mods.Count() > 1 || mods.Count() == 0)
                return null;

            return mods.First();
        }

        public void WriteModListDotJson(List<ModInfo> modInfos, string path, string version = Constants.DefaultVersion)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (modInfos == null)
                return;

            File.WriteAllText(path + "\\modlist.json", GenerateModListJson(modInfos, version));
        }

        public string GenerateModListJson(List<ModInfo> modInfos, string version)
        {
            if (modInfos == null)
                return string.Empty;

            string json = "{\"gameVersion\":" + $" \"{version}\"" + ",\"modStatus\":{";

            modInfos.ForEach(x => {
                json += $"\"{x.FolderName.Split("\\").TakeLast(1).ToArray()[0]}\":" + "{" + $"\"bEnabled\":{x.Enabled.ToString().ToLower()}" + "},";
            });
            json = json.Remove(json.Length - 1, 1);
            json += "}}";

            json = JsonPrettify(json);

            return json;
        }

        public void WriteModsModDotJson(List<ModInfo> modInfos)
        {
            if (modInfos == null)
                return;

            modInfos.ForEach(x => WriteModDotJson(x));
        }

        public void WriteModDotJson(ModInfo modInfo)
        {
            if (modInfo == null)
                return;

            var folderPath = modInfo.FolderName;
            File.WriteAllText($"{modInfo.FolderName}\\mod.json", JsonSerializer.Serialize<Mod>(modInfo.Mod, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>
        /// Will async loop over the files and generate the ModInfos
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public List<ModInfo> GetModInfoFromFilesInfo(List<FileInfo> files)
        {
            if (files == null)
                return new();

            return files
                .Where(x => x.Name == "mod.json")
                .Select(x =>
                    {
                        return new ModInfo()
                        {
                            Mod = JsonSerializer.Deserialize<Mod>(File.ReadAllText(x.FullName)),
                            FolderName = x.DirectoryName,
                        };
                    })
                .ToList();
        }

        public bool IsMainModsFolder(string path)
        {
            if(string.IsNullOrEmpty(path)) 
                return false;

            var files = ScanFolder(path);
            return IsMainModsFolder(files);
        }

        public bool IsMainModsFolder(List<FileInfo> files)
        {
            if (files == null) 
                return false;

            return files.Count(x => x.Name == "modlist.json") == 1;
        }

        private static string JsonPrettify(string json)
        {
            using var jDoc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
        }

        public bool RemoveMod(ModInfo mod)
        {
            try
            {
                Directory.Delete(mod.FolderName, true);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool InsertDirectory(string source, string target)
        {
            try
            {
                Directory.Delete(target, true);
                Directory.CreateDirectory(target);
                DeepCopy(new DirectoryInfo(source), target);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool UnzipAndInsertDirectory(string source, string target, out string extractedFolderName)
        {
            extractedFolderName = string.Empty;
            try
            {
                using ArchiveFile archiveFile = new(source);
                extractedFolderName = Path.GetDirectoryName(archiveFile?.Entries?.FirstOrDefault(x => x.FileName.Contains("mod.json"))?.FileName) ?? string.Empty;

                Directory.Delete($"{target}\\{extractedFolderName}", true);
                Directory.CreateDirectory($"{target}\\{extractedFolderName}");
                archiveFile.Extract(target, true);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private void DeepCopy(DirectoryInfo directory, string destinationDir)
        {
            foreach (string dir in Directory.GetDirectories(directory.FullName, "*", SearchOption.AllDirectories))
            {
                string dirToCreate = dir.Replace(directory.FullName, destinationDir);
                Directory.CreateDirectory(dirToCreate);
            }

            foreach (string newPath in Directory.GetFiles(directory.FullName, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(directory.FullName, destinationDir), true);
            }
        }
    }
}
