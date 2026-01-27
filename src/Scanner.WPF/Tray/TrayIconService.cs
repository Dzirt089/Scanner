using System.Windows;

namespace Scanner.WPF.Tray;

// winforms-компонент, главное не мешать WPF и WinForms UI в одном потоке
public sealed class TrayIconService : IDisposable
{
	private NotifyIcon? notify;

	public void Initialize()
	{
		if (notify != null) return;

		notify = new NotifyIcon
		{
			Icon = SystemIcons.Application,
			Visible = true,
			Text = "Scanner WPF"
		};

		var menu = new ContextMenuStrip();
		menu.Items.Add("Показать", null, (_, __) => ShowMainWindow());
		menu.Items.Add("Выход", null, (_, __) => System.Windows.Application.Current.Shutdown());

		notify.ContextMenuStrip = menu;
		notify.DoubleClick += (_, __) => ShowMainWindow();
	}

	private static void ShowMainWindow()
	{
		var w = System.Windows.Application.Current.MainWindow;
		if (w == null) return;

		w.Show();
		w.WindowState = WindowState.Normal;
		w.Activate();
	}

	public void Dispose()
	{
		if (notify != null)
		{
			notify.Visible = false;
			notify.Dispose();
			notify = null;
		}
	}
}
