using Dapper;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Extensions;
using Scanner.Abstractions.Models;

namespace Scanner.Infrastructure.Repositories
{
	public class PortCompliteRepository : IPortCompliteRepository
	{
		private readonly IDbConnectionFactory connectionFactory;

		public PortCompliteRepository(IDbConnectionFactory connectionFactory)
		{
			this.connectionFactory = connectionFactory;
		}

		private static readonly Dictionary<string, string> _validColumns = new()
		{
			["korpus"] = "[korpus]",
			["montaj"] = "[montaj]",
			["sila"] = "[sila]",
			["uprav"] = "[uprav]",
			["check"] = "[check]",
		};

		public async Task<int?> GetIdDbRowsByIdAutoAsync(int idAuto, CancellationToken token = default)
		{
			string sql = $"SELECT id FROM tbVKT_PlanAuto_PartComplete WHERE idAuto = @IdAuto";

			await using var con = await connectionFactory.CreateAsync();

			var cmd = new CommandDefinition(sql, new { IdAuto = idAuto }, cancellationToken: token);
			int? result = await con.QueryFirstOrDefaultAsync<int>(cmd);
			return result;
		}

		public async Task<bool> SetDateDbRowsByIdAutoAsync(string column, int idAuto, DateTime date, CancellationToken token = default)
		{
			if (!_validColumns.TryGetValue(column, out var columnSql))
				throw new Exception($"Недопустимое значение столбца: {column}");

			string sql = $"UPDATE tbVKT_PlanAuto_PartComplete Set {columnSql} = @DateNow WHERE idAuto = @IdAuto";

			await using var con = await connectionFactory.CreateAsync();

			var cmd = new CommandDefinition(sql, new { DateNow = date, IdAuto = idAuto }, cancellationToken: token);
			int row = await con.ExecuteAsync(cmd);
			return row > 0;
		}

		public async Task<InformationOnAutomation?> GetNameAutoDbRowsByIdAutoAsync(int idAuto, string column, DateTime date, CancellationToken token = default)
		{
			var departamentSql = column?.ToScannerDepartment();

			string sql = @$"SELECT id as IdAuto
							  ,[zakNm] as ZakNm
							  ,[art] as Art
							  ,[name] as Name
							  ,[nameVkc] as NameVkc
							  ,[planed] as Planed
							  ,@Department as Department
							  ,@ScanerTime as ScanerTime
							FROM tbVKT_PlanAuto WHERE id = @IdAuto";

			await using var con = await connectionFactory.CreateAsync();

			var cmd = new CommandDefinition(sql, new { IdAuto = idAuto, Department = departamentSql, ScanerTime = date }, cancellationToken: token);
			var result = await con.QueryFirstOrDefaultAsync<InformationOnAutomation>(cmd);
			return result;
		}
	}
}
