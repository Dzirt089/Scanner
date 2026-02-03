using Microsoft.Extensions.Logging;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Models;

namespace Scanner.Services.ErrorReports
{
	public sealed class ErrorReporter : IErrorReporter
	{
		private readonly ErrorReportChannel channel;
		private readonly ILogger<ErrorReporter> logger;

		public ErrorReporter(ErrorReportChannel channel, ILogger<ErrorReporter> logger)
		{
			this.channel = channel;
			this.logger = logger;
		}

		public void Report(Exception ex, string source)
		{
			if (!channel.Channel.Writer.TryWrite(new ErrorReport(ex, source, DateTime.Now)))
				logger.LogError(ex, "Failed to enqueue ErrorReport from {Source}", source);
		}
	}
}
