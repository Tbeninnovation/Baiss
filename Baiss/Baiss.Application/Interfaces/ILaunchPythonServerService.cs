
using System.Diagnostics;


namespace Baiss.Application.Interfaces;



public interface ILaunchPythonServerService
{
	int PythonServerPort { get; }
	Process? LaunchPythonServer();
	Task<bool> StopPythonServerAsync();
	bool IsServerRunning();
}
