using Scanner.Models.Models;

using System.Threading.Channels;

namespace Scanner.Services.ScannerServices
{
	public sealed class ScanChannel
	{
		public Channel<ScanLine> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<ScanLine>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
	}
}
