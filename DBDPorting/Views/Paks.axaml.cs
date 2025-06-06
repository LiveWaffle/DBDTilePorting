using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DBDPorting.Views;

public partial class Paks : Window
{
    public Paks()
    {
        InitializeComponent();
        SetDirBox.Text = MainWindow.PakDirectory;
    }

    private async void SetDir(object? sender, RoutedEventArgs e)
    {
            var DirectoryDialog = new OpenFolderDialog
            {
                
            }; 
            var Directory = await DirectoryDialog.ShowAsync((Window)this);

            SetDirBox.Text = Directory;
        
            if (Directory != null)
            {
                var file = Directory;
                    Console.WriteLine("Directory set to "+ file);
            }
    }
}