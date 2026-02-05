using CommunityToolkit.Mvvm.Messaging;

using MailerVKT;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Configuration;
using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Models;
using Scanner.Infrastructure.ConnectionFactory;
using Scanner.Infrastructure.Repositories;
using Scanner.Services.ErrorReports;
using Scanner.Services.ProcessingServices;
using Scanner.Services.ScannerBackgroundServices;
using Scanner.Services.ScannerServices;
using Scanner.Services.SystemServices;
using Scanner.WPF.EventEnricher;
using Scanner.WPF.Messages;
using Scanner.WPF.Tray;
using Scanner.WPF.ViewModels;
using Scanner.WPF.Views;

using Serilog;
using Serilog.Sinks.MSSqlServer;

using System.Collections.ObjectModel;
using System.Data;
using System.Windows;

namespace Scanner.WPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{
		/// <summary>
		/// Межпроцессная синхронизация
		/// </summary>
		/// <remarks>
		/// Named Mutex — чтобы не было двух экземпляров приложения (второй просто не стартует).
		/// </remarks>
		private static Mutex? instanceMutex;

		private static bool TryAcquireSingleInstanceMutex()
		{
			// Имя должно быть уникальным в системе
			// Local\ — достаточно для обычного десктоп-приложения (в пределах сессии пользователя)
			// Global\ — если нужно на весь компьютер/все сессии (актуально для терминалок/сервисов)
			// Global *может потребовать права / усложнить жизнь в корпоративной среде.
			const string mutexName = @"Local\VKT_ScannerApp_Mutex";

			bool createdNew;

			try
			{
				instanceMutex = new Mutex(initiallyOwned: true, name: mutexName, out createdNew);
			}
			catch (AbandonedMutexException)
			{
				// Предыдущий экземпляр умер криво — mutex "брошен".
				// Мы считаем, что теперь "мы первые".
				createdNew = true;
			}

			if (!createdNew)
			{
				// Не удалось получить мьютекс — уже есть запущенный экземпляр
				return false;
			}

			return true;
		}

		/// <summary>
		/// Статическое свойство для хранения экземпляра хоста приложения.
		/// </summary>
		public static IHost Host { get; } = Microsoft.Extensions.Hosting.Host
			.CreateDefaultBuilder()
			.UseSerilog((context, services, config) =>
			{
				var connection = context.Configuration["ConnectionStrings:ConnectionString"]
				?? throw new InvalidOperationException("ConnectionStrings:ConnectionString is not configured.");

				var options = new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions
				{
					TableName = "tbVKT_PlanAuto_ScannerLogs",
					AutoCreateSqlTable = true,

					BatchPostingLimit = 50,
					BatchPeriod = TimeSpan.FromSeconds(2),
				};

				var columnOptions = new ColumnOptions();
				columnOptions.Store.Remove(StandardColumn.Properties);

				columnOptions.Store.Add(StandardColumn.LogEvent);
				columnOptions.LogEvent.DataLength = -1; // Максимальная длина для LogEvent (неограниченная)				

				columnOptions.AdditionalColumns = new Collection<SqlColumn>
				{
					new SqlColumn("scan_id", SqlDbType.NVarChar) { DataLength = 32, PropertyName = "scan_id", AllowNull = true },
					new SqlColumn("port",    SqlDbType.NVarChar) { DataLength = 32, PropertyName = "port",    AllowNull = true },
					new SqlColumn("column",  SqlDbType.NVarChar) { DataLength = 16, PropertyName = "column",  AllowNull = true },
					new SqlColumn("id_auto", SqlDbType.Int)      { PropertyName = "id_auto", AllowNull = true },
				};
				columnOptions.LogEvent.ExcludeAdditionalProperties = true;

				config.MinimumLevel.Information()
					.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
					.Enrich.FromLogContext()
					.Enrich.With(new ActivityCorrelationEnricher())
					.WriteTo.File("Logs/scanner-.log", rollingInterval: Serilog.RollingInterval.Day, shared: true)
					.WriteTo.Async(_ =>
						_.MSSqlServer(
							connectionString: connection,
							sinkOptions: options,
							columnOptions: columnOptions));

			})
			.ConfigureServices((context, services) =>
			{
				// infrastructure
				services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
				services.AddSingleton<TrayIconService>();
				services.AddScoped<IScanEventSink, MessengerScanEventSink>();

				// channels
				services.AddSingleton<ScanChannel>();
				services.AddSingleton<ErrorReportChannel>();

				// mailer
				services.AddSingleton<Sender>();

				//Db
				services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
				services.AddScoped<IPortCompliteRepository, PortCompliteRepository>();

				//UI
				services.AddScoped<MainViewModel>();

				// загружаем настройки в DI из json
				services.Configure<ScannerOptions>(context.Configuration.GetSection("Scanner"));
				services.Configure<DbConfiguration>(context.Configuration.GetSection("ConnectionStrings"));

				// обработка данных и отправка в БД
				services.AddScoped<IScanProcessor, ScanProcessor>();

				// Produccers
				services.AddSingleton<IErrorReporter, ErrorReporter>();  // отправка ошибок в канал отчетов

				// состояние сканера
				services.AddSingleton<IScannerRuntimeState, ScannerRuntimeState>();

				// фоновая работа (BackgroundService)
				services.AddHostedService<SerialScannerHostedService>(); // Producer
				services.AddHostedService<ScanProcessingHostedService>(); // Consumer
				services.AddHostedService<ErrorReportingHostedService>(); // Consumer
			})
			.Build();

