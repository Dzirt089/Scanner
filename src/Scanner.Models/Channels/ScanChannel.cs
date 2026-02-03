using Scanner.Abstractions.Models;

using System.Threading.Channels;

namespace Scanner.Abstractions.Channels
{
	public sealed class ScanChannel
	{
		public Channel<ScanLine> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<ScanLine>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
	}
}
