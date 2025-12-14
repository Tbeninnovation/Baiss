using System.Threading.Tasks;

namespace Baiss.UI.Services
{
    public interface IDialogService
    {
        Task<string?> ShowFolderPickerAsync(string title = "Select Folder");
        Task<string[]> ShowMultipleFolderPickerAsync(string title = "Select Folders");
    }
}
