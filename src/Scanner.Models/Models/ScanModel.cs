using CommunityToolkit.Mvvm.ComponentModel;

using System.ComponentModel.DataAnnotations;

namespace Scanner.Abstractions.Models
{
	public partial class ScanModel : ObservableValidator
	{
		[ObservableProperty]
		[Required]
		private string? _name;

		[ObservableProperty]
		[Required]
		private bool _isActive;

		[ObservableProperty]
		[Required]
		private DateTime? _lastReceived;
	}
}
