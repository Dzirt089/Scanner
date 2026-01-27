using Scanner.Abstractions.Models;

namespace Scanner.Abstractions.Contracts
{
	public interface IScanProcessor
	{
		Task ProcessAsync(ScanLine line, CancellationToken token);
	}
}
