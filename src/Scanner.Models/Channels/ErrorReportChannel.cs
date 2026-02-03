using Scanner.Abstractions.Models;

using System.Threading.Channels;

namespace Scanner.Abstractions.Channels
{
	public sealed class ErrorReportChannel
	{
		public Channel<ErrorReport> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<ErrorReport>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
	}
}
