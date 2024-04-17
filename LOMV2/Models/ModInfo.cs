namespace LOM.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class ModInfo : ICloneable
{
    public Mod? Mod {get ;set;} = new Mod();
    public string? FolderName {get ;set; }

    [JsonIgnore]
    public string? FolderNameShort { get => FolderName.Split("\\").TakeLast(1).First(); }
    [JsonIgnore]
    public bool Enabled { get => Mod.bEnabled; set => Mod.bEnabled = value; }
    [JsonIgnore]
    public Dictionary<ModInfo, List<string>> OverridenByMods { get; set; } = new();
    [JsonIgnore]
    public Dictionary<ModInfo, List<string>> Overriding { get; set; } = new();
    [JsonIgnore]
    public bool IsOverriden => OverridenByMods.Any();
    [JsonIgnore]
    public bool IsOverriding => Overriding.Any();
    [JsonIgnore]
    public List<string> OverridenByModsLabels => OverridenByMods.Select(x => x.Key.DisplayName).ToList();
    [JsonIgnore]
    public List<string> OverridingLabls => Overriding.Select(x => x.Key.DisplayName).ToList();
    [JsonIgnore]
    public bool Highlight { get; set; } = false;

    #region acces stuff
    [JsonIgnore]
    public string? DisplayName {get => Mod?.displayName; set => Mod.displayName = value; }
    [JsonIgnore]
    public string? Version {get => Mod?.version; set => Mod.version = value; }
    [JsonIgnore]
    public int? BuildNumber {get => Mod?.buildNumber; set => Mod.buildNumber = value; }
    [JsonIgnore]
    public string? Description {get => Mod?.description; set => Mod.description = value; }
    [JsonIgnore]
    public string? Author {get => Mod?.author; set => Mod.author = value; }
    [JsonIgnore]
    public string? AuthorURL {get => Mod?.authorURL; set => Mod.authorURL = value; }
    [JsonIgnore]
    public double? DefaultLoadOrder {get => Mod?.defaultLoadOrder; set => Mod.defaultLoadOrder = value; }
    [JsonIgnore]
    public string? GameVersion {get => Mod?.gameVersion; set => Mod.gameVersion = value; }
    [JsonIgnore]
    public List<string>? Manifest {get => Mod?.manifest; set => Mod.manifest = value; }
    [JsonIgnore]
    public long? SteamPublishedFileId {get => Mod?.steamPublishedFileId; set => Mod.steamPublishedFileId = value; }
    [JsonIgnore]
    public int? SteamLastSubmittedBuildNumber {get => Mod?.steamLastSubmittedBuildNumber; set => Mod.steamLastSubmittedBuildNumber = value; }
    [JsonIgnore]
    public string? SteamModVisibility {get => Mod?.steamModVisibility; set => Mod.steamModVisibility = value; }
    #endregion

    public object Clone()
    {
        return new ModInfo()
        {
            Mod = Mod.Clone() as Mod,
            FolderName = FolderName,
            OverridenByMods = OverridenByMods,
            Overriding = Overriding
        };
    }
}