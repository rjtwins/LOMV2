using LOM.Models;
using LOM.ViewModels;
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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Timers;
using Window = System.Windows.Window;

namespace LOM;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; set; } = new();
    private ISystemIO _systemIO { get; set; }

    private bool _updatingCell = false;

    private System.Timers.Timer _resizeTimer = new(100) { Enabled = false };

    public MainWindow(ISystemIO systemIO)
    {
        _systemIO = systemIO;
        InitializeComponent();

        this.DataContext = ViewModel;

        ModItemDataGrid.DataContext = ViewModel;

        this.VersionTextBox.Text = Constants.DefaultVersion;
        LoadSystemSettings();

        _resizeTimer.Elapsed += ResizeTimerElapsed;
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

    private async void Up_Button_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItem(-1);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private async void UpFive_Button_Click(object sender, RoutedEventArgs e)
    {
        var delta = -5;
        if (ModItemDataGrid.SelectedIndex + delta < 1)
        {
            delta = -ModItemDataGrid.SelectedIndex;
        }

        MoveSelectedItem(delta);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private async void DownFive_Button_Click(object sender, RoutedEventArgs e)
    {
        var delta = 5;
        if (ModItemDataGrid.SelectedIndex + delta >= this.ModItemDataGrid.Items.Count)
        {
            delta = ModItemDataGrid.Items.Count - ModItemDataGrid.SelectedIndex - 1;
        }

        MoveSelectedItem(delta);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private async void Down_Button_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItem(1);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private async void ToTop_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = ModItemDataGrid.SelectedItem as ModInfo;

        if (selectedItem == null)
            return;

        var oldIndex = ViewModel.ModInfos.IndexOf(selectedItem);

        MoveSelectedItem(-oldIndex);

        Debug.WriteLine(String.Join("\n", ViewModel.ModInfos.Select(x => $"{x.DisplayName} | {x.DefaultLoadOrder}")));
    }

    private async void ToBottom_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = ModItemDataGrid.SelectedItem as ModInfo;

        if (selectedItem == null)
            return;

        var oldIndex = ViewModel.ModInfos.IndexOf(selectedItem);

        MoveSelectedItem(ModItemDataGrid.Items.Count - oldIndex - 1);

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


        var version = VersionTextBox.Text;
        if (string.IsNullOrEmpty(version))
        {
            version = Constants.DefaultVersion;
        }

        _systemIO.WriteModListDotJson(mods, ViewModel.MainModsFolder, version);
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
        if (!this._updatingCell)
        {
            _updatingCell = true;
            ViewModel.OnPropertyChanged(nameof(ViewModel.SelectedModLabel));
            ViewModel.OnPropertyChanged(nameof(ViewModel.SelectedMod));

            // Spaghetti code to detect mods that are out of order...can't rely on the current selected item always...
            for (var i = 0; i < this.ModItemDataGrid.Items.Count; i++)
            {
                var item = this.ModItemDataGrid.Items[i] as ModInfo;
                var index = Convert.ToInt32(item.DefaultLoadOrder);
                var currentIndex = i + 1;

                if (index != currentIndex)
                {
                    if (index < 1)
                    {
                        index = 1;
                    }
                    else if (index > this.ModItemDataGrid.Items.Count)
                    {
                        index = this.ModItemDataGrid.Items.Count;
                    }

                    var deltaIndex = index - currentIndex;

                    this.ModItemDataGrid.SelectedIndex = currentIndex - 1;
                    item.DefaultLoadOrder = currentIndex;
                    MoveSelectedItem(deltaIndex);
                    break;
                }
            }

            _updatingCell = false;
        }
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
        mods.RemoveAll(x => x.FolderName == mod.FolderName);
        mods.Add(mod);

        mods = SortAndUpdateMods(mods);
        UpdateGrid(mods);
    }

    private void PersistSystemSettings()
    {
        int windowX = 0;
        int windowY = 0;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            windowX = (int)this.Width;
            windowY = (int)this.Height;
        });

        this.Dispatcher.Invoke(() =>
        {
            var systemSettings = new SystemSettingsDto()
                                 {
                                     MainModsFolder = ViewModel.MainModsFolder,
                                     ModSources = ViewModel.ModSources.ToList(),
                                     WindowX = ((int?)windowX) ?? 1050,
                                     WindowY = ((int?)windowY) ?? 500,
                                     Vender = ViewModel.Vender,
                                     ExEPath = ViewModel.ExEPath,
                                     GameVersion = this.VersionTextBox.Text,
                                 };
            
            if (!Directory.Exists(LoadOrderManagerFolder))
                Directory.CreateDirectory(LoadOrderManagerFolder);
            
            File.WriteAllText(Path.Combine(LoadOrderManagerFolder, "settings.json"), JsonSerializer.Serialize(systemSettings));
            
            // recursively write each preset to its own file to make sharing easier
            foreach (var preset in ViewModel.Presets)
            {
                File.WriteAllText(Path.Combine(LoadOrderManagerFolder, $"{preset.Key}.txt"), preset.Value);
            }
        });
    }

    public string LoadOrderManagerFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "LOMV2");

    private void LoadSystemSettings()
    {
        var settingsFile = Path.Combine(LoadOrderManagerFolder, "settings.json");

        if (!File.Exists(settingsFile))
            return;

        try
        {
            var systemSettingsJson = File.ReadAllText(settingsFile);
            SystemSettingsDto systemSettings = JsonSerializer.Deserialize<SystemSettingsDto>(systemSettingsJson);
            ViewModel.ModSources = systemSettings.ModSources.ToHashSet();
            ViewModel.MainModsFolder = systemSettings.MainModsFolder;
            ViewModel.Vender = systemSettings.Vender;
            ViewModel.ExEPath = systemSettings.ExEPath;
            ViewModel.Presets = systemSettings.Presets; // kept for backwards compatibility
            foreach (var file in Directory.GetFiles(LoadOrderManagerFolder))
            {
                // any file aside from the settings file is a preset
                if (file != settingsFile)
                {
                    var presetName = Path.GetFileNameWithoutExtension(file);
                    ViewModel.Presets.Add(presetName, File.ReadAllText(file));
                }
            }

            this.Dispatcher.Invoke(() =>
            {
                this.Width = systemSettings.WindowX;
                this.Height = systemSettings.WindowY;
                this.VersionTextBox.Text = systemSettings.GameVersion;
            });

            switch (ViewModel.Vender)
            {
                case Enums.Vender.None:
                    break;
                case Enums.Vender.Steam:
                    SteamVenderMenuItem.Header = "Steam - X";
                    break;
                case Enums.Vender.Epic:
                    EpicVenderMenuItem.Header = "Epic - X";
                    break;
                case Enums.Vender.WindowsStore:
                    WindowsStoreVenderMenuItem.Header = "Windows - X";
                    break;
                case Enums.Vender.Other:
                    OtherVenderMenuItem.Header = "Other - X";
                    break;
                default:
                    break;
            }
        }
        catch (Exception)
        {

        }
        RefreshMods();
    }

    private void SavePreset(string presetName, string presetValue = "")
    {
        if (ViewModel.Presets.ContainsKey(presetName))
            if (MessageBox.Show($"Preset name {presetName} allready in use,\ndo you want to override?", "", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;
        
        ViewModel.Presets[presetName] = string.IsNullOrWhiteSpace(presetValue) ? GetPresetStringForCurrentMods() : presetValue;

        ViewModel.OnPropertyChanged(nameof(ViewModel.Presets));
        ViewModel.OnPropertyChanged(nameof(ViewModel.PresetNames));

        PersistSystemSettings();
    }

    private string GetPresetStringForCurrentMods()
    {
        var modInfos = ViewModel.ModInfos.ToArray();

        var presetKeys = modInfos.Select(x => new PresetModInfo(x.DisplayName, x.Author, x.FolderNameShort, x.Enabled, x.DefaultLoadOrder)).ToArray();

        return JsonSerializer.Serialize(presetKeys);
    }

    private void LoadPreset(string presetName)
    {
        if (!ViewModel.Presets.TryGetValue(presetName, out string presetJson))
            return;

        var presetMods = JsonSerializer.Deserialize<List<PresetModInfo>>(presetJson);

        if (presetMods == null)
            return;

        var mods = ViewModel.ModInfos.ToList();

        mods.ForEach(mod => mod.Enabled = false);
        mods.ForEach(mod =>
        {
            var match = presetMods.FirstOrDefault(presetMod => presetMod.DisplayName == mod.DisplayName &&
                presetMod.Author == mod.Author &&
                presetMod.FolderShort == mod.FolderNameShort);

            if (match == null)
            {
                mod.DefaultLoadOrder = int.MaxValue;
                return;
            }

            mod.Enabled = match.Enabled;
            mod.DefaultLoadOrder = match.LoadOrder;
        });

        mods = SortAndUpdateMods(mods);
        UpdateGrid(mods);
    }

    private void RemovePreset(string presetName)
    {
        ViewModel.Presets.Remove(presetName);
        if (File.Exists(Path.Combine(LoadOrderManagerFolder, $"{presetName}.txt")))
        {
            File.Delete(Path.Combine(LoadOrderManagerFolder, $"{presetName}.txt"));
        }
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

    public void Copy_Preset_To_Clipboard_Button_Click(object sender, RoutedEventArgs e)
    {
        var selectedPreset = PresetListBox.SelectedItem as string;
        if (selectedPreset == null)
            return;

        Clipboard.SetText($"{selectedPreset}:{GetPresetStringForCurrentMods()}");
    }

    public void Paste_Preset_From_Clipboard_Button_Click(object sender, RoutedEventArgs e)
    {
        var clipboardValue = Clipboard.GetText();
        if (!string.IsNullOrEmpty(clipboardValue))
        {
            try
            {
                var presetText = clipboardValue.Split(":", 2);

                SavePreset(presetText[0], presetText[1]);
                LoadPreset(presetText[0]);
            }
            catch (Exception _)
            {
                // swallow the failure for now
            }
        }
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

    private void VersionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        //Reset
        UserControlsGrid.IsEnabled = true;
        if (ViewModel.FilterActive)
        {
            UpdateGrid(ViewModel.BackupModInfos.ToList());
        }

        var mods = ViewModel.ModInfos.ToList();
        mods.ForEach(x => x.Highlight = false);
        UpdateGrid(mods);

        //Do filter stuff
        var text = FilterTextBox.Text;

        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        var filteredMods = new List<ModInfo>();

        if (ViewModel.HightlightChecked)
        {
            filteredMods = HightlightFilteredMods(text);
            UpdateGrid(filteredMods);
            return;
        }

        UserControlsGrid.IsEnabled = false;
        filteredMods = FilterMods(text);
        UpdateGrid(filteredMods);
    }

    private List<ModInfo> FilterMods(string text)
    {
        ViewModel.BackupModInfos = new ObservableCollection<ModInfo>(ViewModel.ModInfos.ToList());
        ViewModel.FilterActive = true;

        var filteredMods = ViewModel.ModInfos.ToList();
        return filteredMods.Where(x => x.DisplayName.ToLower().Contains(text.ToLower()) || x.Author.ToLower().Contains(text.ToLower())).ToList();
    }

    private List<ModInfo> HightlightFilteredMods(string text)
    {
        var filteredMods = ViewModel.ModInfos.ToList();
        filteredMods.Where(x => x.DisplayName.ToLower().Contains(text.ToLower()) || x.Author.Contains(text.ToLower())).ToList().ForEach(x => x.Highlight = true);
        filteredMods.Where(x => !(x.DisplayName.ToLower().Contains(text.ToLower()) || x.Author.Contains(text.ToLower()))).ToList().ForEach(x => x.Highlight = false);
        return filteredMods;
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        FilterTextBox_TextChanged(null, null);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void ResizeTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _resizeTimer.Stop();

        PersistSystemSettings();
    }

    public void Open_Main_Mods_Folder_Button_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.MainModsFolder;
        OpenFolder(selected);
    }

    public void Open_Secondary_Mods_Folder_Button_Click(object sender, RoutedEventArgs e)
    {
        var selected = SecondaryFoldersListBox.SelectedItem as string;
        OpenFolder(selected);
    }

    private void OpenFolder(string folder)
    {
        if (folder == null)
            return;

        if (!Directory.Exists(folder))
        {
            MessageBox.Show($"Folder: {folder} does not exist.");
        }

        try
        {
            Process.Start("explorer.exe", folder);
        }
        catch (Exception)
        {
            MessageBox.Show($"Could not open folder: {folder}");
            throw;
        }
    }

    public void Steam_Vender_Menu_Item_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Vender = Enums.Vender.Steam;
        ViewModel.ExEPath = string.Empty;
        ResetMenuItemHeaders();
        SteamVenderMenuItem.Header = "Steam - X";
        PersistSystemSettings();
    }

    public void Epic_Vender_Menu_Item_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Vender = Enums.Vender.Epic;
        ViewModel.ExEPath = string.Empty;
        ResetMenuItemHeaders();
        EpicVenderMenuItem.Header = "Epic - X";
        PersistSystemSettings();
    }

    public void WindowsStore_Vender_Menu_Item_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Vender = Enums.Vender.WindowsStore;
        ViewModel.ExEPath = string.Empty;
        ResetMenuItemHeaders();
        WindowsStoreVenderMenuItem.Header = "Windows Store - X";
        PersistSystemSettings();
    }

    public void Other_Vender_Menu_Item_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Vender = Enums.Vender.Other;
        string filePath = PickFile();
        ResetMenuItemHeaders();
        ViewModel.ExEPath = filePath;
        OtherVenderMenuItem.Header = "Other - X";
        PersistSystemSettings();
    }

    private void ResetMenuItemHeaders()
    {
        SteamVenderMenuItem.Header = "Steam";
        EpicVenderMenuItem.Header = "Epic";
        OtherVenderMenuItem.Header = "Other";
        WindowsStoreVenderMenuItem.Header = "Windows Store";
    }

    private string PickFile()
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog();
        dialog.Multiselect = false;
        dialog.Filter = "(.exe)|*.exe";
        dialog.CheckFileExists = true;
        dialog.CheckPathExists = true;
        dialog.DefaultExt = ".exe";

        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK)
            return "";
        return dialog.FileName;
    }

    private void LaunchEPIC()
    {
        try
        {
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(@"start com.epicgames.launcher://apps/Hoopoe?action=launch&silent=true");
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();
        }
        catch (Exception)
        {
            MessageBox.Show("Launching via Epic Launcher failed.");
        }
    }

    private void LaunchSteam()
    {
        try
        {

            Process.Start("explorer", @"steam://rungameid/784080");
        }
        catch (Exception)
        {
            MessageBox.Show("Launching via steam failed.");
        }
    }

    private void LaunchGameOther()
    {
        if(string.IsNullOrEmpty(ViewModel.ExEPath) || string.IsNullOrWhiteSpace(ViewModel.ExEPath))
            return;
        try
        {
            Process.Start(ViewModel.ExEPath);
        }
        catch (Exception)
        {
            MessageBox.Show($"Launching {ViewModel.ExEPath} failed.");
        }
    }

    private void LaunchWindowsStore()
    {
        Process cmd = new Process();
        cmd.StartInfo.FileName = "cmd.exe";
        cmd.StartInfo.RedirectStandardInput = true;
        cmd.StartInfo.RedirectStandardOutput = true;
        cmd.StartInfo.CreateNoWindow = true;
        cmd.StartInfo.UseShellExecute = false;
        cmd.Start();

        cmd.StandardInput.WriteLine(@"explorer.exe shell:appsFolder\PiranhaGamesInc.MechWarrior5Mercenaries_skpx0jhaqqap2!9PB86W3JK8Z5");
        cmd.StandardInput.Flush();
        cmd.StandardInput.Close();
        cmd.WaitForExit();
    }

    public void Start_Game_Button_Click(object sender, EventArgs e)
    {
        switch (ViewModel.Vender)
        {
            case Enums.Vender.None:
                MessageBox.Show("Please select a vendor first.");
                break;
            case Enums.Vender.Steam:
                LaunchSteam();
                break;
            case Enums.Vender.Epic:
                LaunchEPIC();
                break;
            case Enums.Vender.Other:
                if(string.IsNullOrEmpty(ViewModel.ExEPath) || string.IsNullOrWhiteSpace(ViewModel.ExEPath))
                {
                    MessageBox.Show("Path to MW5 exe was not set, please select a path by selecting \"Other\" as vender.");
                    return;
                }
                LaunchGameOther();
                break;
            case Enums.Vender.WindowsStore:
                LaunchWindowsStore();
                break;
            default:
                break;
        }
    }
}
