using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Models;

namespace Scanner.Services.ScannerBackgroundServices
{
	public sealed class ScanProcessingHostedService : BackgroundService
	{
		private readonly ScanChannel channel;
		private readonly IServiceScopeFactory scopeFactory;
		private readonly ILogger<ScanProcessingHostedService> logger;
		private readonly IScannerRuntimeState scannerRuntimeState;
		private readonly IErrorReporter reporter;

		public ScanProcessingHostedService(ScanChannel channel, IServiceScopeFactory scopeFactory, ILogger<ScanProcessingHostedService> logger, IScannerRuntimeState scannerRuntimeState, IErrorReporter reporter)
		{
			this.channel = channel;
			this.scopeFactory = scopeFactory;
			this.logger = logger;
			this.scannerRuntimeState = scannerRuntimeState;
			this.reporter = reporter;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await foreach (ScanLine scan in channel.Channel.Reader.ReadAllAsync(stoppingToken))
			{
				var scanId = Guid.NewGuid().ToString("N");

				using (logger.BeginScope(new Dictionary<string, object?>
				{
					["port"] = scan.PortName,
					["line"] = scan.Line,
					["scan_id"] = scanId,
				}))
				{
					logger.LogInformation("Достали данные из канала (это “точка входа” в обработку)"); //dequeued
					try
					{
						using var scope = scopeFactory.CreateScope();
						var processor = scope.ServiceProvider.GetRequiredService<IScanProcessor>();

						await processor.ProcessAsync(scan, stoppingToken);
						logger.LogInformation("Нашли автоматику и отправили в UI"); //completed

						var isUpd = scannerRuntimeState.TryUpdateName(scan.PortName, scan.Line);
						if (isUpd)
							logger.LogInformation("Обновили имя сканера и отправили в UI");
						else
							logger.LogInformation("Имя сканера не изменилось");
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
					catch (Exception ex)
					{
						reporter.Report(ex, "Ошибка при обработке строки сканирования");
						logger.LogError(ex, "Предупреждение! Ошибка при обработке строки сканирования");//failed
					}
				}
			}
		}
	}
}
