namespace Baiss.Application.Interfaces;


public interface ITreeStructureService
{
	Task UpdateTreeStructureAsync(List<string> paths, List<string> extensions, CancellationToken cancellationToken = default);
	Task DeleteFromTreeStructureAsync(List<string> paths);
	Task DeleteFromTreeStructureWithExtensionsAsync(List<string> extensions);

}
