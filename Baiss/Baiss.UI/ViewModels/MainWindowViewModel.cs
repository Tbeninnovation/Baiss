using System;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Baiss.UI.Models;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Linq;
using Baiss.Domain.Entities;
using Baiss.Application.UseCases;
using Baiss.Application.DTOs;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using System.Reflection;
using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI.Universal;

namespace Baiss.UI.ViewModels;

public class NavigationItem : ViewModelBase
{
    public required string Id { get; set; }

    private string _title = string.Empty;
    public required string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public required string Icon { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming != value)
            {
                _isRenaming = value;
                OnPropertyChanged(nameof(IsRenaming));
            }
        }
    }

    private string _editingTitle = string.Empty;
    public string EditingTitle
    {
        get => _editingTitle;
        set
        {
            if (_editingTitle != value)
            {
                _editingTitle = value;
                OnPropertyChanged(nameof(EditingTitle));
            }
        }
    }

    public DateTime? LastActivity { get; set; }
    public Guid? ConversationId { get; set; } // Add ConversationId property
}

public enum ViewType
{
    Chat,
    Settings
}

public class ExpandableSourceItem : ViewModelBase
{
    public SourceItem SourceItem { get; set; }

    private bool _isExpanded = false;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }
    }

    public string FileName => SourceItem?.FileName ?? "";
    public string DisplayFileName
    {
        get
        {
            if (string.IsNullOrEmpty(FileName)) return "Unknown File";
            var parts = FileName.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[parts.Length - 1] : FileName;
        }
    }

    public string ScoreText => "Score: 60%"; // You can make this dynamic later
    public string OverviewText => SourceItem?.FileChunk?.FullText ?? "";

    public ExpandableSourceItem(SourceItem sourceItem)
    {
        SourceItem = sourceItem;
    }
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SendMessageUseCase _sendMessageUseCase;
    private readonly GetConversationsUseCase _getConversationsUseCase;
    private readonly GetConversationByIdUseCase _getConversationByIdUseCase;
    private readonly ConversationManagementUseCase _conversationManagementUseCase;
    private readonly IExternalApiService _externalApiService;

    private readonly CaptureScreenUseCase _captureScreenUseCase;
    private System.Threading.Timer? _serverStatusTimer;

    private bool _isSidebarOpen = false;
    private double _sidebarWidth = 0;
    private bool _isBotTyping;
    private NavigationItem? _selectedNavigationItem;
    private Guid? _currentConversationId;
    private bool _showNoModelsModal = false;
    private bool _showModelErrorModal = false;
    private string _modelErrorMessage = string.Empty;

    private bool _isCollapsed = false;
    private bool _isServerOnline = false;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                OnPropertyChanged(nameof(IsCollapsed));
            }
        }
    }

    public bool IsServerOnline
    {
        get => _isServerOnline;
        set
        {
            if (_isServerOnline != value)
            {
                _isServerOnline = value;
                OnPropertyChanged(nameof(IsServerOnline));
                OnPropertyChanged(nameof(ServerStatusColor));
                OnPropertyChanged(nameof(CanSendMessage));
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SendStreamingMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string ServerStatusColor => IsServerOnline ? "#43D590" : "#EC4B32";

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (_isSidebarOpen != value)
            {
                _isSidebarOpen = value;
                OnPropertyChanged(nameof(IsSidebarOpen));
            }
        }
    }

    public double SidebarWidth
    {
        get => _sidebarWidth;
        set
        {
            if (_sidebarWidth != value)
            {
                _sidebarWidth = value;
                OnPropertyChanged(nameof(SidebarWidth));
            }
        }
    }

    private bool _isSourceSidebarOpen = false;
    public bool IsSourceSidebarOpen
    {
        get => _isSourceSidebarOpen;
        set
        {
            if (_isSourceSidebarOpen != value)
            {
                _isSourceSidebarOpen = value;
                OnPropertyChanged(nameof(IsSourceSidebarOpen));
            }
        }
    }

    private double _sourceSidebarWidth = 0;
    public double SourceSidebarWidth
    {
        get => _sourceSidebarWidth;
        set
        {
            if (_sourceSidebarWidth != value)
            {
                _sourceSidebarWidth = value;
                OnPropertyChanged(nameof(SourceSidebarWidth));
            }
        }
    }

    private bool _showScrollToBottom = false;
    public bool ShowScrollToBottom
    {
        get => _showScrollToBottom;
        set
        {
            if (_showScrollToBottom != value)
            {
                _showScrollToBottom = value;
                OnPropertyChanged(nameof(ShowScrollToBottom));
            }
        }
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
    public ObservableCollection<NavigationItem> NavigationItems { get; } = new ObservableCollection<NavigationItem>();

    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (_selectedNavigationItem != value)
            {
                // Deselect previous item
                if (_selectedNavigationItem != null)
                {
                    _selectedNavigationItem.IsSelected = false;
                }

                _selectedNavigationItem = value;

                // Select new item
                if (_selectedNavigationItem != null)
                {
                    _selectedNavigationItem.IsSelected = true;
                }

                OnPropertyChanged(nameof(SelectedNavigationItem));
                RefreshNavigationItems();
            }
        }
    }

    private string _newMessage = string.Empty;

    public string NewMessage
    {
        get => _newMessage;
        set
        {
            if (_newMessage != value)
            {
                _newMessage = value;
                OnPropertyChanged(nameof(NewMessage));
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SendStreamingMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBotTyping
    {
        get => _isBotTyping;
        set
        {
            if (_isBotTyping != value)
            {
                _isBotTyping = value;
                OnPropertyChanged(nameof(IsBotTyping));
                OnPropertyChanged(nameof(CanSendMessage));
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SendStreamingMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowNoModelsModal
    {
        get => _showNoModelsModal;
        set
        {
            if (_showNoModelsModal != value)
            {
                _showNoModelsModal = value;
                OnPropertyChanged(nameof(ShowNoModelsModal));
                OnPropertyChanged(nameof(CanSendMessage));
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SendStreamingMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowModelErrorModal
    {
        get => _showModelErrorModal;
        set
        {
            if (_showModelErrorModal != value)
            {
                _showModelErrorModal = value;
                OnPropertyChanged(nameof(ShowModelErrorModal));
                OnPropertyChanged(nameof(CanSendMessage));
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SendStreamingMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string ModelErrorMessage
    {
        get => _modelErrorMessage;
        set
        {
            if (_modelErrorMessage != value)
            {
                _modelErrorMessage = value;
                OnPropertyChanged(nameof(ModelErrorMessage));
            }
        }
    }

    public ObservableCollection<Dataset> Datasets { get; } = new ObservableCollection<Dataset>();

    public class Dataset
    {
        public required string Name { get; set; }
        public required string FilePath { get; set; }
        public string Icon => GetIconForFile(Name);

        private static string GetIconForFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Use image icon for image files and screenshots
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".tiff", ".ico" };

            if (imageExtensions.Contains(extension))
            {
                return "/Assets/image.svg";
            }

            // Use file-text icon for all other file types
            return "/Assets/file-text.svg";
        }
    }

    public ICommand ToggleSidebarCommand { get; }
    public ICommand SendMessageCommand { get; }
    public ICommand SendStreamingMessageCommand { get; }
    public ICommand AddFileCommand { get; }
    public ICommand SelectNavigationItemCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand CreateNewChatCommand { get; }
    public ICommand ToggleCollapseCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }
    public ICommand NavigateBackCommand { get; }
    public ICommand RemoveFileCommand { get; }  // New command
    public ICommand RenameConversationCommand { get; }
    public ICommand DeleteConversationCommand { get; }
    public ICommand StartRenameCommand { get; }
    public ICommand ConfirmRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand ToggleSourcesCommand { get; }
    public ICommand ShowSourcesInSidebarCommand { get; }
    public ICommand OpenSourceFileCommand { get; }
    public ICommand ToggleSourceExpandedCommand { get; }
    public ICommand CaptureScreenButtonCommand { get; }
    public ICommand CloseNoModelsModalCommand { get; }
    public ICommand CloseModelErrorModalCommand { get; }
    public ICommand GoToSettingsCommand { get; }
    public ICommand ScrollToBottomCommand { get; }
    public ICommand CopyMessageCommand { get; }

    private string _lastOpenedFileContent = string.Empty;

    public string LastOpenedFileContent
    {
        get => _lastOpenedFileContent;
        set
        {
            if (_lastOpenedFileContent != value)
            {
                _lastOpenedFileContent = value;
                OnPropertyChanged(nameof(LastOpenedFileContent));
            }
        }
    }

    private ViewType _currentView = ViewType.Chat;

    public ViewType CurrentView
    {
        get => _currentView;
        set
        {
            if (_currentView != value)
            {
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
                OnPropertyChanged(nameof(IsChatView));
                OnPropertyChanged(nameof(IsSettingsView));
            }
        }
    }

    public bool IsChatView => CurrentView == ViewType.Chat;
    public bool IsSettingsView => CurrentView == ViewType.Settings;

    private SettingsViewModel _settingsViewModel;

    public SettingsViewModel SettingsViewModel
    {
        get => _settingsViewModel;
        set
        {
            if (_settingsViewModel != value)
            {
                _settingsViewModel = value;
                OnPropertyChanged(nameof(SettingsViewModel));
            }
        }
    }

    // Expose settings properties for binding
    public ObservableCollection<SettingsNavigationItem> SettingsNavigationItems => _settingsViewModel.SettingsNavigationItems;
    public string WelcomeMessage => _settingsViewModel.WelcomeMessage;
    public string CurrentSettingsContent => _settingsViewModel.CurrentSettingsContent;
    public ICommand SelectSettingsItemCommand => _settingsViewModel.SelectSettingsItemCommand;

    public MainWindowViewModel(
        SendMessageUseCase sendMessageUseCase,
        GetConversationsUseCase getConversationsUseCase,
        GetConversationByIdUseCase getConversationByIdUseCase,
        ConversationManagementUseCase conversationManagementUseCase,
        SettingsViewModel settingsViewModel,
        CaptureScreenUseCase captureScreenUseCase,
        ISettingsRepository settingsRepository,
        IModelRepository modelRepository,
        IExternalApiService externalApiService
        // IUniversalAIService universalAIService,
        // IEmbeddingsService embeddingsService
        )
    {
        _sendMessageUseCase = sendMessageUseCase ?? throw new ArgumentNullException(nameof(sendMessageUseCase));
        _getConversationsUseCase = getConversationsUseCase ?? throw new ArgumentNullException(nameof(getConversationsUseCase));
        _getConversationByIdUseCase = getConversationByIdUseCase ?? throw new ArgumentNullException(nameof(getConversationByIdUseCase));
        _conversationManagementUseCase = conversationManagementUseCase ?? throw new ArgumentNullException(nameof(conversationManagementUseCase));
        _captureScreenUseCase = captureScreenUseCase ?? throw new ArgumentNullException(nameof(captureScreenUseCase));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
        _externalApiService = externalApiService ?? throw new ArgumentNullException(nameof(externalApiService));
        // _universalAIService = universalAIService; // optional in case of local-only
        // _embeddingsService = embeddingsService;   // optional in case of local-only
        _sidebarWidth = 0;
        _isSidebarOpen = false;

        // Start server status checking
        StartServerStatusChecking();

        // Initialize SettingsViewModel
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _settingsViewModel.PropertyChanged += (s, e) =>
        {
            // Forward property changes from SettingsViewModel
            if (e.PropertyName == nameof(SettingsViewModel.WelcomeMessage))
                OnPropertyChanged(nameof(WelcomeMessage));
            else if (e.PropertyName == nameof(SettingsViewModel.CurrentSettingsContent))
                OnPropertyChanged(nameof(CurrentSettingsContent));
            else if (e.PropertyName == nameof(SettingsViewModel.SettingsNavigationItems))
                OnPropertyChanged(nameof(SettingsNavigationItems));
            else if (e.PropertyName == "ChatModelSavedForValidation")
            {
                // Re-validate chat model when selection has been saved
                Console.WriteLine("Chat model saved - validating...");
                _ = ValidateChatModelAsync();
            }
        };

        // Load conversations on startup
        _ = LoadConversationsAsync();

        ToggleSidebarCommand = new RelayCommand(() =>
        {
            if (IsSidebarOpen)
            {
                SidebarWidth = 0;
                IsSidebarOpen = false;
            }
            else
            {
                // Close source sidebar if open
                if (IsSourceSidebarOpen)
                {
                    SourceSidebarWidth = 0;
                    IsSourceSidebarOpen = false;
                }

                SidebarWidth = 280;
                IsSidebarOpen = true;
            }
        });

        SelectNavigationItemCommand = new RelayCommand<NavigationItem>(item =>
        {
            if (item != null)
            {
                SelectedNavigationItem = item;
                LoadChatForNavigationItem(item);
            }
        });

        NavigateToSettingsCommand = new RelayCommand(async () =>
        {
            CurrentView = ViewType.Settings;
            // Reload settings to show any newly saved data (like file paths)
            await _settingsViewModel.ReloadSettingsAsync();
        });

        NavigateBackCommand = new RelayCommand(() =>
        {
            CurrentView = ViewType.Chat;
            _ = ValidateChatModelAsync();
        });

        OpenSettingsCommand = new RelayCommand(() =>
        {
            CurrentView = ViewType.Settings;
        });

        OpenAboutCommand = new RelayCommand(() =>
        {
            Messages.Add(new ChatMessage
            {
                Content = "About clicked! This is baiss.desktop - an AI chat application.",
                Timestamp = DateTime.Now,
                IsMine = false
            });
        });

        CreateNewChatCommand = new RelayCommand(() =>
        {
            _ = ValidateChatModelAsync();
            Messages.Clear();
            ShowScrollToBottom = false; // Hide scroll button when starting new chat
            _currentConversationId = null;

            // Deselect current navigation item
            if (SelectedNavigationItem != null)
            {
                SelectedNavigationItem.IsSelected = false;
            }
            SelectedNavigationItem = null;
        });

        SendMessageCommand = new RelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(NewMessage))
            {
                // Check if server is online
                if (!IsServerOnline)
                {
                    Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000);
                    return;
                }

                // Check if files are present and embedding model is selected
                if (Datasets.Count > 0)
                {
                    var hasEmbeddingModel = await CheckIfEmbeddingModelSelectedAsync();
                    if (!hasEmbeddingModel)
                    {
                        Views.MainWindow.ToastServiceInstance.ShowError("You need to select an embedding model first to send files", 4000);
                        return;
                    }
                }

                // Use streaming by default for better UX
                await SendStreamingMessageAsync();
            }
        }, () => CanSendMessage && !string.IsNullOrWhiteSpace(NewMessage) && IsServerOnline);

        SendStreamingMessageCommand = new RelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(NewMessage))
            {
                // Check if server is online
                if (!IsServerOnline)
                {
                    Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000);
                    return;
                }

                // Check if files are present and embedding model is selected
                if (Datasets.Count > 0)
                {
                    var hasEmbeddingModel = await CheckIfEmbeddingModelSelectedAsync();
                    if (!hasEmbeddingModel)
                    {
                        Views.MainWindow.ToastServiceInstance.ShowError("You need to select an embedding model first to send files", 4000);
                        return;
                    }
                }

                await SendStreamingMessageAsync();
            }
        }, () => CanSendMessage && !string.IsNullOrWhiteSpace(NewMessage) && IsServerOnline);

        AddFileCommand = new AsyncRelayCommand(async param =>
        {
            try
            {
                // Try to get Window and its StorageProvider from param or via ApplicationLifetime
                Window? window = null;
                IStorageProvider? provider = null;
                if (param is Window w && w.StorageProvider is { } sp)
                {
                    window = w;
                    provider = sp;
                }
                else if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                         desktop.MainWindow != null &&
                         desktop.MainWindow.StorageProvider is { } mainSp)
                {
                    window = desktop.MainWindow;
                    provider = mainSp;
                }
                if (window == null || provider == null)
                {
                    Console.Write("StorageProvider or Window is null");
                    return;
                }

                var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select File",
                    AllowMultiple = false
                    // No FileTypeFilter - let users select any file
                });

                if (files.Count >= 1)
                {
                    var selectedFile = files[0];
                    var fileExtension = Path.GetExtension(selectedFile.Name).ToLowerInvariant();

                    // Check if the selected file type is supported
                    if (!IsFileTypeSupported(fileExtension))
                    {
                        // Show toast error for unsupported file type
                        try
                        {
                            Views.MainWindow.ToastServiceInstance.ShowError($"File type '{fileExtension}' is not supported. Supported types: .pdf, .txt, .docx, .csv, .md, .xlsx", 5000);
                        }
                        catch { }
                        return;
                    }

                    // Get the full file path
                    var filePath = selectedFile.TryGetLocalPath();
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Views.MainWindow.ToastServiceInstance.ShowError("Could not get file path", 3000);
                        return;
                    }

                    await using var stream = await selectedFile.OpenReadAsync();
                    using var streamReader = new StreamReader(stream);
                    LastOpenedFileContent = await streamReader.ReadToEndAsync();
                    Datasets.Add(new Dataset { Name = selectedFile.Name, FilePath = filePath });
                }
                else
                {
                    Console.Write("No file selected");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Views.MainWindow.ToastServiceInstance.ShowError($"Error adding file: {ex.Message}", 5000);
                }
                catch { }
            }
        });

        ToggleCollapseCommand = new RelayCommand(ToggleCollapse);
        RemoveFileCommand = new RelayCommand<Dataset>(RemoveFile);

        // Navigation item menu commands
        RenameConversationCommand = new RelayCommand<NavigationItem>(RenameConversation);
        DeleteConversationCommand = new RelayCommand<NavigationItem>(async item => await DeleteConversation(item));
        StartRenameCommand = new RelayCommand<NavigationItem>(StartRename);
        ConfirmRenameCommand = new RelayCommand<NavigationItem>(async item => await ConfirmRename(item));
        CancelRenameCommand = new RelayCommand<NavigationItem>(CancelRename);
        ToggleSourcesCommand = new RelayCommand<ChatMessage>(ToggleSources);
        ShowSourcesInSidebarCommand = new RelayCommand<ChatMessage>(ShowSourcesInSidebar);
        OpenSourceFileCommand = new RelayCommand<ExpandableSourceItem>(OpenSourceFile);
        ToggleSourceExpandedCommand = new RelayCommand<ExpandableSourceItem>(ToggleSourceExpanded);

        // Modal is controlled by validation (default hidden)
        ShowNoModelsModal = false;
        ShowModelErrorModal = false;

        CaptureScreenButtonCommand = new RelayCommand(OnCaptureScreenButtonClicked);

        // Modal commands
        CloseNoModelsModalCommand = new RelayCommand(() => ShowNoModelsModal = false);
        CloseModelErrorModalCommand = new RelayCommand(() => ShowModelErrorModal = false);
        GoToSettingsCommand = new RelayCommand(() => {
            CurrentView = ViewType.Settings;
            ShowNoModelsModal = false;
            ShowModelErrorModal = false;
        });

        ScrollToBottomCommand = new RelayCommand(() => {
            // This command will be triggered from the view
            // The actual scrolling logic will be in the code-behind
        });

        // Copy message content to clipboard
        CopyMessageCommand = new RelayCommand<ChatMessage>(async message =>
        {
            if (message == null || string.IsNullOrEmpty(message.Content)) return;
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    var clipboard = desktop.MainWindow.Clipboard;
                    if (clipboard != null)
                    {
                        // Strip XML-like tags (e.g., <answer>, </answer>) from content
                        var cleanContent = System.Text.RegularExpressions.Regex.Replace(
                            message.Content, 
                            @"</?(?:answer|thinking|search_tool)[^>]*>", 
                            "", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        ).Trim();
                        
                        await clipboard.SetTextAsync(cleanContent);
                        Views.MainWindow.ToastServiceInstance.ShowSuccess("Message copied to clipboard", 2000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to copy to clipboard: {ex.Message}");
            }
        });
    }

    private async Task LoadConversationsAsync()
    {
        try
        {
            var conversations = await _getConversationsUseCase.ExecuteAsync();

            NavigationItems.Clear();

            /* Add "New Chat" option at the top
            var newChatItem = new NavigationItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = "New Chat",
                Icon = "??",
                IsSelected = false,
                LastActivity = DateTime.Now,
                ConversationId = null
            };
            NavigationItems.Add(newChatItem);*/

            // Add existing conversations
            foreach (var conversation in conversations)
            {
                var navigationItem = new NavigationItem
                {
                    Id = conversation.ConversationId.ToString(),
                    ConversationId = conversation.ConversationId,
                    Title = conversation.Title,
                    Icon = "??",
                    IsSelected = false,
                    LastActivity = conversation.UpdatedAt
                };
                NavigationItems.Add(navigationItem);
            }

            // Select "New Chat" by default
            // SelectedNavigationItem = newChatItem;
        }
        catch (Exception ex)
        {
            // Handle error loading conversations
            Console.WriteLine($"Error loading conversations: {ex.Message}");

            // Fallback to default new chat
            var newChatItem = new NavigationItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = "New Chat",
                Icon = "??",
                IsSelected = true,
                LastActivity = DateTime.Now,
                ConversationId = null
            };
            NavigationItems.Add(newChatItem);
            SelectedNavigationItem = newChatItem;
        }
    }

    private async void LoadChatForNavigationItem(NavigationItem item)
    {
        await ValidateChatModelAsync();
        // Clear current messages
        Messages.Clear();
        ShowScrollToBottom = false; // Hide scroll button when switching chats

        if (item.ConversationId.HasValue)
        {
            // Load existing conversation
            try
            {
                var conversation = await _getConversationByIdUseCase.ExecuteAsync(item.ConversationId.Value);
                // Console.WriteLine($"conversation : {conversation}");
                // var sources = System.Text.Json.JsonSerializer.Deserialize<List<SourceItem>>(msg.Sources);
                if (conversation != null && conversation.Messages != null)
                {
                    // Load messages from conversation
                    foreach (var message in conversation.Messages)
                    {
                        // Deserialize sources if they exist
                        var sources = new List<SourceItem>();
                        if (!string.IsNullOrEmpty(message.Sources))
                        {
                            try
                            {
                                sources = System.Text.Json.JsonSerializer.Deserialize<List<SourceItem>>(message.Sources) ?? new List<SourceItem>();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error deserializing sources: {ex.Message}");
                            }
                        }

                        // Convert paths from DTO to UI model
                        var paths = new List<PathScore>();
                        if (message.Paths != null && message.Paths.Any())
                        {
                            paths = message.Paths.Select(p => new PathScore
                            {
                                Path = p.Path,
                                Score = p.Score
                            }).ToList();
                        }

                        Messages.Add(new ChatMessage
                        {
                            Content = message.Content ?? string.Empty,
                            Timestamp = message.SentAt,
                            IsMine = message.SenderType == SenderType.USER,
                            Sources = sources,
                            Paths = paths
                        });
                    }
                }

                // Set current conversation ID
                _currentConversationId = item.ConversationId;
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage
                {
                    Content = $"Error loading conversation: {ex.Message}",
                    Timestamp = DateTime.Now,
                    IsMine = false
                });
            }
        }
        else
        {
            // This is a "New Chat" - clear conversation ID
            _currentConversationId = null;

            Messages.Add(new ChatMessage
            {
                Content = "Start a new conversation by typing a message below.",
                Timestamp = DateTime.Now,
                IsMine = false
            });
        }
    }

    // Dependencies for validation
    private readonly ISettingsRepository _settingsRepository;
    private readonly IModelRepository _modelRepository;
    private readonly IUniversalAIService? _universalAIService;
    private readonly IEmbeddingsService? _embeddingsService;

    public async Task ValidateChatModelAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetAsync();

            // Check if chat model is selected (embedding model is optional)
            var hasChatModel = await CheckIfChatModelSelectedAsync(settings);
            // Console.WriteLine($"ValidateChatModelAsync - hasChatModel: {hasChatModel}, ShowNoModelsModal: {ShowNoModelsModal}");

            if (!hasChatModel)
            {
                // Only show the modal if it hasn't been shown before
                if (settings != null && !settings.HasShownWelcomeModal)
                {
                    Console.WriteLine("Showing modal - first time");
                    ShowNoModelsModal = true;
                    ShowModelErrorModal = false;

                    // Mark that we've shown the modal
                    settings.HasShownWelcomeModal = true;
                    await _settingsRepository.SaveAsync(settings);
                }
                else
                {
                    // Don't show modal if already shown before
                    Console.WriteLine("Not showing modal - already shown before");
                    ShowNoModelsModal = false;
                    ShowModelErrorModal = false;
                }
                return;
            }

            // All checks passed - hide any modals
            // Console.WriteLine("Hiding modal - chat model is selected");
            ShowNoModelsModal = false;
            ShowModelErrorModal = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ValidateChatModelAsync error: {ex.Message}");
        }
    }

    private async Task<bool> CheckIfModelsProperlySelectedAsync(Baiss.Domain.Entities.Settings? settings)
    {
        try
        {
            if (settings == null)
            {
                return false;
            }

            // Check if chat model is selected and valid
            var chatModelId = settings.AIChatModelId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(chatModelId))
            {
                return false; // No chat model selected
            }

            var chatModel = await _modelRepository.GetModelByIdAsync(chatModelId);
            if (chatModel == null || !chatModel.IsActive)
            {
                return false; // Chat model not found or inactive
            }

            // Check if embedding model is selected and valid
            var embeddingModelId = settings.AIEmbeddingModelId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(embeddingModelId))
            {
                return false; // No embedding model selected
            }

            var embeddingModel = await _modelRepository.GetModelByIdAsync(embeddingModelId);
            if (embeddingModel == null || !embeddingModel.IsActive)
            {
                return false; // Embedding model not found or inactive
            }

            // Both chat and embedding models are properly selected and valid
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking model selections: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckIfChatModelSelectedAsync(Baiss.Domain.Entities.Settings? settings)
    {
        try
        {
            if (settings == null)
            {
                return false;
            }

            // Check if chat model is selected and valid
            var chatModelId = settings.AIChatModelId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(chatModelId))
            {
                return false; // No chat model selected
            }

            var chatModel = await _modelRepository.GetModelByIdAsync(chatModelId);
            if (chatModel == null || !chatModel.IsActive)
            {
                return false; // Chat model not found or inactive
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking chat model selection: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckIfEmbeddingModelSelectedAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetAsync();
            if (settings == null)
            {
                return false;
            }

            // Check if embedding model is selected and valid
            var embeddingModelId = settings.AIEmbeddingModelId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(embeddingModelId))
            {
                return false; // No embedding model selected
            }

            var embeddingModel = await _modelRepository.GetModelByIdAsync(embeddingModelId);
            if (embeddingModel == null || !embeddingModel.IsActive)
            {
                return false; // Embedding model not found or inactive
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking embedding model selection: {ex.Message}");
            return false;
        }
    }

    private void RefreshNavigationItems()
    {
        // Trigger UI refresh for navigation items
        OnPropertyChanged(nameof(NavigationItems));
    }

    // Method to add a new chat
    public void CreateNewChat(string? title = null)
    {
        var newChat = new NavigationItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = title ?? $"Chat {NavigationItems.Count + 1}",
            Icon = "??",
            LastActivity = DateTime.Now,
            ConversationId = null
        };

        NavigationItems.Insert(0, newChat); // Add at the beginning
        SelectedNavigationItem = newChat;
    }

    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    private void RemoveFile(Dataset? dataset)
    {
        if (dataset != null && Datasets.Contains(dataset))
        {
            Datasets.Remove(dataset);
        }
    }

    // Navigation item menu methods
    private void StartRename(NavigationItem? item)
    {
        if (item == null) return;

        item.EditingTitle = item.Title;
        item.IsRenaming = true;
    }

    private async Task ConfirmRename(NavigationItem? item)
    {
        if (item == null || item.ConversationId == null) return;

        var newTitle = item.EditingTitle?.Trim();
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            CancelRename(item);
            Views.MainWindow.ToastServiceInstance.ShowError("Please enter a valid name for your conversation.", 3000);
            return;
        }

        try
        {
            var success = await _conversationManagementUseCase.RenameTitleAsync(item.ConversationId.Value, newTitle);
            if (success)
            {
                // Update the title - this should now trigger PropertyChanged
                item.Title = newTitle;
                item.IsRenaming = false;

                // Force UI refresh
                RefreshNavigationItems();
                OnPropertyChanged(nameof(NavigationItems));
                Views.MainWindow.ToastServiceInstance.ShowSuccess($"Conversation renamed to \"{newTitle}\"", 2500);
            }
            else
            {
                // Show error message - title might be too long or invalid
                Views.MainWindow.ToastServiceInstance.ShowError("Couldn't rename conversation. Try a shorter title.", 4000);
                CancelRename(item);
            }
        }
        catch (Exception ex)
        {
            Views.MainWindow.ToastServiceInstance.ShowError($"Error renaming conversation: {ex.Message}", 4000);
            CancelRename(item);
        }
    }

    private void CancelRename(NavigationItem? item)
    {
        if (item == null) return;

        item.IsRenaming = false;
        item.EditingTitle = string.Empty;
    }

    private void RenameConversation(NavigationItem? item)
    {
        StartRename(item);
    }

    private async Task DeleteConversation(NavigationItem? item)
    {
        if (item == null || item.ConversationId == null) return;

        try
        {
            var success = await _conversationManagementUseCase.DeleteConversationAsync(item.ConversationId.Value);
            if (success)
            {
                // Remove from UI
                NavigationItems.Remove(item);

                // If this was the selected conversation, clear the chat
                if (SelectedNavigationItem == item)
                {
                    SelectedNavigationItem = null;
                    _currentConversationId = null;
                    Messages.Clear();
                    Messages.Add(new ChatMessage
                    {
                        Content = "Conversation deleted. Start a new conversation by typing a message below.",
                        Timestamp = DateTime.Now,
                        IsMine = false
                    });
                }

                RefreshNavigationItems();
            }
            else
            {
                Messages.Add(new ChatMessage
                {
                    Content = "Failed to delete conversation.",
                    Timestamp = DateTime.Now,
                    IsMine = false
                });
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Content = $"Error deleting conversation: {ex.Message}",
                Timestamp = DateTime.Now,
                IsMine = false
            });
        }
    }

    private void ToggleSources(ChatMessage? message)
    {
        if (message == null) return;

        message.IsSourcesExpanded = !message.IsSourcesExpanded;
    }

    private ObservableCollection<ExpandableSourceItem> _sidebarFiles = new ObservableCollection<ExpandableSourceItem>();
    public ObservableCollection<ExpandableSourceItem> SidebarFiles => _sidebarFiles;

    private void ShowSourcesInSidebar(ChatMessage? message)
    {
        if (message == null || message.Sources == null || !message.Sources.Any())
            return;

        // Close chat history sidebar if open
        if (IsSidebarOpen)
        {
            SidebarWidth = 0;
            IsSidebarOpen = false;
        }

        // Populate sidebar files
        SidebarFiles.Clear();
        foreach (var s in message.Sources)
        {
            SidebarFiles.Add(new ExpandableSourceItem(s));
        }

        // Open source sidebar and set width if closed
        if (!IsSourceSidebarOpen)
        {
            SourceSidebarWidth = 300;
            IsSourceSidebarOpen = true;
        }
        else
        {
            // ensure width is reasonable
            SourceSidebarWidth = Math.Max(SourceSidebarWidth, 300);
        }
    }

    private void ToggleSourceExpanded(ExpandableSourceItem? source)
    {
        if (source == null) return;
        source.IsExpanded = !source.IsExpanded;
    }

    private void OpenSourceFile(ExpandableSourceItem? source)
    {
        if (source == null) return;

        // Use the file chunk full text as the content to display
        LastOpenedFileContent = source.SourceItem?.FileChunk?.FullText ?? string.Empty;

        // Ensure source sidebar stays open
        if (!IsSourceSidebarOpen)
        {
            SourceSidebarWidth = 300;
            IsSourceSidebarOpen = true;
        }
    }

    private async Task SendStreamingMessageAsync()
    {
        // Add user message to UI
        Messages.Add(new ChatMessage
        {
            Content = NewMessage,
            Timestamp = DateTime.Now,
            IsMine = true
        });

        // Update the current navigation item's last activity
        if (SelectedNavigationItem != null)
        {
            SelectedNavigationItem.LastActivity = DateTime.Now;
            RefreshNavigationItems();
        }

        string userMessage = NewMessage;
        NewMessage = string.Empty;

        // Create streaming assistant message
        var streamingMessage = new ChatMessage
        {
            Content = "",
            Timestamp = DateTime.Now,
            IsMine = false,
            IsStreaming = true,
            IsLoadingMessage = true
        };
        Messages.Add(streamingMessage);

        var hasStartedStreaming = false;

        // Show typing indicator
        IsBotTyping = true;

        try
        {
            // Collect file paths from Datasets
            var filePaths = Datasets.Select(d => d.FilePath).Where(p => !string.IsNullOrEmpty(p)).ToList();

            // Create DTO and send message using streaming use case
            var sendMessageDto = new SendMessageDto
            {
                Content = userMessage,
                ConversationId = _currentConversationId,
                FilePaths = filePaths
            };

            await foreach (var (textChunk, finalResult) in _sendMessageUseCase.ExecuteStreamingAsync(sendMessageDto))
            {
                if (!string.IsNullOrEmpty(textChunk))
                {
                    // Update UI on main thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Check for code execution status marker
                        if (textChunk.StartsWith("[CODE_EXEC:"))
                        {
                            // Parse the marker: [CODE_EXEC:status:error]
                            var parts = textChunk.TrimStart('[').TrimEnd(']').Split(':');
                            if (parts.Length >= 2)
                            {
                                var isSuccess = parts[1] == "success";
                                var errorMsg = parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) 
                                    ? string.Join(":", parts.Skip(2)) 
                                    : null;
                                
                                // Get the current code block index (count of code segments)
                                var codeBlockCount = streamingMessage.MessageSegments
                                    .OfType<Models.CodeExecutionMessageSegment>()
                                    .Count();
                                
                                // Update the last code block (index = count - 1)
                                if (codeBlockCount > 0)
                                {
                                    streamingMessage.UpdateCodeExecutionResult(codeBlockCount - 1, isSuccess, errorMsg);
                                }
                            }
                            return; // Don't append the marker to content
                        }
                        
                        if (!hasStartedStreaming && streamingMessage.IsLoadingMessage)
                        {
                            streamingMessage.IsLoadingMessage = false;
                        }
                        hasStartedStreaming = true;
                        streamingMessage.AppendContent(textChunk);
                    });
                }
                else if (finalResult != null)
                {
                    // Handle final result
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        streamingMessage.IsStreaming = false;
                        streamingMessage.IsLoadingMessage = false;

                        var contentSuccess = finalResult.Content?.Success;
                        var isSuccessfulResponse = finalResult.IsSuccessful && contentSuccess != false;

                        if (isSuccessfulResponse)
                        {
                            // Update current conversation ID if it was a new conversation
                            if (finalResult.IsNewConversation && finalResult.ConversationId != Guid.Empty)
                            {
                                _currentConversationId = finalResult.ConversationId;

                                // Add new conversation to navigation items
                                var newConversationItem = new NavigationItem
                                {
                                    Id = _currentConversationId.ToString()!,
                                    ConversationId = _currentConversationId,
                                    Title = finalResult.ConversationTitle ?? "New Chat",
                                    Icon = "??",
                                    IsSelected = true,
                                    LastActivity = DateTime.Now
                                };

                                // Deselect current item
                                if (SelectedNavigationItem != null)
                                {
                                    SelectedNavigationItem.IsSelected = false;
                                }

                                // Add new item at the beginning and select it
                                NavigationItems.Insert(0, newConversationItem);
                                SelectedNavigationItem = newConversationItem;
                            }

                            // Add sources if available
                            var sources = finalResult.Content?.Sources ?? finalResult.Sources ?? new List<SourceItem>();
                            streamingMessage.Sources = sources;

                            // Add paths if available
                            if (finalResult.Paths != null && finalResult.Paths.Any())
                            {
                                streamingMessage.Paths = finalResult.Paths.Select(p => new PathScore
                                {
                                    Path = p.Path,
                                    Score = p.Score
                                }).ToList();
                            }
                        }
                        else
                        {
                            string errorText = finalResult.ErrorMessage
                                               ?? finalResult.Content?.Error
                                               ?? finalResult.Content?.Response?.Messages?
                                                   .FirstOrDefault()?.Content?
                                                   .FirstOrDefault()?.Text
                                               ?? "The assistant cannot respond right now.";

                            // Show model error modal with generic guidance
                            ModelErrorMessage = string.Empty;
                            ShowModelErrorModal = true;
                            ShowNoModelsModal = false;
                            // Do not surface the error inside the chat transcript
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Handle any unexpected errors
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                streamingMessage.IsStreaming = false;
                streamingMessage.IsLoadingMessage = false;

                // Show model error modal with generic guidance
                ModelErrorMessage = string.Empty;
                ShowModelErrorModal = true;
                ShowNoModelsModal = false;
                // Do not surface the error inside the chat transcript
            });
        }
        finally
        {
            // Remove typing indicator
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBotTyping = false;
                streamingMessage.IsLoadingMessage = false;
            });
        }
    }

    private async void OnCaptureScreenButtonClicked()
    {
        try
        {
            // Collapse the window
            IsCollapsed = true;

            // Wait for 500 milliseconds
            await Task.Delay(500);

            string applicationPath = Assembly.GetExecutingAssembly().Location;
            string applicationDirectory = Path.GetDirectoryName(applicationPath) ?? AppContext.BaseDirectory;
            string screenshotsDirectory = Path.Combine(applicationDirectory, "screenshots");

            // Create directory if it doesn't exist
            if (!Directory.Exists(screenshotsDirectory))
            {
                Directory.CreateDirectory(screenshotsDirectory);
            }

            // Generate timestamp for unique filenames
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            int monitorCount = await _captureScreenUseCase.GetMonitorCountAsync();
            Console.WriteLine($"Detected monitor count: {monitorCount}");

            // Take screenshot of all monitors if more than 1 monitor
            if (monitorCount > 1)
            {
                for (int i = 0; i < monitorCount && i < 5; i++) // Limit to first 5 monitors
                {
                    string fileName = $"screenshot_monitor_{i + 1}_{timestamp}.png";
                    string filePath = Path.Combine(screenshotsDirectory, fileName);
                    bool success = await _captureScreenUseCase.CaptureScreenshotAsync(filePath, i);
                    Console.WriteLine($"Screenshot of monitor {i} {(success ? "succeeded" : "failed")}");

                    if (success)
                    {
                        // Add the screenshot to the chat as an attached file
                        Datasets.Add(new Dataset { Name = fileName, FilePath = filePath });
                    }
                }
            }
            else
            {
                // Take screenshot of primary monitor
                string fileName = $"screenshot_{timestamp}.png";
                string filePath = Path.Combine(screenshotsDirectory, fileName);
                bool success = await _captureScreenUseCase.CaptureScreenshotAsync(filePath, 0);
                Console.WriteLine($"Screenshot of primary monitor {(success ? "succeeded" : "failed")}");

                if (success)
                {
                    // Add the screenshot to the chat as an attached file
                    Datasets.Add(new Dataset { Name = fileName, FilePath = filePath });
                }
                else
                {
                    // Show error message if screenshot failed
                    Messages.Add(new ChatMessage
                    {
                        Content = "Failed to capture screenshot. Please try again.",
                        Timestamp = DateTime.Now,
                        IsMine = false
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Handle any errors during screenshot capture
            Messages.Add(new ChatMessage
            {
                Content = $"Error capturing screenshot: {ex.Message}",
                Timestamp = DateTime.Now,
                IsMine = false
            });
            Console.WriteLine($"Screenshot error: {ex}");
        }
        finally
        {
            // Uncollapse the window
            IsCollapsed = false;
        }
    }

    /// <summary>
    /// Checks if a file extension is supported by the application
    /// </summary>
    private bool IsFileTypeSupported(string fileExtension)
    {
        var supportedExtensions = new[] { ".pdf", ".txt", ".docx", ".csv", ".md", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
        return supportedExtensions.Contains(fileExtension.ToLowerInvariant());
    }

    /// <summary>
    /// Public method to validate dropped files based on supported file types
    /// </summary>
    public bool TryAddDroppedFile(string filePath)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        if (!IsFileTypeSupported(fileExtension))
        {
            // Show toast error for unsupported file type
            try
            {
                Views.MainWindow.ToastServiceInstance.ShowError($"File type '{fileExtension}' is not supported. Supported types: .pdf, .txt, .docx, .csv, .md, .xlsx", 5000);
            }
            catch { }
            return false;
        }

        var fileName = Path.GetFileName(filePath);
        Datasets.Add(new Dataset { Name = fileName, FilePath = filePath });
        return true;
    }

    public bool CanSendMessage => !_isBotTyping && !ShowNoModelsModal && !ShowModelErrorModal && IsServerOnline;

    private void StartServerStatusChecking()
    {
        // Check immediately
        _ = CheckServerStatusAsync();

        // Set up timer to check every 5 seconds
        _serverStatusTimer = new System.Threading.Timer(
            async _ => await CheckServerStatusAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5)
        );
    }

    private async Task CheckServerStatusAsync()
    {
        try
        {
            var isOnline = await _externalApiService.CheckServerStatus();

            // Update UI on main thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsServerOnline = isOnline;
            });
        }
        catch
        {
            // If any exception, server is offline
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsServerOnline = false;
            });
        }
    }
}
