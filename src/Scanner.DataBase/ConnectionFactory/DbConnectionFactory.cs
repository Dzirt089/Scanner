using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

using Scanner.Abstractions.Configuration;
using Scanner.Abstractions.Contracts;

namespace Scanner.Infrastructure.ConnectionFactory
{
	public sealed class DbConnectionFactory : IDbConnectionFactory
	{
		private readonly DbConfiguration configuration;

		public DbConnectionFactory(IOptions<DbConfiguration> options)
		{
			this.configuration = options.Value;
		}

		public async Task<SqlConnection> CreateAsync(CancellationToken token = default)
		{
			SqlConnection sql = new(configuration.ConnectionString);
			await sql.OpenAsync(token);
			return sql;
		}
	}
}
