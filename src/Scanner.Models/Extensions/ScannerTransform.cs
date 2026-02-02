namespace Scanner.Abstractions.Extensions
{
	public static class ScannerTransform
	{
		public static string ToScannerDepartment(this string department)
		{
			return department switch
			{
				"korpus" => "051 Корпус",
				"montaj" => "051 Монтажка",
				"sila" => "051 Сила",
				"uprav" => "051 Управление",
				"check" => "051 Проверка",
				_ => throw new ArgumentOutOfRangeException(nameof(department), department, null)
			};
		}

		public static bool TryParseScan(this string? input, out string column, out int idAuto)
		{
			column = string.Empty;
			idAuto = 0;

			if (string.IsNullOrWhiteSpace(input))
				return false;

			var parts = input.Trim().Split('|', 2);
			if (parts.Length != 2) return false;

			column = parts[0].Trim();
			if (column.Length == 0) return false;

			var rightPart = parts[1].Trim();
			var dash = rightPart.IndexOf('-');
			if (dash < 0 || dash == rightPart.Length - 1) return false;

			var idPart = rightPart[(dash + 1)..].Trim();
			return int.TryParse(idPart, out idAuto);
		}
	}
}
