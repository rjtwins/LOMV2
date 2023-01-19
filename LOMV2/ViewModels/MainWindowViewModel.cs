using LOM.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LOM.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ModInfo> ModInfos { get; set; } = new();
        public Dictionary<string, string> Presets { get; set; } = new();
        public List<string> PresetNames => Presets.Keys.ToList();
        public HashSet<string> ModSources { get; set; } = new();
        public List<string> ModSourcesNames => ModSources.ToList();
        public string MainModsFolder { get; set; } = string.Empty;
        public ModInfo? SelectedMod { get; set; }
        public string SelectedModLabel => SelectedMod?.DisplayName ?? string.Empty;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        public void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
