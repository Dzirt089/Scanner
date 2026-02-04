using MailerVKT;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Models;

namespace Scanner.Services.ScannerBackgroundServices
{
	public sealed class ErrorReportingHostedService : BackgroundService
	{
		private readonly ILogger<ErrorReportingHostedService> logger;
		private readonly ErrorReportChannel channel;
		private readonly Sender sender;

		private readonly Dictionary<string, DateTime> lastErrorReportTimes = new();
		private readonly TimeSpan minReportInterval = TimeSpan.FromMinutes(1);

		public ErrorReportingHostedService(ILogger<ErrorReportingHostedService> logger, ErrorReportChannel channel, Sender sender)
		{
			this.logger = logger;
			this.channel = channel;
			this.sender = sender;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await foreach (var report in channel.Channel.Reader.ReadAllAsync(stoppingToken))
			{
				try
				{
					var ex = report.Exception;

					// 1) порт занят — не шлём
					if (ex is UnauthorizedAccessException &&
						report.SourceContext.StartsWith("PortListener.Open", StringComparison.OrdinalIgnoreCase))
						continue;

					// 2) штатная отмена — не шлём
					if (ex is OperationCanceledException)
						continue;

					logger.LogError(ex, "Необработанная ошибка [{Source}]", report.SourceContext);
					var signature = $"{report.SourceContext}|{ex.GetType().FullName}|{ex.Message}";

					if (lastErrorReportTimes.TryGetValue(signature, out var last) && (DateTime.Now - last) < minReportInterval)
						continue;

					lastErrorReportTimes[signature] = DateTime.Now;

					await sender.SendAsync(new MailParameters
					{
						Text = BuildMailText(report),
						Recipients = ["teho19@vkt-vent.ru"],
						RecipientsBcc = ["progto@vkt-vent.ru"],
						Subject = "Errors in Scanner.WPF",
						SenderName = "Scanner.WPF",
					});
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception mailEx)
				{
					logger.LogWarning(mailEx, "Не удалось отправить электронное письмо с отчетом об ошибке.");
				}
			}
		}

		private static string BuildMailText(ErrorReport report)
		{
			var ex = report.Exception;

			return $@"
<pre>
Scanner.WPF
UTC time: {report.DateTime:O}
Source: {report.SourceContext}

User: {Environment.UserName}
Machine: {Environment.MachineName}

Exception: {ex.GetType().FullName}
Message: {ex.Message}

StackTrace:
{ex.StackTrace}

Inner:
{ex.InnerException}
</pre>";
		}
	}
}
