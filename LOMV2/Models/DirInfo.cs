namespace LOM.Models;

using System.Collections.Generic;
public class DirInfoRoot{
    public string Name {get; set;} = string.Empty;
    public List<DirInfo> Content {get; set;} = new();
}

public class DirInfo{
    public string Name {get; set;} = string.Empty;
    public string Path {get; set;} = string.Empty;
    public string Type {get; set;} = string.Empty;
    public string Content {get; set;} = string.Empty;

    
}