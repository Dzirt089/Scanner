using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Contracts;
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
		private readonly IScannerRuntimeState scannerRuntimeState;
		private readonly IErrorReporter reporter;

		/// <summary>
		/// потокобезопасные операции add/update/remove (слушатели, runtime state).
		/// </summary>
		private readonly ConcurrentDictionary<string, PortListener> listeners = new(StringComparer.OrdinalIgnoreCase);

		private readonly ConcurrentDictionary<string, string> continuePorts = new(StringComparer.OrdinalIgnoreCase);

		public SerialScannerHostedService(ScanChannel channel, ILogger<SerialScannerHostedService> logger, IOptions<ScannerOptions> options, IScannerRuntimeState scannerRuntimeState, IErrorReporter reporter)
		{
			this.channel = channel;
			this.logger = logger;
			this.options = options.Value;
			this.scannerRuntimeState = scannerRuntimeState;
			this.reporter = reporter;
		}

		private bool TryOpen(PortListener listener)
		{
			try
			{
				listener.Open();
				return true;
			}
			catch (UnauthorizedAccessException ex)
			{
				//порт занят другим приложением
				continuePorts.TryAdd(listener.PortName, ex.Message);
				logger.LogWarning(ex, "Порт занят другим приложением: {port}", listener.PortName);
				return false;
			}
			catch (Exception ex)
			{
				reporter.Report(ex, $"PortListener.Open:{listener.PortName}");
				logger.LogWarning(ex, "Не удалось открыть port: {port}", listener.PortName);
				return false;
			}
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
				DeleteDeathPorts(portsSet);

				// фильтруем занятые порты
				FilterBusyPorts(portsSet, continuePorts);

				// добавляем новый port или обновляем существующий
				AddOrUpdatePortListener(portsSet, now);
				UpdateScannerRuntimeState();
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(options.PortScanIntervalSeconds), stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break; // Выход из цикла при отмене\закрытии программы
				}
				catch (Exception ex)
				{
					reporter.Report(ex, "Ошибка в работе с COM-портами");
					logger.LogError(ex, "Предупреждение! Ошибка в работе с COM-портами");//failed
				}
			}
		}

		private void FilterBusyPorts(HashSet<string> portsSet, ConcurrentDictionary<string, string> busyPorts)
		{
			var busyPortsArray = continuePorts.Keys.ToArray();
			var busyPortsSet = new HashSet<string>(busyPortsArray, StringComparer.OrdinalIgnoreCase);

			foreach (var port in busyPortsSet)
			{
				if (portsSet.Contains(port))
				{
					portsSet.Remove(port);
					if (listeners.TryRemove(port, out var listener))
					{
						listener.Dispose();
						scannerRuntimeState.Remove(port);
						logger.LogInformation("Слушатель расположенный к {port} (порт занят)", port);
					}
				}
				else
				{
					//порт освободился
					continuePorts.TryRemove(port, out var _);
				}
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
				if (listeners.TryGetValue(port, out var existing))
				{
					// Watchdog(сторожевой таймер): если порт "залип" (давно нет данных) — пересоздадим listener
					var staleSec = (now - existing.LastReceived).TotalSeconds;
					if ((staleSec > options.PortStaleSeconds) || (existing.HasFaulted))
					{
						logger.LogWarning(existing.LastError, "Слушатель устарел или завершился с ошибкой для {port}, воссоздаём", port);
						existing.Dispose();
						scannerRuntimeState.Remove(existing.PortName);
						listeners.TryRemove(port, out var _);
					}
					continue;
				}

				var listner = new PortListener(port, channel.Channel.Writer, reporter);
				var isOpen = TryOpen(listner);
				if (isOpen)
				{
					listeners.TryAdd(port, listner);
					logger.LogInformation("Слушатель создан для port: {port}", port);
				}
				else
				{
					listner.Dispose();
				}

				//// добавляем новый port или обновляем существующий
				//listeners.AddOrUpdate(port,
				//	addValueFactory: port =>
				//	{
				//		var listner = new PortListener(port, channel.Channel.Writer, reporter);
				//		TryOpen(listner);
				//		logger.LogInformation("Слушатель создан для port: {port}", port);
				//		return listner;
				//	},
				//	updateValueFactory: (port, existing) =>
				//	{
				//		// Watchdog(сторожевой таймер): если порт "залип" (давно нет данных) — пересоздадим listener
				//		var staleSec = (now - existing.LastReceived).TotalSeconds;
				//		if ((staleSec > options.PortStaleSeconds) || (existing.HasFaulted))
				//		{
				//			logger.LogWarning(existing.LastError, "Слушатель устарел или завершился с ошибкой для {port}, воссоздаём", port);
				//			existing.Dispose();
				//			scannerRuntimeState.Remove(existing.PortName);

				//			var listner = new PortListener(port, channel.Channel.Writer, reporter);
				//			TryOpen(listner);
				//			return listner;
				//		}

				//		return existing;
				//	});
			}
		}
		private void UpdateScannerRuntimeState()
		{
			foreach (var keyValue in listeners)
			{
				var listener = keyValue.Value;
				scannerRuntimeState.TryUpsert(keyValue.Key, new ScanModel() { IsActive = true, LastReceived = DateTime.Now });
			}
		}

		/// <summary>
		/// удаляем исчезнувший port со слушателем
		/// </summary>
		/// <param name="ports"></param>
		private void DeleteDeathPorts(HashSet<string> portsSet)
		{
			foreach (var keyValue in listeners)
			{
				if (!portsSet.Contains(keyValue.Key))
				{
					if (listeners.TryRemove(keyValue.Key, out var listener))
					{
						listener.Dispose();
						scannerRuntimeState.Remove(keyValue.Key);
						logger.LogInformation("Слушатель расположенный к {port} (порт исчез)", keyValue.Key);
					}

					if (continuePorts.TryRemove(keyValue.Key, out var _))
					{
						logger.LogInformation("Порт освобождён {port} (порт исчез)", keyValue.Key);
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
