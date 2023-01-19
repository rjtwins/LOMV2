namespace LOM.Models;
using System.Collections.Generic;

public class Mod
{
    public string displayName { get; set; }
    public string version { get; set; }
    public int? buildNumber { get; set; }
    public string description { get; set; }
    public string author { get; set; }
    public string authorURL { get; set; }
    public double? defaultLoadOrder { get; set; }
    public string gameVersion { get; set; }
    public List<string> manifest { get; set; }
    public long? steamPublishedFileId { get; set; }
    public int? steamLastSubmittedBuildNumber { get; set; }
    public string steamModVisibility { get; set; }
    public bool bEnabled { get; set; } = false;
}
