namespace Scanner.Abstractions.Models
{
	public sealed class ScanDbRow
	{
		public int Id { get; set; }
		public int IdAuto { get; set; }

		public DateTime? Korpus { get; set; }

		public DateTime? Montaj { get; set; }

		public DateTime? Sila { get; set; }

		public DateTime? Uprav { get; set; }

		public DateTime? Check { get; set; }
	}
}
