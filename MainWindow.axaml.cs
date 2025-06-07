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
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.UEFormat.Enums;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using DBDPorting.Views;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;


namespace DBDPorting;




public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        FindPaks();
        Mountall();
        
    }
    

    public static string PakDirectory;
    private async Task Mountall()
    {
        var version = new VersionContainer(EGame.GAME_DeadByDaylight);
        var provider = new DefaultFileProvider(PakDirectory, SearchOption.TopDirectoryOnly, version);
        provider.Initialize();

        string AesKey = "0x22B1639B548124925CF7B9CBAA09F9AC295FCF0324586D6B37EE1D42670B39B3"; 
        provider.SubmitKey(new FGuid(), new FAesKey(AesKey));
        
        var mappings = "https://raw.githubusercontent.com/TheNaeem/Unreal-Mappings-Archive/main/Dead%20by%20Daylight/8.0.0%20PTB/Mappings.usmap";
        var localPath = Path.Combine(Path.GetTempPath(), "DBD.usmap");
        
        using (var httpClient = new HttpClient())
        {
            var data = await httpClient.GetByteArrayAsync(mappings);
            await File.WriteAllBytesAsync(localPath, data);
        }
        
        provider.MappingsContainer = new FileUsmapTypeMappingsProvider(localPath);

        provider.PostMount();
        Log.Information("Mounted Ready to export");
    }
    private const string AesKey = "0x22B1639B548124925CF7B9CBAA09F9AC295FCF0324586D6B37EE1D42670B39B3";
    private const string ExportDir = "./exports";

    private async Task FindPaks()
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
private async void Exporter(object? sender, RoutedEventArgs e)
    {
        
        if (string.IsNullOrWhiteSpace(PakDirectory) || !Directory.Exists(PakDirectory))
        {
            Log.Warning("Pak directory is not set or does not exist!");
            return;
        }

        await Task.Run(() =>
        {
            Export(ExportType.Texture | ExportType.Sound | ExportType.Mesh | ExportType.Animation);
        });
    }
    private string _exportDirectory = "./exports";

    [Flags]
    public enum ExportType
    {
        None = 0,
        Texture = 1 << 0,
        Sound = 1 << 1,
        Mesh = 1 << 2,
        Animation = 1 << 3,
    }

    private void Export(ExportType type)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console(theme: AnsiConsoleTheme.Literate).CreateLogger();

        OodleHelper.DownloadOodleDll();
        OodleHelper.Initialize(OodleHelper.OODLE_DLL_NAME);

        var version = new VersionContainer(EGame.GAME_DeadByDaylight);
        var provider = new DefaultFileProvider(PakDirectory, SearchOption.TopDirectoryOnly, version)
        {
            MappingsContainer = new FileUsmapTypeMappingsProvider(Path.Combine(Path.GetTempPath(), "DBD.usmap"))
        };
        provider.Initialize();
        provider.SubmitKey(new FGuid(), new FAesKey(AesKey));
        provider.PostMount();

        var files = provider.Files.Values
            .GroupBy(it => StringUtils.SubstringBeforeLast(it.Path, '/'))
            .ToDictionary(it => it.Key, it => it.ToArray());

        var options = new ExporterOptions
        {
            LodFormat = ELodFormat.FirstLod,
            MeshFormat = EMeshFormat.UEFormat,
            AnimFormat = EAnimFormat.UEFormat,
            MaterialFormat = EMaterialFormat.AllLayersNoRef,
            TextureFormat = ETextureFormat.Png,
            CompressionFormat = EFileCompressionFormat.None,
            Platform = version.Platform,
            SocketFormat = ESocketFormat.Bone,
            ExportMorphTargets = true,
            ExportMaterials = false
        };

        var exportCount = 0;
        var watch = Stopwatch.StartNew();

        foreach (var (folder, packages) in files)
        {
            Log.Information("Scanning {Folder} ({Count} packages)", folder, packages.Length);

            Parallel.ForEach(packages, package =>
            {
                if (!provider.TryLoadPackage(package, out var pkg)) return;

                for (var i = 0; i < pkg.ExportMapLength; i++)
                {
                    var pointer = new FPackageIndex(pkg, i + 1).ResolvedObject;
                    if (pointer?.Object is null) continue;

                    var dummy = ((AbstractUePackage)pkg).ConstructObject(pointer.Class?.Object?.Value as UStruct, pkg);
                    switch (dummy)
                    {
                        case UTexture when type.HasFlag(ExportType.Texture) && pointer.Object.Value is UTexture texture:
                            try
                            {
                                Log.Information("{ExportType} found in {PackageName}", dummy.ExportType, package.Name);
                                SaveTexture(folder, texture, version.Platform, options, ref exportCount);
                            }
                            catch (Exception e)
                            {
                                Log.Warning(e, "Failed to decode {TextureName}", texture.Name);
                                return;
                            }
                            break;

                        case USoundWave when type.HasFlag(ExportType.Sound):
                        case UAkMediaAssetData when type.HasFlag(ExportType.Sound):
                            Log.Information("{ExportType} found in {PackageName}", dummy.ExportType, package.Name);

                            pointer.Object.Value.Decode(true, out var format, out var bytes);
                            if (bytes is not null)
                            {
                                var fileName = $"{pointer.Object.Value.Name}.{format.ToLower()}";
                                WriteToFile(folder, fileName, bytes, fileName, ref exportCount);
                            }
                            break;

                        case UAnimSequenceBase when type.HasFlag(ExportType.Animation):
                        case USkeletalMesh when type.HasFlag(ExportType.Mesh):
                        case UStaticMesh when type.HasFlag(ExportType.Mesh):
                        case USkeleton when type.HasFlag(ExportType.Mesh):
                            Log.Information("{ExportType} found in {PackageName}", dummy.ExportType, package.Name);

                            var exporter = new CUE4Parse_Conversion.Exporter(pointer.Object.Value, options);
                            if (exporter.TryWriteToDir(new DirectoryInfo(ExportDir), out _, out var filePath))
                            {
                                WriteToLog(folder, Path.GetFileName(filePath), ref exportCount);
                            }
                            break;
                    }
                }
            });
        }

        watch.Stop();

        Log.Information("Exported {ExportCount} files ({Types}) in {Time}",
            exportCount,
            type.ToStringBitfield(),
            watch.Elapsed);
    }

    private void SaveTexture(string folder, UTexture texture, ETexturePlatform platform, ExporterOptions options, ref int exportCount)
    {
        var bitmaps = new[] { texture.Decode(platform) };
        switch (texture)
        {
            case UTexture2DArray textureArray:
                bitmaps = textureArray.DecodeTextureArray(platform);
                break;
            case UTextureCube:
                bitmaps[0] = bitmaps[0]?.ToPanorama();
                break;
        }

        foreach (var bitmap in bitmaps)
        {
            if (bitmap is null) continue;
            var bytes = bitmap.Encode(options.TextureFormat, out var extension);
            var fileName = $"{texture.Name}.{extension}";

            WriteToFile(folder, fileName, bytes, $"{fileName} ({bitmap.Width}x{bitmap.Height})", ref exportCount);
        }
    }

    private void WriteToFile(string folder, string fileName, byte[] bytes, string logMessage, ref int exportCount)
    {
        var exportPath = Path.Combine(ExportDir, folder);
        Directory.CreateDirectory(exportPath);
        File.WriteAllBytes(Path.Combine(exportPath, fileName), bytes);
        WriteToLog(folder, logMessage, ref exportCount);
    }

    private void WriteToLog(string folder, string logMessage, ref int exportCount)
    {
        Log.Information("Exported {LogMessage} out of {Folder}", logMessage, folder);
        exportCount++;
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



