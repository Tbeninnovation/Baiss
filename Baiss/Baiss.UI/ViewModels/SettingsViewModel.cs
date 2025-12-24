using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.IO;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Baiss.UI.Services;
using Baiss.Application.UseCases;
using Baiss.Application.DTOs;
using Baiss.Domain.Entities;
using Baiss.Application.Interfaces;
using Sprache;

namespace Baiss.UI.ViewModels;

public class FolderItem : ViewModelBase
{
    private string _path = string.Empty;
    private bool _isLoading;
    private bool _isPendingRemove;

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsPendingRemove
    {
        get => _isPendingRemove;
        set => SetProperty(ref _isPendingRemove, value);
    }
}

public class AIModel : ViewModelBase
{
    private static readonly char[] PathSeparators = new[] { '/', '\\' };

    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Size { get; set; }
    public required string Description { get; set; }
    public string? ModelType { get; set; } // "Chat", "Embedding", or null for general models
    public string? Usage { get; set; }
    public string? DefaultDownloadUrl { get; set; }
    public ObservableCollection<GgufVariant> Variants { get; } = new();
    public string Author { get; set; } = string.Empty;
    public int Downloads { get; set; }
    public int Likes { get; set; }
    public string DownloadsFormatted => Downloads > 0 ? Downloads.ToString("N0", CultureInfo.InvariantCulture) : "0";

    private bool _isDescriptionExpanded;
    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        set => SetProperty(ref _isDescriptionExpanded, value);
    }

    public bool IsDescriptionLong => !string.IsNullOrEmpty(Description) && (Description.Length > 150 || Description.Count(c => c == '\n') > 1);

    private ICommand? _toggleDescriptionExpandedCommand;
    public ICommand ToggleDescriptionExpandedCommand => _toggleDescriptionExpandedCommand ??= new RelayCommand(() => IsDescriptionExpanded = !IsDescriptionExpanded);

    private bool _isDownloading = false;
    private bool _isDownloaded = false;
    private double _downloadProgress = 0.0;
    private bool _isFirst = false;
    private bool _isAwaitingDownloadConfirmation;
    private bool _isPendingDelete;
    private string? _activeProcessId;

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnVisualStateChanged();
            }
        }
    }

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (SetProperty(ref _isDownloaded, value))
            {
                OnVisualStateChanged();
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public bool IsFirst
    {
        get => _isFirst;
        set => SetProperty(ref _isFirst, value);
    }

    public bool IsAwaitingDownloadConfirmation
    {
        get => _isAwaitingDownloadConfirmation;
        set
        {
            if (SetProperty(ref _isAwaitingDownloadConfirmation, value))
            {
                OnVisualStateChanged();
            }
        }
    }

    public bool IsPendingDelete
    {
        get => _isPendingDelete;
        set
        {
            if (SetProperty(ref _isPendingDelete, value))
            {
                OnVisualStateChanged();
            }
        }
    }

    public bool ShowDownloadButton => !IsDownloaded && !IsDownloading && !IsAwaitingDownloadConfirmation;
    public bool ShowDownloadConfirmation => IsAwaitingDownloadConfirmation && !IsDownloading;
    public bool ShowDownloadSpinner => IsDownloading;
    public bool ShowCancelButton => IsAwaitingDownloadConfirmation || IsDownloading;
    public bool ShowDeleteButton => IsDownloaded && !IsAwaitingDownloadConfirmation && !IsPendingDelete && !IsDownloading;
    public bool ShowDeleteConfirmation => IsPendingDelete && !IsDownloading;

    public string? ActiveProcessId
    {
        get => _activeProcessId;
        set => SetProperty(ref _activeProcessId, value);
    }

    public string DisplayText => ShortDisplayName;
    public string ShortDisplayName => BuildShortDisplayName(Name);

    public override string ToString() => DisplayText;

    private void OnVisualStateChanged()
    {
        OnPropertyChanged(nameof(ShowDownloadButton));
        OnPropertyChanged(nameof(ShowDownloadConfirmation));
        OnPropertyChanged(nameof(ShowDownloadSpinner));
        OnPropertyChanged(nameof(ShowCancelButton));
        OnPropertyChanged(nameof(ShowDeleteButton));
        OnPropertyChanged(nameof(ShowDeleteConfirmation));
    }

    private static string BuildShortDisplayName(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var candidate = source.Trim();

        var separatorIndex = candidate.LastIndexOfAny(PathSeparators);
        if (separatorIndex >= 0 && separatorIndex < candidate.Length - 1)
        {
            candidate = candidate[(separatorIndex + 1)..];
        }

        var colonIndex = candidate.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex < candidate.Length - 1)
        {
            candidate = candidate[(colonIndex + 1)..];
        }

        if (candidate.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
            candidate.EndsWith("-gguf", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^5];
        }

        return candidate;
    }
}

public class MockLocalModel : ViewModelBase
{
    public required string Name { get; set; }
    public required string Size { get; set; }
    public required string Description { get; set; }

    private bool _isDownloaded;

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (SetProperty(ref _isDownloaded, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string Title => $"{Name} ΓÇó {Size}";
    public string StatusText => IsDownloaded ? "Downloaded and ready to use" : "Click download to install";
}

public class SettingsNavigationItem : ViewModelBase
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Icon { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public class GgufVariant : ViewModelBase
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public string ShortDisplayName => DisplayName.Replace(".gguf", "", StringComparison.OrdinalIgnoreCase);
    public required string SizeText { get; set; }
    public required string DownloadUrl { get; set; }
    public bool IsDefault { get; set; }
    public AIModel? ParentModel { get; set; }

    public bool ShowDownload => !IsDownloading && !IsDownloaded;

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(ShowDownload));
            }
        }
    }

    private bool _isDownloaded;
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (SetProperty(ref _isDownloaded, value))
            {
                OnPropertyChanged(nameof(ShowDownload));
            }
        }
    }

    private double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }
}

public class ScheduleOption
{
    public string DisplayName { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
}

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private const long ModelDownloadSizeBytes = 3L * 1024 * 1024 * 1024; // 3 GB per model download
    private string _welcomeMessage = "Configure your AI assistant settings and preferences.";
    private SettingsNavigationItem? _selectedSettingsItem;
    private string _currentSettingsTitle = "General";
    private string _currentSettingsContent = "Select a settings category from the sidebar.";

    // Tab visibility properties
    private bool _isGeneralSelected = true;
    private bool _isAIAccessSelected = false;
    private bool _isAIModelsSelected = false;
    private bool _isSupportSelected = false;

    // Schedule Settings
    private string _treeStructureSchedule = "0 0 0 * * ?";
    private bool _treeStructureScheduleEnabled = false;
    private bool _hasScheduleChanges = false;
    private bool _showScheduleSuccess = false;
    private ScheduleOption? _selectedScheduleOption;

    public ObservableCollection<ScheduleOption> ScheduleOptions { get; } = new ObservableCollection<ScheduleOption>();

    public ScheduleOption? SelectedScheduleOption
    {
        get => _selectedScheduleOption;
        set
        {
            // if (!CanEnableSchedule)
            // {
            //     OnPropertyChanged(nameof(SelectedScheduleOption));
            //     return;
            // }

            if (SetProperty(ref _selectedScheduleOption, value) && value != null)
            {
                TreeStructureSchedule = value.CronExpression;
            }
        }
    }

    public string TreeStructureSchedule
    {
        get => _treeStructureSchedule;
        set
        {
            if (SetProperty(ref _treeStructureSchedule, value))
            {
                HasScheduleChanges = true;
            }
        }
    }

    public bool TreeStructureScheduleEnabled
    {
        get => _treeStructureScheduleEnabled;
        set
        {
            // if (!CanEnableSchedule && value)
            // {
            //     OnPropertyChanged(nameof(TreeStructureScheduleEnabled));
            //     return;
            // }

            if (SetProperty(ref _treeStructureScheduleEnabled, value))
            {
                HasScheduleChanges = true;
            }
        }
    }

    public bool HasScheduleChanges
    {
        get => _hasScheduleChanges;
        set => SetProperty(ref _hasScheduleChanges, value);
    }

    public bool ShowScheduleSuccess
    {
        get => _showScheduleSuccess;
        set => SetProperty(ref _showScheduleSuccess, value);
    }

    public ICommand ApplyScheduleCommand { get; }
    public ICommand CancelScheduleCommand { get; }

    private string _checkNowButtonText = "Check now";
    private string _currentVersionText = "Version: --";
    private string _chooseFoldersButtonText = "Choose folders...";
    private bool _isCheckingUpdate = false;

    private bool _hasReadAccessChanges;
    private bool _hasWritePermissionChanges;
    private bool _hasFileTypesChanges;
    private bool _showReadAccessSuccess;
    private bool _showWritePermissionSuccess;
    private bool _showFileTypesSuccess;
    private ToastMessage? _fileTypesWarningToast;

    // Temporary folder removal notification
    private ToastMessage? _folderRemovalToast;

    private bool _isTreeStructureRunning;
    private bool _isTreeStructurePaused;
    private bool _isCheckingTreeStructure;
    private System.Threading.Timer? _statusTimer;

    private bool _isLoadingSettings;

    private bool _originalNoReadAccess;
    private bool _originalAllowReadAccess = true;
    private bool _originalAllowUpdateFiles = true;
    private bool _originalAllowCreateFiles = true;
    private string _originalWritePermissionPath = @"C:\User\Documents";
    private bool _originalAllowMdFiles = true;
    private bool _originalAllowDocxFiles = true;
    private bool _originalAllowXlsFiles = true;
    private bool _originalAllowPdfFiles = true;
    private bool _originalAllowTxtFiles = true;
    private bool _originalAllowCsvFiles = true;

    // General settings properties
    private bool _performanceHigh = true;
    private bool _performanceMedium = false;
    private bool _performanceLow = false;
    private bool _autoUpdatesEnabled = true;

    // AI Model properties (old - for compatibility)
    private AIModel? _selectedAIModel;
    private int _selectedModelIndex = -1;

    public ObservableCollection<AIModel> AvailableModels { get; } = new ObservableCollection<AIModel>();

    public ICommand SearchExternalModelCommand { get; }

    private async Task SearchExternalModelAsync(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(ExternalModelSearchText)) return;

