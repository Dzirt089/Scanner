using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Extensions;
using Scanner.Abstractions.Models;

using System.Collections.Concurrent;

namespace Scanner.Services.ScannerServices
{
	public sealed class ScannerRuntimeState : IScannerRuntimeState
	{
		private readonly ConcurrentDictionary<string, ScanModel> listenersScanModel = new(StringComparer.OrdinalIgnoreCase);

		public void TryUpsert(string portName, ScanModel scanModel)
		{
			if (!listenersScanModel.TryGetValue(portName, out _))
				listenersScanModel[portName] = scanModel;
		}

		public void Remove(string portName) =>
			listenersScanModel.TryRemove(portName, out _);

		public bool TryUpdateName(string portName, string line)
		{
			if (listenersScanModel.TryGetValue(portName, out var scanModel))
			{
				if (!line.TryParseScan(out var column, out _)) return false;

				var newName = column?.ToScannerDepartment();
				if (newName == null) return false;

				scanModel.Name = newName;
				return true;
			}
			return false;
		}

		public IReadOnlyCollection<ScanModel> GetAllScans() =>
			listenersScanModel.Values.ToArray();
	}
}
