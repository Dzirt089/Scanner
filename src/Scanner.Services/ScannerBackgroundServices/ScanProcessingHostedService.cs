using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Scanner.Abstractions.Contracts;
using Scanner.Services.ScannerServices;

namespace Scanner.Services.ScannerBackgroundServices
{
	public sealed class ScanProcessingHostedService : BackgroundService
	{
		private readonly ScanChannel channel;
		private readonly IServiceScopeFactory scopeFactory;
		private readonly ILogger<ScanProcessingHostedService> logger;

		public ScanProcessingHostedService(ScanChannel channel, IServiceScopeFactory scopeFactory, ILogger<ScanProcessingHostedService> logger)
		{
			this.channel = channel;
			this.scopeFactory = scopeFactory;
			this.logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await foreach (Abstractions.Models.ScanLine scan in channel.Channel.Reader.ReadAllAsync(stoppingToken))
			{
				try
				{
					using var scope = scopeFactory.CreateScope();
					var processor = scope.ServiceProvider.GetRequiredService<IScanProcessor>();

					await processor.ProcessAsync(scan, stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
				catch (Exception ex) { logger.LogWarning(ex, "Scan processing failed: {Port} {Line}", scan.PortName, scan.Line); }
			}
		}
	}
}
