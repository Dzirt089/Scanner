namespace Scanner.WPF.Options
{
	public class ScannerOptions
	{
		/// <summary>
		/// Известные префиксы
		/// </summary>
		/// <remarks>
		/// Корпус -> Body;
		/// Монтаж -> Mounting;
		/// Сила -> Power;
		/// Управление -> Management;
		/// Проверка -> Check;
		/// </remarks>
		public string[] KnownPrefixes { get; init; } = [];

		/// <summary>
		/// Интервал проверки порта в секундах
		/// </summary>
		public int PortScanIntervalSeconds { get; init; } = 3;

		/// <summary>
		/// Время ожидания порта в секундах
		/// </summary>
		public int PortStaleSeconds { get; init; } = 15;
	}
}
