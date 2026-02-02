using OpenTelemetry;

using Serilog;

using System.Diagnostics;

namespace Scanner.WPF.Telemetry
{
	public sealed class SerilogSpanExporter : BaseExporter<Activity>
	{
		// используем Serilog напрямую — он уже настроен (MSSQL + файл)
		private static readonly ILogger Log = Serilog.Log.ForContext<SerilogSpanExporter>();

		public override ExportResult Export(in Batch<Activity> batch)
		{
			foreach (var activity in batch)
			{
				// минимум полезных полей
				var traceId = activity.TraceId.ToString();
				var spanId = activity.SpanId.ToString();
				var name = activity.DisplayName;
				var kind = activity.Kind.ToString();
				var durMs = activity.Duration.TotalMilliseconds;
				var status = activity.Status.ToString();

				string? dbSystem = null;
				string? dbName = null;

				foreach (var (k, v) in activity.TagObjects)
				{
					if (k == "db.system") dbSystem = v?.ToString();
					else if (k == "db.name") dbName = v?.ToString();
				}

				Log.Information("otel.span {SpanName} kind={Kind} status={Status} dur_ms={DurMs} trace_id={TraceId} span_id={SpanId} db.system={DbSystem} db.name={DbName}",
				name, kind, status, durMs, traceId, spanId, dbSystem, dbName);
			}

			return ExportResult.Success;
		}
	}
}
