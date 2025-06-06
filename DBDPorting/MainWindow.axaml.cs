using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DBDPorting.Views;
using Newtonsoft.Json;
using Serilog;

namespace DBDPorting;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        FindPaks();
    }

    public static string PakDirectory; 
    
    private async void FindPaks()
    {
        LauncherInstalled? launcherInstalled = null;
        foreach (var drive in DriveInfo.GetDrives())
        {
            var LauncherInstallPath = $@"{drive.Name}\ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat";
            if (!File.Exists(LauncherInstallPath)) continue;

            launcherInstalled  = JsonConvert.DeserializeObject<LauncherInstalled>(await File.ReadAllTextAsync(LauncherInstallPath));


            if (launcherInstalled is not null)
            {
                var dbdInfo = launcherInstalled.InstallationList.FirstOrDefault(x => x.AppName == "Brill");
                if (dbdInfo is not null)
                {
                    PakDirectory = dbdInfo.InstallLocation + @"\DeadByDaylight\Content\Paks";
                }
                   
            }
           
        }

    }

    private async void LoadFiles(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false
        };
        var files = await dialog.ShowAsync((Window)this);

        FileDialogBox.Text = files[0];
        
        if (files != null)
        {
            foreach (var file in files)
            {
                Console.WriteLine("Selected file: " + file);
            }
        }
    }

    private void Export(object? sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void SetDir(object? sender, RoutedEventArgs e)
    {
        var window = new Paks();
        window.Show();
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        this.BeginMoveDrag(e);
    }
}

file class LauncherInstalled
{
    public List<LauncherInstalledInfo> InstallationList = [];
}

file class LauncherInstalledInfo
{
    public string InstallLocation;
    public string AppVersion;
    public string AppName;
}