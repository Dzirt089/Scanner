using CommunityToolkit.Mvvm.Messaging;

using MailerVKT;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Models;
using Scanner.Services.ProcessingServices;
using Scanner.Services.ScannerBackgroundServices;
using Scanner.Services.ScannerServices;
using Scanner.Services.SystemServices;
using Scanner.WPF.Tray;
using Scanner.WPF.ViewModels;
using Scanner.WPF.Views;

using System.Windows;

namespace Scanner.WPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{
		/// <summary>
		/// Статическое свойство для хранения экземпляра хоста приложения.
		/// </summary>
		public static IHost Host { get; } = Microsoft.Extensions.Hosting.Host
			.CreateDefaultBuilder()
			.ConfigureServices((context, services) =>
			{
				// infrastructure
				services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
				services.AddSingleton<TrayIconService>();
				services.AddSingleton<ScanChannel>();
				services.AddSingleton<Sender>();

				//UI
				services.AddScoped<MainViewModel>();

				// загружаем настройки в DI из json
				services.Configure<ScannerOptions>(context.Configuration.GetSection("Scanner"));

				// обработка данных и отправка в БД
				services.AddScoped<IScanProcessor, ScanProcessor>();

				// фоновая работа (BackgroundService)
				services.AddHostedService<SerialScannerHostedService>();
				services.AddHostedService<ScanProcessingHostedService>();
			})
			.Build();


		public static IServiceScope? Scope { get; private set; }

		/// <summary>
		/// Метод, вызываемый при запуске приложения.
		/// </summary>
		/// <param name="e"></param>
		protected override async void OnStartup(StartupEventArgs e)
		{
			await Host.StartAsync();
			Scope = Host.Services.CreateScope();

			// Обработчик исключений UI-потока
			DispatcherUnhandledException += App_DispatcherUnhandledException;

			// Обработчик исключений в фоновых потоках
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			// Обработчик необработанных исключений в Task
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

			// Включаем автозапуск один раз в реестре
			const string appName = "Scanner";
			if (!AutoRun.IsEnabledCurrentUser(appName, out _))
			{
				var exe = AutoRun.GetCurrentExePath();
				AutoRun.EnableCurrentUser(appName, exe, "--minimized");
			}

			var tray = Scope.ServiceProvider.GetRequiredService<TrayIconService>();
			tray.Initialize();

			// Если запуск происходит из автозапуска
			var startMinimized = e.Args.Any(_ => string.Equals(_, "--minimized", StringComparison.OrdinalIgnoreCase));

			var mainView = Scope.ServiceProvider.GetRequiredService<MainViewModel>();
			var window = new MainWindow { DataContext = mainView };

			if (startMinimized)
				window.Hide(); // Приложение свернуто в трее
			else
				window.Show(); // Показываем при первом запуске главное окно

			base.OnStartup(e);
		}

		/// <summary>
		/// Метод, вызываемый при выходе из приложения.
		/// </summary>
		/// <param name="e"></param>
		protected override async void OnExit(ExitEventArgs e)
		{
			try
			{
				Scope?.Dispose();
				await Host.StopAsync();
				Host?.Dispose();
			}
			finally { base.OnExit(e); }
		}

		#region Обработчики исключений, отправка сообщений об ошибках

		/// <summary>
		/// Обработчик необработанных исключений в Task
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			// Обработка исключения
			HandleException(e.Exception);
			// Отменяем исключение, чтобы оно не привело к завершению приложения
			e.SetObserved(); // Помечаем исключение как обработанное
		}

		/// <summary>
		/// Обработчик необработанных исключений в AppDomain
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
		{
			// Обработка исключения
			HandleException((Exception)e.ExceptionObject);
		}

		/// <summary>
		/// Обработчик исключений UI-потока
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void App_DispatcherUnhandledException(object? sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			// Обработка исключения
			HandleException(e.Exception);
			e.Handled = true; // Предотвращаем крах приложения
		}

		/// <summary>
		/// Обработчик исключений с выводом сообщения пользователю и отправкой на почту
		/// </summary>
		/// <param name="ex"></param>
		private void HandleException(Exception ex)
		{
			try
			{
				// Получаем сервис отправки почты и отправляем сообщение об ошибке
				var mail = Host.Services.GetRequiredService<Sender>();
				mail.SendAsync(new MailParameters
				{
					Text = TextMail(ex),
					Recipients = ["teho19@vkt-vent.ru"],
					RecipientsBcc = ["progto@vkt-vent.ru"],
					Subject = "Errors in Scanner.WPF",
					SenderName = "Scanner.WPF",
				}).ConfigureAwait(false);

				// Показ сообщения пользователю
				System.Windows.MessageBox.Show(
					"Произошла ошибка в работе приложения. Сообщите разработчикам в ТО.",
					"Произошла критическая ошибка.",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			}
			catch
			{
				// Показ сообщения пользователю
				System.Windows.MessageBox.Show(
					"Произошла вторая ошибка в работе приложения. Сообщите разработчикам в ТО.",
					"Произошла критическая ошибка.",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Метод для формирования текста письма с информацией об исключении.
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		private static string TextMail(Exception ex)
		{

			return $@"
<pre>
Scanner.WPF,
Время: {DateTime.Now},
Глобальная обработка исключений.

Учётная запись: {Environment.UserName}
Имя компьютера: {Environment.MachineName}

Сводка об ошибке: 

Message: {ex.Message}.


StackTrace: {ex.StackTrace}.


Source: {ex.Source}.


InnerException: {ex?.InnerException}.

</pre>";
		}
		#endregion
	}

}
