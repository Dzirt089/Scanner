using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Extensions;
using Scanner.Abstractions.Messages;
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
				logger.LogDebug("Отфильтровали по префиксам. Промах с префиесом");//scan.filtered prefix_miss
				return;
			}

			if (!line.Line.TryParseScan(out var column, out var idAuto))
			{
				logger.LogWarning("Предупреждение. Не спарсились данные");//scan.failed parse_error
				return;
			}

			using (logger.BeginScope(new Dictionary<string, object>
			{
				["id_auto"] = idAuto,
				["column"] = column
			}))
			{
				logger.LogInformation("Данные успешно спарсились");

				var dateNow = DateTime.Now;
				var isChecked = await compliteRepository.SetDateDbRowsByIdAutoAsync(column, idAuto, dateNow, token);
				if (!isChecked)
				{
					logger.LogWarning("Предупреждение. Обновлено НОЛЬ записей в БД");//scan.failed db_update_zero_rows
					return;
				}

				logger.LogInformation("БД обновлена");//db.updated

				InformationOnAutomation? automation = await compliteRepository.GetNameAutoDbRowsByIdAutoAsync(idAuto, column, dateNow, token);
				if (automation is null)
				{
					logger.LogWarning("Предупреждение. Автоматика не найдена");//scan.failed automation_not_found
					return;
				}

				logger.LogInformation("Результаты: Номер заказа={zak} Артикул={art} Отделение={dep} Участок={vkc}",
					automation.ZakNm, automation.Art, automation.Department, automation.NameVkc);

				scanEventSink.PublishScanEvent(new ScanReceivedMessage(automation));

				return;
			}
		}
	}
}
