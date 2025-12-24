using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Baiss.UI.ViewModels;
using Baiss.UI.Views;
using Baiss.UI.Services;
using Baiss.Infrastructure.Db;
using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Baiss.Application.UseCases;
using Baiss.Application.Interfaces;
using Baiss.Infrastructure.Repositories;
using Baiss.Infrastructure.Services;
using Baiss.Infrastructure.Services.Security;
using Baiss.UI.Configuration;
using Baiss.Infrastructure.Configuration;
using System.Threading.Tasks;
using DotNetEnv;
using Baiss.Infrastructure.Services.AI;
using Baiss.Infrastructure.Extensions;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.ComponentModel;
using ShimSkiaSharp;


namespace Baiss.UI
{
    public partial class App : Avalonia.Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }
        private IJobSchedulerService? _jobScheduler;
        private Process? _pythonServerProcess;
        private List<Process> _llamaCppServerProcesses = new List<Process>();
        private AIProvidersTestService? _aiProvidersTestService;
        private ILogger<App>? _logger;
        private bool _isCleanedUp = false;

        public override void Initialize()
        {
            // Load environment variables first
            Env.Load();

            AvaloniaXamlLoader.Load(this);
            ConfigureServices();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    var configService = ServiceProvider?.GetService<ConfigService>();
                    configService?.CreateConfigFile();

                    // Initialize database before starting the UI
                    // This is production-safe because we handle exceptions gracefully
                    DatabaseStartup.InitializeDatabase();
                    // Initialize Quartz database in main database
                    _logger = ServiceProvider?.GetService<ILogger<App>>();
                    if (_logger != null)
                    {
                        QuartzConfiguration.InitializeQuartzDatabase(_logger);
                    }
                    // ! Start python code server
                    var launchPythonService = ServiceProvider?.GetService<ILaunchPythonServerService>();
                    _pythonServerProcess = launchPythonService?.LaunchPythonServer();
                    LaunchLlamaCppServerAsync();

                }
                catch (DatabaseMigrationException ex)
                {
                    // Show error dialog and exit gracefully
                    ShowDatabaseErrorAndExit(ex.Message);
                    return;
                }
                catch (Exception ex)
                {
                    // Handle unexpected errors during database initialization
                    ShowDatabaseErrorAndExit($"Failed to initialize database: {ex.Message}");
                    return;
                }

                // Start the job scheduler
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _jobScheduler = ServiceProvider?.GetService<IJobSchedulerService>();
                        if (_jobScheduler != null)
                        {
                            await _jobScheduler.StartAsync();

                            // Schedule tree structure update job
                            await ScheduleTreeStructureUpdateJob();
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = ServiceProvider?.GetService<ILogger<App>>();
                        logger?.LogError(ex, "Failed to start job scheduler");
                    }
                });

                // Start the AI Providers test service
                /*_ = Task.Run(async () =>
                {
                    try
                    {
                        _aiProvidersTestService = ServiceProvider?.GetService<AIProvidersTestService>();
                        if (_aiProvidersTestService != null)
                        {
                            await _aiProvidersTestService.StartAsync(CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = ServiceProvider?.GetService<ILogger<App>>();
                        logger?.LogError(ex, "Failed to start AI Providers test service");
                    }
                });*/

                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                var mainWindow = new MainWindow
                {
                    DataContext = ServiceProvider?.GetService<MainWindowViewModel>() ?? throw new InvalidOperationException("Failed to resolve MainWindowViewModel"),
                };
                // var floatingIcon = new FloatingIconWindow();

                // var windowManager = new WindowManager(mainWindow, floatingIcon);

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                // floatingIcon.Show();


                // Handle application shutdown
                desktop.ShutdownRequested += OnShutdownRequested;

                // First-time validation: show setup modal if chat model is missing/invalid
                try
                {
                    if (mainWindow.DataContext is Baiss.UI.ViewModels.MainWindowViewModel vm)
                    {
                        _ = vm.ValidateChatModelAsync();
                    }
                }
                catch { }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging using your existing LoggingConfiguration
            var loggerFactory = LoggingConfiguration.CreateLoggerFactory();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Add database connection factory
            services.AddSingleton<Baiss.Infrastructure.Interfaces.IDbConnectionFactory, Baiss.Infrastructure.Db.DapperConnectionFactory>();

            // Add Quartz.NET services (will use main database)
            services.AddQuartzServices();

            // Add repositories
            services.AddScoped<IConversationRepository, ConversationRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<ISettingsRepository, SettingsRepository>();
            services.AddScoped<IModelRepository, ModelRepository>();
            services.AddScoped<IAvailableModelRepository, AvailableModelRepository>();
            services.AddScoped<IProviderCredentialRepository, ProviderCredentialRepository>();
            services.AddScoped<IResponseChoiceRepository, ResponseChoiceRepository>();
            services.AddScoped<ISearchPathScoreRepository, SearchPathScoreRepository>();

            // Add services
            services.AddSingleton<HttpClient>(); // Add HttpClient for external API calls
            services.AddHttpClient(); // Add HttpClient factory for DatabricksTestService
            services.AddScoped<IExternalApiService, ExternalApiService>();
            services.AddScoped<IAssistantService, AssistantService>();
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
            services.AddScoped<IProviderCredentialService, ProviderCredentialService>();
            services.AddTransient<IDialogService, DialogService>();
            services.AddScoped<ITreeStructureService, TreeStructureService>();
            services.AddScoped<ILaunchServerService, LaunchServerService>();
            services.AddScoped<ILaunchPythonServerService, LaunchPythonServerService>();
            services.AddScoped<IGetLlamaServerService, Baiss.Infrastructure.Services.GetLlamaServerService>();

            // Add Semantic Kernel AI services
            // services.AddSemanticKernelServices();

            // Add ConfigService first (needed to create config file)
            services.AddScoped<ConfigService>();

            // Ensure config file exists before registering PythonBridge service
            EnsureConfigFileExists(services);

            // Add PythonBridge singleton service (config file should exist now)
            // try
            // {
            //     services.AddPythonBridgeService();
            //     services.AddScoped<PythonBridgeDiagnosticsService>(); // Add diagnostics service
            // }
            // catch (Exception ex)
            // {
            //     var logger = loggerFactory.CreateLogger<App>();
            //     logger.LogWarning(ex, "Failed to register PythonBridge service: {Message}. Service will not be available.", ex.Message);
            // }

            // Add other services
            services.AddScoped<ICaptureScreenService, CaptureScreenService>(); // Add Screenshot Service
            // Add use cases
            services.AddScoped<SendMessageUseCase>();
            services.AddScoped<GetConversationsUseCase>();
            services.AddScoped<GetConversationByIdUseCase>();
            services.AddScoped<ConversationManagementUseCase>();
            services.AddScoped<SettingsUseCase>();
            services.AddScoped<CaptureScreenUseCase>();
            services.AddScoped<GetMessagePathsUseCase>();


            // Add ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<SettingsViewModel>();

            ServiceProvider = services.BuildServiceProvider();
        }

        private async Task ScheduleSampleJobs()
        {
            if (_jobScheduler == null) return;

            try
            {
                // Schedule a one-time job to run in 10 seconds
                await _jobScheduler.ScheduleOneTimeJobAsync<Baiss.Infrastructure.Jobs.LogMessageJob>(
                    "sample-onetime-job",
                    TimeSpan.FromSeconds(10),
                    new { message = "Hello from one-time job!" });

                // Schedule a recurring job to run every minute (for demo purposes)
                await _jobScheduler.ScheduleRecurringJobAsync<Baiss.Infrastructure.Jobs.LogMessageJob>(
                    "sample-recurring-job",
                    "1 * * * * ?", // Every minute
                    new { message = "Hello from recurring job!" });

                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogInformation("Sample jobs scheduled successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to schedule sample jobs");
            }
        }

        private async Task ScheduleTreeStructureUpdateJob()
        {
            if (_jobScheduler == null) return;

            try
            {
                using var scope = ServiceProvider?.CreateScope();
                var settingsRepository = scope?.ServiceProvider.GetService<ISettingsRepository>();
                var modelRepository = scope?.ServiceProvider.GetService<IModelRepository>();

                if (settingsRepository == null) return;

                var settings = await settingsRepository.GetAsync();
                if (settings != null && settings.TreeStructureScheduleEnabled && !string.IsNullOrEmpty(settings.TreeStructureSchedule))
                {
                    bool canSchedule = true;

                    // Validate embedding model
                    if (string.IsNullOrEmpty(settings.AIEmbeddingModelId))
                    {
                        canSchedule = false;
                        var logger = ServiceProvider?.GetService<ILogger<App>>();
                        logger?.LogWarning("Skipping tree structure schedule: No embedding model selected.");
                    }
                    else if (modelRepository != null)
                    {
                        var embeddingModel = await modelRepository.GetModelByIdAsync(settings.AIEmbeddingModelId);
                        if (embeddingModel == null)
                        {
                            canSchedule = false;
                            var logger = ServiceProvider?.GetService<ILogger<App>>();
                            logger?.LogWarning("Skipping tree structure schedule: Embedding model {Id} not found in database.", settings.AIEmbeddingModelId);
                        }
                        else if (embeddingModel.Type == "local")
                        {
                            if (string.IsNullOrEmpty(embeddingModel.LocalPath) || !File.Exists(embeddingModel.LocalPath))
                            {
                                canSchedule = false;
                                var logger = ServiceProvider?.GetService<ILogger<App>>();
                                logger?.LogWarning("Skipping tree structure schedule: Local embedding model file not found at {Path}.", embeddingModel.LocalPath ?? "null");
                            }
                        }
                    }

                    if (canSchedule)
                    {
                        await _jobScheduler.ScheduleRecurringJobAsync<Baiss.Infrastructure.Jobs.UpdateTreeStructureJob>(
                            "tree-structure-update-job",
                            settings.TreeStructureSchedule,
                            null);

                        var logger = ServiceProvider?.GetService<ILogger<App>>();
                        logger?.LogInformation("Tree structure update job scheduled with cron: {Cron}", settings.TreeStructureSchedule);
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to schedule tree structure update job");
            }
        }

        private void EnsureConfigFileExists(IServiceCollection services)
        {
            try
            {
                // Check if config file already exists
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configFilePath = Path.Combine(appDirectory, "baiss_config.json");
                if (File.Exists(configFilePath))
                {
                    return; // File already exists, nothing to do
                }

                // Build a temporary service provider to get ConfigService
                using var tempProvider = services.BuildServiceProvider();
                var configService = tempProvider.GetService<ConfigService>();

                if (configService != null)
                {
                    configService.CreateConfigFile();

                    var logger = tempProvider.GetService<ILogger<App>>();
                    logger?.LogInformation("Config file created during service registration");
                }
            }
            catch (Exception ex)
            {
                // If config creation fails, we'll handle it later in the PythonBridge registration
                var tempProvider = services.BuildServiceProvider();
                var logger = tempProvider.GetService<ILogger<App>>();
                logger?.LogWarning(ex, "Failed to create config file during service registration: {Message}", ex.Message);
                tempProvider.Dispose();
            }
        }

        private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            if (_isCleanedUp)
            {
                return;
            }

            // Cancel the shutdown to allow async cleanup
            e.Cancel = true;

            // Hide the main window to indicate shutdown is in progress
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Hide();
            }

            try
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogInformation("Application shutdown requested, cleaning up resources...");

                // Stop the Python server process if it's running
                try
                {
                    if (_pythonServerProcess != null)
                    {
                        bool isRunning = false;
                        try
                        {
                            isRunning = !_pythonServerProcess.HasExited;
                        }
                        catch
                        {
                            // Ignore errors checking status (e.g. process not started or already disposed)
                            isRunning = false;
                        }

                        if (isRunning)
                        {
                            logger?.LogInformation("Stopping Python server process...");
                            try
                            {
                                _pythonServerProcess.Kill(true);
                            }
                            catch (Exception killEx)
                            {
                                logger?.LogWarning(killEx, "Failed to kill Python process object");
                            }
                        }

                        try
                        {
                            _pythonServerProcess.Dispose();
                        }
                        catch { }
                        _pythonServerProcess = null;
                    }
                }
                catch (Exception processEx)
                {
                    logger?.LogWarning(processEx, "Error stopping Python server process");
                }

                // Kill all llama-server processes system-wide (handles any orphaned processes)
                try
                {
                    Console.WriteLine("Killing all llama-server processes on the system...");

                    if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                    {
                        var killProcess = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/killall",
                            Arguments = "-9 llama-server",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var process = Process.Start(killProcess);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            Console.WriteLine("All llama-server processes killed (Unix)");
                        }
                    }
                    else if (OperatingSystem.IsWindows())
                    {
                        var killProcess = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = "/F /IM llama-server.exe /T",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var process = Process.Start(killProcess);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            Console.WriteLine("All llama-server processes killed (Windows)");
                        }
                    }
                }
                catch (Exception killAllEx)
                {
                    Console.WriteLine($"Error killing all llama-server processes: {killAllEx.Message}");
                }

                try
                {
                    Console.WriteLine("Killing all Python processes on the system...");

                    if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                    {
                        var killProcess = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/pkill",
                            Arguments = "-9 -f \"run_local.py\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var process = Process.Start(killProcess);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            Console.WriteLine("All Python server processes killed (Unix)");
                        }

                        var killProcess2 = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/pkill",
                            Arguments = "-9 -i python",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var process2 = Process.Start(killProcess2);
                        if (process2 != null)
                        {
                            await process2.WaitForExitAsync();
                            Console.WriteLine("All python processes killed (Unix)");
                        }
                    }
                    else if (OperatingSystem.IsWindows())
                    {
                        var killProcess = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = "/F /IM python.exe /T",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var process = Process.Start(killProcess);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            Console.WriteLine("All python.exe processes killed (Windows)");
                        }

                        var killProcess2 = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = "/F /IM pythonw.exe /T",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var process2 = Process.Start(killProcess2);
                        if (process2 != null)
                        {
                            await process2.WaitForExitAsync();
                            Console.WriteLine("All pythonw.exe processes killed (Windows)");
                        }
                    }
                }
                catch (Exception killAllPythonEx)
                {
                    Console.WriteLine($"Error killing all Python processes: {killAllPythonEx.Message}");
                }

                // Stop the job scheduler
                if (_jobScheduler != null)
                {
                    try
                    {
                        logger?.LogInformation("Stopping job scheduler...");
                        await _jobScheduler.StopAsync();
                        logger?.LogInformation("Job scheduler stopped successfully");
                    }
                    catch (Exception jobEx)
                    {
                        logger?.LogWarning(jobEx, "Error stopping job scheduler");
                    }
                }

                // Stop the AI providers test service
                if (_aiProvidersTestService != null)
                {
                    try
                    {
                        logger?.LogInformation("Stopping AI providers test service...");
                        await _aiProvidersTestService.StopAsync(CancellationToken.None);
                        logger?.LogInformation("AI providers test service stopped successfully");
                    }
                    catch (Exception aiEx)
                    {
                        logger?.LogWarning(aiEx, "Error stopping AI providers test service");
                    }
                }

                // Dispose the service provider and all registered services
                try
                {
                    logger?.LogInformation("Disposing service provider...");
                    ServiceProvider?.Dispose();
                    ServiceProvider = null;
                    logger?.LogInformation("Service provider disposed successfully");
                }
                catch (Exception serviceEx)
                {
                    logger?.LogWarning(serviceEx, "Error disposing service provider");
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Error during application shutdown");
            }
            finally
            {
                // Force garbage collection to clean up any remaining resources
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                _isCleanedUp = true;
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    desktopLifetime.Shutdown();
                }
            }
        }

        private static void ShowDatabaseErrorAndExit(string errorMessage)
        {
            // In a real application, you might want to show a proper error dialog
            // For now, we'll use a simple message box approach
            var errorWindow = new Window
            {
                Title = "Database Error",
                Width = 100,
                Height = 100,
                Content = new TextBlock
                {
                    Text = $"Database initialization failed:\n\n{errorMessage}\n\nThe application will now exit.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(20)
                }
            };

            // Show the error window
            errorWindow.Show();

            // Wait a moment for the user to see the error, then exit
            System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ => Environment.Exit(1));
        }


        private async Task LaunchLlamaCppServerAsync()
        {
            try
            {
                var getLlamaServerService = ServiceProvider?.GetService<IGetLlamaServerService>();
                if (getLlamaServerService == null)
                {
                    _logger?.LogError("IGetLlamaServerService not available");
                    return;
                }
                await getLlamaServerService.DownloadAndExtractLlamaServerAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory));

                var launchServerService = ServiceProvider?.GetService<ILaunchServerService>();
                if (launchServerService == null)
                {
                    _logger?.LogError("ILaunchServerService not available");
                    return;
                }

                var process1 = await launchServerService.LaunchLlamaCppServerAsync("chat");
                if (process1 != null && process1.Process != null)
                {
                    _llamaCppServerProcesses.Add(process1.Process); // Store the process for cleanup
                    _logger?.LogInformation("llama-cpp chat server launched successfully with PID: {ProcessId}", process1.Process.Id);
                }
                await Task.Delay(2000); // Small delay to avoid port conflicts
                var process2 = await launchServerService.LaunchLlamaCppServerAsync("embedding", null, " --embeddings");
                if (process2 != null && process2.Process != null)
                {
                    _llamaCppServerProcesses.Add(process2.Process); // Store the process for cleanup
                    _logger?.LogInformation("llama-cpp embedding server launched successfully with PID: {ProcessId}", process2.Process.Id);
                }
                if (process1 == null && process2 == null)
                {
                    _logger?.LogError("Failed to launch llama-cpp server");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error launching llama-cpp server: {Message}", ex.Message);
            }
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}

