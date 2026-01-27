using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using Scanner.Abstractions.Messages;

using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Scanner.WPF.ViewModels
{
	public sealed partial class MainViewModel : ObservableObject, IRecipient<ScanReceivedMessage>
	{
		private readonly Dispatcher dispatcher;
		public ObservableCollection<string> LastScans { get; } = new();


		public MainViewModel(IMessenger messenger)
		{
			dispatcher = System.Windows.Application.Current.Dispatcher;
			messenger.Register<ScanReceivedMessage>(this);
		}

		public void Receive(ScanReceivedMessage message)
		{
			dispatcher.BeginInvoke(() =>
			{
				LastScans.Insert(0, $@"[{message.Value.PortName}] {message.Value.Line}");
				if (LastScans.Count > 200) LastScans.RemoveAt(LastScans.Count - 1);
			});
		}
	}
}
