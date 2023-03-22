using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static LOM.Models.Enums;

namespace LOM.Models
{
    public class SystemSettingsDto
    {
        public string MainModsFolder { get; set; } = string.Empty;
        public List<string> ModSources { get; set; } = new List<string>();
        public Dictionary<string, string> Presets { get; set; } = new();
        public int WindowX { get; set; } = 1050;
        public int WindowY { get; set; } = 500;
        public Vender Vender { get; set; } = Vender.None;
        public string ExEPath { get; set; } = string.Empty;
        public string GameVersion { get; set; } = Constants.DefaultVersion;
    }
}
