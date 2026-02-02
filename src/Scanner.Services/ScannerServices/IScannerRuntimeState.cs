using Scanner.Abstractions.Models;

namespace Scanner.Services.ScannerServices
{
	public interface IScannerRuntimeState
	{
		void TryUpsert(string portName, ScanModel scanModel);
		void Remove(string portName);
		IReadOnlyCollection<ScanModel> GetAllScans();
		bool TryUpdateName(string portName, string newName);
	}
}
