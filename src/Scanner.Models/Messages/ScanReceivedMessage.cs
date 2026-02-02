using CommunityToolkit.Mvvm.Messaging.Messages;

using Scanner.Abstractions.Models;

namespace Scanner.Abstractions.Messages
{
	public sealed class ScanReceivedMessage : ValueChangedMessage<InformationOnAutomation>
	{
		public ScanReceivedMessage(InformationOnAutomation value) : base(value)
		{
		}
	}
}
