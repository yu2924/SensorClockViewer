namespace SensorClockViewer.Models
{
	public class SensorRecord
	{
		public long Dt { get; set; }
		public float Temp { get; set; }
		public int Humidity { get; set; }
		public int Pressure { get; set; }
		public int Co2 { get; set; }
		public int Iaq { get; set; }
	}
}
