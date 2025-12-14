// using Baiss.Application.Interfaces;
// using Baiss.Infrastructure.Services;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using System.Text.Json;


// namespace Baiss.Infrastructure.Extensions;

// /// <summary>
// /// Extension methods for registering Python bridge services
// /// </summary>
// public static class PythonBridgeServiceExtensions
// {
//     /// <summary>
//     /// Registers the singleton PythonBridge service
//     /// </summary>
//     public static IServiceCollection AddPythonBridgeService(this IServiceCollection services, string pythonPath, string scriptsPath)
//     {
//         services.AddSingleton<IPythonBridgeService>(provider =>
//         {
//             var logger = provider.GetRequiredService<ILogger<PythonBridgeService>>();
//             return PythonBridgeService.GetInstance(pythonPath, scriptsPath, logger);
//         });

//         return services;
//     }

//     /// <summary>
//     /// Registers the singleton PythonBridge service with configuration from environment variables
//     /// </summary>
//     public static IServiceCollection AddPythonBridgeService(this IServiceCollection services)
//     {

//         string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
//         string configFilePath = Path.Combine(appDirectory, "baiss_config.json");

//         string jsonString = File.ReadAllText(configFilePath);
//         var config = JsonSerializer.Deserialize<ConfigeDto>(jsonString);

//         var pythonPath = config?.PythonPath;

//         var scriptsPath = string.Empty;
//         scriptsPath = config?.BaissPythonCorePath + "/baiss/shared/python/baiss_agents/app/api/v1/endpoints";

//         if (OperatingSystem.IsWindows())
//         {
//             scriptsPath = config?.BaissPythonCorePath + "\\baiss\\shared\\python\\baiss_agents\\app\\api\\v1\\endpoints";
//         }

//         return services.AddPythonBridgeService(pythonPath, scriptsPath);
//     }
// }
