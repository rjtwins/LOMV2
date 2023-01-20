using LOM.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace LOM.Services
{
    public interface ISystemIO
    {
        List<ModInfo> GetModInfoFromFilesInfo(List<FileInfo> files);
        List<FileInfo> ScanFolder(string path);
        bool IsMainModsFolder (string path);
        bool IsMainModsFolder (List<FileInfo> files);
        void WriteModListDotJson(List<ModInfo> modInfos, string path, string version = "1.1.328");
        void WriteModsModDotJson(List<ModInfo> modInfos);
        bool RemoveMod(ModInfo mod);
        bool InsertDirectory(string source, string target);
        bool UnzipAndInsertDirectory(string source, string target, out string extractedFolderName);
        ModInfo? GetSingleMod(string path);
    }
}