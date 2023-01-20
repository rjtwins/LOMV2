using LOM.Models;
using LOM.ViewModels;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using LOM.Services;
using MessageBox = System.Windows.MessageBox;
using System.Diagnostics;
using ListBox = System.Windows.Controls.ListBox;
using System.IO;
using System.Text.Json;
using Microsoft.VisualBasic;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Security.Policy;

namespace LOM;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; set; } = new();
    private ISystemIO _systemIO { get; set; }

    public MainWindow(ISystemIO systemIO)
    {
        _systemIO = systemIO;
        InitializeComponent();

        this.DataContext = ViewModel;

        ModItemDataGrid.DataContext = ViewModel;

        LoadSystemSettings();
    }

    private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
    {
        RefreshMods();

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private void Select_Main_Mods_Folder(object sender, RoutedEventArgs e)
    {
        string folderPath = PickFolder();

        ViewModel.MainModsFolder = folderPath ?? string.Empty;

        RefreshMods();

        PersistSystemSettings();

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private void Add_Secondary_Mods_Folder(object sender, RoutedEventArgs e)
    {
        string folderPath = PickFolder();
        if (string.IsNullOrEmpty(folderPath))
            return;

        ViewModel.ModSources.Add(folderPath);

        RefreshMods();

        PersistSystemSettings();

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private async void Upp_Button_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItem(-1);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));

    }

    private async void Down_Button_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItem(1);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private void Info_Click(object sender, RoutedEventArgs e)
    {
        Process.Start("explorer", "https://www.nexusmods.com/mechwarrior5mercenaries/mods/174");
    }

    private void MoveSelectedItem(int delta)
    {
        var selectedItem = ModItemDataGrid.SelectedItem as ModInfo;

        if (selectedItem == null)
            return;

        var oldIndex = ViewModel.ModInfos.IndexOf(selectedItem);
        var newIndex = oldIndex + delta;

        if (newIndex < 0 || newIndex > ViewModel.ModInfos.Count() - 1)
            return;

        ViewModel.ModInfos.Move(oldIndex, newIndex);

        var mods = UpdateDefaultLoadOrder(ViewModel.ModInfos.ToList());
        
        UpdateGrid(mods);

        ModItemDataGrid.SelectedIndex = newIndex;

        GetOverridingData();
    }

    private void UpdateGrid(List<ModInfo> mods)
    {
        ViewModel.ModInfos.Clear();
        ViewModel.ModInfos = new ObservableCollection<ModInfo>(mods);
        ViewModel.OnPropertyChanged(nameof(ViewModel.ModInfos));
    }

    private List<ModInfo> SortAndUpdateMods(List<ModInfo> mods)
    {
        mods = mods.OrderBy(x => x.DefaultLoadOrder).ToList();
        return UpdateDefaultLoadOrder(mods);
    }

    private List<ModInfo> UpdateDefaultLoadOrder(List<ModInfo> mods)
    {
        for (int i = 0; i < mods.Count(); i++)
        {
            mods[i].DefaultLoadOrder = i + 1;
        }
        return mods;
    }

    private void Apply(object sender, RoutedEventArgs e)
    {
        //Some checks:
        if (string.IsNullOrEmpty(ViewModel.MainModsFolder))
        {
            MessageBox.Show("Main mods folder containing modlist.json was not selected.\nPlease select a valid main mods folder before trying to apply.", "Folder not set");
            return;
        }

        var mods = ViewModel.ModInfos.ToList().Select(x => x.Clone() as ModInfo).ToList();

        _systemIO.WriteModListDotJson(mods, ViewModel.MainModsFolder);
        _systemIO.WriteModsModDotJson(mods);
    }

    private void RefreshMods()
    {
        var mods = GenerateModListFromMainPath(ViewModel.MainModsFolder);
        ViewModel.ModSources.ToList().ForEach(async x =>
        {
            mods.AddRange(GenerateModListFromSecondaryPath(x));
        });

        mods = SortAndUpdateMods(mods);
        UpdateGrid(mods);

        ViewModel.OnPropertyChanged(nameof(ViewModel.ModSources));
        ViewModel.OnPropertyChanged(nameof(ViewModel.ModSourcesNames));
        ViewModel.OnPropertyChanged(nameof(ViewModel.MainModsFolder));

        GetOverridingData();
    }

    private List<ModInfo> GenerateModListFromMainPath(string folderPath)
    {
        var files = _systemIO.ScanFolder(folderPath);
        var mods = _systemIO.GetModInfoFromFilesInfo(files);
        var temp = ViewModel.ModInfos.ToList();

        //Remove all mods that have the main mods folder as their source:
        temp.Where(x => x.FolderName.Contains(ViewModel.MainModsFolder)).ToList().ForEach(x => temp.Remove(x));

        //Update old to new main mods source:
        ViewModel.MainModsFolder = folderPath;

        //Add all found mods.
        mods.ForEach(x => temp.Add(x));

        return mods;
    }

    private List<ModInfo> GenerateModListFromSecondaryPath(string folderPath)
    {
        var files = _systemIO.ScanFolder(folderPath);
        var mods = _systemIO.GetModInfoFromFilesInfo(files);
        var temp = ViewModel.ModInfos.ToList();

        if (ViewModel.MainModsFolder == folderPath)
            return new();

        if (!ViewModel.ModSources.Contains(folderPath))
        {
            var duplicates = mods.Where(x => ViewModel.ModInfos.Any(y => y.DisplayName == x.DisplayName &&
                y.Author == x.Author)).ToList();

            if (duplicates.Count > 0)
            {
                string displayNames = "- " + string.Join("\n- ", duplicates.Select(x => x.DisplayName).ToList());
                MessageBox.Show($"The following duplicates where detected while inporting mods from selected folder \n{displayNames}\n" +
                    $"Please remove these mods from either directory, refresh and try again.");
                return new();
            }
        }

        //Remove all mods that have the secondary mods folder as their source:
        temp.Where(x => x.FolderName.Contains(folderPath)).ToList().ForEach(x => temp.Remove(x));

        //Update old to new main mods source:
        ViewModel.ModSources.Add(folderPath);

        //Add all found mods.
        mods.ForEach(x => temp.Add(x));

        return mods;
    }

    public void GetOverridingData()
    {
        var modList = ViewModel.ModInfos.ToList();

        modList.ForEach(x => {
            x.OverridenByMods = new();
            x.Overriding = new();
        });

        modList
            .Where(mod => mod.Enabled)
            .ToList()
            .ForEach(mod =>
            {
                modList
                .Where(mod2 => mod2.Enabled)
                .Where(mod2 => mod2.DefaultLoadOrder < mod.DefaultLoadOrder)
                .ToList()
                .ForEach(mod2 =>
                {
                    if (mod.DisplayName == mod2.DisplayName)
                        return;

                    List<string> conflicting = mod.Manifest?.Where(x => mod2.Manifest?.Contains(x) ?? false)?.ToList() ?? new();
                    if (conflicting.Any())
                    {
                        mod.Overriding[mod2] = conflicting.ToList();
                        mod2.OverridenByMods[mod] = conflicting.ToList();
                    }
                });
            });
    }

    private string PickFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK)
            return "";
        return dialog.SelectedPath;
    }

    private void ModItemDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
        e.Handled = !double.TryParse(e.Text, out var number);
    }

    private void ModItemDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        ViewModel.OnPropertyChanged(nameof(ViewModel.SelectedModLabel));
        ViewModel.OnPropertyChanged(nameof(ViewModel.SelectedMod));
    }

    private void Overriding_ListBox_Selected(object sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        var selectedIndex = listBox?.SelectedIndex;

        if (selectedIndex == null || selectedIndex < 0)
            return;

        var manifest = ViewModel?.SelectedMod?.Overriding.ElementAt(selectedIndex.Value).Value;

        if (manifest == null)
            return;

        ManifestListBox.ItemsSource = manifest;
    }

    private void OverridenBy_ListBox_Selected(object sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        var selectedIndex = listBox?.SelectedIndex;

        if (selectedIndex == null || selectedIndex < 0)
            return;

        var manifest = ViewModel?.SelectedMod?.OverridenByMods.ElementAt(selectedIndex.Value).Value;

        if (manifest == null)
            return;

        ManifestListBox.ItemsSource = manifest;
    }

    private void Enable_All_Button_Click(object sender, RoutedEventArgs e)
    {
        var mods = ViewModel.ModInfos.ToList();
        mods.ForEach(x => x.Enabled = true);

        UpdateGrid(mods);
    }

    private void Disable_All_Button_Click(object sender, RoutedEventArgs e)
    {
        var mods = ViewModel.ModInfos.ToList();
        mods.ForEach(x => x.Enabled = false);

        UpdateGrid(mods);
    }

    private void Remove_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedMod = ModItemDataGrid.SelectedItem as ModInfo;

        if (selectedMod == null)
            return;

        var result = MessageBox.Show($"Are you sure you want to PERMANENTLY remove {selectedMod.DisplayName} your disk?", "ARE YOU SURE", MessageBoxButton.YesNo);

        if (result != MessageBoxResult.Yes)
            return;

        _systemIO.RemoveMod(selectedMod);

        var mods = ViewModel.ModInfos.ToList();
        mods.Remove(selectedMod);
        SortAndUpdateMods(mods);
        UpdateGrid(mods);
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (ViewModel.MainModsFolder == null)
        {
            MessageBox.Show($"Main mods folder has not been selected.\nPlease select a mains mods folder first.", "No Folder Selected");
        }

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        // Note that you can have more than one file.
        string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

        if (files.Length != 1)
        {
            MessageBox.Show($"More the one entry detected.\nPlease only drag-in one folder or a .zip/.rar file.");
            return;
        }

        if (Directory.Exists(files[0]))
        {
            var path = files[0].Split("\\").Last();
            if (!_systemIO.InsertDirectory(files[0], ViewModel.MainModsFolder + "\\" + path))
                return;

            AddSingleMod(ViewModel.MainModsFolder + "\\" + path);
            return;
        }
        var fileInfo = new FileInfo(files[0]);

        var supportedExtensions = new string[] { ".zip", ".rar", ".7z" };

        if (!supportedExtensions.Contains(fileInfo.Extension))
        {
            MessageBox.Show($"File extension not supported.\nPlease only drag-in one folder or a .zip/.rar file.");
            return;
        }

        if (!_systemIO.UnzipAndInsertDirectory(files[0], ViewModel.MainModsFolder, out string extractedFolderName))
        {
            MessageBox.Show($"Failed to extract file.");
            return;
        }
        else
        {
            AddSingleMod(ViewModel.MainModsFolder + "\\" + extractedFolderName);
            return;
        }

        var path2 = System.IO.Path.GetFileNameWithoutExtension(files[0]);
        AddSingleMod(ViewModel.MainModsFolder + "\\" + path2);
    }

    private void AddSingleMod(string path)
    {
        var mod = _systemIO.GetSingleMod(path);
        if (mod == null)
        {
            MessageBox.Show($"Failed to parse mod.json in {path}.\nThere may be residial files/folders leftover from failed operation!");
            return;
        }

        var mods = ViewModel.ModInfos.ToList();
        mods.Add(mod);

        mods = SortAndUpdateMods(mods);
        UpdateGrid(mods);
    }

    private void PersistSystemSettings()
    {
        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var systemSettings = new SystemSettingsDto() {
            MainModsFolder = ViewModel.MainModsFolder,
            ModSources = ViewModel.ModSources.ToList(),
            Presets = ViewModel.Presets
        };

        if (!Directory.Exists($"{systemFolder}\\LOMV2"))
            Directory.CreateDirectory($"{systemFolder}\\LOMV2");

        File.WriteAllText($"{systemFolder}\\LOMV2\\settings.json", JsonSerializer.Serialize(systemSettings));
    }

    private void LoadSystemSettings()
    {
        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!File.Exists($"{systemFolder}\\LOMV2\\settings.json"))
            return;

        var systemSettingsJson = File.ReadAllText($"{systemFolder}\\LOMV2\\settings.json");
        SystemSettingsDto systemSettings = JsonSerializer.Deserialize<SystemSettingsDto>(systemSettingsJson);
        ViewModel.ModSources = systemSettings.ModSources.ToHashSet();
        ViewModel.MainModsFolder = systemSettings.MainModsFolder;
        ViewModel.Presets = systemSettings.Presets;

        RefreshMods();
    }

    private void SavePreset(string presetName)
    {
        if (ViewModel.Presets.ContainsKey(presetName))
            if (MessageBox.Show($"Preset name {presetName} allready in use,\ndo you want to override?", "", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

        ViewModel.Presets[presetName] = JsonSerializer.Serialize(ViewModel.ModInfos.ToArray());

        ViewModel.OnPropertyChanged(nameof(ViewModel.Presets));
        ViewModel.OnPropertyChanged(nameof(ViewModel.PresetNames));

        PersistSystemSettings();
    }

    private void LoadPreset(string presetName)
    {
        if (!ViewModel.Presets.TryGetValue(presetName, out string presetJson))
            return;

        var presetMods = JsonSerializer.Deserialize<List<ModInfo>>(presetJson);

        if (presetMods == null)
            return;

        var mods = ViewModel.ModInfos.ToList();

        mods.ForEach(mod => mod.Enabled = false);
        mods.ForEach(mod =>
        {
            var match = presetMods.FirstOrDefault(presetMod => presetMod.DisplayName == mod.DisplayName &&
                presetMod.Author == mod.Author &&
                presetMod.FolderNameShort == mod.FolderNameShort);

            if (match == null)
            {
                mod.DefaultLoadOrder = int.MaxValue;
                return;
            }

            mod.Enabled = match.Enabled;
            mod.DefaultLoadOrder = match.DefaultLoadOrder;
        });

        mods = SortAndUpdateMods(mods);
        UpdateGrid(mods);
    }

    private void RemovePreset(string presetName)
    {
        ViewModel.Presets.Remove(presetName);
        PersistSystemSettings();

        ViewModel.OnPropertyChanged(nameof(ViewModel.Presets));
        ViewModel.OnPropertyChanged(nameof(ViewModel.PresetNames));
    }

    public void Load_Preset_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedPreset = PresetListBox.SelectedItem as string;
        if (selectedPreset == null)
            return;

        LoadPreset(selectedPreset);
    }

    public void Save_Preset_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedPreset = PresetListBox.SelectedItem as string ?? string.Empty;

        InputDialog inputDialog = new("Preset Name:", selectedPreset);
        if (inputDialog.ShowDialog() == false)
            return;

        string answer = inputDialog.Answer;
        if (string.IsNullOrEmpty(answer))
            return;

        SavePreset(inputDialog.Answer);
    }

    public void Remove_Preset_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedPreset = PresetListBox.SelectedItem as string;
        if (selectedPreset == null)
            return;

        RemovePreset(selectedPreset);
    }

    public void Remove_Secondary_Folder_Button_Click(Object sender, RoutedEventArgs e)
    {
        var selected = SecondaryFoldersListBox.SelectedItem as string;
        if (selected == null) 
            return;

        var mods = ViewModel.ModInfos.ToList();
        mods.RemoveAll(x => x.FolderName.Contains(selected));
        mods = SortAndUpdateMods(mods);
        UpdateGrid(mods);

        ViewModel.ModSources.RemoveWhere(x => x == selected);

        PersistSystemSettings();
        ViewModel.OnPropertyChanged(nameof(ViewModel.ModSources));
        ViewModel.OnPropertyChanged(nameof(ViewModel.ModSourcesNames));
    }

    public void Enabled_CheckBox_Clicked(object sender, RoutedEventArgs e)
    {
        GetOverridingData();
        var mods = ViewModel.ModInfos.ToList();
        UpdateGrid(mods);
    }

    public void PreviewKeyDown_Key_Down(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Delete:
                Remove_Button_Click(null, null);
                break;
            default:
                break;
        }
    }
}
