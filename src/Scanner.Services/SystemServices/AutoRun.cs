using Microsoft.Win32;

using System.ComponentModel;

namespace Scanner.Services.SystemServices;

public static class AutoRun
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

	// ClickOnce-вариант: автозапуск через .appref-ms (ищем в Start Menu)
	public static bool TryEnableCurrentUserClickOnce(string appName, string appRefMsFileName, out Exception? error)
	{
		error = null;
		try
		{
			EnableCurrentUserClickOnce(appName, appRefMsFileName);
			return true;
		}
		catch (Exception ex)
		{
			error = ex;
			return false;
		}
	}

	public static bool TryDisableCurrentUser(string appName, out Exception? error)
	{
		error = null;
		try
		{
			DisableCurrentUser(appName);
			return true;
		}
		catch (Exception ex)
		{
			error = ex;
			return false;
		}
	}

	public static bool IsEnabledCurrentUser(string appName, out string? commandLine)
	{
		commandLine = null;
		if (!OperatingSystem.IsWindows()) return false;

		using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
		commandLine = key?.GetValue(appName) as string;
		return !string.IsNullOrWhiteSpace(commandLine);
	}

	public static void DisableCurrentUser(string appName)
	{
		if (!OperatingSystem.IsWindows()) return;

		using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
		key?.DeleteValue(appName, throwOnMissingValue: false);
	}

	public static void EnableCurrentUserClickOnce(string appName, string appRefMsFileName)
	{
		if (!OperatingSystem.IsWindows()) return;

		if (string.IsNullOrWhiteSpace(appName)) throw new ArgumentException("appName is empty");
		if (string.IsNullOrWhiteSpace(appRefMsFileName)) throw new ArgumentException("appRefMsFileName is empty");

		var appRefMsPath = FindAppRefMs(appRefMsFileName)
			?? throw new FileNotFoundException(
				$"Не найден {appRefMsFileName}.appref-ms в меню Пуск (Programs/CommonPrograms).");

		// Это ровно то, как Windows открывает .appref-ms 
		var value = BuildClickOnceCommandLine(appRefMsPath);

		using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
			?? throw new Win32Exception("Cannot open HKCU Run key");

		key.SetValue(appName, value, RegistryValueKind.String);
	}

	private static string BuildClickOnceCommandLine(string appRefMsPath)
	{
		// Просто полный путь в кавычках — Windows сама обработает .appref-ms через ассоциацию
		return $"\"{appRefMsPath}\"";
	}


	private static string? FindAppRefMs(string appRefMsFileName)
	{
		// Start Menu\Programs текущего пользователя
		var p1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

		// Start Menu\Programs для всех пользователей
		var p2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

		// Иногда ClickOnce кладёт ярлык и на Desktop — можешь включить при желании:
		// var p3 = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

		static string? TryFind(string root, string fileName)
		{
			if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;

			return Directory.EnumerateFiles(root, "*.appref-ms", SearchOption.AllDirectories)
				.FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
		}

		return TryFind(p1, appRefMsFileName) ?? TryFind(p2, appRefMsFileName);
	}
}
