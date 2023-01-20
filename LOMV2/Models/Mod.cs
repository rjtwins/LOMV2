namespace LOM.Models;

using System;
using System.Collections.Generic;
using System.Drawing;

public class Mod : ICloneable
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

    public object Clone()
    {
        return new Mod()
        {
            displayName = displayName,
            version = version,
            buildNumber = buildNumber,
            description = description,
            author = author,
            authorURL = authorURL,
            defaultLoadOrder = defaultLoadOrder,
            gameVersion = gameVersion,
            manifest = manifest,
            steamPublishedFileId = steamPublishedFileId,
            steamLastSubmittedBuildNumber = steamLastSubmittedBuildNumber,
            steamModVisibility = steamModVisibility,
            bEnabled = bEnabled,
        };
    }
}
