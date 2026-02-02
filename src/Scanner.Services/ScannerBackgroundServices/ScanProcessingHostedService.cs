using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Metrics;
using Scanner.Abstractions.Models;
using Scanner.Services.ScannerServices;

using System.Diagnostics;

namespace Scanner.Services.ScannerBackgroundServices
{
	public sealed class ScanProcessingHostedService : BackgroundService
	{
		private readonly ScanChannel channel;
		private readonly IServiceScopeFactory scopeFactory;
		private readonly ILogger<ScanProcessingHostedService> logger;
		private readonly IScannerRuntimeState scannerRuntimeState;

		public ScanProcessingHostedService(ScanChannel channel, IServiceScopeFactory scopeFactory, ILogger<ScanProcessingHostedService> logger, IScannerRuntimeState scannerRuntimeState)
		{
			this.channel = channel;
			this.scopeFactory = scopeFactory;
			this.logger = logger;
			this.scannerRuntimeState = scannerRuntimeState;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await foreach (ScanLine scan in channel.Channel.Reader.ReadAllAsync(stoppingToken))
			{
				ScannerTelemetry.ScansDequeued.Add(1);
				using var activity = ScannerTelemetry.ActivitySource.StartActivity("scan.process", ActivityKind.Consumer);
				activity?.SetTag("scanner.port", scan.PortName);
				activity?.SetTag("scanner.line", scan.Line);

				// ВАЖНО: BeginScope даст “сквозные” поля в логах (в т.ч. в SQL через Serilog)
				using (logger.BeginScope(new Dictionary<string, object?>
				{
					["port"] = scan.PortName,
					["line"] = scan.Line,
					["trace_id"] = Activity.Current?.TraceId.ToString(),
					["span_id"] = Activity.Current?.SpanId.ToString(),
				}))
				{
					try
					{
						using var scope = scopeFactory.CreateScope();
						var processor = scope.ServiceProvider.GetRequiredService<IScanProcessor>();

						await processor.ProcessAsync(scan, stoppingToken);
						scannerRuntimeState.TryUpdateName(scan.PortName, scan.Line);
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
					catch (Exception ex)
					{
						ScannerTelemetry.ScansFailed.Add(1);

						activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
						activity?.AddException(ex);						

						logger.LogWarning(ex, "Scan processing failed: {Port} {Line}", scan.PortName, scan.Line);
					}
				}
			}
		}
	}
}
