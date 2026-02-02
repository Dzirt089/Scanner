using CommunityToolkit.Mvvm.Messaging;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Messages;

namespace Scanner.WPF.Messages
{
	public sealed class MessengerScanEventSink(IMessenger messenger) : IScanEventSink
	{
		public void PublishScanEvent(ScanReceivedMessage scanReceivedMessage) => messenger.Send(scanReceivedMessage);
	}
}
