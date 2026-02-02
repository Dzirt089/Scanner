using Microsoft.Data.SqlClient;

namespace Scanner.Abstractions.Contracts
{
	public interface IDbConnectionFactory
	{
		Task<SqlConnection> CreateAsync(CancellationToken token = default);
	}
}
