using Scanner.Abstractions.Models;

using System.Threading.Channels;

namespace Scanner.Abstractions.Channels
{
	public sealed class ErrorReportChannel
	{
		/// <summary>
		/// Потокобезопасная очередь producer/consumer (не даёт двум потокам одновременно “ломать” данные).
		/// </summary>
		public Channel<ErrorReport> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<ErrorReport>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
	}
}
