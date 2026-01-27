using System.ComponentModel;
using System.Windows;

namespace Scanner.WPF.Views
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			e.Cancel = true;
			Hide();
			base.OnClosing(e);
		}
	}
}