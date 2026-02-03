namespace Scanner.Abstractions.Models
{
	public sealed record ErrorReport(Exception Exception, string SourceContext, DateTime DateTime);
}