		public static IServiceScope? Scope { get; private set; }

		/// <summary>
		/// Метод, вызываемый при запуске приложения.
		/// </summary>
		/// <param name="e"></param>
		protected override async void OnStartup(StartupEventArgs e)
		{
			if (!TryAcquireSingleInstanceMutex())
			{
				// Уже запущен другой экземпляр приложения. Выходим.
				Shutdown();
				return;
			}

			await Host.StartAsync();
			Scope = Host.Services.CreateScope();

			var report = Host.Services.GetRequiredService<IErrorReporter>();

			// Обработчик исключений UI-потока
			DispatcherUnhandledException += (_, asgs) =>
			{
				report.Report(asgs.Exception, "DispatcherUnhandledException");
				asgs.Handled = true;
				ShowFatalOnce();
			};

			// Обработчик исключений в фоновых потоках
			AppDomain.CurrentDomain.UnhandledException += (_, args) =>
			{
				report.Report((Exception)args.ExceptionObject, "CurrentDomain_UnhandledException");
				ShowFatalOnce();
			};

			// Обработчик необработанных исключений в Task
			TaskScheduler.UnobservedTaskException += (_, args) =>
			{
				report.Report(args.Exception, "UnobservedTaskException");
				args.SetObserved();
				ShowFatalOnce();
			};

			// Включаем автозапуск один раз в реестре
			const string AppName = "Scanner";
			const string AppRefMsFile = "Сканеры.appref-ms";
			if (!AutoRun.IsEnabledCurrentUser(AppName, out _))
			{
				AutoRun.TryEnableCurrentUserClickOnce(AppName, AppRefMsFile, out _);
			}

			var tray = Scope.ServiceProvider.GetRequiredService<TrayIconService>();
			tray.Initialize();

			var mainView = Scope.ServiceProvider.GetRequiredService<MainViewModel>();
			var window = new MainWindow { DataContext = mainView };
			window.Hide(); // Приложение свернуто в трее

			base.OnStartup(e);
		}

		private static int fatalShown;
		private static void ShowFatalOnce()
		{
			// Interlocked.Exchange — атомарный “выполни один раз”
			if (Interlocked.Exchange(ref fatalShown, 1) != 0) return;

			System.Windows.Application.Current.Dispatcher.Invoke((() =>
			{
				System.Windows.MessageBox.Show(
					"Произошла необработанная ошибка приложения. Работа будет продолжена, но возможны сбои в работе.",
					"Ошибка",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}));
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
			finally
			{
				try { instanceMutex?.ReleaseMutex(); } catch { }
				instanceMutex?.Dispose();
				base.OnExit(e);
			}
		}
	}

}
