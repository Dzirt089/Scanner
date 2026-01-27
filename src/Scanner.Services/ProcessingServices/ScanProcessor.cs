using CommunityToolkit.Mvvm.Messaging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Messages;
using Scanner.Abstractions.Models;

namespace Scanner.Services.ProcessingServices
{
	public sealed class ScanProcessor : IScanProcessor
	{
		private readonly ILogger<ScanProcessor> logger;
		private readonly ScannerOptions options;
		private readonly IMessenger messenger;

		public ScanProcessor(ILogger<ScanProcessor> logger, IOptions<ScannerOptions> options, IMessenger messenger)
		{
			this.logger = logger;
			this.options = options.Value;
			this.messenger = messenger;
		}

		public Task ProcessAsync(ScanLine line, CancellationToken token)
		{
			if (!options.KnownPrefixes.Any(_ => line.Line.StartsWith(_, StringComparison.OrdinalIgnoreCase)))
				return Task.CompletedTask;

			// Парсим строку и отправляем в БД
			logger.LogInformation("Принят скан: {Line}", line);

			messenger.Send(new ScanReceivedMessage(line));

			return Task.CompletedTask;
		}
	}
}
