using Serilog.Core;
using Serilog.Events;

using System.Diagnostics;

namespace Scanner.WPF.EventEnricher
{
	public sealed class ActivityCorrelationEnricher : ILogEventEnricher
	{
		public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
		{
			var activity = Activity.Current;
			if (activity is null) return;

			logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
		}
	}
}
