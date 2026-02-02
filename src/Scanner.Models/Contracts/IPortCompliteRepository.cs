using Scanner.Abstractions.Models;

namespace Scanner.Abstractions.Contracts
{
	public interface IPortCompliteRepository
	{
		Task<int?> GetIdDbRowsByIdAutoAsync(int idAuto, CancellationToken token = default);

		Task<bool> SetDateDbRowsByIdAutoAsync(string column, int idAuto, DateTime date, CancellationToken token = default);

		Task<InformationOnAutomation?> GetNameAutoDbRowsByIdAutoAsync(int idAuto, string column, DateTime date, CancellationToken token = default);
	}
}
