using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Scanner.Abstractions.Metrics
{
	public static class ScannerTelemetry
	{
		public const string ActivitySourceName = "Scanner";
		public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

		public const string MeterName = "Scanner";
		public static readonly Meter Meter = new(MeterName);

		public static readonly Counter<long> ScansDequeued =
			Meter.CreateCounter<long>("scanner_scans_dequeued");

		public static readonly Counter<long> ScansFiltered =
			Meter.CreateCounter<long>("scanner_scans_filtered");

		public static readonly Counter<long> ScansDbUpdated =
			Meter.CreateCounter<long>("scanner_scans_db_updated");

		public static readonly Counter<long> ScansFailed =
			Meter.CreateCounter<long>("scanner_scans_failed");
	}
}
