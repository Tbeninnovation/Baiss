using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Baiss.UI.Services
{
    public enum ToastType
    {
        Info,
        Success,
        Error
    }

    public class ToastMessage
    {
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ToastType Type { get; set; } = ToastType.Info;

        // Helper properties for XAML binding
        public bool IsInfoType => Type == ToastType.Info;
        public bool IsSuccessType => Type == ToastType.Success;
        public bool IsErrorType => Type == ToastType.Error;
    }

    public interface IToastService
    {
        ObservableCollection<ToastMessage> Messages { get; }
        void Show(string message, int durationMs = 3000);
        void ShowSuccess(string message, int durationMs = 3000);
        void ShowError(string message, int durationMs = 3000);
        ToastMessage ShowPersistent(string message, ToastType type = ToastType.Info);
        void Dismiss(ToastMessage toast);
    }

    public class ToastService : IToastService
    {
        public ObservableCollection<ToastMessage> Messages { get; } = new();
        
        public void Show(string message, int durationMs = 3000)
        {
            _ = EnqueueToast(message, ToastType.Info, durationMs);
        }

        public void ShowSuccess(string message, int durationMs = 3000)
        {
            _ = EnqueueToast(message, ToastType.Success, durationMs);
        }

        public void ShowError(string message, int durationMs = 3000)
        {
            _ = EnqueueToast(message, ToastType.Error, durationMs);
        }

        public ToastMessage ShowPersistent(string message, ToastType type = ToastType.Info)
        {
            return EnqueueToast(message, type, null);
        }

        public void Dismiss(ToastMessage toast)
        {
            if (toast == null)
            {
                return;
            }

            void RemoveToast()
            {
                Messages.Remove(toast);
            }

            if (Dispatcher.UIThread.CheckAccess())
                RemoveToast();
            else
                Dispatcher.UIThread.Post(RemoveToast);
        }

        private ToastMessage EnqueueToast(string message, ToastType type, int? durationMs)
        {
            var toast = new ToastMessage { Message = message, Type = type };

            void AddToast()
            {
                Messages.Add(toast);
                if (durationMs.HasValue)
                {
                    _ = AutoRemoveAsync(toast, durationMs.Value);
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
                AddToast();
            else
                Dispatcher.UIThread.Post(AddToast);

            return toast;
        }

        private async Task AutoRemoveAsync(ToastMessage toast, int durationMs)
        {
            try
            {
                await Task.Delay(durationMs);
                Messages.Remove(toast);
            }
            catch { /* ignore */ }
        }
    }
}
