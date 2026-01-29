using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Scanner.Abstractions.Models;
using Scanner.Services.ScannerServices;
using Scanner.Services.SystemServices;

using System.Collections.Concurrent;

namespace Scanner.Services.ScannerBackgroundServices
{
	public sealed class SerialScannerHostedService : BackgroundService
	{
		private readonly ScanChannel channel;
		private readonly ILogger<SerialScannerHostedService> logger;
		private readonly ScannerOptions options;

		private readonly ConcurrentDictionary<string, PortListener> listeners = new();

		public SerialScannerHostedService(ScanChannel channel, ILogger<SerialScannerHostedService> logger, IOptions<ScannerOptions> options)
		{
			this.channel = channel;
			this.logger = logger;
			this.options = options.Value;
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
				//Полученные порты из компьютера
				var ports = PresentComPorts.Get().ToArray();
				var portsSet = new HashSet<string>(ports, StringComparer.OrdinalIgnoreCase);
				var now = DateTime.Now;

				// удаляем исчезнувший port со слушателем
				DeleteDeathPorts(ports);

				// добавляем новый port или обновляем существующий
				AddOrUpdatePortListener(portsSet, now);

				await Task.Delay(TimeSpan.FromSeconds(options.PortScanIntervalSeconds), stoppingToken);
			}
		}

		/// <summary>
		/// добавляем новый port или обновляем существующий
		/// </summary>
		/// <param name="portsSet"></param>
		/// <param name="now"></param>
		private void AddOrUpdatePortListener(HashSet<string> portsSet, DateTime now)
		{
			foreach (var port in portsSet)
			{
				// добавляем новый port или обновляем существующий
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
						// Watchdog(сторожевой таймер): если порт "залип" (давно нет данных) — пересоздадим listener
						var staleSec = (now - existing.LastReceived).TotalSeconds;
						if ((staleSec > options.PortStaleSeconds) || (existing.HasFaulted))
						{
							logger.LogWarning("Слушатель устарел или завершился с ошибкой для {Port}, воссоздаём", port);
							existing.Dispose();

							var listner = new PortListener(port, channel.Channel.Writer);
							TryOpen(listner);
							return listner;
						}

						return existing;
					});
			}
		}

		/// <summary>
		/// удаляем исчезнувший port со слушателем
		/// </summary>
		/// <param name="ports"></param>
		private void DeleteDeathPorts(string[] ports)
		{
			foreach (var keyValue in listeners)
			{
				if (Array.IndexOf(ports, keyValue.Key) < 0)
				{
					if (listeners.TryRemove(keyValue.Key, out var listener))
					{
						listener.Dispose();
						logger.LogInformation("Слушатель расположенный к {Port} (порт исчез)", keyValue.Key);
					}
				}
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
