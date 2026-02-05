using Scanner.Services.SystemServices;

using System.IO;
using System.Windows;

namespace Scanner.WPF.Tray;

public sealed class TrayIconService : IDisposable
{
	private const string AppName = "Scanner";
	private const string AppRefMsFile = "Сканеры.appref-ms"; // <-- проверь реальное имя!

	private NotifyIcon? notify;
	private ToolStripMenuItem? autorunItem;

	public void Initialize()
	{
		if (notify != null) return;

		var baseDir = AppContext.BaseDirectory;
		var iconPath = Path.Combine(baseDir, "Icons", "Scanner-3.ico");

		Icon icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

		notify = new NotifyIcon
		{
			Icon = icon,
			Visible = true,
			Text = "Scanner WPF"
		};

		var menu = new ContextMenuStrip();

		menu.Items.Add("Показать", null, (_, __) => ShowMainWindow());

		autorunItem = new ToolStripMenuItem("Автозагрузка")
		{
			CheckOnClick = false
		};
		autorunItem.Click += (_, __) => ToggleAutorun();
		menu.Items.Add(autorunItem);

		menu.Items.Add("Выход", null, (_, __) => System.Windows.Application.Current.Shutdown());

		menu.Opening += (_, __) => RefreshAutorunState();

		notify.ContextMenuStrip = menu;
		notify.DoubleClick += (_, __) => ShowMainWindow();

		RefreshAutorunState();
		ShowBalloon("Автозапуск", "Программа успешно запущена");
	}

	private void RefreshAutorunState()
	{
		if (autorunItem is null) return;

		var enabled = AutoRun.IsEnabledCurrentUser(AppName, out _);
		autorunItem.Checked = enabled;
		autorunItem.Enabled = OperatingSystem.IsWindows();
	}

	private void ToggleAutorun()
	{
		if (autorunItem is null) return;

		var enabled = AutoRun.IsEnabledCurrentUser(AppName, out _);

		if (!enabled)
		{
			// ClickOnce-включение
			if (!AutoRun.TryEnableCurrentUserClickOnce(AppName, AppRefMsFile, out var err))
			{
				ShowBalloon("Автозагрузка", $"Не удалось включить: {err?.Message}");
				RefreshAutorunState();
				return;
			}

			ShowBalloon("Автозагрузка", "Включена (ClickOnce)");
		}
		else
		{
			if (!AutoRun.TryDisableCurrentUser(AppName, out var err))
			{
				ShowBalloon("Автозагрузка", $"Не удалось выключить: {err?.Message}");
				RefreshAutorunState();
				return;
			}

			ShowBalloon("Автозагрузка", "Выключена");
		}

		RefreshAutorunState();
	}

	private void ShowBalloon(string title, string text)
	{
		if (notify is null) return;
		notify.BalloonTipTitle = title;
		notify.BalloonTipText = text;
		notify.ShowBalloonTip(2500);
	}

	private static void ShowMainWindow()
	{
		var w = System.Windows.Application.Current.MainWindow;
		if (w == null) return;

		w.Show();
		w.WindowState = WindowState.Normal;
		w.Activate();
		w.Topmost = true;
		w.Topmost = false;
		w.Focus();
	}

	public void Dispose()
	{
		if (notify != null)
		{
			notify.Visible = false;
			notify.Icon?.Dispose();
			notify.Icon = null;
			notify.Dispose();
			notify = null;
		}
	}
}
