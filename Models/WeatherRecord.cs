namespace SensorClockViewer.Models
{
	public class WeatherRecord
	{
		public long Dt { get; set; }
		public float Temp { get; set; }
		public int Pressure { get; set; }
		public int Humidity { get; set; }
		public int Clouds { get; set; }
		public float WindSpeed { get; set; }
		public int WindDeg { get; set; }
		public int WeatherId { get; set; }
		public string WeatherIcon { get; set; } = string.Empty;
	}
}
