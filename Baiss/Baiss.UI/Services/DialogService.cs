using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using System.Linq;

namespace Baiss.UI.Services
{
    public class DialogService : IDialogService
    {
        public async Task<string?> ShowFolderPickerAsync(string title = "Select Folder")
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                var storageProvider = mainWindow?.StorageProvider;
                
                if (storageProvider != null)
                {
                    var options = new FolderPickerOpenOptions
                    {
                        Title = title,
                        AllowMultiple = false
                    };

                    var result = await storageProvider.OpenFolderPickerAsync(options);
                    return result.FirstOrDefault()?.Path.LocalPath;
                }
            }
            return null;
        }

        public async Task<string[]> ShowMultipleFolderPickerAsync(string title = "Select Folders")
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                var storageProvider = mainWindow?.StorageProvider;
                
                if (storageProvider != null)
                {
                    var options = new FolderPickerOpenOptions
                    {
                        Title = title,
                        AllowMultiple = true
                    };

                    var result = await storageProvider.OpenFolderPickerAsync(options);
                    return result.Select(f => f.Path.LocalPath).ToArray();
                }
            }
            return new string[0];
        }
    }
}

