using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Scanner.Models.Models;
using Scanner.Services.ScannerServices;

using System.Collections.Concurrent;
using System.IO.Ports;

namespace Scanner.Services.ScannerBackgroundServices
{
	public sealed class SerialScannerHostedService : BackgroundService
	{
		private readonly ScanChannel channel;
		private readonly ILogger<SerialScannerHostedService> logger;
		private readonly IOptions<ScannerOptions> options;

		private readonly ConcurrentDictionary<string, PortListener> listeners = new();

		public SerialScannerHostedService(ScanChannel channel, ILogger<SerialScannerHostedService> logger, IOptions<ScannerOptions> options)
		{
			this.channel = channel;
			this.logger = logger;
			this.options = options;
		}

		private void TryOpen(PortListener listener)
		{
			try { listener.Open(); }
			catch (Exception ex) { logger.LogWarning(ex, "Не удалось открыть port: {Port}", listener.PortName); }
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				var ports = SerialPort.GetPortNames();
				var now = DateTime.Now;

				foreach (var port in ports)
				{
					listeners.AddOrUpdate(port,
						addValueFactory: port =>
						{
							var listner = new PortListener(port, channel.Channel.Writer);
							TryOpen(listner);
							logger.LogInformation("Слушатель создан для port: {Port}", port);
							return listner;
						},
						updateValueFactory: (port, existing) =>
						{
							// Watchdog: если порт "залип" (давно нет данных) — пересоздадим listener
							var staleSec = (now - existing.LastReceived).TotalSeconds;
							if (staleSec > options.Value.PortStaleSeconds)
							{
								logger.LogWarning("Слушатель устарел для {Port}, воссоздание (устаревшие секунды={Stale})", port, staleSec);
								existing.Dispose();

								var listner = new PortListener(port, channel.Channel.Writer);
								TryOpen(listner);
								return listner;
							}

							return existing;
						});
				}

				// удаляем исчезнувший port со слушателем
				foreach (var keyValue in listeners)
				{
					if (Array.IndexOf(ports, keyValue.Key) < 0)
					{
						if (listeners.TryRemove(keyValue.Key, out var listener))
						{
							listener.Dispose();
							logger.LogInformation("Слушатель расположен к {Port} (порт исчез)", keyValue.Key);
						}
					}
				}

				await Task.Delay(TimeSpan.FromSeconds(options.Value.PortScanIntervalSeconds), stoppingToken);
			}
		}

		public override Task StopAsync(CancellationToken cancellationToken)
		{
			foreach (var listener in listeners.Values)
				listener.Dispose();

			return base.StopAsync(cancellationToken);
		}
	}
}
