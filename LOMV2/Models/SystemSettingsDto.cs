using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace LOM.Models
{
    public class SystemSettingsDto
    {
        public string MainModsFolder { get; set; } = string.Empty;
        public List<string> ModSources { get; set; } = new List<string>();
        public Dictionary<string, string> Presets { get; set; } = new();
    }
}
