using Microsoft.Win32;

using System.ComponentModel;
using System.Diagnostics;

namespace Scanner.Services.SystemServices
{
	public static class AutoRun
	{
		// Ключи Run
		private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

		/// <summary>
		/// Включить автозапуск для текущего пользователя (HKCU).
		/// </summary>
		public static void EnableCurrentUser(string appName, string exePath, string? args = null)
		{
			// Выполняем только на Windows, чтобы избежать CA1416.
			if (!OperatingSystem.IsWindows())
				return;

			if (string.IsNullOrWhiteSpace(appName)) throw new ArgumentException("appName is empty");
			if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentException("exePath is empty");
			if (!File.Exists(exePath)) throw new FileNotFoundException("exe not found", exePath);

			var value = BuildCommandLine(exePath, args);

			using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
				?? throw new Win32Exception("Cannot open HKCU Run key");

			key.SetValue(appName, value, RegistryValueKind.String);
		}

		/// <summary>
		/// Выключить автозапуск для текущего пользователя (HKCU).
		/// </summary>
		public static void DisableCurrentUser(string appName)
		{
			// Выполняем только на Windows, чтобы избежать CA1416.
			if (!OperatingSystem.IsWindows())
				return;

			using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

			// Если ключа нет — ничего не делать
			if (key is null)
				return;

			key.DeleteValue(appName, throwOnMissingValue: false);
		}

		/// <summary>
		/// Проверить включен ли автозапуск для текущего пользователя.
		/// </summary>
		public static bool IsEnabledCurrentUser(string appName, out string? commandLine)
		{
			// Выполняем только на Windows, чтобы избежать CA1416.
			if (OperatingSystem.IsWindows())
			{
				using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
				commandLine = key?.GetValue(appName) as string;
				return !string.IsNullOrWhiteSpace(commandLine);
			}
			else
			{
				commandLine = null;
				return false;
			}
		}

		/// <summary>
		/// Правильно формируем командную строку для Run: "path to exe" + args
		/// </summary>
		private static string BuildCommandLine(string exePath, string? args)
		{
			// Кавычки вокруг exe обязательны, если есть пробелы — мы ставим всегда, так безопаснее.
			var quotedExe = $"\"{exePath}\"";
			if (string.IsNullOrWhiteSpace(args))
				return quotedExe;

			return quotedExe + " " + args.Trim();
		}

		/// <summary>
		/// Удобный способ получить путь к текущему exe
		/// </summary>
		public static string GetCurrentExePath()
		{
			// .NET 6+
			if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
				return Environment.ProcessPath!;

			// fallback
			return Process.GetCurrentProcess().MainModule!.FileName!;
		}
	}
}
