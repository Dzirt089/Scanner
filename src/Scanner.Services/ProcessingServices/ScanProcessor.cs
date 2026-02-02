using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Extensions;
using Scanner.Abstractions.Messages;
using Scanner.Abstractions.Metrics;
using Scanner.Abstractions.Models;

namespace Scanner.Services.ProcessingServices
{
	public sealed class ScanProcessor : IScanProcessor
	{
		private readonly ILogger<ScanProcessor> logger;
		private readonly ScannerOptions options;
		private readonly IScanEventSink scanEventSink;
		private readonly IPortCompliteRepository compliteRepository;

		public ScanProcessor(
			ILogger<ScanProcessor> logger,
			IOptions<ScannerOptions> options,
			IScanEventSink scanEventSink,
			IPortCompliteRepository compliteRepository)
		{
			this.logger = logger;
			this.options = options.Value;
			this.scanEventSink = scanEventSink;
			this.compliteRepository = compliteRepository;
		}

		public async Task ProcessAsync(ScanLine line, CancellationToken token)
		{
			if (!options.KnownPrefixes.Any(_ => line.Line.StartsWith(_, StringComparison.OrdinalIgnoreCase)))
			{
				ScannerTelemetry.ScansFiltered.Add(1);
				return;
			}

			if (!line.Line.TryParseScan(out var column, out var idAuto))
			{
				ScannerTelemetry.ScansFailed.Add(1);
				logger.LogWarning("Не удалось распарсить скан: {Line}", line.Line);
				return;
			}

			var dateNow = DateTime.Now;
			var isChecked = await compliteRepository.SetDateDbRowsByIdAutoAsync(column, idAuto, dateNow, token);
			if (!isChecked)
			{
				ScannerTelemetry.ScansFailed.Add(1);
				logger.LogWarning("Не удалось обновить запись для idAuto: {IdAuto}, column: {Column}", idAuto, column);
				return;
			}

			ScannerTelemetry.ScansDbUpdated.Add(1);

			InformationOnAutomation? automations = await compliteRepository.GetNameAutoDbRowsByIdAutoAsync(idAuto, column, dateNow, token);
			if (automations is null)
			{
				ScannerTelemetry.ScansFailed.Add(1);
				logger.LogWarning("Не найдена запись автоматики для idAuto: {IdAuto}, column: {Column}", idAuto, column);
				return;
			}

			scanEventSink.PublishScanEvent(new ScanReceivedMessage(automations));

			return;
		}
	}
}
