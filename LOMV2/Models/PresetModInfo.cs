using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace LOM.Models
{
    class PresetModInfo
    {
        public string DisplayName { get; set; }

        public string Author { get; set; }

        public string FolderShort { get; set; }

        public bool Enabled { get; set; }

        public double? LoadOrder { get; set; }

        public PresetModInfo(string displayName, string author, string folderShort, bool enabled, double? loadOrder)
        {
            this.DisplayName = displayName;
            this.Author = author;
            this.FolderShort = folderShort;
            this.Enabled = enabled;
            this.LoadOrder = loadOrder;
        }
    }
}
