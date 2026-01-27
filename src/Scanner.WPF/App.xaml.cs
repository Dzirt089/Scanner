using CommunityToolkit.Mvvm.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Models;
using Scanner.Services.ProcessingServices;
using Scanner.Services.ScannerBackgroundServices;
using Scanner.Services.ScannerServices;
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

			var tray = Scope.ServiceProvider.GetRequiredService<TrayIconService>();
			tray.Initialize();

			

			var mainView = Scope.ServiceProvider.GetRequiredService<MainViewModel>();
			var window = new MainWindow { DataContext = mainView };
			window.Show();

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
	}

}
