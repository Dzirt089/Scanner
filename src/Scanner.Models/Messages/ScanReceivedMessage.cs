using CommunityToolkit.Mvvm.Messaging.Messages;

using Scanner.Abstractions.Models;

namespace Scanner.Abstractions.Messages
{
	public sealed class ScanReceivedMessage : ValueChangedMessage<ScanLine>
	{
		public ScanReceivedMessage(ScanLine value) : base(value)
		{
		}
	}
}
