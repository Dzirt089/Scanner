namespace Scanner.Abstractions.Models
{
	public sealed class InformationOnAutomation
	{
		public int IdAuto { get; set; }
		public required string ZakNm { get; set; }
		public required string Art { get; set; }
		public required string Name { get; set; }
		public required string NameVkc { get; set; }
		public DateTime Planed { get; set; }
		public DateTime ScanerTime { get; set; }
		public required string Department { get; set; }
	}
}
