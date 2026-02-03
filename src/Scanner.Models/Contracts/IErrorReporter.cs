namespace Scanner.Abstractions.Contracts
{
	public interface IErrorReporter
	{
		void Report(Exception ex, string source);
	}
}
