using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using Scanner.Abstractions.Messages;
using Scanner.Abstractions.Models;
using Scanner.Services.ScannerServices;

using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Scanner.WPF.ViewModels
{
	public sealed partial class MainViewModel : ObservableObject, IRecipient<ScanReceivedMessage>
	{
		private readonly Dispatcher dispatcher;
		private readonly IScannerRuntimeState scannerRuntime;

		[ObservableProperty]
		private ObservableCollection<InformationOnAutomation> _lastScans = new();

		[ObservableProperty]
		private ObservableCollection<ScanModel> _scans = new();

		public MainViewModel(IMessenger messenger, IScannerRuntimeState scannerRuntime)
		{
			dispatcher = System.Windows.Application.Current.Dispatcher;
			messenger.Register<ScanReceivedMessage>(this);
			this.scannerRuntime = scannerRuntime;
		}

		public void Receive(ScanReceivedMessage message)
		{
			dispatcher.BeginInvoke(() =>
			{
				LastScans.Insert(0, message.Value);
				if (LastScans.Count > 200) LastScans.RemoveAt(LastScans.Count - 1);
			});
		}

		[ObservableProperty]
		private bool _isPopupOpen;

		[RelayCommand]
		private void RefreshScans()
		{
			var scans = scannerRuntime.GetAllScans();
			Scans = new ObservableCollection<ScanModel>(scans.Where(_ => !string.IsNullOrWhiteSpace(_.Name)));
			IsPopupOpen = true; // Открываем попап после обновления
		}

		[RelayCommand]
		private void ClosePopup() => IsPopupOpen = false;
	}
}