        try
        {
            IsLoadingModels = true;
            var result = await _settingsUseCase.SearchAndSaveExternalModelAsync(ExternalModelSearchText);
            if (result.Success)
            {
                Views.MainWindow.ToastServiceInstance.ShowSuccess(result.Message ?? "Model saved successfully", 5000);
                // Refresh available models
                await LoadLocalModelsAsync();
                ExternalModelSearchText = string.Empty; // Clear search
            }
            else
            {
                Views.MainWindow.ToastServiceInstance.ShowError(result.Error ?? "Failed to save model", 5000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching external model");
            Views.MainWindow.ToastServiceInstance.ShowError("An unexpected error occurred", 5000);
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    public AIModel? SelectedAIModel
    {
        get => _selectedAIModel;
        set => SetProperty(ref _selectedAIModel, value);
    }

    public int SelectedModelIndex
    {
        get => _selectedModelIndex;
        set
        {
            if (SetProperty(ref _selectedModelIndex, value))
            {
                // Update the selected model based on index
                if (value >= 0 && value < AvailableModels.Count)
                {
                    SelectedAIModel = AvailableModels[value];
                }
            }
        }
    }

    // New AI Model Configuration properties
    private string _aiModelType = ModelTypes.Local;
    private string _selectedProvider = ModelProviders.Python;
    // Legacy generic model id removed. Track only purpose-specific selections.
    private string _selectedAIModelId = string.Empty; // retained temporarily for non-databricks providers if still needed
    // Provider credentials support
    private readonly IProviderCredentialService? _providerCredentialService;
    private readonly HttpClient _httpClient;
    private bool _hasStoredSecret;

    // Network connectivity monitoring
    private bool _isNetworkAvailable = true;
    private bool _hasShownNetworkLossToast = false;
    private ToastMessage? _networkLossToast = null;
    private System.Timers.Timer? _networkStatusDebounceTimer;
    private bool _pendingNetworkStatus;
    private DateTime _lastNetworkLossToastTime = DateTime.MinValue;
    private DateTime _lastNetworkRestoredToastTime = DateTime.MinValue;
    private static readonly TimeSpan MinToastInterval = TimeSpan.FromSeconds(2); // Minimum 2 seconds between same type of toasts

    public bool IsNetworkAvailable
    {
        get => _isNetworkAvailable;
        private set
        {
            if (SetProperty(ref _isNetworkAvailable, value))
            {
                // Debounce network status changes to prevent duplicate toasts
                DebounceNetworkStatusChange(value);
            }
        }
    }

    public bool HasStoredSecret
    {
        get => _hasStoredSecret;
        private set
        {
            if (SetProperty(ref _hasStoredSecret, value))
            {
                OnPropertyChanged(nameof(ShowSecretInput));
                OnPropertyChanged(nameof(ShowReplaceSecretButton));
            }
        }
    }
    private bool _isEditingSecret;
    public bool IsEditingSecret
    {
        get => _isEditingSecret;
        set
        {
            if (SetProperty(ref _isEditingSecret, value))
            {
                OnPropertyChanged(nameof(ShowSecretInput));
                OnPropertyChanged(nameof(ShowReplaceSecretButton));
            }
        }
    }
    public bool ShowSecretInput => IsEditingSecret || !HasStoredSecret;
    public bool ShowReplaceSecretButton => HasStoredSecret && !IsEditingSecret;
    private string _providerSecretInput = string.Empty; // write-only; never show existing secret
    public string ProviderSecretInput { get => _providerSecretInput; set { if (SetProperty(ref _providerSecretInput, value)) MarkCredentialsDirty(); } }
    private string? _providerOrganizationId;
    public string? ProviderOrganizationId { get => _providerOrganizationId; set { if (SetProperty(ref _providerOrganizationId, value)) MarkCredentialsDirty(); } }
    private string? _providerModelOverride;
    public string? ProviderModelOverride { get => _providerModelOverride; set { if (SetProperty(ref _providerModelOverride, value)) MarkCredentialsDirty(); } }
    // Azure
    private string? _azureEndpoint; public string? AzureEndpoint { get => _azureEndpoint; set { if (SetProperty(ref _azureEndpoint, value)) MarkCredentialsDirty(); } }
    private string? _azureDeploymentName; public string? AzureDeploymentName { get => _azureDeploymentName; set { if (SetProperty(ref _azureDeploymentName, value)) MarkCredentialsDirty(); } }
    private string? _azureApiVersion; public string? AzureApiVersion { get => _azureApiVersion; set { if (SetProperty(ref _azureApiVersion, value)) MarkCredentialsDirty(); } }
    // // Databricks
    // private string? _databricksWorkspaceUrl; public string? DatabricksWorkspaceUrl { get => _databricksWorkspaceUrl; set { if (SetProperty(ref _databricksWorkspaceUrl, value)) MarkCredentialsDirty(); } }
    // private string? _databricksServingEndpoint; public string? DatabricksServingEndpoint { get => _databricksServingEndpoint; set { if (SetProperty(ref _databricksServingEndpoint, value)) MarkCredentialsDirty(); } }
    // private string? _databricksModelName; public string? DatabricksModelName { get => _databricksModelName; set { if (SetProperty(ref _databricksModelName, value)) MarkCredentialsDirty(); } }
    public bool IsOpenAISelected => SelectedProvider == ModelProviders.OpenAI;
    public bool IsAnthropicSelected => SelectedProvider == ModelProviders.Anthropic;
    public bool IsAzureSelected => SelectedProvider == ModelProviders.Azure;
    // public bool IsDatabricksSelected => SelectedProvider == ModelProviders.Databricks;
    public bool HasSelectedProvider => !string.IsNullOrWhiteSpace(SelectedProvider);
    private string? _cachedProviderSecret;
    private bool _hasPendingCredentialChanges;
    private bool _isSecretVisible;
    private bool _isValidatingCredentials;
    private string? _credentialValidationError;
    private string? _credentialValidationSuccess;
    private string _newModelId = string.Empty;
    private string _newModelName = string.Empty;
    private string? _newModelDescription;
    private string _newModelPurpose = string.Empty;
    private bool _isManagingModels;
    // private string? _pendingDeleteDatabricksModelId;
    private string? _pendingDeleteLocalModelId;
    private FolderItem? _pendingFolderRemoval;
    private readonly AsyncRelayCommand _addProviderModelCommand;
    private AIModelDto? _selectedDbAIModel;
    private bool _isLoadingModels = false;
    private bool _suppressAutoLoad = false; // Prevents cascading provider/model reload during initialization
    private bool _isSaving = false;
    private bool _isSavingCredentials = false;
    private System.Threading.CancellationTokenSource? _autoSaveCts;
    private readonly TimeSpan _autoSaveDelay = TimeSpan.FromMilliseconds(500);

    public ObservableCollection<AIModelDto> DatabaseAIModels { get; } = new ObservableCollection<AIModelDto>();
    public ObservableCollection<string> AvailableProviders { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> AvailableModelPurposes { get; } = new ObservableCollection<string>();

    private string _modelPlaceholderText = "Choose a model to start with...";
    public string ModelPlaceholderText
    {
        get => _modelPlaceholderText;
        set => SetProperty(ref _modelPlaceholderText, value);
    }

    public string AIModelType
    {
        get => _aiModelType;
        set
        {
            if (SetProperty(ref _aiModelType, value))
            {
                if (!_suppressAutoLoad)
                {
                    _logger.LogDebug("AI Model Type changed to: {Type}", value);
                }
                OnPropertyChanged(nameof(IsLocalSelected));
                OnPropertyChanged(nameof(IsHostedSelected));
                UpdateAvailableProviders(value);
            }
        }
    }

    private string _aiModelProviderScope = "local"; // local | hosted
    // private string _aiModelProviderScope = "local"; // local | hosted | databricks
    public string AIModelProviderScope
    {
        get => _aiModelProviderScope;
        set
        {
            if (SetProperty(ref _aiModelProviderScope, value))
            {
                _logger.LogDebug("AI Model Provider Scope changed to: {Scope}", value);
                // Keep AIModelType coherent (legacy logic expects only local/hosted)
                if (value == "local" && AIModelType != ModelTypes.Local)
                {
                    AIModelType = ModelTypes.Local;
                }
                else if (value != "local" && AIModelType != ModelTypes.Hosted)
                {
                    AIModelType = ModelTypes.Hosted;
                }
                // Persist scope only (fire and forget) without requiring model selection
                _ = _settingsUseCase.UpdateAIModelProviderScopeAsync(value);
                // Reload models for new scope (fire and forget)
                _ = LoadModelsForCurrentScopeAsync();
            }
        }
    }

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                _addProviderModelCommand?.RaiseCanExecuteChanged();
                if (_suppressAutoLoad) return; // skip during controlled init
                if (string.IsNullOrWhiteSpace(value)) return;
                _logger.LogDebug("Provider changed to: {Provider}, reloading models for type: {Type}", value, _aiModelType);
                // Attempt to preserve current SelectedAIModelId if it belongs to new provider (will be verified in load method)
                var preserveId = SelectedAIModelId;
                // Use unified scope-based loader; it will respect SelectedProvider
                _ = LoadModelsForCurrentScopeAsync(preserveId);
                ScheduleAutoSave();
                _ = LoadProviderCredentialsAsync(value);
                OnPropertyChanged(nameof(IsOpenAISelected));
                OnPropertyChanged(nameof(IsAnthropicSelected));
                OnPropertyChanged(nameof(IsAzureSelected));
                // OnPropertyChanged(nameof(IsDatabricksSelected));
                OnPropertyChanged(nameof(HasSelectedProvider));
            }
        }
    }

    public string SelectedAIModelId
    {
        get => _selectedAIModelId;
        set => SetProperty(ref _selectedAIModelId, value);
    }

    public AIModelDto? SelectedDbAIModel
    {
        get => _selectedDbAIModel;
        set
        {
            if (SetProperty(ref _selectedDbAIModel, value))
            {
                SelectedAIModelId = value?.Id ?? string.Empty;
                OnPropertyChanged(nameof(SelectedModelName));

                // Check if there are changes instead of auto-saving
                CheckForAIModelChanges();
            }
        }
    }

    public string SelectedModelName
    {
        get => SelectedDbAIModel?.Name ?? string.Empty;
    }

    public bool IsLoadingModels
    {
        get => _isLoadingModels;
        set => SetProperty(ref _isLoadingModels, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    public bool IsSavingCredentials
    {
        get => _isSavingCredentials;
        set
        {
            if (SetProperty(ref _isSavingCredentials, value))
            {
                OnPropertyChanged(nameof(CanSaveCredentials));
            }
        }
    }
    public bool IsValidatingCredentials
    {
        get => _isValidatingCredentials;
        private set
        {
            if (SetProperty(ref _isValidatingCredentials, value))
            {
                OnPropertyChanged(nameof(CanSaveCredentials));
            }
        }
    }

    public bool HasPendingCredentialChanges
    {
        get => _hasPendingCredentialChanges;
        private set
        {
            if (SetProperty(ref _hasPendingCredentialChanges, value))
            {
                OnPropertyChanged(nameof(CanSaveCredentials));
            }
        }
    }

    public bool CanSaveCredentials => !_isSavingCredentials && !IsValidatingCredentials && HasPendingCredentialChanges;

    public bool IsSecretVisible
    {
        get => _isSecretVisible;
        set
        {
            if (SetProperty(ref _isSecretVisible, value))
            {
                OnPropertyChanged(nameof(SecretPasswordChar));
            }
        }
    }

    public char SecretPasswordChar => IsSecretVisible ? '\0' : '*';

    // private bool _isDatabricksApiKeyVisible;
    // public bool IsDatabricksApiKeyVisible
    // {
    //     get => _isDatabricksApiKeyVisible;
    //     set
    //     {
    //         if (SetProperty(ref _isDatabricksApiKeyVisible, value))
    //         {
    //             OnPropertyChanged(nameof(DatabricksApiKeyPasswordChar));
    //         }
    //     }
    // }

    // public char DatabricksApiKeyPasswordChar => IsDatabricksApiKeyVisible ? '\0' : '*';

    public string? CredentialValidationError
    {
        get => _credentialValidationError;
        private set => SetProperty(ref _credentialValidationError, value);
    }

    public string? CredentialValidationSuccess
    {
        get => _credentialValidationSuccess;
        private set => SetProperty(ref _credentialValidationSuccess, value);
    }

    public string NewModelId
    {
        get => _newModelId;
        set
        {
            if (SetProperty(ref _newModelId, value))
            {
                OnPropertyChanged(nameof(CanAddModel));
                _addProviderModelCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewModelName
    {
        get => _newModelName;
        set
        {
            if (SetProperty(ref _newModelName, value))
            {
                OnPropertyChanged(nameof(CanAddModel));
                _addProviderModelCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public string? NewModelDescription
    {
        get => _newModelDescription;
        set
        {
            if (SetProperty(ref _newModelDescription, value))
            {
                // no derived properties
            }
        }
    }

    public string NewModelPurpose
    {
        get => _newModelPurpose;
        set
        {
            if (SetProperty(ref _newModelPurpose, value))
            {
                OnPropertyChanged(nameof(CanAddModel));
            }
        }
    }

    public bool IsManagingModels
    {
        get => _isManagingModels;
        private set
        {
            if (SetProperty(ref _isManagingModels, value))
            {
                OnPropertyChanged(nameof(CanAddModel));
                _addProviderModelCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanAddModel => !IsManagingModels && !string.IsNullOrWhiteSpace(NewModelId) && !string.IsNullOrWhiteSpace(NewModelName) && !string.IsNullOrWhiteSpace(SelectedProvider) && !string.IsNullOrWhiteSpace(NewModelPurpose);

    // Debug properties to help troubleshoot


    public bool IsLocalSelected
    {
        get => AIModelType == ModelTypes.Local;
        set
        {
            if (value)
            {
                AIModelType = ModelTypes.Local;
                OnPropertyChanged(nameof(IsHostedSelected));
                UpdateAvailableProviders(ModelTypes.Local);
            }
        }
    }

    public bool IsHostedSelected
    {
        get => AIModelType == ModelTypes.Hosted;
        set
        {
            if (value)
            {
                AIModelType = ModelTypes.Hosted;
                OnPropertyChanged(nameof(IsLocalSelected));
                UpdateAvailableProviders(ModelTypes.Hosted);
            }
        }
    }

    // Model Provider Selection Properties
    private bool _isLocalModelsSelected = true;
    private bool _isHostedModelsSelected = false;
    // private bool _isDatabricksModelsSelected = false;

    public bool IsLocalModelsSelected
    {
        get => _isLocalModelsSelected;
        set
        {
            if (SetProperty(ref _isLocalModelsSelected, value))
            {
                if (value)
                {
                    IsHostedModelsSelected = false;
                    // IsDatabricksModelsSelected = false;
                    IsLocalModelsExpanded = true;

                    // Automatically open the dropdown when selected
                    IsLocalModelFrameExpanded = true;

                    if (AIModelProviderScope != "local")
                    {
                        AIModelProviderScope = "local"; // triggers persistence via setter
                    }
                    else
                    {
                        // Scope already local; ensure models loaded
                        _ = LoadModelsForCurrentScopeAsync();
                    }
                    _ = RefreshLocalDownloadStatusesAsync();
                }
                else
                {
                    // Close the frame when deselected
                    IsLocalModelFrameExpanded = false;
                }
            }
        }
    }

    public bool IsHostedModelsSelected
    {
        get => _isHostedModelsSelected;
        set
        {
            if (SetProperty(ref _isHostedModelsSelected, value))
            {
                if (value)
                {
                    IsLocalModelsSelected = false;
                    // IsDatabricksModelsSelected = false;
                    IsHostedModelsExpanded = true;
                    if (AIModelProviderScope != "hosted")
                    {
                        AIModelProviderScope = "hosted"; // triggers persistence via setter
                    }
                    else
                    {
                        _ = LoadModelsForCurrentScopeAsync();
                    }
                }
                else
                {
                    // Close the frame when deselected
                    IsHostedModelFrameExpanded = false;
                }
            }
        }
    }

    // public bool IsDatabricksModelsSelected
    // {
    //     get => _isDatabricksModelsSelected;
    //     set
    //     {
    //         if (SetProperty(ref _isDatabricksModelsSelected, value))
    //         {
    //             if (value)
    //             {
    //                 IsLocalModelsSelected = false;
    //                 IsHostedModelsSelected = false;
    //                 IsDatabricksModelsExpanded = true;

    //                 // Automatically open the dropdown when selected
    //                 IsDatabricksModelFrameExpanded = true;

    //                 // Databricks is a hosted provider, so set model type to hosted
    //                 if (AIModelType != ModelTypes.Hosted)
    //                 {
    //                     AIModelType = ModelTypes.Hosted;
    //                 }

    //                 // Set provider to Databricks and load credentials
    //                 if (SelectedProvider != ModelProviders.Databricks)
    //                 {
    //                     SelectedProvider = ModelProviders.Databricks;
    //                 }
    //                 _ = LoadProviderCredentialsAsync(ModelProviders.Databricks);

    //                 if (AIModelProviderScope != "databricks")
    //                 {
    //                     AIModelProviderScope = "databricks"; // triggers persistence via setter
    //                 }
    //                 else
    //                 {
    //                     _ = LoadModelsForCurrentScopeAsync();
    //                 }
    //             }
    //             else
    //             {
    //                 // Close the frame when deselected
    //                 IsDatabricksModelFrameExpanded = false;
    //             }
    //         }
    //     }
    // }

    /// <summary>
    /// Unified loader that populates model collections based on current provider scope and selected provider.
    /// </summary>
    private async Task LoadModelsForCurrentScopeAsync(string? preserveModelId = null)
    {
        if (_suppressAutoLoad) return;
        try
        {
            IsLoadingModels = true;
            IEnumerable<AIModelDto> sourceModels = Enumerable.Empty<AIModelDto>();
            switch (AIModelProviderScope)
            {
                case "local":
                    sourceModels = await _settingsUseCase.GetAIModelsByTypeAsync(ModelTypes.Local);
                    break;
                case "hosted":
                    // Hosted non-databricks; filter by current selected provider if not empty and not databricks
                    var hostedModels = await _settingsUseCase.GetAIModelsByTypeAsync(ModelTypes.Hosted);
                    if (!string.IsNullOrWhiteSpace(SelectedProvider) && !string.Equals(SelectedProvider, ModelProviders.Databricks, StringComparison.OrdinalIgnoreCase))
                    {
                        hostedModels = hostedModels.Where(m => string.Equals(m.Provider, SelectedProvider, StringComparison.OrdinalIgnoreCase));
                    }
                    sourceModels = hostedModels;
                    break;
                    // case "databricks":
                    //     var all = await _settingsUseCase.GetAvailableAIModelsAsync();
                    //     // Case-insensitive filter; also guard for null Purpose
                    //     sourceModels = all.Where(m => string.Equals(m.Provider, ModelProviders.Databricks, StringComparison.OrdinalIgnoreCase));
                    //     break;
            }

            DatabaseAIModels.Clear();
            foreach (var m in sourceModels)
            {
                DatabaseAIModels.Add(m);
            }
            OnPropertyChanged(nameof(DatabaseAIModels));
            // OnPropertyChanged(nameof(HasDatabricksModels));

            // if (AIModelProviderScope == "databricks")
            // {
            //     PopulateDatabricksModelCollections();
            //     if (DatabricksChatModels.Count == 0 && DatabricksEmbeddingModels.Count == 0)
            //     {
            //         _logger.LogWarning("Databricks scope selected but no models found. Raw DB count: {DbCount}", DatabaseAIModels.Count);
            //         foreach (var m in DatabaseAIModels)
            //         {
            //             _logger.LogWarning("Model debug -> Id={Id} Provider={Provider} Purpose='{Purpose}' Type={Type}", m.Id, m.Provider, m.Purpose ?? "<null>", m.Type);
            //         }
            //     }
            // }

            // Attempt to restore selection (legacy single selection path)
            if (!string.IsNullOrEmpty(preserveModelId))
            {
                var match = DatabaseAIModels.FirstOrDefault(m => m.Id == preserveModelId);
                if (match != null)
                {
                    SelectedDbAIModel = match;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scope-based model load failed for scope {Scope}", AIModelProviderScope);
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    // Model Expansion Properties
    private bool _isLocalModelsExpanded = true;
    private bool _isHostedModelsExpanded = false;
    // private bool _isDatabricksModelsExpanded = false;

    public bool IsLocalModelsExpanded
    {
        get => _isLocalModelsExpanded;
        set => SetProperty(ref _isLocalModelsExpanded, value);
    }

    public bool IsHostedModelsExpanded
    {
        get => _isHostedModelsExpanded;
        set => SetProperty(ref _isHostedModelsExpanded, value);
    }

    // public bool IsDatabricksModelsExpanded
    // {
    //     get => _isDatabricksModelsExpanded;
    //     set => SetProperty(ref _isDatabricksModelsExpanded, value);
    // }

    // Model Frame Expansion Properties
    private bool _isLocalModelFrameExpanded = false;
    private bool _isHostedModelFrameExpanded = false;
    // private bool _isDatabricksModelFrameExpanded = false;

    public bool IsLocalModelFrameExpanded
    {
        get => _isLocalModelFrameExpanded;
        set => SetProperty(ref _isLocalModelFrameExpanded, value);
    }

    public bool IsHostedModelFrameExpanded
    {
        get => _isHostedModelFrameExpanded;
        set => SetProperty(ref _isHostedModelFrameExpanded, value);
    }

    // public bool IsDatabricksModelFrameExpanded
    // {
    //     get => _isDatabricksModelFrameExpanded;
    //     set => SetProperty(ref _isDatabricksModelFrameExpanded, value);
    // }

    // Selected Models for Different Providers
    private AIModel? _selectedLocalChatModel;
    private AIModel? _selectedLocalEmbeddingModel;
    private AIModel? _selectedHostedChatModel;
    private AIModel? _selectedHostedEmbeddingModel;
    // private AIModel? _selectedDatabricksChatModel;
    // private AIModel? _selectedDatabricksEmbeddingModel;
    // private string? _selectedDatabricksEmbeddingModelId; // new: track embedding ID

    public AIModel? SelectedLocalChatModel
    {
        // ! herrrr
        get => _selectedLocalChatModel;
        // set => SetProperty(ref _selectedLocalChatModel, value);
        set
        {
            if (SetProperty(ref _selectedLocalChatModel, value))
            {
                // Auto-save settings when chat model selection changes
                if (!_suppressAutoLoad)
                {
                    _selectedAIModelId = value?.Id; // sync generic id for legacy compatibility
                    OnPropertyChanged(nameof(SelectedAIModelId));
                    _ = SaveAIModelSettingsAndValidateAsync();
                }
            }
        }

    }

    public AIModel? SelectedLocalEmbeddingModel
    {
        get => _selectedLocalEmbeddingModel;
        set
        {
            var oldModel = _selectedLocalEmbeddingModel;
            if (SetProperty(ref _selectedLocalEmbeddingModel, value))
            {
                if (oldModel != null) oldModel.PropertyChanged -= OnSelectedEmbeddingModelPropertyChanged;
                if (value != null) value.PropertyChanged += OnSelectedEmbeddingModelPropertyChanged;

                OnPropertyChanged(nameof(CanEnableSchedule));
                OnPropertyChanged(nameof(ScheduleDisabledReason));

                if (!CanEnableSchedule && TreeStructureScheduleEnabled)
                {
                    TreeStructureScheduleEnabled = false;
                }

                // Auto-save when selection changes (if not suppressed)
                if (!_suppressAutoLoad)
                {
                    _ = SaveEmbeddingModelSettingsAsync(showSuccessToast: false);
                }
            }
        }
    }

    private void OnSelectedEmbeddingModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AIModel.IsDownloaded))
        {
            OnPropertyChanged(nameof(CanEnableSchedule));
            OnPropertyChanged(nameof(ScheduleDisabledReason));

            if (!CanEnableSchedule && TreeStructureScheduleEnabled)
            {
                TreeStructureScheduleEnabled = false;
            }
        }
    }

    public bool CanEnableSchedule => SelectedLocalEmbeddingModel != null && SelectedLocalEmbeddingModel.IsDownloaded;

    public string ScheduleDisabledReason => CanEnableSchedule
        ? "Enable scheduled updates"
        : "You must select and download an embedding model first.";

    public AIModel? SelectedHostedChatModel
    {
        get => _selectedHostedChatModel;
        set
        {
            if (SetProperty(ref _selectedHostedChatModel, value))
            {
                // Auto-save settings when chat model selection changes
                if (!_suppressAutoLoad)
                {
                    _ = SaveAIModelSettingsAndValidateAsync();
                }
            }
        }
    }

    public AIModel? SelectedHostedEmbeddingModel
    {
        get => _selectedHostedEmbeddingModel;
        set => SetProperty(ref _selectedHostedEmbeddingModel, value);
    }

    // public AIModel? SelectedDatabricksChatModel
    // {
    //     get => _selectedDatabricksChatModel;
    //     set
    //     {
    //         if (SetProperty(ref _selectedDatabricksChatModel, value))
    //         {
    //             // Auto-save settings when chat model selection changes
    //             if (!_suppressAutoLoad)
    //             {
    //                 // Keep the ID-based selection property in sync so XAML SelectedValue can resolve later
    //                 _selectedDatabricksChatModelId = value?.Id;
    //                 OnPropertyChanged(nameof(SelectedDatabricksChatModelId));
    //                 _ = SaveAIModelSettingsAsync(showSuccessToast: false);
    //             }
    //         }
    //     }
    // }

    // private string? _selectedDatabricksChatModelId;
    // /// <summary>
    // /// ID-based selection helper so the ComboBox can pre-select using SelectedValue/SelectedValuePath
    // /// even if the actual AIModel instance hasn't been materialized yet when the view binds.
    // /// </summary>
    // public string? SelectedDatabricksChatModelId
    // {
    //     get => _selectedDatabricksChatModel?.Id ?? _selectedDatabricksChatModelId;
    //     set
    //     {
    //         if (_selectedDatabricksChatModelId != value)
    //         {
    //             _selectedDatabricksChatModelId = value;
    //             // Attempt to resolve the AIModel instance if the collection is already populated
    //             if (!string.IsNullOrWhiteSpace(value))
    //             {
    //                 var match = DatabricksChatModels.FirstOrDefault(m => m.Id == value);
    //                 if (match != null && !ReferenceEquals(match, _selectedDatabricksChatModel))
    //                 {
    //                     _selectedDatabricksChatModel = match;
    //                     OnPropertyChanged(nameof(SelectedDatabricksChatModel));
    //                 }
    //             }
    //             else if (_selectedDatabricksChatModel != null)
    //             {
    //                 _selectedDatabricksChatModel = null;
    //                 OnPropertyChanged(nameof(SelectedDatabricksChatModel));
    //             }
    //             OnPropertyChanged();
    //         }
    //     }
    // }


    // public AIModel? SelectedDatabricksEmbeddingModel
    // {
    //     get => _selectedDatabricksEmbeddingModel;
    //     set
    //     {
    //         if (SetProperty(ref _selectedDatabricksEmbeddingModel, value))
    //         {
    //             // Auto-save settings when embedding model selection changes
    //             if (!_suppressAutoLoad)
    //             {
    //                 _selectedDatabricksEmbeddingModelId = value?.Id; // sync id
    //                 OnPropertyChanged(nameof(SelectedDatabricksEmbeddingModelId));
    //                 _ = SaveAIModelSettingsAsync(showSuccessToast: false);
    //             }
    //         }
    //     }
    // }

    // public string? SelectedDatabricksEmbeddingModelId
    // {
    //     get => _selectedDatabricksEmbeddingModel?.Id ?? _selectedDatabricksEmbeddingModelId;
    //     set
    //     {
    //         if (_selectedDatabricksEmbeddingModelId != value)
    //         {
    //         _selectedDatabricksEmbeddingModelId = value;
    //             if (!string.IsNullOrWhiteSpace(value))
    //             {
    //                 var match = DatabricksEmbeddingModels.FirstOrDefault(m => m.Id == value);
    //                 if (match != null && !ReferenceEquals(match, _selectedDatabricksEmbeddingModel))
    //                 {
    //                     _selectedDatabricksEmbeddingModel = match;
    //                     OnPropertyChanged(nameof(SelectedDatabricksEmbeddingModel));
    //                 }
    //             }
    //             else if (_selectedDatabricksEmbeddingModel != null)
    //             {
    //                 _selectedDatabricksEmbeddingModel = null;
    //                 OnPropertyChanged(nameof(SelectedDatabricksEmbeddingModel));
    //             }
    //             OnPropertyChanged();
    //         }
    //     }
    // }

    // Downloaded Models Collections
    public ObservableCollection<AIModel> DownloadedLocalModels { get; } = new ObservableCollection<AIModel>();
    public ObservableCollection<AIModel> DownloadedHostedModels { get; } = new ObservableCollection<AIModel>();
    public ObservableCollection<AIModel> DownloadedLocalChatModels { get; } = new ObservableCollection<AIModel>();
    public ObservableCollection<AIModel> DownloadedLocalEmbeddingModels { get; } = new ObservableCollection<AIModel>();

    // // Separate collections for Databricks chat and embedding models
    // public ObservableCollection<AIModel> DatabricksChatModels { get; } = new ObservableCollection<AIModel>();
    // public ObservableCollection<AIModel> DatabricksEmbeddingModels { get; } = new ObservableCollection<AIModel>();

    public bool HasDownloadedLocalModels => DownloadedLocalModels.Count > 0;
    public bool HasDownloadedHostedModels => DownloadedHostedModels.Count > 0;
    public bool HasDownloadedLocalChatModels => DownloadedLocalChatModels.Count > 0;
    public bool HasDownloadedLocalEmbeddingModels => DownloadedLocalEmbeddingModels.Count > 0;

    // Model search functionality
    private string _externalModelSearchText = string.Empty;
    public string ExternalModelSearchText
    {
        get => _externalModelSearchText;
        set => SetProperty(ref _externalModelSearchText, value);
    }

    private string _huggingFaceApiKey = string.Empty;
    private string _originalHuggingFaceApiKey = string.Empty;

    public string HuggingFaceApiKey
    {
        get => _huggingFaceApiKey;
        set
        {
            if (SetProperty(ref _huggingFaceApiKey, value))
            {
                OnPropertyChanged(nameof(IsHuggingFaceTokenSaved));
                (SaveHuggingFaceTokenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsHuggingFaceTokenSaved => !string.IsNullOrWhiteSpace(_originalHuggingFaceApiKey);

    public ICommand SaveHuggingFaceTokenCommand { get; }
    public ICommand DeleteHuggingFaceTokenCommand { get; }

    private bool CanSaveHuggingFaceToken(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(HuggingFaceApiKey) && HuggingFaceApiKey != _originalHuggingFaceApiKey;
    }

    private async Task SaveHuggingFaceTokenAsync()
    {
        await SaveAIModelSettingsAsync(showSuccessToast: false);
        _originalHuggingFaceApiKey = HuggingFaceApiKey;
        OnPropertyChanged(nameof(IsHuggingFaceTokenSaved));
        (SaveHuggingFaceTokenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        try { Views.MainWindow.ToastServiceInstance.ShowSuccess("Hugging Face API Key saved successfully", 3000); } catch { }
    }

    private async Task DeleteHuggingFaceTokenAsync()
    {
        HuggingFaceApiKey = string.Empty;
        await SaveAIModelSettingsAsync(showSuccessToast: false);
        _originalHuggingFaceApiKey = string.Empty;
        OnPropertyChanged(nameof(IsHuggingFaceTokenSaved));
        (SaveHuggingFaceTokenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        try { Views.MainWindow.ToastServiceInstance.ShowSuccess("Hugging Face API Key deleted successfully", 3000); } catch { }
    }

    private string _modelSearchText = string.Empty;
    public string ModelSearchText
    {
        get => _modelSearchText;
        set
        {
            if (SetProperty(ref _modelSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredDownloadedLocalModels));
            }
        }
    }

    public IEnumerable<AIModel> FilteredDownloadedLocalModels
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ModelSearchText))
            {
                return DownloadedLocalModels;
            }

            var searchLower = ModelSearchText.ToLower();
            return DownloadedLocalModels.Where(m =>
                m.DisplayText?.ToLower().Contains(searchLower) == true ||
                m.Description?.ToLower().Contains(searchLower) == true ||
                m.Author?.ToLower().Contains(searchLower) == true);
        }
    }

    // Provider URLs and Credentials
    private string _hostedModelUrl = string.Empty;

    public string HostedModelUrl
    {
        get => _hostedModelUrl;
        set => SetProperty(ref _hostedModelUrl, value);
    }

    // // Databricks URL and API Key map to existing credential properties
    // public string DatabricksUrl
    // {
    //     get => _databricksWorkspaceUrl ?? string.Empty;
    //     set
    //     {
    //         if (SetProperty(ref _databricksWorkspaceUrl, value))
    //         {
    //             OnPropertyChanged(nameof(DatabricksWorkspaceUrl));
    //             CheckForDatabricksCredentialsChanges();
    //         }
    //     }
    // }

    // public string DatabricksApiKey
    // {
    //     get => _providerSecretInput;
    //     set
    //     {
    //         if (SetProperty(ref _providerSecretInput, value))
    //         {
    //             // Mark as editing secret so the system knows to save it
    //             if (!string.IsNullOrWhiteSpace(value))
    //             {
    //                 IsEditingSecret = true;
    //             }
    //             OnPropertyChanged(nameof(ProviderSecretInput));
    //             CheckForDatabricksCredentialsChanges();
    //         }
    //     }
    // }

    // AI Access settings properties
    private bool _noReadAccess = false;
    private bool _allowReadAccess = true;
    private bool _allowUpdateFiles = true;
    private bool _allowCreateFiles = true;
    private string _writePermissionPath = @"C:\User\Documents";
    private bool _allowMdFiles = true;
    private bool _allowDocxFiles = true;
    private bool _allowXlsFiles = true;
    private bool _allowPdfFiles = true;
    private bool _allowTxtFiles = true;
    private bool _allowCsvFiles = true;

    // Schedule settings properties
    private bool _enableWorkingHours = false;
    private System.TimeSpan _workingHoursStart = new System.TimeSpan(9, 0, 0);
    private System.TimeSpan _workingHoursEnd = new System.TimeSpan(17, 0, 0);
    private bool _workingDayMonday = true;
    private bool _workingDayTuesday = true;
    private bool _workingDayWednesday = true;
    private bool _workingDayThursday = true;
    private bool _workingDayFriday = true;
    private bool _workingDaySaturday = false;
    private bool _workingDaySunday = false;
    private bool _enableDailySummary = false;
    private bool _enableWeeklyPlanning = false;
    private bool _enableFileOrganization = false;

    private readonly IDialogService _dialogService;
    private readonly SettingsUseCase _settingsUseCase;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IExternalApiService _externalApiService;

    // Track removed items for deletion logic
    private readonly List<string> _removedPaths = new List<string>();
    private readonly List<string> _resumePaths = new List<string>();
    private readonly List<string> _removedExtensions = new List<string>();
    private readonly List<string> _originalPaths = new List<string>();
    private readonly List<string> _originalExtensions = new List<string>();
    private bool _isResumePending;

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set => SetProperty(ref _welcomeMessage, value);
    }

    public string CurrentSettingsTitle
    {
        get => _currentSettingsTitle;
        set => SetProperty(ref _currentSettingsTitle, value);
    }

    public string CurrentSettingsContent
    {
        get => _currentSettingsContent;
        set => SetProperty(ref _currentSettingsContent, value);
    }

    // Tab visibility properties
    public bool IsGeneralSelected
    {
        get => _isGeneralSelected;
        set => SetProperty(ref _isGeneralSelected, value);
    }

    public bool IsAIAccessSelected
    {
        get => _isAIAccessSelected;
        set => SetProperty(ref _isAIAccessSelected, value);
    }

    public bool IsAIModelsSelected
    {
        get => _isAIModelsSelected;
        set => SetProperty(ref _isAIModelsSelected, value);
    }

    public bool IsSupportSelected
    {
        get => _isSupportSelected;
        set => SetProperty(ref _isSupportSelected, value);
    }

    public string CheckNowButtonText
    {
        get => _checkNowButtonText;
        set => SetProperty(ref _checkNowButtonText, value);
    }

    public string CurrentVersionText
    {
        get => _currentVersionText;
        set => SetProperty(ref _currentVersionText, value);
    }

    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        set => SetProperty(ref _isCheckingUpdate, value);
    }

    public string ChooseFoldersButtonText
    {
        get => _chooseFoldersButtonText;
        set => SetProperty(ref _chooseFoldersButtonText, value);
    }

    public bool HasReadAccessChanges
    {
        get => _hasReadAccessChanges;
        private set => SetProperty(ref _hasReadAccessChanges, value);
    }

    public bool ShowReadAccessSuccess
    {
        get => _showReadAccessSuccess;
        private set => SetProperty(ref _showReadAccessSuccess, value);
    }

    // AI Model Selection Changes
    private bool _hasAIModelChanges;
    private AIModelDto? _originalSelectedModel;

    public bool HasAIModelChanges
    {
        get => _hasAIModelChanges;
        private set => SetProperty(ref _hasAIModelChanges, value);
    }

    // // Databricks Credentials Changes
    // private bool _hasDatabricksCredentialsChanges;
    // private string? _originalDatabricksUrl;
    // private string? _originalDatabricksApiKey;

    // public bool HasDatabricksCredentialsChanges
    // {
    //     get => _hasDatabricksCredentialsChanges;
    //     private set => SetProperty(ref _hasDatabricksCredentialsChanges, value);
    // }

    // public bool HasDatabricksModels => DatabaseAIModels?.Any() ?? false;

    // public string? PendingDeleteDatabricksModelId
    // {
    //     get => _pendingDeleteDatabricksModelId;
    //     private set => SetProperty(ref _pendingDeleteDatabricksModelId, value);
    // }

    public string? PendingDeleteLocalModelId
    {
        get => _pendingDeleteLocalModelId;
        private set => SetProperty(ref _pendingDeleteLocalModelId, value);
    }

    public bool HasReadAccessFolders => ReadAccessFolders.Count > 0;
    public bool ShowChooseFoldersInline => AllowReadAccess && !HasReadAccessFolders;
    public bool ShowChooseFoldersBelow => AllowReadAccess && HasReadAccessFolders;
    public bool IsTreeStructurePaused
    {
        get => _isTreeStructurePaused;
        private set
        {
            if (SetProperty(ref _isTreeStructurePaused, value))
            {
                OnPropertyChanged(nameof(PauseResumeButtonText));
                OnPropertyChanged(nameof(ShowPauseResumeButton));
                OnPropertyChanged(nameof(ShowPauseButton));
                OnPropertyChanged(nameof(ShowContinueButton));
                OnPropertyChanged(nameof(CanPauseResume));
                OnPropertyChanged(nameof(CanResumeTree));
            }
        }
    }
    public string PauseResumeButtonText => IsTreeStructurePaused ? "Continue" : "Pause";
    public bool ShowPauseResumeButton => AllowReadAccess && (IsTreeStructureRunning || IsTreeStructurePaused || _isResumePending);
    public bool ShowPauseButton => AllowReadAccess && IsTreeStructureRunning && !_isResumePending;
    public bool ShowContinueButton => AllowReadAccess && (IsTreeStructurePaused || _isResumePending);
    public bool CanResumeTree => IsTreeStructurePaused;
    public bool CanPauseResume => IsTreeStructureRunning || IsTreeStructurePaused;

    public FolderItem? PendingFolderRemoval
    {
        get => _pendingFolderRemoval;
        private set
        {
            if (SetProperty(ref _pendingFolderRemoval, value))
            {
                OnPropertyChanged(nameof(HasPendingFolderRemoval));
            }
        }
    }

    public bool HasPendingFolderRemoval => PendingFolderRemoval != null;

    public bool HasWritePermissionChanges
    {
        get => _hasWritePermissionChanges;
        private set => SetProperty(ref _hasWritePermissionChanges, value);
    }

    public bool ShowWritePermissionSuccess
    {
        get => _showWritePermissionSuccess;
        private set => SetProperty(ref _showWritePermissionSuccess, value);
    }

    public bool HasFileTypesChanges
    {
        get => _hasFileTypesChanges;
        private set => SetProperty(ref _hasFileTypesChanges, value);
    }

    public bool ShowFileTypesSuccess
    {
        get => _showFileTypesSuccess;
        private set => SetProperty(ref _showFileTypesSuccess, value);
    }

    public bool IsTreeStructureRunning
    {
        get => _isTreeStructureRunning;
        private set
        {
            if (SetProperty(ref _isTreeStructureRunning, value))
            {
                if (value)
                {
                    SetResumePending(false);
                    IsTreeStructurePaused = false;
                    ChooseFoldersButtonText = "Choose folders...";
                }
                foreach (var folder in ReadAccessFolders)
                {
                    folder.IsLoading = value;
                }

                OnPropertyChanged(nameof(ShowPauseResumeButton));
                OnPropertyChanged(nameof(ShowPauseButton));
                OnPropertyChanged(nameof(ShowContinueButton));
                OnPropertyChanged(nameof(CanPauseResume));
                OnPropertyChanged(nameof(CanResumeTree));
            }
        }
    }

    public bool IsCheckingTreeStructure
    {
        get => _isCheckingTreeStructure;
        private set => SetProperty(ref _isCheckingTreeStructure, value);
    }

    // General settings properties
    public bool PerformanceHigh
    {
        get => _performanceHigh;
        set => SetProperty(ref _performanceHigh, value);
    }

    public bool PerformanceMedium
    {
        get => _performanceMedium;
        set => SetProperty(ref _performanceMedium, value);
    }

    public bool PerformanceLow
    {
        get => _performanceLow;
        set => SetProperty(ref _performanceLow, value);
    }

    public bool AutoUpdatesEnabled
    {
        get => _autoUpdatesEnabled;
        set => SetProperty(ref _autoUpdatesEnabled, value);
    }

    // AI Access settings properties
    public bool NoReadAccess
    {
        get => _noReadAccess;
        set
        {
            if (SetProperty(ref _noReadAccess, value))
            {
                if (value)
                {
                    AllowReadAccess = false;
                }
                if (!_isLoadingSettings)
                {
                    CheckReadAccessChanges();
                }
            }
        }
    }

    public bool AllowReadAccess
    {
        get => _allowReadAccess;
        set
        {
            if (SetProperty(ref _allowReadAccess, value))
            {
                if (value)
                {
                    NoReadAccess = false;
                }
                if (!_isLoadingSettings)
                {
                    CheckReadAccessChanges();
                }
                if (!value)
                {
                    IsTreeStructurePaused = false;
                    SetResumePending(false);
                }
                OnPropertyChanged(nameof(ShowChooseFoldersInline));
                OnPropertyChanged(nameof(ShowChooseFoldersBelow));
                OnPropertyChanged(nameof(ShowPauseResumeButton));
                OnPropertyChanged(nameof(ShowPauseButton));
                OnPropertyChanged(nameof(ShowContinueButton));
                OnPropertyChanged(nameof(CanPauseResume));
                OnPropertyChanged(nameof(CanResumeTree));
            }
        }
    }

    public ObservableCollection<FolderItem> ReadAccessFolders { get; } = new ObservableCollection<FolderItem>();

    public bool AllowUpdateFiles
    {
        get => _allowUpdateFiles;
        set
        {
            if (SetProperty(ref _allowUpdateFiles, value) && !_isLoadingSettings)
            {
                CheckWritePermissionChanges();
            }
        }
    }

    public bool AllowCreateFiles
    {
        get => _allowCreateFiles;
        set
        {
            if (SetProperty(ref _allowCreateFiles, value) && !_isLoadingSettings)
            {
                CheckWritePermissionChanges();
            }
        }
    }

    public string WritePermissionPath
    {
        get => _writePermissionPath;
        set
        {
            if (SetProperty(ref _writePermissionPath, value) && !_isLoadingSettings)
            {
                CheckWritePermissionChanges();
            }
        }
    }

    public bool AllowMdFiles
    {
        get => _allowMdFiles;
        set
        {
            if (SetProperty(ref _allowMdFiles, value) && !_isLoadingSettings)
            {
                CheckFileTypesChanges();
            }
        }
    }    public bool AllowDocxFiles
    {
        get => _allowDocxFiles;
        set
        {
            if (SetProperty(ref _allowDocxFiles, value) && !_isLoadingSettings)
            {
                CheckFileTypesChanges();
            }
        }
    }    public bool AllowXlsFiles
    {
        get => _allowXlsFiles;
        set
        {
            if (SetProperty(ref _allowXlsFiles, value) && !_isLoadingSettings)
            {
                CheckFileTypesChanges();
            }
        }
    }    public bool AllowPdfFiles
    {
        get => _allowPdfFiles;
        set
        {
            if (SetProperty(ref _allowPdfFiles, value) && !_isLoadingSettings)
            {
                CheckFileTypesChanges();
            }
        }
    }    public bool AllowTxtFiles
    {
        get => _allowTxtFiles;
        set
        {
            if (SetProperty(ref _allowTxtFiles, value) && !_isLoadingSettings)
            {
                CheckFileTypesChanges();
            }
        }
    }    public bool AllowCsvFiles
    {
        get => _allowCsvFiles;
        set
        {
            if (SetProperty(ref _allowCsvFiles, value) && !_isLoadingSettings)
            {
                CheckFileTypesChanges();
            }
        }
    }    // Schedule settings properties
    public bool EnableWorkingHours
    {
        get => _enableWorkingHours;
        set => SetProperty(ref _enableWorkingHours, value);
    }

    public System.TimeSpan WorkingHoursStart
    {
        get => _workingHoursStart;
        set => SetProperty(ref _workingHoursStart, value);
    }

    public System.TimeSpan WorkingHoursEnd
    {
        get => _workingHoursEnd;
        set => SetProperty(ref _workingHoursEnd, value);
    }

    public bool WorkingDayMonday
    {
        get => _workingDayMonday;
        set => SetProperty(ref _workingDayMonday, value);
    }

    public bool WorkingDayTuesday
    {
        get => _workingDayTuesday;
        set => SetProperty(ref _workingDayTuesday, value);
    }

    public bool WorkingDayWednesday
    {
        get => _workingDayWednesday;
        set => SetProperty(ref _workingDayWednesday, value);
    }

    public bool WorkingDayThursday
    {
        get => _workingDayThursday;
        set => SetProperty(ref _workingDayThursday, value);
    }

    public bool WorkingDayFriday
    {
        get => _workingDayFriday;
        set => SetProperty(ref _workingDayFriday, value);
    }

    public bool WorkingDaySaturday
    {
        get => _workingDaySaturday;
        set => SetProperty(ref _workingDaySaturday, value);
    }

    public bool WorkingDaySunday
    {
        get => _workingDaySunday;
        set => SetProperty(ref _workingDaySunday, value);
    }

    public bool EnableDailySummary
    {
        get => _enableDailySummary;
        set => SetProperty(ref _enableDailySummary, value);
    }

    public bool EnableWeeklyPlanning
    {
        get => _enableWeeklyPlanning;
        set => SetProperty(ref _enableWeeklyPlanning, value);
    }

    public bool EnableFileOrganization
    {
        get => _enableFileOrganization;
        set => SetProperty(ref _enableFileOrganization, value);
    }

    public ObservableCollection<SettingsNavigationItem> SettingsNavigationItems { get; } = new ObservableCollection<SettingsNavigationItem>();

    public SettingsNavigationItem? SelectedSettingsItem
    {
        get => _selectedSettingsItem;
        set
        {
            if (_selectedSettingsItem != value)
            {
                // Deselect previous item
                if (_selectedSettingsItem != null)
                {
                    _selectedSettingsItem.IsSelected = false;
                }

                _selectedSettingsItem = value;

                // Select new item
                if (_selectedSettingsItem != null)
                {
                    _selectedSettingsItem.IsSelected = true;
                }

                OnPropertyChanged(nameof(SelectedSettingsItem));
                UpdateTabVisibility();
            }
        }
    }

    public ICommand SelectSettingsItemCommand { get; }
    public ICommand UpdateNowCommand { get; }
    public ICommand ChooseFoldersCommand { get; }
    public ICommand RefreshFoldersCommand { get; }
    public ICommand RemoveFolderCommand { get; }
    public ICommand RequestRemoveFolderCommand { get; }
    public ICommand ConfirmRemoveFolderCommand { get; }
    public ICommand CancelRemoveFolderCommand { get; }
    public ICommand BrowseWritePathCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAIPermissionsCommand { get; }
    public ICommand SaveAIModelCommand { get; }
    public ICommand DownloadModelCommand { get; }
    public ICommand DownloadVariantCommand { get; }
    public ICommand RequestDownloadModelCommand { get; }
    public ICommand CancelDownloadRequestCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand RemoveModelCommand { get; }
    public ICommand RequestDeleteLocalModelCommand { get; }
    public ICommand CancelDeleteLocalModelCommand { get; }
    public ICommand ReplaceSecretCommand { get; }
    public ICommand ToggleShowSecretCommand { get; }
    public ICommand SaveCredentialsCommand { get; }
    public ICommand ToggleSecretVisibilityCommand { get; }
    // public ICommand ToggleDatabricksApiKeyVisibilityCommand { get; }
    public ICommand ApplyReadAccessCommand { get; }
    public ICommand CancelReadAccessCommand { get; }
    public ICommand PauseResumeTreeCommand { get; }
    public ICommand ApplyWritePermissionCommand { get; }
    public ICommand CancelWritePermissionCommand { get; }
    public ICommand ApplyFileTypesCommand { get; }
    public ICommand CancelFileTypesCommand { get; }
    public ICommand ApplyAIModelChangesCommand { get; }
    public ICommand CancelAIModelChangesCommand { get; }
    // public ICommand CancelDatabricksCredentialsCommand { get; }
    public ICommand SendFeedbackCommand { get; }
    public ICommand OpenDocumentationCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand AddProviderModelCommand => _addProviderModelCommand;
    public ICommand RemoveProviderModelCommand { get; }
    public ICommand ToggleLocalModelFrameCommand { get; }
    public ICommand ToggleHostedModelFrameCommand { get; }
    // public ICommand ToggleDatabricksModelFrameCommand { get; }
    public ICommand ApplyHostedModelUrlCommand { get; }
    // public ICommand ApplyDatabricksCredentialsCommand { get; }
    // public ICommand AddDatabricksModelCommand { get; }
    // public ICommand RequestDeleteDatabricksModelCommand { get; }
    // public ICommand CancelDeleteDatabricksModelCommand { get; }
    // public ICommand DeleteDatabricksModelCommand { get; }

    public SettingsViewModel(IDialogService dialogService, SettingsUseCase settingsUseCase, ILogger<SettingsViewModel> logger, IExternalApiService externalApiService, IProviderCredentialService? providerCredentialService = null, IHttpClientFactory? httpClientFactory = null)
    {
        _dialogService = dialogService;
        _settingsUseCase = settingsUseCase;
        _logger = logger;
        _externalApiService = externalApiService;
        _providerCredentialService = providerCredentialService;
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        // Set up collection change notifications
        DownloadedLocalModels.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasDownloadedLocalModels));
        DownloadedHostedModels.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasDownloadedHostedModels));
        ReadAccessFolders.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasReadAccessFolders));
            OnPropertyChanged(nameof(ShowChooseFoldersInline));
            OnPropertyChanged(nameof(ShowChooseFoldersBelow));
        };

        // Initialize model purposes dropdown
        AvailableModelPurposes.Add(Baiss.Domain.Entities.ModelPurposes.Chat);
        AvailableModelPurposes.Add(Baiss.Domain.Entities.ModelPurposes.Embedding);

        // Set default purpose to chat
        NewModelPurpose = Baiss.Domain.Entities.ModelPurposes.Chat;

        // // Wire up collection changed to try deferred selection by ID
        // DatabricksChatModels.CollectionChanged += (s, e) => TryResolveDatabricksChatSelectionById();
        // DatabricksEmbeddingModels.CollectionChanged += (s, e) => TryResolveDatabricksEmbeddingSelectionById();

        InitializeSettingsNavigationItems();
        InitializeAIModels();

        SearchExternalModelCommand = new AsyncRelayCommand(SearchExternalModelAsync);
        SaveHuggingFaceTokenCommand = new AsyncRelayCommand(async _ => await SaveHuggingFaceTokenAsync(), CanSaveHuggingFaceToken);
        DeleteHuggingFaceTokenCommand = new AsyncRelayCommand(async _ => await DeleteHuggingFaceTokenAsync());

        SelectSettingsItemCommand = new RelayCommand<SettingsNavigationItem>(item =>
        {
            if (item != null)
            {
                SelectedSettingsItem = item;
            }
        });

        UpdateNowCommand = new AsyncRelayCommand(async _ =>
        {
            bool needUpdate = CheckNowButtonText.ToLower().Contains("update");
            bool isUpdate = CheckNowButtonText.ToLower().Contains("updating");

            if (isUpdate)
            {
                return;
            }
            try
            {
                IsCheckingUpdate = true;
                if (needUpdate)
                {
                    CheckNowButtonText = "Updating...";
                }
                else
                {
                    CheckNowButtonText = "Checking...";
                }

                // Determine performance level from UI
                var performanceLevel = PerformanceLow ? PerformanceLevel.Small :
                                      PerformanceMedium ? PerformanceLevel.Medium :
                                      PerformanceLevel.High;

                var updateGeneralSettings = new UpdateGeneralSettingsDto
                {
                    Performance = performanceLevel,
                    EnableAutoUpdate = AutoUpdatesEnabled,
                    CheckUpdate = true,
                    NeedUpdate = needUpdate
                };

                var result = await _settingsUseCase.UpdateGeneralSettingsAsync(updateGeneralSettings);
                if (result?.AppVersion != null)
                {
                    // CurrentVersionText = $"Version: {result.AppVersion}";
                    CheckNowButtonText = $"Update {result.AppVersion}";
                    if (result?.AppVersion == "Updating...")
                    {
                        CheckNowButtonText = $"Updating...";
                    }
                }
                else
                {
                    CheckNowButtonText = "Check now";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                CheckNowButtonText = "Check failed";
                await Task.Delay(3000);
                CheckNowButtonText = "Check now";
            }
            finally
            {
                IsCheckingUpdate = false;
                await CheckTreeStructureThreadAsync();
            }
        });

        ChooseFoldersCommand = new AsyncRelayCommand(async _ =>
        {
            // Check if server is online
            var isServerOnline = await _externalApiService.CheckServerStatus();
            if (!isServerOnline)
            {
                Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000);
                return;
            }

            // Check if embedding model is selected before allowing folder selection
            var hasEmbeddingModel = await CheckIfEmbeddingModelSelectedAsync();
            if (!hasEmbeddingModel)
            {
                Views.MainWindow.ToastServiceInstance.ShowError("You need to select an embedding model first to choose folders for AI access", 4000);
                return;
            }

            if (IsTreeStructureRunning)
            {
                StopBackgroundOperations();
                IsTreeStructurePaused = true;
                return;
            }

            IsTreeStructurePaused = false;

            var selectedFolders = await _dialogService.ShowMultipleFolderPickerAsync("Choose folders for AI read access");
            if (selectedFolders != null && selectedFolders.Length > 0)
            {
                foreach (var folder in selectedFolders)
                {
                    if (!ReadAccessFolders.Any(f => string.Equals(f.Path, folder, StringComparison.OrdinalIgnoreCase)))
                    {
                        var folderItem = new FolderItem
                        {
                            Path = folder,
                            IsLoading = IsTreeStructureRunning
                        };
                        ReadAccessFolders.Add(folderItem);
                        _removedPaths.RemoveAll(p => string.Equals(p, folder, StringComparison.OrdinalIgnoreCase));
                    }
                }
                CheckReadAccessChanges();
            }
        });

        RefreshFoldersCommand = new AsyncRelayCommand(async _ =>
        {
            PendingFolderRemoval = null;
            HideTemporaryFolderRemovalNotification();
            _settingsUseCase.RefreshTreeStructure();
            await RefreshAIPermissionsFromDatabaseAsync();
            await CheckTreeStructureThreadAsync();
        });

        RemoveFolderCommand = new RelayCommand<FolderItem>(folder =>
        {
            RemoveReadAccessFolder(folder);
        });

        RequestRemoveFolderCommand = new RelayCommand<FolderItem>(async folder =>
        {
            if (folder == null)
            {
                return;
            }

            // Check if server is online before allowing folder removal
            var isServerOnline = await _externalApiService.CheckServerStatus();
            if (!isServerOnline)
            {
                Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000);
                return;
            }

            if (PendingFolderRemoval == folder)
            {
                return;
            }

            if (PendingFolderRemoval != null)
            {
                PendingFolderRemoval.IsPendingRemove = false;
            }

            folder.IsPendingRemove = true;
            PendingFolderRemoval = folder;
        });

        ConfirmRemoveFolderCommand = new RelayCommand<FolderItem>(async folder =>
        {
            if (folder == null)
            {
                folder = PendingFolderRemoval;
            }

            if (folder == null)
            {
                return;
            }

            // Check if server is online before confirming folder removal
            var isServerOnline = await _externalApiService.CheckServerStatus();
            if (!isServerOnline)
            {
                Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000);
                return;
            }

            RemoveReadAccessFolder(folder);
        });

        CancelRemoveFolderCommand = new RelayCommand<FolderItem>(folder =>
        {
            if (folder == null)
            {
                folder = PendingFolderRemoval;
            }

            if (folder == null)
            {
                return;
            }

            folder.IsPendingRemove = false;
            if (PendingFolderRemoval == folder)
            {
                PendingFolderRemoval = null;
            }
        });

        PauseResumeTreeCommand = new RelayCommand(() =>
        {
            if (!IsTreeStructureRunning)
            {
                return;
            }

            _resumePaths.Clear();
            foreach (var folder in ReadAccessFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder.Path) &&
                    !_resumePaths.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
                {
                    _resumePaths.Add(folder.Path);
                }
            }

            StopBackgroundOperations();
            IsTreeStructurePaused = true;
            SetResumePending(true);
        });

        BrowseWritePathCommand = new AsyncRelayCommand(async _ =>
        {
            var selectedFolder = await _dialogService.ShowFolderPickerAsync("Choose write permission folder");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                WritePermissionPath = selectedFolder;
            }
        });

        CancelCommand = new RelayCommand(CancelReadAccessChanges);

        SaveCommand = new RelayCommand(async () =>
        {
            await SaveGeneralSettingsAsync();
        });

        SaveAIPermissionsCommand = new RelayCommand(async () =>
        {
            await SaveAIPermissionsAsync();
        });

        SaveAIModelCommand = new RelayCommand(async () =>
        {
            await SaveAIModelSettingsAsync();
        });

        RequestDownloadModelCommand = new RelayCommand<AIModel>(model =>
        {
            if (model == null || model.IsDownloading || model.IsDownloaded)
            {
                return;
            }

            model.IsAwaitingDownloadConfirmation = true;
            model.IsPendingDelete = false;
        });

        CancelDownloadRequestCommand = new RelayCommand<AIModel>(model =>
        {
            if (model == null)
            {
                foreach (var localModel in DownloadedLocalModels)
                {
                    localModel.IsAwaitingDownloadConfirmation = false;
                    localModel.IsPendingDelete = false;
                }

                PendingDeleteLocalModelId = null;
                return;
            }

            model.IsAwaitingDownloadConfirmation = false;
            model.IsPendingDelete = false;
            PendingDeleteLocalModelId = null;
        });

        CancelDownloadCommand = new RelayCommand<AIModel>(model =>
        {
            if (model == null)
            {
                return;
            }

            if (model.IsAwaitingDownloadConfirmation)
            {
                // Cancel before download starts
                model.IsAwaitingDownloadConfirmation = false;
                model.IsPendingDelete = false;
                PendingDeleteLocalModelId = null;
            }
            else if (model.IsDownloading)
            {

                StopActiveDownloadTracking(model);

                if (!string.IsNullOrWhiteSpace(model.ActiveProcessId))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // not work
                            await StopRemoteDownloadAsync(model.ActiveProcessId);
                            await _settingsUseCase.DeleteModelAsync(model.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error stopping download for model {ModelId}", model.Id);
                        }
                    });
                }
            }
        });

        DownloadModelCommand = new RelayCommand<AIModel>(model =>
        {
            if (model == null || model.IsDownloading || model.IsDownloaded)
            {
                return;
            }

            if (!HasSufficientDiskSpaceForModel(out var diskSpaceError))
            {
                if (!string.IsNullOrWhiteSpace(diskSpaceError))
                {
                    try { Views.MainWindow.ToastServiceInstance.ShowError(diskSpaceError, 5000); } catch { }
                }
                return;
            }

            PendingDeleteLocalModelId = null;
            model.IsAwaitingDownloadConfirmation = false;
            model.IsPendingDelete = false;

            // Start the download asynchronously without blocking the command
            Task.Run(async () =>
            {
                try
                {
                    await StartLocalModelDownloadAsync(model, model.DefaultDownloadUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting download for model {ModelId}", model.Id);
                }
            });
        });

        DownloadVariantCommand = new RelayCommand<GgufVariant>(variant =>
        {
            if (variant?.ParentModel == null)
            {
                return;
            }

            var model = variant.ParentModel;
            if (model.IsDownloading || model.IsDownloaded || variant.IsDownloading || variant.IsDownloaded)
            {
                return;
            }

            if (!HasSufficientDiskSpaceForModel(out var diskSpaceError))
            {
                if (!string.IsNullOrWhiteSpace(diskSpaceError))
                {
                    try { Views.MainWindow.ToastServiceInstance.ShowError(diskSpaceError, 5000); } catch { }
                }
                return;
            }

            PendingDeleteLocalModelId = null;
            model.IsAwaitingDownloadConfirmation = false;
            model.IsPendingDelete = false;
            variant.IsDownloading = true;
            variant.DownloadProgress = 0;

            Task.Run(async () =>
            {
                try
                {
                    await StartVariantDownloadAsync(model, variant);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting variant download for model {ModelId}", model.Id);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        variant.IsDownloading = false;
                        variant.DownloadProgress = 0;
                    });
                }
            });
        });

        RemoveModelCommand = new AsyncRelayCommand(async param =>
        {
            if (param is not AIModel model)
            {
                return;
            }

            var removedModelName = model.Name;
            var activeProcessId = model.ActiveProcessId;

            try
            {
                // Stop any active download tracking first
                // StopActiveDownloadTracking(model);
                // if (!string.IsNullOrWhiteSpace(activeProcessId))
                // {
                //     _ = StopRemoteDownloadAsync(activeProcessId);
                // }

                // Call the actual delete model API
                var deleteResult = await _settingsUseCase.DeleteModelAsync(model.Id);

                // Even if backend delete fails, desyncing the UI leaves a broken state.
                // Reset local state so the user can retry download or delete.
                model.IsDownloaded = false;
                model.DownloadProgress = 0;
                model.IsDownloading = false;
                model.ActiveProcessId = null;
                PendingDeleteLocalModelId = null;
                model.IsAwaitingDownloadConfirmation = false;
                model.IsPendingDelete = false;

                // Update the purpose-based collections when model is deleted or reset
                UpdateDownloadedModelsByPurpose();
                _ = RefreshLocalDownloadStatusesAsync();

                if (deleteResult)
                {
                    try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"Model '{removedModelName}' deleted successfully", 3000); } catch { }
                }
                else
                {
                    try { Views.MainWindow.ToastServiceInstance.ShowError($"Backend failed to delete '{removedModelName}'. State reset locally; please retry.", 5000); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model {ModelName}: {Message}", removedModelName, ex.Message);
                try { Views.MainWindow.ToastServiceInstance.ShowError($"Error deleting model '{removedModelName}': {ex.Message}", 5000); } catch { }
            }
        });

        RequestDeleteLocalModelCommand = new RelayCommand<AIModel>(model =>
        {
            if (model == null)
            {
                PendingDeleteLocalModelId = null;
                foreach (var localModel in DownloadedLocalModels)
                {
                    localModel.IsPendingDelete = false;
                    localModel.IsAwaitingDownloadConfirmation = false;
                }
                return;
            }

            var shouldConfirm = PendingDeleteLocalModelId != model.Id;
            PendingDeleteLocalModelId = shouldConfirm ? model.Id : null;

            foreach (var localModel in DownloadedLocalModels)
            {
                var isTarget = ReferenceEquals(localModel, model) && shouldConfirm;
                localModel.IsPendingDelete = isTarget;
            }

            model.IsAwaitingDownloadConfirmation = false;
        });

        CancelDeleteLocalModelCommand = new RelayCommand<AIModel>(model =>
        {
            if (model == null)
            {
                PendingDeleteLocalModelId = null;
                foreach (var localModel in DownloadedLocalModels)
                {
                    localModel.IsPendingDelete = false;
                }
                return;
            }

            if (PendingDeleteLocalModelId == model.Id)
            {
                PendingDeleteLocalModelId = null;
            }

            model.IsPendingDelete = false;
            model.IsAwaitingDownloadConfirmation = false;
        });

        ReplaceSecretCommand = new RelayCommand(() =>
        {
            IsEditingSecret = true;
            ProviderSecretInput = string.Empty;
        });

        ToggleShowSecretCommand = new RelayCommand(() =>
        {
            if (!IsEditingSecret)
            {
                return;
            }

            IsEditingSecret = false;
            _providerSecretInput = string.Empty;
            OnPropertyChanged(nameof(ProviderSecretInput));
            IsSecretVisible = false;
            HasPendingCredentialChanges = false;
            CredentialValidationError = null;
            CredentialValidationSuccess = null;
        });

        SaveCredentialsCommand = new AsyncRelayCommand(async _ =>
        {
            await SaveProviderCredentialsAsync();
        });

        ToggleSecretVisibilityCommand = new RelayCommand(() =>
        {
            IsSecretVisible = !IsSecretVisible;
        });

        // ToggleDatabricksApiKeyVisibilityCommand = new RelayCommand(() =>
        // {
        //     IsDatabricksApiKeyVisible = !IsDatabricksApiKeyVisible;
        // });

        ApplyReadAccessCommand = new AsyncRelayCommand(async _ => await ApplyReadAccessChangesAsync());
        CancelReadAccessCommand = new RelayCommand(CancelReadAccessChanges);
        ApplyWritePermissionCommand = new AsyncRelayCommand(async _ => await ApplyWritePermissionChangesAsync());
        CancelWritePermissionCommand = new RelayCommand(CancelWritePermissionChanges);
        ApplyFileTypesCommand = new AsyncRelayCommand(async _ => await ApplyFileTypesChangesAsync());
        CancelFileTypesCommand = new RelayCommand(CancelFileTypesChanges);
        ApplyAIModelChangesCommand = new AsyncRelayCommand(async _ => await ApplyAIModelChangesAsync());
        CancelAIModelChangesCommand = new RelayCommand(CancelAIModelChanges);
        // CancelDatabricksCredentialsCommand = new RelayCommand(CancelDatabricksCredentialsChanges);

        SendFeedbackCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://baiss.ai/feedback",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open feedback page");
            }
        });

        OpenDocumentationCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://baiss.ai/documentation",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open documentation");
            }
        });

        UninstallCommand = new RelayCommand(() =>
        {
            _logger.LogInformation("Uninstall initiated");
        });

        ToggleLocalModelFrameCommand = new RelayCommand(() =>
        {
            // Only allow expansion if local models is selected
            if (IsLocalModelsSelected)
            {
                IsLocalModelFrameExpanded = !IsLocalModelFrameExpanded;
            }
        });

        ToggleHostedModelFrameCommand = new RelayCommand(() =>
        {
            // Only allow expansion if hosted models is selected
            if (IsHostedModelsSelected)
            {
                IsHostedModelFrameExpanded = !IsHostedModelFrameExpanded;
            }
        });

        // ToggleDatabricksModelFrameCommand = new RelayCommand(() =>
        // {
        //     // Only allow expansion if databricks models is selected
        //     if (IsDatabricksModelsSelected)
        //     {
        //         IsDatabricksModelFrameExpanded = !IsDatabricksModelFrameExpanded;
        //     }
        // });

        ApplyHostedModelUrlCommand = new AsyncRelayCommand(async _ =>
        {
            await ApplyHostedModelUrlAsync();
        });

        // ApplyDatabricksCredentialsCommand = new AsyncRelayCommand(async _ =>
        // {
        //     await ApplyDatabricksCredentialsAsync();
        // });

        // AddDatabricksModelCommand = new AsyncRelayCommand(async _ =>
        // {
        //     await AddDatabricksModelAsync();
        // });

        // RequestDeleteDatabricksModelCommand = new RelayCommand<AIModelDto>(model =>
        // {
        //     if (model == null)
        //     {
        //         PendingDeleteDatabricksModelId = null;
        //         return;
        //     }

        //     PendingDeleteDatabricksModelId = PendingDeleteDatabricksModelId == model.Id
        //         ? null
        //         : model.Id;
        // });

        // CancelDeleteDatabricksModelCommand = new RelayCommand<AIModelDto>(model =>
        // {
        //     if (model == null)
        //     {
        //         PendingDeleteDatabricksModelId = null;
        //         return;
        //     }

        //     if (PendingDeleteDatabricksModelId == model.Id)
        //     {
        //         PendingDeleteDatabricksModelId = null;
        //     }
        // });

        // DeleteDatabricksModelCommand = new AsyncRelayCommand(async param =>
        // {
        //     await DeleteDatabricksModelAsync(param as AIModelDto);
        // });

        _addProviderModelCommand = new AsyncRelayCommand(async _ => await AddProviderModelAsync(), _ => CanAddModel);
        RemoveProviderModelCommand = new AsyncRelayCommand(async param =>
        {
            await RemoveProviderModelAsync(param as AIModelDto);
        });

        // Select AI Models by default
        SelectedSettingsItem = SettingsNavigationItems.FirstOrDefault(x => x.Id == "ai-models");

        // Initialize default AI model configuration
        UpdateAvailableProviders(ModelTypes.Local);

        // Load existing settings
        _ = LoadSettingsAsync();
        _ = CheckTreeStructureThreadAsync();

        _statusTimer = new System.Threading.Timer(_ =>
        {
            _ = CheckTreeStructureThreadAsync();
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        ApplyScheduleCommand = new AsyncRelayCommand(async _ => await ApplyScheduleChangesAsync());
        CancelScheduleCommand = new RelayCommand(CancelScheduleChanges);

        InitializeScheduleOptions();
    }

    private void InitializeScheduleOptions()
    {
        ScheduleOptions.Clear();
        ScheduleOptions.Add(new ScheduleOption { DisplayName = "Every 15 Minutes", CronExpression = "0 0/15 * * * ?" });
        ScheduleOptions.Add(new ScheduleOption { DisplayName = "Every Hour", CronExpression = "0 0 * * * ?" });
        ScheduleOptions.Add(new ScheduleOption { DisplayName = "Every 6 Hours", CronExpression = "0 0 0/6 * * ?" });
        ScheduleOptions.Add(new ScheduleOption { DisplayName = "Every Day at Midnight", CronExpression = "0 0 0 * * ?" });
        ScheduleOptions.Add(new ScheduleOption { DisplayName = "Every Week (Sunday)", CronExpression = "0 0 0 ? * SUN" });
        ScheduleOptions.Add(new ScheduleOption { DisplayName = "Every 10 Seconds (Testing)", CronExpression = "0/10 * * * * ?" });
    }

    private async Task ApplyScheduleChangesAsync()
    {
        try
        {
            IsSaving = true;
            var dto = new UpdateTreeStructureScheduleDto
            {
                Schedule = TreeStructureSchedule,
                Enabled = TreeStructureScheduleEnabled
            };

            var result = await _settingsUseCase.UpdateTreeStructureScheduleAsync(dto);
            if (result != null)
            {
                HasScheduleChanges = false;
                ShowScheduleSuccess = true;

                // Hide success message after 3 seconds
                _ = Task.Delay(3000).ContinueWith(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowScheduleSuccess = false);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule settings");
            Views.MainWindow.ToastServiceInstance.ShowError("Failed to save schedule settings", 4000);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void CancelScheduleChanges()
    {
        // Reload settings to revert changes
        _ = LoadSettingsAsync();
        HasScheduleChanges = false;
    }

    // private void TryResolveDatabricksChatSelectionById()
    // {
    //     if (!string.IsNullOrWhiteSpace(_selectedDatabricksChatModelId))
    //     {
    //         var match = DatabricksChatModels.FirstOrDefault(m => m.Id == _selectedDatabricksChatModelId);
    //         if (match != null && !ReferenceEquals(match, _selectedDatabricksChatModel))
    //         {
    //             _selectedDatabricksChatModel = match;
    //             OnPropertyChanged(nameof(SelectedDatabricksChatModel));
    //         }
    //     }
    // }

    // private void TryResolveDatabricksEmbeddingSelectionById()
    // {
    //     if (!string.IsNullOrWhiteSpace(_selectedDatabricksEmbeddingModelId))
    //     {
    //         var match = DatabricksEmbeddingModels.FirstOrDefault(m => m.Id == _selectedDatabricksEmbeddingModelId);
    //         if (match != null && !ReferenceEquals(match, _selectedDatabricksEmbeddingModel))
    //         {
    //             _selectedDatabricksEmbeddingModel = match;
    //             OnPropertyChanged(nameof(SelectedDatabricksEmbeddingModel));
    //         }
    //     }
    // }

    private void InitializeAIModels()
    {
        AvailableModels.Clear();
        DownloadedLocalModels.Clear();
        DownloadedLocalChatModels.Clear();
        DownloadedLocalEmbeddingModels.Clear();
        SelectedAIModel = null;
        _ = LoadLocalModelsAsync();
    }

    // Tracks default download URL and non-default GGUF variants per model
    private readonly Dictionary<string, (string? DefaultUrl, string? DefaultSize, List<GgufVariant> Variants)> _modelVariantCache = new();

    private void ApplyLocalModelCatalog(IEnumerable<AIModelDto> models)
    {
        AvailableModels.Clear();
        DownloadedLocalModels.Clear();

        var index = 0;
        AIModel? firstEmbeddingModel = null;
        foreach (var dto in models)
        {
            _modelVariantCache.TryGetValue(dto.Id, out var cached);
            var variants = cached.Variants ?? new List<GgufVariant>();

            var sizeOverride = cached.DefaultSize ?? DetermineModelSize(dto);

            var selectionModel = CreateLocalModelViewModel(dto, defaultDownloadUrl: cached.DefaultUrl, sizeOverride: sizeOverride);
            AvailableModels.Add(selectionModel);

            var downloadModel = CreateLocalModelViewModel(dto, defaultDownloadUrl: cached.DefaultUrl, sizeOverride: sizeOverride);
            // Ensure embedding models start in a clean "not downloaded" state until a download completes
            if (string.Equals(downloadModel.Usage, ModelPurposes.Embedding, StringComparison.OrdinalIgnoreCase))
            {
                downloadModel.IsDownloaded = false;
                downloadModel.IsDownloading = false;
                downloadModel.DownloadProgress = 0;
            }
            downloadModel.IsFirst = index == 0;
            DownloadedLocalModels.Add(downloadModel);

            foreach (var variant in variants)
            {
                variant.ParentModel = downloadModel;
                downloadModel.Variants.Add(variant);
            }

            if (string.Equals(downloadModel.Usage, ModelPurposes.Embedding, StringComparison.OrdinalIgnoreCase) && firstEmbeddingModel == null)
            {
                firstEmbeddingModel = downloadModel;
            }
            index++;
        }

        if (DownloadedLocalModels.Count == 0)
        {
            OnPropertyChanged(nameof(HasDownloadedLocalModels));
        }



        // Update downloaded models by purpose collections
        UpdateDownloadedModelsByPurpose();

        OnPropertyChanged(nameof(FilteredDownloadedLocalModels));
    }

    /// <summary>
    /// Updates the downloaded models collections organized by purpose (Chat/Embedding)
    /// Only includes models that are actually downloaded
    /// </summary>
    private void UpdateDownloadedModelsByPurpose()
    {
        DownloadedLocalChatModels.Clear();
        DownloadedLocalEmbeddingModels.Clear();

        foreach (var model in DownloadedLocalModels)
        {
            if (!model.IsDownloaded)
                continue;

            var purpose = model.Usage?.ToLowerInvariant() ?? "";

            if (purpose == "chat")
            {
                DownloadedLocalChatModels.Add(model);
            }
            else if (purpose == "embedding")
            {
                DownloadedLocalEmbeddingModels.Add(model);
            }
            else
            {
                // the models problem fixxxxxxxxxxxxxx me
                DownloadedLocalChatModels.Add(model);
                DownloadedLocalEmbeddingModels.Add(model);
            }
        }

        // Restore saved local model selections after collections are updated
        RestoreSavedLocalModelSelections();
    }

    /// <summary>
    /// Restores saved local model selections after local model collections have been populated
    /// </summary>
    private async void RestoreSavedLocalModelSelections()
    {
        try
        {
            // Only restore if we're in local model scope and not suppressing auto-load
            // We check individual selections inside to allow partial restoration
            if (AIModelProviderScope == "local" && !_suppressAutoLoad)
            {
                // Get current settings from database
                var settings = await _settingsUseCase.GetSettingsUseCaseAsync();

                // Restore chat model selection if not already selected
                if (SelectedLocalChatModel == null && !string.IsNullOrWhiteSpace(settings.AIChatModelId))
                {
                    var localChatModel = DownloadedLocalChatModels.FirstOrDefault(m => m.Id == settings.AIChatModelId);
                    if (localChatModel != null)
                    {
                        SelectedLocalChatModel = localChatModel;
                        _logger.LogDebug("Restored local chat model selection: {ModelName} ({ModelId})", localChatModel.Name, localChatModel.Id);
                    }
                }

                // Restore embedding model selection if not already selected
                if (SelectedLocalEmbeddingModel == null && !string.IsNullOrWhiteSpace(settings.AIEmbeddingModelId))
                {
                    var localEmbeddingModel = DownloadedLocalEmbeddingModels.FirstOrDefault(m => m.Id == settings.AIEmbeddingModelId);
                    if (localEmbeddingModel != null)
                    {
                        SelectedLocalEmbeddingModel = localEmbeddingModel;
                        _logger.LogDebug("Restored local embedding model selection: {ModelName} ({ModelId})", localEmbeddingModel.Name, localEmbeddingModel.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring saved local model selections: {Message}", ex.Message);
        }
    }

    private static AIModel CreateLocalModelViewModel(AIModelDto dto, string? defaultDownloadUrl = null, string? sizeOverride = null, IEnumerable<GgufVariant>? variants = null)
    {
        var purpose = string.IsNullOrWhiteSpace(dto.Purpose)
            ? null
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(dto.Purpose);

        return new AIModel
        {
            Id = dto.Id,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? dto.Id : dto.Name,
            Size = string.IsNullOrWhiteSpace(sizeOverride) ? DetermineModelSize(dto) : sizeOverride,
            Description = dto.Description ?? string.Empty,
            ModelType = purpose,
            Usage = dto.Purpose,
            DefaultDownloadUrl = defaultDownloadUrl,
            Author = string.IsNullOrWhiteSpace(dto.Author) ? "Janhq" : dto.Author,
            Downloads = dto.Downloads > 0 ? dto.Downloads : 13596,
            Likes = dto.Likes
        };
    }

    private static string DetermineModelSize(AIModelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            return "N/A";
        }

        var description = dto.Description;
        // Attempt to extract a token that looks like a size (contains GB)
        var tokens = description.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var sizeToken = tokens.FirstOrDefault(t => t.Contains("GB", StringComparison.OrdinalIgnoreCase) ||
                                                   t.Contains("MB", StringComparison.OrdinalIgnoreCase));
        return sizeToken ?? "N/A";
    }

    private async Task LoadLocalModelsAsync()
    {
        try
        {
            List<ModelInfo>? availableModels = null;
            // var registeredLocalModels = new List<AIModelDto>();

            try
            {
                availableModels = await _settingsUseCase.DownloadAvailableModelsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve available local models for download.");
            }

            // try
            // {
            //     var models = await _settingsUseCase.GetAIModelsByTypeAsync(ModelTypes.Local);
            //     if (models != null)
            //     {
            //         registeredLocalModels = models.ToList();
            //     }
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogWarning(ex, "Failed to retrieve registered local models.");
            // }

            List<AIModelDto>? effectiveCatalog = null;

            _modelVariantCache.Clear();

            if (availableModels != null && availableModels.Count > 0)
            {
                // Convert available models to AIModelDto format (new backend response)
                effectiveCatalog = availableModels.Select(model =>
                {
                    // Build variants (non-default shown in expand) and capture default info
                    var variants = model.GgufFiles?.Select(f => new GgufVariant
                    {
                        Id = f.Filename ?? string.Empty,
                        DisplayName = string.IsNullOrWhiteSpace(f.Filename) ? "Variant" : f.Filename,
                        SizeText = string.IsNullOrWhiteSpace(f.SizeFormatted) ? "N/A" : f.SizeFormatted,
                        DownloadUrl = f.DownloadUrl ?? string.Empty,
                        IsDefault = f.Default
                    }).ToList() ?? new List<GgufVariant>();

                    var defaultVariant = variants.FirstOrDefault(v => v.IsDefault) ?? variants.FirstOrDefault();
                    _modelVariantCache[model.ModelId] = (defaultVariant?.DownloadUrl, defaultVariant?.SizeText, variants);

                    // Prefer the default GGUF file size text when present
                    var ggufSizeText = defaultVariant?.SizeText
                                       ?? model.GgufFiles?.FirstOrDefault()?.SizeFormatted
                                       ?? string.Empty;

                    var description = string.IsNullOrWhiteSpace(model.Description)
                        ? ggufSizeText
                        : string.IsNullOrWhiteSpace(ggufSizeText)
                            ? model.Description
                            : $"{model.Description} {ggufSizeText}";

                    var purpose = string.Equals(model.Purpose, ModelPurposes.Chat, StringComparison.OrdinalIgnoreCase)
                        ? ModelPurposes.Chat
                        : ModelPurposes.Embedding;

                    return new AIModelDto
                    {
                        Id = model.ModelId,
                        Name = string.IsNullOrWhiteSpace(model.ModelName) ? model.ModelId : model.ModelName,
                        Type = ModelTypes.Local,
                        Provider = ModelProviders.Python,
                        Description = description ?? string.Empty,
                        Purpose = purpose,
                        IsActive = true,
                        Author = model.Author,
                        Downloads = model.Downloads,
                        Likes = model.Likes
                    };
                }).ToList();
            }
            // else if (registeredLocalModels.Count > 0)
            // {
            //     // Fallback to registered models if available models fetch failed
            //     effectiveCatalog = registeredLocalModels;
            // }
            else
            {
                effectiveCatalog = null;
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyLocalModelCatalog(effectiveCatalog ?? new List<AIModelDto>());
            });

            await RefreshLocalDownloadStatusesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load local models.");
        }
    }

    private async Task RefreshLocalDownloadStatusesAsync()
    {
        try
        {
            await Task.Delay(1000); // Small delay to avoid rapid polling

            //  IMPORTANT: This method should only be called if we are sure the models exist
            var downloadListResponse = await _settingsUseCase.GetModelsListExistWIthCheackDbAsync();



            if (downloadListResponse?.Success != true || downloadListResponse.Data == null)
            {
                return;
            }

            var downloads = downloadListResponse.Data.Values;
            if (downloads == null || downloads.Count == 0)
            {
                return;
            }
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    bool statusChanged = false;
                    foreach (var download in downloads)
                    {
                        var localModel = DownloadedLocalModels.FirstOrDefault(m =>
                            string.Equals(m.Id, download.ModelId, StringComparison.OrdinalIgnoreCase));

                        if (localModel == null)
                        {
                            continue;
                        }

                        localModel.ActiveProcessId = download.ProcessId;
                        localModel.DownloadProgress = download.Percentage;
                        var isDownloading = download.Status.Equals("downloading", StringComparison.OrdinalIgnoreCase);
                        var isCompleted = download.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || download.Percentage >= 100;

                        bool wasDownloaded = localModel.IsDownloaded;
                        localModel.IsDownloading = isDownloading && !isCompleted;
                        localModel.IsDownloaded = isCompleted;

                        if (isCompleted)
                        {
                            localModel.DownloadProgress = 100;
                        }

                        // Update variants status based on the downloaded file
                        if (isCompleted && !string.IsNullOrEmpty(download.Entypoint))
                        {
                            var downloadedFilename = Path.GetFileName(download.Entypoint);
                            foreach (var variant in localModel.Variants)
                            {
                                bool isMatch = string.Equals(variant.Id, downloadedFilename, StringComparison.OrdinalIgnoreCase);

                                // Only update if changed to avoid unnecessary property change notifications
                                if (variant.IsDownloaded != isMatch)
                                {
                                    variant.IsDownloaded = isMatch;
                                    if (isMatch)
                                    {
                                        variant.DownloadProgress = 100;
                                        variant.IsDownloading = false;
                                    }
                                    else
                                    {
                                        variant.DownloadProgress = 0;
                                    }
                                }
                            }
                        }

                        // Track if download status changed
                        if (wasDownloaded != localModel.IsDownloaded)
                        {
                            statusChanged = true;
                        }
                    }

                    // Update the purpose-based collections if any download status changed
                    if (statusChanged)
                    {
                        UpdateDownloadedModelsByPurpose();
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh local model download states.");
        }
    }

    private void InitializeSettingsNavigationItems()
    {
        SettingsNavigationItems.Add(new SettingsNavigationItem
        {
            Id = "ai-models",
            Title = "AI Models",
            Icon = "/Assets/setting-models.svg",
            IsSelected = false
        });

        SettingsNavigationItems.Add(new SettingsNavigationItem
        {
            Id = "ai-access",
            Title = "File access",
            Icon = "/Assets/settings-files.svg",
            IsSelected = false
        });

        SettingsNavigationItems.Add(new SettingsNavigationItem
        {
            Id = "support",
            Title = "Support",
            Icon = "/Assets/settings-support.svg",
            IsSelected = false
        });
    }

    private void UpdateTabVisibility()
    {
        if (SelectedSettingsItem == null) return;

        IsGeneralSelected = false;
        IsAIAccessSelected = false;
        IsAIModelsSelected = false;
        IsSupportSelected = false;

        switch (SelectedSettingsItem.Id)
        {
            case "ai-models":
                IsAIModelsSelected = true;
                CurrentSettingsTitle = "AI Models";
                CurrentSettingsContent = @"AI Models

- Local providers
- Hosted endpoints
- Databricks integrations";
                break;
            case "ai-access":
                IsAIAccessSelected = true;
                CurrentSettingsTitle = "File access";
                CurrentSettingsContent = @"File access Settings

- File Read Permissions
- Write Permissions
- Allowed File Types
- Security Settings";
                break;
            case "support":
                IsSupportSelected = true;
                CurrentSettingsTitle = "Support";
                CurrentSettingsContent = @"Support

- App Updates
- Feedback & Issues
- Documentation
- Uninstall BAISS";
                break;
        }
    }

    private void RefreshSettingsNavigationItems()
    {
        // Trigger UI refresh for settings navigation items
        OnPropertyChanged(nameof(SettingsNavigationItems));
    }

    private readonly Dictionary<string, System.Threading.CancellationTokenSource> _downloadCancellationTokens = new();

    private bool HasSufficientDiskSpaceForModel(out string? errorMessage)
    {
        errorMessage = null;

        var targetPath = ResolveModelDownloadPath();

        try
        {
            var safePath = string.IsNullOrWhiteSpace(targetPath) ? Environment.CurrentDirectory : targetPath;
            var fullPath = Path.GetFullPath(safePath);
            var driveRoot = Path.GetPathRoot(fullPath);

            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                _logger.LogWarning("Unable to determine drive root for model download path {Path}", safePath);
                errorMessage = "Unable to determine the target drive for downloads. Please verify there is at least 3 GB of free space.";
                return false;
            }

            var driveInfo = new DriveInfo(driveRoot);
            var availableBytes = driveInfo.AvailableFreeSpace;

            // Calculate total space needed: current downloading models + new model
            var currentlyDownloadingCount = DownloadedLocalModels.Count(m => m.IsDownloading);
            var totalRequiredBytes = (currentlyDownloadingCount + 1) * ModelDownloadSizeBytes;

            if (availableBytes < totalRequiredBytes)
            {
                var totalRequiredGb = BytesToGigabytes(totalRequiredBytes);
                var availableGb = BytesToGigabytes(availableBytes);
                var downloadingModelsInfo = currentlyDownloadingCount > 0 ? $" ({currentlyDownloadingCount} concurrent downloads in progress)" : "";
                errorMessage = $"Not enough free space on {driveRoot}. {totalRequiredGb:0.#} GB required{downloadingModelsInfo}, {availableGb:0.#} GB available.";
                _logger.LogWarning("Insufficient disk space for concurrent downloads. Drive={Drive}, TotalRequiredBytes={Required}, AvailableBytes={Available}, DownloadingCount={Count}", driveRoot, totalRequiredBytes, availableBytes, currentlyDownloadingCount);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check disk space for model downloads at path {Path}", targetPath);
            errorMessage = "Unable to check storage space. Please ensure at least 3 GB of free space is available before downloading.";
            return false;
        }
    }

    private static double BytesToGigabytes(long bytes) => bytes / (1024d * 1024d * 1024d);

    private string ResolveModelDownloadPath()
    {
        if (!string.IsNullOrWhiteSpace(WritePermissionPath))
        {
            return WritePermissionPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "Baiss", "Models");
        }

        return Environment.CurrentDirectory;
    }

    private async Task StartLocalModelDownloadAsync(AIModel model, string? downloadUrl = null)
    {
        PendingDeleteLocalModelId = null;
        StopActiveDownloadTracking(model);

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            downloadUrl = model.DefaultDownloadUrl;
        }

        var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        _downloadCancellationTokens[model.Id] = cancellationTokenSource;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            model.IsAwaitingDownloadConfirmation = false;
            model.IsPendingDelete = false;
            model.IsDownloading = true;
            model.IsDownloaded = false;
            model.DownloadProgress = 0.0;
        });

        try
        {
            var response = await _settingsUseCase.StartModelDownloadAsync(model.Id, downloadUrl);
            if (response?.Success != true || response.Data == null || string.IsNullOrWhiteSpace(response.Data.ProcessId))
            {
                var errorMessage = !string.IsNullOrWhiteSpace(response?.Error)
                    ? response!.Error
                    : (response?.Message ?? "Unable to start model download.");

                _logger.LogWarning("Failed to start download for model {ModelId}: {Message}", model.Id, errorMessage);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    model.IsDownloading = false;
                    model.DownloadProgress = 0.0;
                    model.ActiveProcessId = null;
                });

                try { Views.MainWindow.ToastServiceInstance.ShowError(errorMessage, 5000); } catch { }
                return;
            }

            var processId = response.Data.ProcessId;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                model.ActiveProcessId = processId;
            });

            await TrackDownloadProgressAsync(model, processId, cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Download start cancelled for model {ModelId}", model.Id);
        }
        catch (Exception ex) when (IsNetworkError(ex) && !IsNetworkAvailable)
        {
            _logger.LogWarning(ex, "Network error starting download for model {ModelId} - network is unavailable", model.Id);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                model.IsDownloading = false;
                model.DownloadProgress = 0.0;
                model.ActiveProcessId = null;
            });

            // Don't show error toast - network monitoring will handle it
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error starting download for model {ModelId}", model.Id);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                model.IsDownloading = false;
                model.DownloadProgress = 0.0;
                model.ActiveProcessId = null;
            });

            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to start model download.", 5000); } catch { }
        }
        finally
        {
            if (_downloadCancellationTokens.TryGetValue(model.Id, out var existingSource) && ReferenceEquals(existingSource, cancellationTokenSource))
            {
                _downloadCancellationTokens.Remove(model.Id);
            }

            cancellationTokenSource.Dispose();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                model.IsAwaitingDownloadConfirmation = false;
                model.IsPendingDelete = false;
            });
        }
    }

    private async Task StartVariantDownloadAsync(AIModel model, GgufVariant variant)
    {
        var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        _downloadCancellationTokens[model.Id] = cancellationTokenSource;

        try
        {
            var response = await _settingsUseCase.StartModelDownloadAsync(model.Id, variant.DownloadUrl);
            if (response?.Success != true || response.Data == null || string.IsNullOrWhiteSpace(response.Data.ProcessId))
            {
                var errorMessage = !string.IsNullOrWhiteSpace(response?.Error)
                    ? response!.Error
                    : (response?.Message ?? "Unable to start variant download.");

                _logger.LogWarning("Failed to start download for variant {VariantId}: {Message}", variant.Id, errorMessage);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    variant.IsDownloading = false;
                    variant.DownloadProgress = 0.0;
                });

                try { Views.MainWindow.ToastServiceInstance.ShowError(errorMessage, 5000); } catch { }
                return;
            }

            var processId = response.Data.ProcessId;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                model.ActiveProcessId = processId;
            });

            await TrackVariantDownloadProgressAsync(variant, processId, cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Download start cancelled for variant {VariantId}", variant.Id);
        }
        catch (Exception ex) when (IsNetworkError(ex) && !IsNetworkAvailable)
        {
            _logger.LogWarning(ex, "Network error starting download for variant {VariantId} - network is unavailable", variant.Id);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                variant.IsDownloading = false;
                variant.DownloadProgress = 0.0;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error starting download for variant {VariantId}", variant.Id);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                variant.IsDownloading = false;
                variant.DownloadProgress = 0.0;
            });

            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to start variant download.", 5000); } catch { }
        }
        finally
        {
            if (_downloadCancellationTokens.TryGetValue(model.Id, out var existingSource) && ReferenceEquals(existingSource, cancellationTokenSource))
            {
                _downloadCancellationTokens.Remove(model.Id);
            }

            cancellationTokenSource.Dispose();
        }
    }

    private async Task TrackVariantDownloadProgressAsync(GgufVariant variant, string processId, System.Threading.CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        var model = variant.ParentModel;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ModelDownloadProgressResponse? progressResponse = null;

                try
                {
                    progressResponse = await _settingsUseCase.GetModelDownloadProgressAsync(processId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving download progress for variant {VariantId}", variant.Id);

                    if (IsNetworkError(ex) && !IsNetworkAvailable)
                    {
                        _logger.LogInformation("Skipping progress check due to network unavailability for variant {VariantId}", variant.Id);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }
                }

                if (progressResponse?.Success != true || progressResponse.Data == null)
                {
                    if (IsNetworkAvailable)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            variant.IsDownloading = false;
                            variant.IsDownloaded = false;
                        });

                        try { Views.MainWindow.ToastServiceInstance.ShowError($"Unable to fetch progress for variant '{variant.DisplayName}'.", 5000); } catch { }
                        break;
                    }
                    else
                    {
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }
                }

                var data = progressResponse.Data;
                var status = data.Status ?? string.Empty;
                var percentage = ClampPercentage(data.Percentage);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    variant.DownloadProgress = percentage;
                    variant.IsDownloading = !IsDownloadCompleted(status, percentage) && !IsDownloadFailed(status);
                    variant.IsDownloaded = IsDownloadCompleted(status, percentage);
                });

                if (IsDownloadCompleted(status, percentage))
                {
                    // Ensure model is registered in DB before UI updates
                    if (model != null)
                    {
                        try
                        {
                            await _settingsUseCase.UpdateModelAsync(model.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to register model {ModelId} in database", model.Id);
                        }
                    }

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        variant.IsDownloading = false;
                        variant.IsDownloaded = true;
                        variant.DownloadProgress = 100.0;

                        if (model != null)
                        {
                            model.IsDownloaded = true;
                            model.ActiveProcessId = null;
                            UpdateDownloadedModelsByPurpose();

                            var isChat = string.Equals(model.Usage, ModelPurposes.Chat, StringComparison.OrdinalIgnoreCase);
                            var isEmbedding = string.Equals(model.Usage, ModelPurposes.Embedding, StringComparison.OrdinalIgnoreCase);
                            // If usage is not explicitly Chat or Embedding, it's treated as both (see UpdateDownloadedModelsByPurpose)
                            var isUnknown = !isChat && !isEmbedding;

                            if (isChat || isUnknown)
                            {
                                var chatModel = DownloadedLocalChatModels.FirstOrDefault(m => m.Id == model.Id);
                                if (chatModel != null)
                                {
                                    SelectedLocalChatModel = chatModel;
                                    await SaveAIModelSettingsAsync(showSuccessToast: false);
                                    await _settingsUseCase.RestartServerAsync("chat");
                                }
                            }

                            if (isEmbedding || isUnknown)
                            {
                                var embeddingModel = DownloadedLocalEmbeddingModels.FirstOrDefault(m => m.Id == model.Id);
                                if (embeddingModel != null)
                                {
                                    SelectedLocalEmbeddingModel = embeddingModel;
                                    await SaveAIModelSettingsAsync(showSuccessToast: false);
                                    await _settingsUseCase.RestartServerAsync("embedding");
                                }
                            }
                        }
                    });

                    try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"'{variant.DisplayName}' downloaded successfully!", 5000); } catch { }
                    break;
                }

                if (IsDownloadFailed(status))
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        variant.IsDownloading = false;
                        variant.IsDownloaded = false;
                        variant.DownloadProgress = 0.0;
                    });

                    try { Views.MainWindow.ToastServiceInstance.ShowError($"Download failed for variant '{variant.DisplayName}'.", 5000); } catch { }
                    break;
                }

                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Download tracking cancelled for variant {VariantId}", variant.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking download progress for variant {VariantId}", variant.Id);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                variant.IsDownloading = false;
                variant.DownloadProgress = 0.0;
            });
        }
    }

    private async Task TrackDownloadProgressAsync(AIModel model, string processId, System.Threading.CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ModelDownloadProgressResponse? progressResponse = null;

                try
                {
                    progressResponse = await _settingsUseCase.GetModelDownloadProgressAsync(processId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving download progress for model {ModelId}", model.Id);

                    // Check if this is a network-related error
                    if (IsNetworkError(ex) && !IsNetworkAvailable)
                    {
                        // Don't show error toast for network issues when we know network is down
                        // The network monitoring will handle the toast notification
                        _logger.LogInformation("Skipping progress check due to network unavailability for model {ModelId}", model.Id);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }
                }

                if (progressResponse?.Success != true || progressResponse.Data == null)
                {
                    // Only show error if network is available (to avoid duplicate error messages)
                    if (IsNetworkAvailable)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            model.IsDownloading = false;
                            model.IsDownloaded = false;
                            model.ActiveProcessId = null;
                        });

                        try { Views.MainWindow.ToastServiceInstance.ShowError($"Unable to fetch progress for '{model.Name}'.", 5000); } catch { }
                        break;
                    }
                    else
                    {
                        // Network is down, wait and retry
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }
                }

                var data = progressResponse.Data;
                var status = data.Status ?? string.Empty;
                var percentage = ClampPercentage(data.Percentage);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    model.DownloadProgress = percentage;
                    model.IsDownloading = !IsDownloadCompleted(status, percentage) && !IsDownloadFailed(status);
                    model.IsDownloaded = IsDownloadCompleted(status, percentage);
                });

                if (IsDownloadCompleted(status, percentage))
                {
                    // Ensure model is registered in DB before UI updates (which might trigger usage)
                    try
                    {
                        await _settingsUseCase.UpdateModelAsync(model.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to register model {ModelId} in database", model.Id);
                    }

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        model.IsDownloading = false;
                        model.IsDownloaded = true;
                        model.DownloadProgress = 100.0;
                        model.ActiveProcessId = null;

                        // Update the purpose-based collections when download completes
                        UpdateDownloadedModelsByPurpose();

                        // Auto-select model after download completes
                        var isChat = string.Equals(model.Usage, ModelPurposes.Chat, StringComparison.OrdinalIgnoreCase);
                        var isEmbedding = string.Equals(model.Usage, ModelPurposes.Embedding, StringComparison.OrdinalIgnoreCase);
                        var isUnknown = !isChat && !isEmbedding;

                        if (isChat || isUnknown)
                        {
                            var chatModel = DownloadedLocalChatModels.FirstOrDefault(m => m.Id == model.Id);
                            if (chatModel != null)
                            {
                                SelectedLocalChatModel = chatModel;
                                await SaveAIModelSettingsAsync(showSuccessToast: false);
                                await _settingsUseCase.RestartServerAsync("chat");
                            }
                        }

                        if (isEmbedding || isUnknown)
                        {
                            var embeddingModel = DownloadedLocalEmbeddingModels.FirstOrDefault(m => m.Id == model.Id);
                            if (embeddingModel != null)
                            {
                                SelectedLocalEmbeddingModel = embeddingModel;
                                await SaveAIModelSettingsAsync(showSuccessToast: false);
                                await _settingsUseCase.RestartServerAsync("embedding");
                            }
                        }
                    });

                    try
                    {
                        Views.MainWindow.ToastServiceInstance.ShowSuccess($"Model '{model.Name}' downloaded successfully", 4000);
                    }
                    catch { }
                    break;
                }

                if (IsDownloadFailed(status))
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        model.IsDownloading = false;
                        model.IsDownloaded = false;
                        model.ActiveProcessId = null;

                        // Update the purpose-based collections when download fails
                        UpdateDownloadedModelsByPurpose();
                    });

                    try { Views.MainWindow.ToastServiceInstance.ShowError($"Download failed for '{model.Name}'.", 5000); } catch { }
                    break;
                }

                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            _settingsUseCase.StopModelDownloadAsync(processId).ConfigureAwait(false);
            // _settingsUseCase.DeleteModelAsync(model.Id).ConfigureAwait(false);
            _logger.LogInformation("Download monitoring cancelled for model {ModelId}", model.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while monitoring download for model {ModelId}", model.Id);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                model.IsDownloading = false;
            });
        }
        finally
        {
            await RefreshLocalDownloadStatusesAsync();
        }
    }

    private static double ClampPercentage(double value) => Math.Max(0.0, Math.Min(100.0, value));

    private static bool IsDownloadCompleted(string status, double percentage)
    {
        if (percentage >= 99.9)
        {
            return true;
        }

        return status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("finished", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("success", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDownloadFailed(string status)
    {
        return status.Equals("failed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("error", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("stopped", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private void RemoveReadAccessFolder(FolderItem? folder)
    {
        if (folder == null)
        {
            return;
        }

        // Instead of immediately removing, mark as pending removal
        folder.IsPendingRemove = true;

        if (_originalPaths.Contains(folder.Path) && !_removedPaths.Contains(folder.Path))
        {
            _removedPaths.Add(folder.Path);
        }

        CheckReadAccessChanges();

        // Show temporary notification
        ShowTemporaryFolderRemovalNotification();
    }

    private void ShowTemporaryFolderRemovalNotification()
    {
        if (_folderRemovalToast != null)
        {
            return;
        }

        try
        {
            _folderRemovalToast = Views.MainWindow.ToastServiceInstance.ShowPersistent(
                "Removed folders will stop being indexed until you apply or cancel these changes.",
                ToastType.Error);
        }
        catch
        {
            // ignore toast failures
        }
    }

    private void HideTemporaryFolderRemovalNotification()
    {
        if (_folderRemovalToast == null)
        {
            return;
        }

        try
        {
            Views.MainWindow.ToastServiceInstance.Dismiss(_folderRemovalToast);
        }
        catch
        {
            // ignore toast failures
        }
        finally
        {
            _folderRemovalToast = null;
        }
    }

    private void StopActiveDownloadTracking(AIModel model)
    {
        if (_downloadCancellationTokens.TryGetValue(model.Id, out var cancellation))
        {
            cancellation.Cancel();
            _downloadCancellationTokens.Remove(model.Id);
            cancellation.Dispose();
        }

        model.IsDownloading = false;
        model.DownloadProgress = 0.0;
        model.IsAwaitingDownloadConfirmation = false;
        model.IsPendingDelete = false;
        model.ActiveProcessId = null;
        if (PendingDeleteLocalModelId == model.Id)
        {
            PendingDeleteLocalModelId = null;
        }
    }

    private async Task StopRemoteDownloadAsync(string processId)
    {
        try
        {
            await _settingsUseCase.StopModelDownloadAsync(processId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop model download process {ProcessId}", processId);
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // Try to load existing settings first
            SettingsDto? settings = null;
            try
            {
                settings = await _settingsUseCase.GetSettingsUseCaseAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Settings not found"))
            {
                _logger.LogDebug("Settings not found");
            }

            if (settings != null)
            {
                // Load general settings
                LoadGeneralSettingsFromDto(settings);

                // Load AI permissions settings
                LoadAIPermissionsFromDto(settings);

                // Load AI model settings
                LoadAIModelSettingsFromDto(settings);

                // Load schedule settings
                TreeStructureSchedule = settings.TreeStructureSchedule ?? "0 0 0 * * ?";
                TreeStructureScheduleEnabled = settings.TreeStructureScheduleEnabled;

                // Match with options
                SelectedScheduleOption = ScheduleOptions.FirstOrDefault(o => o.CronExpression == TreeStructureSchedule);

                HasScheduleChanges = false;

                _logger.LogDebug("Settings loaded successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Public method to reload settings when navigating to settings view
    /// </summary>
    public async Task ReloadSettingsAsync()
    {
        await LoadSettingsAsync();
    }

    private async Task LoadGeneralSettingsAsync()
    {
        try
        {
            var settings = await _settingsUseCase.GetSettingsUseCaseAsync();
            if (settings != null)
            {
                LoadGeneralSettingsFromDto(settings);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading general settings: {Message}", ex.Message);
        }
    }

    private void LoadGeneralSettingsFromDto(SettingsDto settings)
    {
        // Map performance level to UI
        PerformanceHigh = settings.Performance == PerformanceLevel.High;
        PerformanceMedium = settings.Performance == PerformanceLevel.Medium;
        PerformanceLow = settings.Performance == PerformanceLevel.Small;

        // Map other general settings
        AutoUpdatesEnabled = settings.EnableAutoUpdate;

        // Initialize version display
        if (!string.IsNullOrEmpty(settings.AppVersion))
        {
            CurrentVersionText = $"Version: {settings.AppVersion}";
        }

    }

    private void LoadAIPermissionsFromDto(SettingsDto settings)
    {
        _isLoadingSettings = true;
        try
        {
            AllowReadAccess = settings.AllowFileReading;
            NoReadAccess = !settings.AllowFileReading;
            AllowUpdateFiles = settings.AllowUpdateCreatedFiles;
            AllowCreateFiles = settings.AllowCreateNewFiles;
            WritePermissionPath = settings.NewFilesSavePath ?? @"C:\\User\\Documents\";

            ReadAccessFolders.Clear();
            _originalPaths.Clear();
            if (settings.AllowedPaths != null)
            {
                foreach (var path in settings.AllowedPaths)
                {
                    var folderItem = new FolderItem
                    {
                        Path = path,
                        IsLoading = IsTreeStructureRunning
                    };
                    ReadAccessFolders.Add(folderItem);
                    _originalPaths.Add(path);
                }
            }

            _removedPaths.Clear();

            var allowedExtensions = settings.AllowedFileExtensions?.ToList() ?? new List<string>();
            _originalExtensions.Clear();
            _originalExtensions.AddRange(allowedExtensions);

            AllowDocxFiles = allowedExtensions.Contains("docx");
            AllowXlsFiles = allowedExtensions.Any(ext => ext.Equals("xls", StringComparison.OrdinalIgnoreCase) || ext.Equals("xlsx", StringComparison.OrdinalIgnoreCase));
            AllowPdfFiles = allowedExtensions.Contains("pdf");
            AllowTxtFiles = allowedExtensions.Contains("txt");
            AllowCsvFiles = allowedExtensions.Contains("csv");
            AllowMdFiles = allowedExtensions.Contains("md");

            _removedExtensions.Clear();

            _originalNoReadAccess = NoReadAccess;
            _originalAllowReadAccess = AllowReadAccess;
            _originalAllowUpdateFiles = AllowUpdateFiles;
            _originalAllowCreateFiles = AllowCreateFiles;
            _originalWritePermissionPath = WritePermissionPath ?? @"C:\\User\\Documents\";
            _originalAllowMdFiles = AllowMdFiles;
            _originalAllowDocxFiles = AllowDocxFiles;
            _originalAllowXlsFiles = AllowXlsFiles;
            _originalAllowPdfFiles = AllowPdfFiles;
            _originalAllowTxtFiles = AllowTxtFiles;
            _originalAllowCsvFiles = AllowCsvFiles;
        }
        finally
        {
            _isLoadingSettings = false;
            CheckReadAccessChanges();
            CheckWritePermissionChanges();
            CheckFileTypesChanges();
            ShowReadAccessSuccess = false;
            ShowWritePermissionSuccess = false;
            ShowFileTypesSuccess = false;
        }
    }

    private void LoadAIModelSettingsFromDto(SettingsDto settings)
    {
        _suppressAutoLoad = true;
        try
        {
            var savedType = settings.AIModelType ?? ModelTypes.Local;
            var savedModelId = string.Empty; // legacy removed
            var savedScope = string.IsNullOrWhiteSpace(settings.AIModelProviderScope) ? "local" : settings.AIModelProviderScope.Trim().ToLowerInvariant();

            AIModelType = savedType; // setter suppressed
            SelectedAIModelId = savedModelId; // keep empty
            UpdateAvailableProviders(savedType);

            // Initialize provider scope and corresponding radio button booleans from persisted value
            _aiModelProviderScope = savedScope; // set backing field directly to avoid triggering persistence
            OnPropertyChanged(nameof(AIModelProviderScope));

            _huggingFaceApiKey = settings.HuggingFaceApiKey ?? string.Empty;
            _originalHuggingFaceApiKey = _huggingFaceApiKey;
            OnPropertyChanged(nameof(HuggingFaceApiKey));
            OnPropertyChanged(nameof(IsHuggingFaceTokenSaved));
            (SaveHuggingFaceTokenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

            // Reset all selection flags then set the one matching scope (avoid recursive setter logic during init)
            _isLocalModelsSelected = false;
            _isHostedModelsSelected = false;
            // _isDatabricksModelsSelected = false;

            switch (savedScope)
            {
                case "hosted":
                    _isHostedModelsSelected = true;
                    _isHostedModelFrameExpanded = true;
                    break;
                    // case "databricks":
                    //     _isDatabricksModelsSelected = true;
                    //     _isDatabricksModelFrameExpanded = true;
                    //     // Ensure hosted model type for databricks
                    //     if (AIModelType != ModelTypes.Hosted)
                    //     {
                    //         AIModelType = ModelTypes.Hosted;
                    //     }
                    //     // When switching type to hosted, refresh providers list so Databricks is available
                    //     UpdateAvailableProviders(ModelTypes.Hosted);
                    //     // Preselect Databricks provider if available
                    //     if (AvailableProviders.Contains(ModelProviders.Databricks))
                    //     {
                    //         _selectedProvider = ModelProviders.Databricks; // silent set (no auto load during suppress)
                    //         OnPropertyChanged(nameof(SelectedProvider));
                    //         // Preload credentials for databricks provider
                    //         _ = LoadProviderCredentialsAsync(ModelProviders.Databricks);
                    //     }
                    break;
                default:
                    _isLocalModelsSelected = true; // local
                    _isLocalModelFrameExpanded = true;
                    break;
            }

            OnPropertyChanged(nameof(IsLocalModelsSelected));
            OnPropertyChanged(nameof(IsHostedModelsSelected));
            // OnPropertyChanged(nameof(IsDatabricksModelsSelected));
            OnPropertyChanged(nameof(IsLocalModelFrameExpanded));
            OnPropertyChanged(nameof(IsHostedModelFrameExpanded));
            // OnPropertyChanged(nameof(IsDatabricksModelFrameExpanded));

            // Load separate chat and embedding model selections for Databricks
            LoadSeparateModelSelectionsAsync(settings);

            // Start async initialization without blocking UI thread, using the current (possibly updated) type
            _ = InitializeModelSelectionAsync(AIModelType, savedModelId);
        }
        finally
        {
            _suppressAutoLoad = false;
        }
    }

    // /// <summary>
    // /// Populates the separate Databricks chat and embedding model collections from DatabaseAIModels
    // /// </summary>
    // private void PopulateDatabricksModelCollections(string? desiredChatId = null, string? desiredEmbeddingId = null)
    // {
    //     // Preserve current selections if caller did not specify explicit desired IDs
    //     var preserveChatId = desiredChatId ?? SelectedDatabricksChatModel?.Id;
    //     var preserveEmbeddingId = desiredEmbeddingId ?? SelectedDatabricksEmbeddingModel?.Id;

    //     DatabricksChatModels.Clear();
    //     DatabricksEmbeddingModels.Clear();

    //     var databricksModels = DatabaseAIModels.Where(m => string.Equals(m.Provider, ModelProviders.Databricks, StringComparison.OrdinalIgnoreCase));

    //     foreach (var model in databricksModels)
    //     {
    //         // Use model purpose from database; if missing, default to chat for backward compatibility
    //         var purpose = string.IsNullOrWhiteSpace(model.Purpose) ? ModelPurposes.Chat : model.Purpose;
    //         if (purpose == ModelPurposes.Chat)
    //         {
    //             DatabricksChatModels.Add(new AIModel
    //             {
    //                 Id = model.Id,
    //                 Name = model.Name,
    //                 Size = "N/A",
    //                 Description = model.Description ?? "Databricks Model",
    //                 ModelType = "Chat"
    //             });
    //         }
    //         else if (purpose == ModelPurposes.Embedding)
    //         {
    //             DatabricksEmbeddingModels.Add(new AIModel
    //             {
    //                 Id = model.Id,
    //                 Name = model.Name,
    //                 Size = "N/A",
    //                 Description = model.Description ?? "Databricks Model",
    //                 ModelType = "Embedding"
    //             });
    //         }
    //     }

    //     _logger.LogDebug("Populated {ChatCount} chat models and {EmbeddingCount} embedding models",
    //         DatabricksChatModels.Count, DatabricksEmbeddingModels.Count);

    //     // Determine final target IDs (explicit desired overrides preserved)
    //     var finalChatId = !string.IsNullOrWhiteSpace(desiredChatId) ? desiredChatId : preserveChatId;
    //     var finalEmbeddingId = !string.IsNullOrWhiteSpace(desiredEmbeddingId) ? desiredEmbeddingId : preserveEmbeddingId;

    //     // Always rebind selection objects to collection instances (old references become detached after repopulation)
    //     if (!string.IsNullOrWhiteSpace(finalChatId))
    //     {
    //         var chatItem = DatabricksChatModels.FirstOrDefault(m => m.Id == finalChatId);
    //         if (chatItem != null)
    //         {
    //             if (SelectedDatabricksChatModel?.Id != chatItem.Id)
    //             {
    //                 SelectedDatabricksChatModel = chatItem;
    //                 _logger.LogDebug("Bound chat selection to collection instance: {Id}", chatItem.Id);
    //             }
    //         }
    //         else
    //         {
    //             // Clear if saved ID no longer present
    //             if (SelectedDatabricksChatModel != null)
    //             {
    //                 SelectedDatabricksChatModel = null;
    //                 _logger.LogDebug("Cleared chat selection; saved id {Id} not found", finalChatId);
    //             }
    //         }
    //     }
    //     else
    //     {
    //         // No target -> leave as is or clear if object detached (not in list)
    //         if (SelectedDatabricksChatModel != null && !DatabricksChatModels.Any(m => m.Id == SelectedDatabricksChatModel.Id))
    //         {
    //             SelectedDatabricksChatModel = null;
    //             _logger.LogDebug("Cleared detached chat selection (id {Id})", preserveChatId);
    //         }
    //     }

    //     if (!string.IsNullOrWhiteSpace(finalEmbeddingId))
    //     {
    //         var embItem = DatabricksEmbeddingModels.FirstOrDefault(m => m.Id == finalEmbeddingId);
    //         if (embItem != null)
    //         {
    //             if (SelectedDatabricksEmbeddingModel?.Id != embItem.Id)
    //             {
    //                 SelectedDatabricksEmbeddingModel = embItem;
    //                 _logger.LogDebug("Bound embedding selection to collection instance: {Id}", embItem.Id);
    //             }
    //         }
    //         else
    //         {
    //             if (SelectedDatabricksEmbeddingModel != null)
    //             {
    //                 SelectedDatabricksEmbeddingModel = null;
    //                 _logger.LogDebug("Cleared embedding selection; saved id {Id} not found", finalEmbeddingId);
    //             }
    //         }
    //     }
    //     else
    //     {
    //         if (SelectedDatabricksEmbeddingModel != null && !DatabricksEmbeddingModels.Any(m => m.Id == SelectedDatabricksEmbeddingModel.Id))
    //         {
    //             SelectedDatabricksEmbeddingModel = null;
    //             _logger.LogDebug("Cleared detached embedding selection (id {Id})", preserveEmbeddingId);
    //         }
    //     }
    // }

    private async void LoadSeparateModelSelectionsAsync(SettingsDto settings)
    {
        try
        {
            // Handle local model selections
            if (AIModelProviderScope == "local")
            {
                // Load local models and restore selections
                if (!string.IsNullOrWhiteSpace(settings.AIChatModelId))
                {
                    var localChatModel = DownloadedLocalChatModels.FirstOrDefault(m => m.Id == settings.AIChatModelId);
                    if (localChatModel != null)
                    {
                        SelectedLocalChatModel = localChatModel;
                    }
                }
                if (!string.IsNullOrWhiteSpace(settings.AIEmbeddingModelId))
                {
                    var localEmbeddingModel = DownloadedLocalEmbeddingModels.FirstOrDefault(m => m.Id == settings.AIEmbeddingModelId);
                    if (localEmbeddingModel != null)
                    {
                        SelectedLocalEmbeddingModel = localEmbeddingModel;
                    }
                }
            }
            // Handle hosted model selections (non-Databricks)
            else if (AIModelProviderScope == "hosted")
            {
                // Load hosted models and restore selections
                if (!string.IsNullOrWhiteSpace(settings.AIChatModelId))
                {
                    var hostedChatModel = DownloadedHostedModels.FirstOrDefault(m => m.Id == settings.AIChatModelId);
                    if (hostedChatModel != null)
                    {
                        SelectedHostedChatModel = hostedChatModel;
                    }
                }
                if (!string.IsNullOrWhiteSpace(settings.AIEmbeddingModelId))
                {
                    var hostedEmbeddingModel = DownloadedHostedModels.FirstOrDefault(m => m.Id == settings.AIEmbeddingModelId);
                    if (hostedEmbeddingModel != null)
                    {
                        SelectedHostedEmbeddingModel = hostedEmbeddingModel;
                    }
                }
            }
            // Handle Databricks model selections
            // else if (AIModelProviderScope == "databricks")
            // {
            //     try
            //     {
            //         var all = await _settingsUseCase.GetAvailableAIModelsAsync();
            //         var dbr = all.Where(m => string.Equals(m.Provider, ModelProviders.Databricks, StringComparison.OrdinalIgnoreCase));
            //         DatabaseAIModels.Clear();
            //         foreach (var m in dbr) DatabaseAIModels.Add(m);
            //         OnPropertyChanged(nameof(DatabaseAIModels));
            //         OnPropertyChanged(nameof(HasDatabricksModels));
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger.LogError(ex, "Failed to preload Databricks models for dropdowns");
            //     }

            //     // Do not create detached selection objects here; just trigger population with desired IDs
            //     // Store IDs so deferred collection population can bind
            //     if (!string.IsNullOrWhiteSpace(settings.AIChatModelId))
            //     {
            //         _selectedDatabricksChatModelId = settings.AIChatModelId;
            //         OnPropertyChanged(nameof(SelectedDatabricksChatModelId));
            //     }
            //     if (!string.IsNullOrWhiteSpace(settings.AIEmbeddingModelId))
            //     {
            //         _selectedDatabricksEmbeddingModelId = settings.AIEmbeddingModelId;
            //         OnPropertyChanged(nameof(SelectedDatabricksEmbeddingModelId));
            //     }
            //     PopulateDatabricksModelCollections(settings.AIChatModelId, settings.AIEmbeddingModelId);
            // }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading separate model selections: {Message}", ex.Message);
        }
    }

    private async Task InitializeModelSelectionAsync(string modelType, string savedModelId)
    {
        try
        {
            // ! heere is issue
            var allModels = await _settingsUseCase.GetAIModelsByTypeAsync(modelType);
            AIModelDto? selected = null;
            if (!string.IsNullOrWhiteSpace(savedModelId))
            {
                selected = allModels.FirstOrDefault(m => m.Id == savedModelId);
            }

            if (selected != null)
            {
                // silent provider set
                SetProperty(ref _selectedProvider, selected.Provider, nameof(SelectedProvider));
                await LoadAIModelsByTypeAndProviderAsync(modelType, selected.Provider, selected.Id);
                // Load credentials for initial provider
                await LoadProviderCredentialsAsync(selected.Provider);
                _logger.LogDebug("AI model init: Type={Type}, Provider={Provider}, Model={Model}", modelType, selected.Provider, selected.Id);
            }
            else
            {
                var provider = !string.IsNullOrWhiteSpace(SelectedProvider)
                    ? SelectedProvider
                    : (allModels.FirstOrDefault()?.Provider ?? AvailableProviders.FirstOrDefault() ?? string.Empty);
                SetProperty(ref _selectedProvider, provider, nameof(SelectedProvider));
                await LoadAIModelsByTypeAndProviderAsync(modelType, provider);
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    await LoadProviderCredentialsAsync(provider);
                }
                _logger.LogDebug("AI model init: Type={Type}, Provider={Provider}, Model=<none>", modelType, provider);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI model init failed: {Message}", ex.Message);
        }
    }


    private async Task SaveGeneralSettingsAsync()
    {
        if (IsSaving) return; // Prevent multiple simultaneous saves

        try
        {
            IsSaving = true;

            // Determine performance level from UI
            var performanceLevel = PerformanceLow ? PerformanceLevel.Small :
                                  PerformanceMedium ? PerformanceLevel.Medium :
                                  PerformanceLevel.High;


            var updateGeneraleSettings = new UpdateGeneralSettingsDto
            {
                Performance = performanceLevel,
                EnableAutoUpdate = AutoUpdatesEnabled
            };
            // Call the use case to save general settings
            var result = await _settingsUseCase.UpdateGeneralSettingsAsync(updateGeneraleSettings);

            // Refresh settings from database to reflect saved changes
            await RefreshGeneralSettingsFromDatabaseAsync();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving general settings: {Message}", ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveAIPermissionsAsync()
    {
        if (IsSaving) return; // Prevent multiple simultaneous saves

        IsSaving = true;

        try
        {
            // Check if server is online before saving extension changes
            var isServerOnline = await _externalApiService.CheckServerStatus();
            if (!isServerOnline)
            {
                try { Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000); } catch { }
                return;
            }

            // Collect current file extensions from UI checkboxes
            var currentExtensions = new List<string>();
            if (AllowMdFiles) currentExtensions.Add("md");
            if (AllowDocxFiles) currentExtensions.Add("docx");
            if (AllowXlsFiles)
            {
                currentExtensions.Add("xls");
                currentExtensions.Add("xlsx");
            }
            if (AllowPdfFiles) currentExtensions.Add("pdf");
            if (AllowTxtFiles) currentExtensions.Add("txt");
            if (AllowCsvFiles) currentExtensions.Add("csv");

            // Build the list of extensions to send to the server
            var allowedExtensions = new List<AllowedFileExtensionsDtos>();

            // Add currently selected extensions as valid
            foreach (var ext in currentExtensions)
            {
                allowedExtensions.Add(new AllowedFileExtensionsDtos { Extension = ext, IsValid = true });
            }

            // Find extensions that were originally selected but are now unselected (removed)
            foreach (var originalExt in _originalExtensions)
            {
                if (!currentExtensions.Contains(originalExt))
                {
                    // This extension was removed, mark as invalid
                    allowedExtensions.Add(new AllowedFileExtensionsDtos { Extension = originalExt, IsValid = false });

                }
            }

            // Handle manually removed extensions (from _removedExtensions list)
            foreach (var removedExtension in _removedExtensions)
            {
                if (!allowedExtensions.Any(e => e.Extension == removedExtension))
                {
                    allowedExtensions.Add(new AllowedFileExtensionsDtos { Extension = removedExtension, IsValid = false });
                }
            }

            // Collect paths from UI
            var allowedPaths = new List<AllowedPathsDtos>();


            // Handle removed paths
            foreach (var removedPath in _removedPaths)
            {
                allowedPaths.Add(new AllowedPathsDtos { Path = removedPath, IsValid = false });
            }

            // Add current paths as valid
            foreach (var folderItem in ReadAccessFolders)
            {
                allowedPaths.Add(new AllowedPathsDtos { Path = folderItem.Path, IsValid = true });
            }

            // Create the permissions DTO
            var permissionsDto = new UpdateAiPermissionsDto
            {
                AllowFileReading = AllowReadAccess,
                AllowUpdateCreatedFiles = AllowUpdateFiles,
                AllowCreateNewFiles = AllowCreateFiles,
                NewFilesSavePath = WritePermissionPath ?? "",
                AllowedPaths = allowedPaths.Any() ? allowedPaths : null,
                AllowedFileExtensions = allowedExtensions.Any() ? allowedExtensions : null
            };

            // Save via use case - now handles creation automatically if settings don't exist
            var result = await _settingsUseCase.UpdateAiPermissionsAsync(permissionsDto);

            // Refresh settings from database to reflect saved changes
            await RefreshAIPermissionsFromDatabaseAsync();

            // Clear removed items tracking after successful save
            _removedPaths.Clear();
            _removedExtensions.Clear();

            // Update original tracking lists with current state
            _originalPaths.Clear();
            _originalPaths.AddRange(ReadAccessFolders.Select(f => f.Path));

            _originalExtensions.Clear();
            _originalExtensions.AddRange(currentExtensions);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving AI permissions: {Message}", ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }


    private void CheckReadAccessChanges()
    {
        if (_isLoadingSettings) return;

        var currentPaths = ReadAccessFolders.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var originalPaths = _originalPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool pathsChanged = !currentPaths.SetEquals(originalPaths) || _removedPaths.Any();
        bool hasChanges = NoReadAccess != _originalNoReadAccess ||
                          AllowReadAccess != _originalAllowReadAccess ||
                          pathsChanged;

        if (HasReadAccessChanges != hasChanges)
        {
            HasReadAccessChanges = hasChanges;
        }

        if (hasChanges)
        {
            ShowReadAccessSuccess = false;
            if (IsTreeStructurePaused)
            {
                _resumePaths.Clear();
                foreach (var folder in ReadAccessFolders)
                {
                    if (!string.IsNullOrWhiteSpace(folder.Path))
                    {
                        if (!_resumePaths.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
                        {
                            _resumePaths.Add(folder.Path);
                        }
                    }
                }
                SetResumePending(true);
            }
            else
            {
                _resumePaths.Clear();
                SetResumePending(false);
            }
        }
        else
        {
            HideTemporaryFolderRemovalNotification();
        }
    }

    private void SetResumePending(bool value)
    {
        if (_isResumePending == value)
        {
            return;
        }

        _isResumePending = value;
        OnPropertyChanged(nameof(ShowPauseResumeButton));
        OnPropertyChanged(nameof(ShowPauseButton));
        OnPropertyChanged(nameof(ShowContinueButton));
        OnPropertyChanged(nameof(CanResumeTree));
    }

    private void CheckWritePermissionChanges()
    {
        if (_isLoadingSettings) return;

        bool hasChanges = AllowUpdateFiles != _originalAllowUpdateFiles ||
                          AllowCreateFiles != _originalAllowCreateFiles ||
                          !string.Equals(WritePermissionPath ?? string.Empty, _originalWritePermissionPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        if (HasWritePermissionChanges != hasChanges)
        {
            HasWritePermissionChanges = hasChanges;
        }

        if (hasChanges)
        {
            ShowWritePermissionSuccess = false;
        }
    }

    private void CheckFileTypesChanges()
    {
        if (_isLoadingSettings) return;

        bool hasChanges = AllowMdFiles != _originalAllowMdFiles ||
                          AllowDocxFiles != _originalAllowDocxFiles ||
                          AllowXlsFiles != _originalAllowXlsFiles ||
                          AllowPdfFiles != _originalAllowPdfFiles ||
                          AllowTxtFiles != _originalAllowTxtFiles ||
                          AllowCsvFiles != _originalAllowCsvFiles;

        if (HasFileTypesChanges != hasChanges)
        {
            HasFileTypesChanges = hasChanges;
        }

        if (hasChanges)
        {
            ShowFileTypesSuccess = false;
            if (!_isLoadingSettings)
            {
                ShowFileTypesDeletionWarningMessage();
            }
        }
        else
        {
            HideFileTypesDeletionWarning();
        }
    }

    private void ShowFileTypesDeletionWarningMessage()
    {
        if (_fileTypesWarningToast != null)
        {
            return;
        }

        try
        {
            _fileTypesWarningToast = Views.MainWindow.ToastServiceInstance.ShowPersistent(
                "Warning: AI-created files of newly disallowed types will be deleted when you apply these changes.",
                ToastType.Error);
        }
        catch
        {
            // ignore toast errors
        }
    }

    private void HideFileTypesDeletionWarning()
    {
        if (_fileTypesWarningToast == null)
        {
            return;
        }

        try
        {
            Views.MainWindow.ToastServiceInstance.Dismiss(_fileTypesWarningToast);
        }
        catch
        {
            // ignore toast errors
        }
        finally
        {
            _fileTypesWarningToast = null;
        }
    }

    private async Task ApplyReadAccessChangesAsync()
    {
        var wasPaused = IsTreeStructurePaused;
        var hadPendingChanges = HasReadAccessChanges;
        var isResumeAction = wasPaused && !hadPendingChanges;
        var hadFolderRemovals = _removedPaths.Count > 0;

        var resumeSnapshot = isResumeAction
            ? _resumePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        if (isResumeAction)
        {
            SetResumePending(true);
            IsTreeStructurePaused = false;
            _settingsUseCase.ResumeBackgroundOperations();
        }
        else if (wasPaused)
        {
            SetResumePending(true);
        }

        try
        {
            // Remove folders marked for removal from the collection
            var foldersToRemove = ReadAccessFolders.Where(f => f.IsPendingRemove).ToList();
            foreach (var folder in foldersToRemove)
            {
                ReadAccessFolders.Remove(folder);
            }

            var allowedPaths = new List<AllowedPathsDtos>();

            foreach (var removedPath in _removedPaths)
            {
                allowedPaths.Add(new AllowedPathsDtos { Path = removedPath, IsValid = false });
            }

            var resumeSet = new HashSet<string>(resumeSnapshot, StringComparer.OrdinalIgnoreCase);

            if (isResumeAction)
            {
                foreach (var resumePath in resumeSnapshot)
                {
                    allowedPaths.Add(new AllowedPathsDtos { Path = resumePath, IsValid = false });
                    allowedPaths.Add(new AllowedPathsDtos { Path = resumePath, IsValid = true });
                }
            }

            foreach (var resumePath in resumeSnapshot)
            {
                // ensured resumed above; skip duplicating below
            }

            foreach (var folder in ReadAccessFolders)
            {
                if (string.IsNullOrWhiteSpace(folder.Path))
                {
                    continue;
                }

                if (resumeSet.Contains(folder.Path))
                {
                    continue;
                }

                allowedPaths.Add(new AllowedPathsDtos { Path = folder.Path, IsValid = true });
            }

            var dto = new UpdateAiPermissionsDto
            {
                AllowFileReading = AllowReadAccess,
                AllowUpdateCreatedFiles = AllowUpdateFiles,
                AllowCreateNewFiles = AllowCreateFiles,
                NewFilesSavePath = WritePermissionPath ?? string.Empty,
                AllowedPaths = allowedPaths.Any() ? allowedPaths : null,
                AllowedFileExtensions = null
            };

            await _settingsUseCase.UpdateAiPermissionsAsync(dto);

            _originalPaths.Clear();
            _originalPaths.AddRange(ReadAccessFolders.Select(f => f.Path));
            _removedPaths.Clear();
            _resumePaths.Clear();
            if (wasPaused && !isResumeAction)
            {
                foreach (var path in _originalPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        _resumePaths.Add(path);
                    }
                }
            }
            _originalAllowReadAccess = AllowReadAccess;
            _originalNoReadAccess = NoReadAccess;

            HasReadAccessChanges = false;
            ShowReadAccessSuccess = true;
            HideTemporaryFolderRemovalNotification();
            if (hadFolderRemovals)
            {
                try { Views.MainWindow.ToastServiceInstance.ShowSuccess("The folder path has been removed.", 3000); } catch { }
            }

            if (wasPaused && !isResumeAction)
            {
                IsTreeStructurePaused = true;
                SetResumePending(true);
            }
            else if (isResumeAction)
            {
                SetResumePending(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying read access changes: {Message}", ex.Message);
            if (wasPaused)
            {
                SetResumePending(true);
                if (!isResumeAction)
                {
                    IsTreeStructurePaused = true;
                }
            }
        }
    }

    private void CancelReadAccessChanges()
    {
        _isLoadingSettings = true;
        try
        {
            if (PendingFolderRemoval != null)
            {
                PendingFolderRemoval.IsPendingRemove = false;
                PendingFolderRemoval = null;
            }

            NoReadAccess = _originalNoReadAccess;
            AllowReadAccess = _originalAllowReadAccess;

            ReadAccessFolders.Clear();
            foreach (var path in _originalPaths)
            {
                ReadAccessFolders.Add(new FolderItem { Path = path, IsLoading = IsTreeStructureRunning });
            }

            _removedPaths.Clear();
            _resumePaths.Clear();
            HasReadAccessChanges = false;
            ShowReadAccessSuccess = false;
            SetResumePending(false);
            HideTemporaryFolderRemovalNotification();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task ApplyWritePermissionChangesAsync()
    {
        try
        {
            var dto = new UpdateAiPermissionsDto
            {
                AllowFileReading = AllowReadAccess,
                AllowUpdateCreatedFiles = AllowUpdateFiles,
                AllowCreateNewFiles = AllowCreateFiles,
                NewFilesSavePath = WritePermissionPath ?? string.Empty,
                AllowedPaths = null,
                AllowedFileExtensions = null
            };

            await _settingsUseCase.UpdateAiPermissionsAsync(dto);

            _originalAllowUpdateFiles = AllowUpdateFiles;
            _originalAllowCreateFiles = AllowCreateFiles;
            _originalWritePermissionPath = WritePermissionPath ?? string.Empty;

            HasWritePermissionChanges = false;
            ShowWritePermissionSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying write permission changes: {Message}", ex.Message);
        }
    }

    private void CancelWritePermissionChanges()
    {
        _isLoadingSettings = true;
        try
        {
            AllowUpdateFiles = _originalAllowUpdateFiles;
            AllowCreateFiles = _originalAllowCreateFiles;
            WritePermissionPath = _originalWritePermissionPath;

            HasWritePermissionChanges = false;
            ShowWritePermissionSuccess = false;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task ApplyFileTypesChangesAsync()
    {
        try
        {
            // Check if server is online before saving extension changes
            var isServerOnline = await _externalApiService.CheckServerStatus();
            if (!isServerOnline)
            {
                try { Views.MainWindow.ToastServiceInstance.ShowError("Server is starting, wait 30 seconds.", 4000); } catch { }
                return;
            }

            var currentExtensions = new List<string>();
            if (AllowMdFiles) currentExtensions.Add("md");
            if (AllowDocxFiles) currentExtensions.Add("docx");
            if (AllowXlsFiles)
            {
                currentExtensions.Add("xls");
                currentExtensions.Add("xlsx");
            }
            if (AllowPdfFiles) currentExtensions.Add("pdf");
            if (AllowTxtFiles) currentExtensions.Add("txt");
            if (AllowCsvFiles) currentExtensions.Add("csv");

            var allowedExtensions = new List<AllowedFileExtensionsDtos>();

            foreach (var ext in currentExtensions)
            {
                allowedExtensions.Add(new AllowedFileExtensionsDtos { Extension = ext, IsValid = true });
            }

            foreach (var originalExt in _originalExtensions)
            {
                if (!currentExtensions.Contains(originalExt))
                {
                    allowedExtensions.Add(new AllowedFileExtensionsDtos { Extension = originalExt, IsValid = false });
                }
            }

            foreach (var removedExtension in _removedExtensions)
            {
                if (!allowedExtensions.Any(e => string.Equals(e.Extension, removedExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    allowedExtensions.Add(new AllowedFileExtensionsDtos { Extension = removedExtension, IsValid = false });
                }
            }

            var dto = new UpdateAiPermissionsDto
            {
                AllowFileReading = AllowReadAccess,
                AllowUpdateCreatedFiles = AllowUpdateFiles,
                AllowCreateNewFiles = AllowCreateFiles,
                NewFilesSavePath = WritePermissionPath ?? string.Empty,
                AllowedPaths = null,
                AllowedFileExtensions = allowedExtensions.Any() ? allowedExtensions : null
            };

            await _settingsUseCase.UpdateAiPermissionsAsync(dto);

            _originalAllowMdFiles = AllowMdFiles;
            _originalAllowDocxFiles = AllowDocxFiles;
            _originalAllowXlsFiles = AllowXlsFiles;
            _originalAllowPdfFiles = AllowPdfFiles;
            _originalAllowTxtFiles = AllowTxtFiles;
            _originalAllowCsvFiles = AllowCsvFiles;
            _originalExtensions.Clear();
            _originalExtensions.AddRange(currentExtensions);
            _removedExtensions.Clear();

            HasFileTypesChanges = false;
            ShowFileTypesSuccess = true;
            HideFileTypesDeletionWarning();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying file types changes: {Message}", ex.Message);
        }
    }

    private void CancelFileTypesChanges()
    {
        _isLoadingSettings = true;
        try
        {
            AllowMdFiles = _originalAllowMdFiles;
            AllowDocxFiles = _originalAllowDocxFiles;
            AllowXlsFiles = _originalAllowXlsFiles;
            AllowPdfFiles = _originalAllowPdfFiles;
            AllowTxtFiles = _originalAllowTxtFiles;
            AllowCsvFiles = _originalAllowCsvFiles;
            _removedExtensions.Clear();

            HasFileTypesChanges = false;
            ShowFileTypesSuccess = false;
            HideFileTypesDeletionWarning();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void CheckForAIModelChanges()
    {
        // Don't track changes while loading settings
        if (_isLoadingSettings || IsLoadingModels)
        {
            return;
        }

        var hasChanges = _originalSelectedModel?.Id != SelectedDbAIModel?.Id;

        if (HasAIModelChanges != hasChanges)
        {
            HasAIModelChanges = hasChanges;
        }
    }

    private async Task ApplyAIModelChangesAsync()
    {
        try
        {
            _logger.LogInformation("Applying AI model changes");

            // Save the model selection
            await SaveAIModelSettingsAsync(showSuccessToast: false);
            // Update the original to the new selection
            _originalSelectedModel = SelectedDbAIModel;
            HasAIModelChanges = false;

            try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"AI model updated: {SelectedDbAIModel?.Name}", 3000); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying AI model changes: {Message}", ex.Message);
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to save AI model changes", 5000); } catch { }
        }
    }

    private void CancelAIModelChanges()
    {
        _isLoadingSettings = true;
        try
        {
            // Revert to the original selection
            SelectedDbAIModel = _originalSelectedModel;
            HasAIModelChanges = false;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    // // Databricks Credentials Change Tracking
    // private void CheckForDatabricksCredentialsChanges()
    // {
    //     // Don't track changes while loading settings
    //     if (_isLoadingSettings || _suppressAutoLoad)
    //     {
    //         return;
    //     }

    //     // Check if URL or API key changed from original
    //     var urlChanged = _originalDatabricksUrl != DatabricksUrl;
    //     var apiKeyChanged = !string.IsNullOrWhiteSpace(DatabricksApiKey) && _originalDatabricksApiKey != DatabricksApiKey;
    //     var hasChanges = urlChanged || apiKeyChanged;

    //     if (HasDatabricksCredentialsChanges != hasChanges)
    //     {
    //         HasDatabricksCredentialsChanges = hasChanges;
    //     }
    // }

    // private void CancelDatabricksCredentialsChanges()
    // {
    //     _isLoadingSettings = true;
    //     try
    //     {
    //         // Revert to the original values
    //         DatabricksUrl = _originalDatabricksUrl ?? string.Empty;
    //         DatabricksApiKey = _originalDatabricksApiKey ?? string.Empty;
    //         HasDatabricksCredentialsChanges = false;
    //     }
    //     finally
    //     {
    //         _isLoadingSettings = false;
    //     }
    // }

    private Task ApplyHostedModelUrlAsync()
    {
        try
        {
            var rawUrl = HostedModelUrl?.Trim();
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                _logger.LogWarning("Hosted model URL is empty.");
                try { Views.MainWindow.ToastServiceInstance.ShowError("Enter a hosted model URL before connecting.", 4000); } catch { }
                return Task.CompletedTask;
            }

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogWarning("Hosted model URL is invalid: {Url}", rawUrl);
                try { Views.MainWindow.ToastServiceInstance.ShowError("Enter a valid http(s) URL.", 4000); } catch { }
                return Task.CompletedTask;
            }

            HostedModelUrl = parsedUri.ToString().TrimEnd('/');
            CredentialValidationError = null;
            CredentialValidationSuccess = null;

            try { Views.MainWindow.ToastServiceInstance.ShowSuccess("Hosted endpoint saved.", 3000); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying hosted model URL: {Message}", ex.Message);
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to save hosted URL. Check logs for details.", 5000); } catch { }
        }

        return Task.CompletedTask;
    }

    // private async Task ApplyDatabricksCredentialsAsync()
    // {
    //     try
    //     {
    //         _logger.LogInformation("Applying Databricks credentials");
    //         ... [METHOD BODY OMITTED FOR BREVITY - commented out]
    //     }
    // }

    // private async Task AddDatabricksModelAsync()
    // {
    //     try
    //     {
    //         _logger.LogInformation("Adding new Databricks model");
    //         ... [METHOD BODY OMITTED FOR BREVITY - commented out]
    //     }
    // }

    // private async Task DeleteDatabricksModelAsync(AIModelDto? model)
    // {
    //     if (model == null)
    //     {
    //         _logger.LogWarning("Cannot delete model: model is null");
    //         return;
    //     }
    //     ... [METHOD BODY OMITTED FOR BREVITY - commented out]
    // }

    // private async Task<bool> ValidateDatabricksServingEndpointAsync(string servingEndpointName)
    // {
    //     try
    //     {
    //         if (string.IsNullOrWhiteSpace(DatabricksWorkspaceUrl) || string.IsNullOrWhiteSpace(DatabricksApiKey))
    //         {
    //             _logger.LogWarning("Databricks credentials not available for validation");
    //             return false;
    //         }
    //         ... [METHOD BODY OMITTED FOR BREVITY - commented out]
    //     }
    // }

    private void StopBackgroundOperations()
    {
        try
        {
            _settingsUseCase.StopBackgroundOperations();
            IsTreeStructureRunning = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping background operations: {Message}", ex.Message);
        }
    }

    private async Task CheckTreeStructureThreadAsync()
    {
        if (IsCheckingTreeStructure) return;

        try
        {
            // Console.WriteLine(" --------------------- >>>>>>>>>>>>>> Checking tree structure thread status...");
            IsCheckingTreeStructure = true;
            var isRunning = await _settingsUseCase.CheckTreeStructureThread();
            IsTreeStructureRunning = isRunning;
            var result = _settingsUseCase.CheckUpdateCheckThread();
            if (result != null && result == "FINISHED")
            {
                CheckNowButtonText = "Check Now";
            }
            if (result != null && result == "FAILED")
            {
                CheckNowButtonText = "Failed - Retry";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tree structure thread: {Message}", ex.Message);
        }
        finally
        {
            IsCheckingTreeStructure = false;
        }
    }
    private void UpdateAvailableProviders(string modelType)
    {
        var previous = _selectedProvider; // use backing field to avoid triggering setter

        var list = new List<string>();
        if (modelType == ModelTypes.Local)
        {
            list.AddRange(new[] { ModelProviders.Python, ModelProviders.Ollama });
        }
        else if (modelType == ModelTypes.Hosted)
        {
            list.AddRange(new[] { ModelProviders.OpenAI, ModelProviders.Anthropic, /* ModelProviders.Databricks, */ ModelProviders.Azure });
        }

        // If previous provider is still valid, move it to front for consistency
        if (!string.IsNullOrWhiteSpace(previous) && list.Contains(previous))
        {
            list.Remove(previous);
            list.Insert(0, previous);
        }

        AvailableProviders.Clear();
        foreach (var p in list) AvailableProviders.Add(p);
        OnPropertyChanged(nameof(AvailableProviders));

        // Only set silently during suppressed phase; otherwise allow normal setter
        if (_suppressAutoLoad)
        {
            if (list.Count == 0)
            {
                SetProperty(ref _selectedProvider, string.Empty, nameof(SelectedProvider));
            }
            else if (string.IsNullOrWhiteSpace(previous) || !list.Contains(previous))
            {
                SetProperty(ref _selectedProvider, list[0], nameof(SelectedProvider));
            }
            // else keep previous silently
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SelectedProvider) || !AvailableProviders.Contains(SelectedProvider))
            {
                SelectedProvider = AvailableProviders.FirstOrDefault() ?? string.Empty;
            }
        }
    }

    private async Task LoadAIModelsByTypeAndProviderAsync(string type, string provider, string? preserveModelId = null)
    {
        // Prevent concurrent loading
        if (IsLoadingModels) return;

        try
        {
            IsLoadingModels = true;
            // Reduced verbosity: only log once per explicit provider change (handled elsewhere) or init.

            IEnumerable<AIModelDto> models;
            if (provider == ModelProviders.Databricks)
            {
                // Some Databricks entries may have varying Type values; load all and filter by provider (case-insensitive)
                var all = await _settingsUseCase.GetAvailableAIModelsAsync();
                models = all.Where(m => string.Equals(m.Provider, ModelProviders.Databricks, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                models = await _settingsUseCase.GetAIModelsByTypeAsync(type);
            }

            // Filter by provider (case-insensitive)
            var filteredModels = models.Where(m => string.Equals(m.Provider, provider, StringComparison.OrdinalIgnoreCase));

            DatabaseAIModels.Clear();
            foreach (var model in filteredModels)
            {
                DatabaseAIModels.Add(model);
            }

            // Update placeholder text based on whether models exist
            ModelPlaceholderText = DatabaseAIModels.Any()
                ? "Choose a model to start with..."
                : "Add new model to select";

            // Ensure UI updates are reflected
            OnPropertyChanged(nameof(DatabaseAIModels));
            // OnPropertyChanged(nameof(HasDatabricksModels));

            // // Populate separate Databricks chat and embedding collections
            // PopulateDatabricksModelCollections();

            // Small delay to ensure UI has processed the collection update
            await Task.Delay(50);

            // Update selection - need to do this after the collection is populated
            AIModelDto? selectedModel = null;
            string modelIdToFind = preserveModelId ?? SelectedAIModelId;
            if (!string.IsNullOrEmpty(modelIdToFind))
            {
                selectedModel = DatabaseAIModels.FirstOrDefault(m => m.Id == modelIdToFind);
            }

            if (selectedModel != null)
            {
                SelectedDbAIModel = selectedModel;
                // Update the selected model ID if we were using a preserved ID
                if (!string.IsNullOrEmpty(preserveModelId))
                {
                    SelectedAIModelId = preserveModelId;
                }
                // Model found and selected
            }
            else if (DatabaseAIModels.Any())
            {
                // Clear the selected model ID since it doesn't match this provider
                SelectedAIModelId = string.Empty;
                SelectedDbAIModel = DatabaseAIModels.FirstOrDefault();
                _logger.LogDebug("No matching model found, cleared selection and selected first available: {ModelName}",
                    SelectedDbAIModel?.Name ?? "None");
            }
            else
            {
                // Clear the selected model ID since no models are available
                SelectedAIModelId = string.Empty;
                SelectedDbAIModel = null;
                _logger.LogDebug("No models available for type {Type} and provider {Provider}", type, provider);
            }

            // Models loaded successfully
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AI models by type {Type} and provider {Provider}: {Message}",
                type, provider, ex.Message);
            DatabaseAIModels.Clear();
            SelectedDbAIModel = null;
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    private void ScheduleAutoSave()
    {
        // Debounce multiple rapid changes
        _autoSaveCts?.Cancel();
        var cts = new System.Threading.CancellationTokenSource();
        _autoSaveCts = cts;
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_autoSaveDelay, token);
                if (token.IsCancellationRequested) return;
                await SaveAIModelSettingsAsync(showSuccessToast: false);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-save failed: {Message}", ex.Message);
            }
        });
    }

    private void MarkCredentialsDirty()
    {
        if (_suppressAutoLoad) return;
        HasPendingCredentialChanges = true;
        CredentialValidationError = null;
        CredentialValidationSuccess = null;
    }

    private async Task LoadProviderCredentialsAsync(string provider)
    {
        if (_providerCredentialService == null) return;
        try
        {
            var cred = await _providerCredentialService.GetAsync(provider);
            _suppressAutoLoad = true;
            HasStoredSecret = cred != null && !string.IsNullOrWhiteSpace(cred.EncryptedSecret);
            IsEditingSecret = false;
            ProviderSecretInput = string.Empty;
            IsSecretVisible = false;
            _cachedProviderSecret = cred?.SecretPlain;
            // reset
            ProviderOrganizationId = null; ProviderModelOverride = null;
            AzureEndpoint = null; AzureDeploymentName = null; AzureApiVersion = null;
            // DatabricksWorkspaceUrl = null; DatabricksServingEndpoint = null; DatabricksModelName = null;
            if (cred != null && !string.IsNullOrWhiteSpace(cred.ExtraJson))
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(cred.ExtraJson!) ?? new();
                dict.TryGetValue("organizationId", out _providerOrganizationId);
                dict.TryGetValue("model", out _providerModelOverride);
                dict.TryGetValue("endpoint", out _azureEndpoint);
                dict.TryGetValue("deploymentName", out _azureDeploymentName);
                dict.TryGetValue("apiVersion", out _azureApiVersion);
                // dict.TryGetValue("workspaceUrl", out _databricksWorkspaceUrl);
                // dict.TryGetValue("servingEndpoint", out _databricksServingEndpoint);
                // dict.TryGetValue("modelName", out _databricksModelName);
                OnPropertyChanged(nameof(ProviderOrganizationId));
                OnPropertyChanged(nameof(ProviderModelOverride));
                OnPropertyChanged(nameof(AzureEndpoint));
                OnPropertyChanged(nameof(AzureDeploymentName));
                OnPropertyChanged(nameof(AzureApiVersion));
                // OnPropertyChanged(nameof(DatabricksWorkspaceUrl));
                // OnPropertyChanged(nameof(DatabricksServingEndpoint));
                // OnPropertyChanged(nameof(DatabricksModelName));

                // // For Databricks, also populate the UI fields and store originals
                // if (provider == ModelProviders.Databricks)
                // {
                //     OnPropertyChanged(nameof(DatabricksUrl)); // This gets from DatabricksWorkspaceUrl

                //     // Store original values for change tracking
                //     _originalDatabricksUrl = DatabricksUrl;
                //     _originalDatabricksApiKey = cred?.SecretPlain ?? string.Empty;

                //     // Set the API key in the UI (but don't show it, it's a password field)
                //     if (!string.IsNullOrEmpty(cred?.SecretPlain))
                //     {
                //         _providerSecretInput = cred.SecretPlain;
                //         OnPropertyChanged(nameof(DatabricksApiKey));
                //     }

                //     HasDatabricksCredentialsChanges = false;
                // }
            }
            HasPendingCredentialChanges = false;
            CredentialValidationError = null;
            CredentialValidationSuccess = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load provider credentials for {Provider}", provider);
        }
        finally
        {
            _suppressAutoLoad = false;
        }
    }

    private async Task SaveProviderCredentialsAsync()
    {
        if (_providerCredentialService == null) return;
        if (string.IsNullOrWhiteSpace(SelectedProvider)) return;
        if (IsSavingCredentials || !HasPendingCredentialChanges) return;
        try
        {
            var provider = SelectedProvider;
            var normalizedInput = string.IsNullOrWhiteSpace(ProviderSecretInput) ? null : ProviderSecretInput.Trim();
            string? secretCandidate;
            bool replaceSecret;
            if (!HasStoredSecret)
            {
                secretCandidate = normalizedInput;
                replaceSecret = true;
            }
            else if (IsEditingSecret)
            {
                secretCandidate = normalizedInput;
                replaceSecret = !string.IsNullOrWhiteSpace(secretCandidate);
            }
            else
            {
                secretCandidate = string.IsNullOrWhiteSpace(_cachedProviderSecret) ? null : _cachedProviderSecret.Trim();
                replaceSecret = false;
            }
            if (string.IsNullOrWhiteSpace(secretCandidate))
            {
                CredentialValidationError = "Secret is required before saving credentials.";
                CredentialValidationSuccess = null;
                try { Views.MainWindow.ToastServiceInstance.ShowError(CredentialValidationError, 5000); } catch { }
                return;
            }
            var extras = new Dictionary<string, string?>();
            if (IsOpenAISelected || IsAnthropicSelected)
            {
                if (!string.IsNullOrWhiteSpace(ProviderModelOverride)) extras["model"] = ProviderModelOverride;
                if (IsOpenAISelected && !string.IsNullOrWhiteSpace(ProviderOrganizationId)) extras["organizationId"] = ProviderOrganizationId;
            }
            if (IsAzureSelected)
            {
                if (!string.IsNullOrWhiteSpace(AzureEndpoint)) extras["endpoint"] = AzureEndpoint;
                if (!string.IsNullOrWhiteSpace(AzureDeploymentName)) extras["deploymentName"] = AzureDeploymentName;
                if (!string.IsNullOrWhiteSpace(AzureApiVersion)) extras["apiVersion"] = AzureApiVersion;
            }
            // if (IsDatabricksSelected)
            // {
            //     if (!string.IsNullOrWhiteSpace(DatabricksWorkspaceUrl)) extras["workspaceUrl"] = DatabricksWorkspaceUrl;
            //     if (!string.IsNullOrWhiteSpace(DatabricksServingEndpoint)) extras["servingEndpoint"] = DatabricksServingEndpoint;
            //     if (!string.IsNullOrWhiteSpace(DatabricksModelName)) extras["modelName"] = DatabricksModelName;
            // }
            var secretType = provider switch
            {
                // var p when p == ModelProviders.Databricks => "token",
                _ => "api_key"
            };
            string? validationError = null;
            try
            {
                IsValidatingCredentials = true;
                validationError = await ValidateProviderCredentialsAsync(provider, secretCandidate);
            }
            finally
            {
                IsValidatingCredentials = false;
            }

            if (!string.IsNullOrEmpty(validationError))
            {
                CredentialValidationError = validationError;
                CredentialValidationSuccess = null;
                try
                {
                    var toastMessage = string.IsNullOrWhiteSpace(validationError)
                        ? "URL/Token incorrect, try again."
                        : validationError;
                    Views.MainWindow.ToastServiceInstance.ShowError(toastMessage, 5000);
                }
                catch
                {
                    // ignore toast errors
                }
                return;
            }

            CredentialValidationError = null;

            IsSavingCredentials = true;
            await _providerCredentialService.SaveAsync(provider, replaceSecret ? secretCandidate : null, secretType, extras, replaceSecret);
            _cachedProviderSecret = secretCandidate;
            CredentialValidationSuccess = $"Credentials verified and saved for {provider}.";
            HasPendingCredentialChanges = false;

            try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"Saved credentials for {provider}"); } catch { }

            await LoadProviderCredentialsAsync(provider);
            CredentialValidationSuccess = $"Credentials verified and saved for {provider}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save provider credentials");
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to save credentials", 5000); } catch { }
        }
        finally
        {
            IsSavingCredentials = false;
        }
    }

    private async Task<string?> ValidateProviderCredentialsAsync(string provider, string secret)
    {
        try
        {
            var normalizedProvider = provider.ToLowerInvariant();
            var trimmedSecret = secret.Trim();
            return normalizedProvider switch
            {
                var p when p == ModelProviders.OpenAI => await ValidateOpenAiAsync(trimmedSecret),
                var p when p == ModelProviders.Anthropic => await ValidateAnthropicAsync(trimmedSecret),
                var p when p == ModelProviders.Azure => await ValidateAzureAsync(trimmedSecret),
                var p when p == ModelProviders.Databricks => await ValidateDatabricksAsync(trimmedSecret),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential validation failed for provider {Provider}", provider);
            return $"Credential validation failed: {ex.Message}";
        }
    }

    private async Task<string?> ValidateOpenAiAsync(string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models?limit=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        using var response = await _httpClient.SendAsync(request);
        return await EvaluateValidationResponseAsync(response, "OpenAI");
    }

    private async Task<string?> ValidateAnthropicAsync(string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        request.Headers.Add("x-api-key", secret);
        request.Headers.Add("anthropic-version", "2023-06-01");
        using var response = await _httpClient.SendAsync(request);
        return await EvaluateValidationResponseAsync(response, "Anthropic");
    }

    private async Task<string?> ValidateAzureAsync(string secret)
    {
        if (string.IsNullOrWhiteSpace(AzureEndpoint))
        {
            return "Azure endpoint is required.";
        }

        var endpoint = AzureEndpoint!.Trim();
        if (!endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"https://{endpoint.TrimStart('/')}";
        }
        endpoint = endpoint.TrimEnd('/');

        var apiVersion = string.IsNullOrWhiteSpace(AzureApiVersion) ? "2024-02-01" : AzureApiVersion!.Trim();
        var uri = $"{endpoint}/openai/deployments?api-version={apiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("api-key", secret);
        using var response = await _httpClient.SendAsync(request);
        return await EvaluateValidationResponseAsync(response, "Azure OpenAI");
    }

    private async Task<string?> ValidateDatabricksAsync(string secret)
    {
        // if (string.IsNullOrWhiteSpace(DatabricksWorkspaceUrl))
        // {
        //     return "Databricks workspace URL is required.";
        // }

        // Note: Serving endpoint is now stored per-model, not in credentials
        // Models are added separately after credentials are saved

        // var baseUrl = DatabricksWorkspaceUrl!.Trim();
        // if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        // {
        //     baseUrl = $"https://{baseUrl.TrimStart('/')}";
        // }
        // baseUrl = baseUrl.TrimEnd('/');

        // // Try to validate by making a simple request to list endpoints
        // // If this fails, we'll just accept the credentials without validation
        // var endpoint = $"{baseUrl}/api/2.0/serving-endpoints";

        // try
        // {
        //     using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        //     request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        //     using var response = await _httpClient.SendAsync(request);

        //     if (response.IsSuccessStatusCode)
        //     {
        //         return null; // Validation successful
        //     }
        //     else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
        //              response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        //     {
        //         // Token or URL is invalid
        //         return "URL/Token incorrect, try again.";
        //     }
        //     else
        //     {
        //         // Other errors (like 404) - we'll accept the credentials anyway
        //         // since the token might not have permission to list endpoints
        //         // but can still invoke the specific endpoint
        //         _logger.LogWarning("Databricks validation returned {StatusCode}, but accepting credentials anyway", response.StatusCode);
        //         return null;
        //     }
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogWarning(ex, "Databricks validation failed, but accepting credentials anyway");
        //     return null; // Accept credentials even if validation fails
        // }
        return null; // Databricks validation disabled
    }

    private static async Task<string?> EvaluateValidationResponseAsync(HttpResponseMessage response, string providerName)
    {
        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            // ignore body read errors
        }

        var summary = SummarizeBody(body);
        return summary == null
            ? $"{providerName} validation failed ({(int)response.StatusCode} {response.ReasonPhrase})."
            : $"{providerName} validation failed ({(int)response.StatusCode} {response.ReasonPhrase}): {summary}";
    }

    private static string? SummarizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var trimmed = body.Trim();
        if (trimmed.Length > 240)
        {
            return trimmed.Substring(0, 240) + "...";
        }
        return trimmed;
    }

    private async Task AddProviderModelAsync(bool showSuccessToast = true)
    {
        if (!CanAddModel)
        {
            return;
        }

        try
        {
            IsManagingModels = true;
            var dto = await _settingsUseCase.AddAIModelAsync(new CreateAIModelDto
            {
                Id = NewModelId.Trim(),
                Name = NewModelName.Trim(),
                Type = AIModelType,
                Provider = SelectedProvider,
                Description = string.IsNullOrWhiteSpace(NewModelDescription) ? null : NewModelDescription!.Trim(),
                Purpose = NewModelPurpose.Trim(),
                IsActive = true
            });

            if (!DatabaseAIModels.Any(m => m.Id == dto.Id))
            {
                DatabaseAIModels.Add(dto);
                // OnPropertyChanged(nameof(HasDatabricksModels));

                // // Update the separate chat/embedding collections if this is a Databricks model
                // if (dto.Provider == "databricks")
                // {
                //     // Always auto-select the newly added model by passing its ID to PopulateDatabricksModelCollections
                //     if (dto.Purpose == "chat")
                //     {
                //         PopulateDatabricksModelCollections(desiredChatId: dto.Id);
                //     }
                //     else if (dto.Purpose == "embedding")
                //     {
                //         PopulateDatabricksModelCollections(desiredEmbeddingId: dto.Id);
                //     }
                //     else
                //     {
                //         // Fallback: just populate without forcing selection if purpose is unknown
                //         PopulateDatabricksModelCollections();
                //     }
                // }
            }
            SelectedDbAIModel = dto;

            NewModelId = string.Empty;
            NewModelName = string.Empty;
            NewModelDescription = string.Empty;
            NewModelPurpose = Baiss.Domain.Entities.ModelPurposes.Chat; // Reset to default chat purpose

            if (showSuccessToast)
            {
                try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"Model {dto.Name} added.", 3000); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add AI model {ModelId}", NewModelId);
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to add model", 5000); } catch { }
        }
        finally
        {
            IsManagingModels = false;
        }
    }

    private async Task RemoveProviderModelAsync(AIModelDto? model)
    {
        if (model == null) return;

        try
        {
            IsManagingModels = true;
            await _settingsUseCase.DeleteAIModelAsync(model.Id);

            var existing = DatabaseAIModels.FirstOrDefault(m => m.Id == model.Id);
            if (existing != null)
            {
                DatabaseAIModels.Remove(existing);
            }

            if (SelectedDbAIModel?.Id == model.Id)
            {
                SelectedDbAIModel = DatabaseAIModels.FirstOrDefault();
            }

            // Clear purpose-specific selections if deleted
            // if (model.Provider == "databricks")
            // {
            //     if (SelectedDatabricksChatModel?.Id == model.Id)
            //     {
            //         SelectedDatabricksChatModel = null;
            //     }
            //     if (SelectedDatabricksEmbeddingModel?.Id == model.Id)
            //     {
            //         SelectedDatabricksEmbeddingModel = null;
            //     }
            // }

            try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"Model {model.Name} removed.", 3000); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove AI model {ModelId}", model.Id);
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to remove model", 5000); } catch { }
        }
        finally
        {
            IsManagingModels = false;
        }
    }

    private async Task LoadAIModelsByTypeAsync(string type)
    {
        try
        {
            var models = await _settingsUseCase.GetAIModelsByTypeAsync(type);

            DatabaseAIModels.Clear();
            foreach (var model in models)
            {
                DatabaseAIModels.Add(model);
            }

            // Update selection if current model is not available
            if (!DatabaseAIModels.Any(m => m.Id == SelectedAIModelId))
            {
                SelectedDbAIModel = DatabaseAIModels.FirstOrDefault();
            }
            else
            {
                // Find and set the currently selected model
                SelectedDbAIModel = DatabaseAIModels.FirstOrDefault(m => m.Id == SelectedAIModelId);
            }

            _logger.LogDebug("Loaded {Count} AI models of type {Type}", models.Count(), type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AI models by type {Type}: {Message}", type, ex.Message);
        }
    }

    private async Task LoadAllAIModelsAsync()
    {
        try
        {
            var models = await _settingsUseCase.GetAvailableAIModelsAsync();

            DatabaseAIModels.Clear();
            foreach (var model in models)
            {
                DatabaseAIModels.Add(model);
            }

            _logger.LogDebug("Loaded {Count} AI models", models.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AI models: {Message}", ex.Message);
        }
    }

    private async Task SaveAIModelSettingsAsync(bool showSuccessToast = true, bool allowEmptySelections = false)
    {
        if (IsSaving) return; // Prevent multiple simultaneous saves

        try
        {
            IsSaving = true;

            // Validate that we have a model selected based on the model type
            if (AIModelType == ModelTypes.Local)
            {
                // For local models, check local selections
                if (!allowEmptySelections && SelectedLocalChatModel == null && SelectedLocalEmbeddingModel == null)
                {
                    _logger.LogWarning("No local chat or embedding model selected, cannot save.");
                    try { Views.MainWindow.ToastServiceInstance.Show("Select a chat or embedding model", 4000); } catch { }
                    return;
                }
            }
            else if (AIModelType == ModelTypes.Hosted)
            {
                // if (SelectedProvider == ModelProviders.Databricks)
                // {
                //     // For Databricks models, check databricks selections
                //     if (!allowEmptySelections && SelectedDatabricksChatModel == null && SelectedDatabricksEmbeddingModel == null)
                //     {
                //         _logger.LogWarning("No Databricks chat or embedding model selected, cannot save.");
                //         try { Views.MainWindow.ToastServiceInstance.Show("Select a chat or embedding model", 4000); } catch { }
                //         return;
                //     }
                // }
                // else
                // {
                // For other hosted providers (OpenAI, Anthropic, Azure), we might want to add validation here
                // For now, just log that hosted non-Databricks models are being saved without specific validation
                _logger.LogInformation("Saving hosted model settings for provider: {Provider}", SelectedProvider);
                // }
            }

            string? chatModelId = null;
            string? embeddingModelId = null;

            // Determine which models to save based on the model type
            if (AIModelType == ModelTypes.Local)
            {
                chatModelId = allowEmptySelections && SelectedLocalChatModel == null ? string.Empty : SelectedLocalChatModel?.Id;
                embeddingModelId = allowEmptySelections && SelectedLocalEmbeddingModel == null ? string.Empty : SelectedLocalEmbeddingModel?.Id;
            }
            else if (AIModelType == ModelTypes.Hosted)
            {
                // if (SelectedProvider == ModelProviders.Databricks)
                // {
                //     chatModelId = allowEmptySelections && SelectedDatabricksChatModel == null ? string.Empty : SelectedDatabricksChatModel?.Id;
                //     embeddingModelId = allowEmptySelections && SelectedDatabricksEmbeddingModel == null ? string.Empty : SelectedDatabricksEmbeddingModel?.Id;
                // }
                // else
                // {
                // For other hosted providers, check hosted model selections
                chatModelId = allowEmptySelections && SelectedHostedChatModel == null ? string.Empty : SelectedHostedChatModel?.Id;
                embeddingModelId = allowEmptySelections && SelectedHostedEmbeddingModel == null ? string.Empty : SelectedHostedEmbeddingModel?.Id;
                // }
            }

            var updateDto = new UpdateAIModelSettingsDto
            {
                AIModelType = AIModelType,
                AIChatModelId = chatModelId,
                AIEmbeddingModelId = embeddingModelId,
                AIModelProviderScope = AIModelProviderScope,
                HuggingFaceApiKey = HuggingFaceApiKey
            };

            var result = await _settingsUseCase.UpdateAIModelSettingsAsync(updateDto);

            _logger.LogInformation("Successfully saved AI model settings: Type={Type}, Chat={Chat}, Embedding={Embedding}, Provider={Provider}",
                AIModelType, updateDto.AIChatModelId, updateDto.AIEmbeddingModelId, SelectedProvider);

            if (showSuccessToast)
            {
                string savedName;
                if (AIModelType == ModelTypes.Local)
                {
                    savedName = SelectedLocalChatModel?.Name ?? SelectedLocalEmbeddingModel?.Name ?? (allowEmptySelections ? "Cleared selections" : "Model");
                }
                else if (AIModelType == ModelTypes.Hosted)
                {
                    // if (SelectedProvider == ModelProviders.Databricks)
                    // {
                    //     savedName = SelectedDatabricksChatModel?.Name ?? SelectedDatabricksEmbeddingModel?.Name ?? (allowEmptySelections ? "Cleared selections" : "Model");
                    // }
                    // else
                    // {
                    savedName = SelectedHostedChatModel?.Name ?? SelectedHostedEmbeddingModel?.Name ?? SelectedDbAIModel?.Name ?? (allowEmptySelections ? "Cleared selections" : "Model");
                    // }
                }
                else
                {
                    savedName = SelectedDbAIModel?.Name ?? (allowEmptySelections ? "Cleared selections" : "Model");
                }
                try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"AI model settings saved: {savedName}", 3000); } catch { }
            }

            // Reload settings from database to reflect the saved changes
            await RefreshAIModelSettingsFromDatabaseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving AI model settings: {Message}", ex.Message);
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to save AI model settings", 5000); } catch { }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveAIModelSettingsAndValidateAsync()
    {
        await SaveAIModelSettingsAsync(showSuccessToast: false);
        // Notify that chat model selection changed for validation
        OnPropertyChanged("ChatModelSavedForValidation");
    }

    private async Task SaveEmbeddingModelSettingsAsync(bool showSuccessToast = true, bool allowEmptySelections = false)
    {
        if (IsSaving) return; // Prevent multiple simultaneous saves

        try
        {
            IsSaving = true;

            // Validate that we have a model selected based on the model type
            if (AIModelType == ModelTypes.Local)
            {
                // For local models, check local selections
                if (!allowEmptySelections && SelectedLocalEmbeddingModel == null)
                {
                    _logger.LogWarning("No local embedding model selected, cannot save.");
                    try { Views.MainWindow.ToastServiceInstance.Show("Select an embedding model", 4000); } catch { }
                    return;
                }
            }

            string? embeddingModelId = null;

            // Determine which models to save based on the model type
            if (AIModelType == ModelTypes.Local)
            {
                embeddingModelId = allowEmptySelections && SelectedLocalEmbeddingModel == null ? string.Empty : SelectedLocalEmbeddingModel?.Id;
            }
            else if (AIModelType == ModelTypes.Hosted)
            {
                embeddingModelId = allowEmptySelections && SelectedHostedEmbeddingModel == null ? string.Empty : SelectedHostedEmbeddingModel?.Id;
            }

            var updateDto = new UpdateAIModelSettingsDto
            {
                AIModelType = AIModelType,
                AIChatModelId = null, // Explicitly null to avoid affecting chat settings
                AIEmbeddingModelId = embeddingModelId,
                AIModelProviderScope = AIModelProviderScope,
                HuggingFaceApiKey = HuggingFaceApiKey
            };

            var result = await _settingsUseCase.UpdateAIModelSettingsAsync(updateDto);

            _logger.LogInformation("Successfully saved embedding model settings: Type={Type}, Embedding={Embedding}, Provider={Provider}",
                AIModelType, updateDto.AIEmbeddingModelId, SelectedProvider);

            if (showSuccessToast)
            {
                string savedName;
                if (AIModelType == ModelTypes.Local)
                {
                    savedName = SelectedLocalEmbeddingModel?.Name ?? (allowEmptySelections ? "Cleared selection" : "Model");
                }
                else if (AIModelType == ModelTypes.Hosted)
                {
                    savedName = SelectedHostedEmbeddingModel?.Name ?? (allowEmptySelections ? "Cleared selection" : "Model");
                }
                else
                {
                    savedName = (allowEmptySelections ? "Cleared selection" : "Model");
                }
                try { Views.MainWindow.ToastServiceInstance.ShowSuccess($"Embedding model saved: {savedName}", 3000); } catch { }
            }

            // Reload settings from database to reflect the saved changes
            await RefreshAIModelSettingsFromDatabaseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving embedding model settings: {Message}", ex.Message);
            try { Views.MainWindow.ToastServiceInstance.ShowError("Failed to save embedding model settings", 5000); } catch { }
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Refreshes AI model settings from the database after save
    /// </summary>
    private async Task RefreshAIModelSettingsFromDatabaseAsync()
    {
        try
        {
            _logger.LogDebug("Refreshing AI model settings from database");

            // Get the updated settings from the database
            SettingsDto? refreshedSettings = null;
            try
            {
                refreshedSettings = await _settingsUseCase.GetSettingsUseCaseAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Settings not found"))
            {
                _logger.LogWarning("Settings not found after save attempt, this should not happen");
                return;
            }

            if (refreshedSettings != null)
            {
                var updatedModelType = refreshedSettings.AIModelType ?? ModelTypes.Local;
                if (!string.IsNullOrWhiteSpace(refreshedSettings.AIModelProviderScope) && refreshedSettings.AIModelProviderScope != AIModelProviderScope)
                {
                    _aiModelProviderScope = refreshedSettings.AIModelProviderScope; // set backing to avoid recursion
                    OnPropertyChanged(nameof(AIModelProviderScope));
                }
                bool typeChanged = AIModelType != updatedModelType;

                if (typeChanged)
                {
                    _logger.LogDebug("Applying changed settings from DB: Type {OldType}->{NewType}", AIModelType, updatedModelType);

                    AIModelType = updatedModelType; // triggers provider calculation (suppressed logic already handles)

                    if (AvailableProviders.Count > 0)
                    {
                        var provider = AvailableProviders.Contains(SelectedProvider) ? SelectedProvider : AvailableProviders[0];
                        await LoadAIModelsByTypeAndProviderAsync(AIModelType, provider);
                    }
                }
                else
                {
                    _logger.LogDebug("DB settings unchanged; skipping model reload");
                }

                // Reload separate model selections (both local and databricks)
                LoadSeparateModelSelectionsAsync(refreshedSettings);

                // Store the original model selection for change tracking
                _originalSelectedModel = SelectedDbAIModel;
                HasAIModelChanges = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing AI model settings from database: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Refreshes AI permissions settings from the database after save
    /// </summary>
    private async Task RefreshAIPermissionsFromDatabaseAsync()
    {
        try
        {
            _logger.LogDebug("Refreshing AI permissions settings from database");

            // Get the updated settings from the database
            SettingsDto? refreshedSettings = null;
            try
            {
                refreshedSettings = await _settingsUseCase.GetSettingsUseCaseAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Settings not found"))
            {
                _logger.LogWarning("Settings not found after save attempt, this should not happen");
                return;
            }

            if (refreshedSettings != null)
            {
                // Reload AI permissions with fresh data from DB
                LoadAIPermissionsFromDto(refreshedSettings);
                _logger.LogDebug("Successfully refreshed AI permissions settings from database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing AI permissions settings from database: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Refreshes general settings from the database after save
    /// </summary>
    private async Task RefreshGeneralSettingsFromDatabaseAsync()
    {
        try
        {
            _logger.LogDebug("Refreshing general settings from database");

            // Get the updated settings from the database
            SettingsDto? refreshedSettings = null;
            try
            {
                refreshedSettings = await _settingsUseCase.GetSettingsUseCaseAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Settings not found"))
            {
                _logger.LogWarning("Settings not found after save attempt, this should not happen");
                return;
            }

            if (refreshedSettings != null)
            {
                // Reload general settings with fresh data from DB
                LoadGeneralSettingsFromDto(refreshedSettings);
                _logger.LogDebug("Successfully refreshed general settings from database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing general settings from database: {Message}", ex.Message);
        }
    }

    private async Task<bool> CheckIfEmbeddingModelSelectedAsync()
    {
        try
        {
            var settings = await _settingsUseCase.GetSettingsUseCaseAsync();
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

            var availableModels = await _settingsUseCase.GetAvailableAIModelsAsync();
            var embeddingModel = availableModels.FirstOrDefault(m => m.Id == embeddingModelId);
            if (embeddingModel == null)
            {
                return false; // Embedding model not found or inactive
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking embedding model selection");
            return false;
        }
    }

    public void Dispose()
    {
        if (SelectedLocalEmbeddingModel != null)
        {
            SelectedLocalEmbeddingModel.PropertyChanged -= OnSelectedEmbeddingModelPropertyChanged;
        }

        _statusTimer?.Dispose();
        _statusTimer = null;
        HideFileTypesDeletionWarning();
        foreach (var source in _downloadCancellationTokens.Values.ToList())
        {
            try { source.Cancel(); }
            catch { }
            source.Dispose();
        }
        _downloadCancellationTokens.Clear();
        HideTemporaryFolderRemovalNotification();
        _networkStatusDebounceTimer?.Dispose();
        _networkStatusDebounceTimer = null;

        // Clean up network monitoring
        try
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            _logger.LogInformation("Network monitoring cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing from network events");
        }
    }

    #region Network Monitoring

    private void InitializeNetworkMonitoring()
    {
        try
        {
            // Check initial network status
            bool initialStatus = NetworkInterface.GetIsNetworkAvailable();
            IsNetworkAvailable = initialStatus;

            _logger.LogInformation("Network monitoring initialized. Initial status: {IsAvailable}", initialStatus);

            // Subscribe to network changes
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            // Also subscribe to network address changes for more reliable detection
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            _logger.LogInformation("Network event subscriptions completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize network monitoring");
            IsNetworkAvailable = true; // Assume available if we can't check
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        try
        {
            _logger.LogInformation("Network availability changed: {IsAvailable}", e.IsAvailable);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Only update if the status actually changed to prevent duplicate toast notifications
                if (IsNetworkAvailable != e.IsAvailable)
                {
                    IsNetworkAvailable = e.IsAvailable;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling network availability change");
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        try
        {
            bool isAvailable = NetworkInterface.GetIsNetworkAvailable();
            _logger.LogInformation("Network address changed. Current availability: {IsAvailable}", isAvailable);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Only update if the status actually changed to prevent duplicate toast notifications
                if (IsNetworkAvailable != isAvailable)
                {
                    IsNetworkAvailable = isAvailable;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling network address change");
        }
    }

    private void DebounceNetworkStatusChange(bool isAvailable)
    {
        _pendingNetworkStatus = isAvailable;

        // Reset the debounce timer
        _networkStatusDebounceTimer?.Stop();
        _networkStatusDebounceTimer?.Dispose();

        _networkStatusDebounceTimer = new System.Timers.Timer(500); // 500ms debounce
        _networkStatusDebounceTimer.Elapsed += (sender, e) =>
        {
            _networkStatusDebounceTimer?.Stop();
            _networkStatusDebounceTimer?.Dispose();
            _networkStatusDebounceTimer = null;

            // Execute the actual network status change handling
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnNetworkStatusChanged(_pendingNetworkStatus);
            });
        };
        _networkStatusDebounceTimer.AutoReset = false;
        _networkStatusDebounceTimer.Start();
    }

    private void OnNetworkStatusChanged(bool isAvailable)
    {
        _logger.LogInformation("Network status changed to: {IsAvailable}", isAvailable);

        if (!isAvailable)
        {
            // Network lost
            HandleNetworkLoss();
        }
        else
        {
            // Network restored
            HandleNetworkRestored();
        }
    }

    private void HandleNetworkLoss()
    {
        var now = DateTime.Now;
        _logger.LogWarning("Handling network loss. Has active downloads: {HasDownloads}, Already shown toast: {HasShownToast}, Last toast: {LastToast}",
            HasActiveDownloads(), _hasShownNetworkLossToast, _lastNetworkLossToastTime);

        // Prevent multiple network loss toasts within the minimum interval
        if (now - _lastNetworkLossToastTime < MinToastInterval)
        {
            _logger.LogInformation("Skipping network loss toast - too soon after last one (interval: {Interval})", now - _lastNetworkLossToastTime);
            return;
        }

        if (!_hasShownNetworkLossToast)
        {
            _hasShownNetworkLossToast = true;
            _lastNetworkLossToastTime = now;

            try
            {
                // Double-check that we haven't already created a network loss toast
                if (_networkLossToast != null)
                {
                    _logger.LogWarning("Network loss toast already exists, skipping duplicate");
                    return;
                }

                string message = HasActiveDownloads()
                    ? "Network connection lost. Downloads will resume when connection is restored."
                    : "Network connection lost.";

                // Create the toast manually and add it to the collection so we can remove it later
                _networkLossToast = new ToastMessage
                {
                    Message = message,
                    Type = ToastType.Error
                };

                Views.MainWindow.ToastServiceInstance.Messages.Add(_networkLossToast);
                _logger.LogInformation("Network loss toast shown at {Time}", now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show network loss toast");
                _hasShownNetworkLossToast = false; // Reset flag on failure
            }
        }
        else
        {
            _logger.LogInformation("Network loss toast already shown, skipping duplicate");
        }
    }

    private void HandleNetworkRestored()
    {
        var now = DateTime.Now;
        _logger.LogInformation("Handling network restoration. Had shown toast: {HasShownToast}, Last restored toast: {LastToast}",
            _hasShownNetworkLossToast, _lastNetworkRestoredToastTime);

        // Prevent multiple network restored toasts within the minimum interval
        if (now - _lastNetworkRestoredToastTime < MinToastInterval)
        {
            _logger.LogInformation("Skipping network restored toast - too soon after last one (interval: {Interval})", now - _lastNetworkRestoredToastTime);
            return;
        }

        if (_hasShownNetworkLossToast)
        {
            _hasShownNetworkLossToast = false;
            _lastNetworkRestoredToastTime = now;

            // Remove the network loss toast if it's still there
            if (_networkLossToast != null)
            {
                try
                {
                    Views.MainWindow.ToastServiceInstance.Messages.Remove(_networkLossToast);
                    _networkLossToast = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove network loss toast");
                }
            }

            try
            {
                string message = HasActiveDownloads()
                    ? "Network connection restored. Downloads will continue."
                    : "Network connection restored.";

                Views.MainWindow.ToastServiceInstance.ShowSuccess(
                    message,
                    4000
                );
                _logger.LogInformation("Network restored toast shown at {Time}", now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show network restored toast");
            }
        }
        else
        {
            _logger.LogInformation("No network loss toast was shown, skipping restoration toast");
        }
    }

    private bool HasActiveDownloads()
    {
        return DownloadedLocalModels.Any(m => m.IsDownloading);
    }

    private bool IsNetworkError(Exception exception)
    {
        return exception is HttpRequestException ||
               exception is TaskCanceledException ||
               exception is TimeoutException ||
               (exception.Message?.Contains("network", StringComparison.OrdinalIgnoreCase) == true) ||
               (exception.Message?.Contains("connection", StringComparison.OrdinalIgnoreCase) == true);
    }

    #endregion
}
