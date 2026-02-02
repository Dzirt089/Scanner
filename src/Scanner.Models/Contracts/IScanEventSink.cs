using Scanner.Abstractions.Messages;

namespace Scanner.Abstractions.Contracts
{
	public interface IScanEventSink
	{
		void PublishScanEvent(ScanReceivedMessage scanReceivedMessage);
	}
}
