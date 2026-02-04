using Scanner.Abstractions.Models;

using System.Threading.Channels;

namespace Scanner.Abstractions.Channels
{
	public sealed class ScanChannel
	{
		/// <summary>
		/// Потокобезопасная очередь producer/consumer (не даёт двум потокам одновременно “ломать” данные).
		/// </summary>
		public Channel<ScanLine> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<ScanLine>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
	}
}
