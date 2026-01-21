using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Windows;

namespace Scanner.WPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// Статическое свойство для хранения экземпляра хоста приложения.
		/// </summary>
		public static IHost Host { get; } = Microsoft.Extensions.Hosting.Host
			.CreateDefaultBuilder()
			.ConfigureServices((context, services) =>
			{
				// регить DI
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
