using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SensorClockViewer.Models;

namespace SensorClockViewer.Services
{
	public class ServerQueryService : IDisposable
	{
		private readonly HttpClient httpClient = new HttpClient();
		public ServerQueryService()
		{
		}
		public void Dispose()
		{
			httpClient.Dispose();
		}
		private void SetBasicAuth(AppSettings settings)
		{
			if(!string.IsNullOrEmpty(settings.UserName))
			{
				string cred = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.UserName}:{settings.Password}"));
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", cred);
			}
			else
			{
				httpClient.DefaultRequestHeaders.Authorization = null;
			}
		}
		private string BuildUrl(AppSettings settings, string path, long? from, long? to)
		{
			string baseUrl = (settings.Port == 80)
				? $"http://{settings.ServerName}"
				: $"http://{settings.ServerName}:{settings.Port}";
			string url = $"{baseUrl}{path}";
			var queryparams = new List<string>();
			if(from.HasValue) { queryparams.Add($"from={from.Value}"); }
			if(to.HasValue) { queryparams.Add($"to={to.Value}"); }
			if(0 < queryparams.Count) { url += "?" + string.Join("&", queryparams); }
			return url;
		}
		public async Task<List<SensorRecord>> FetchSensorRecordsAsync(AppSettings settings, long? from = null, long? to = null, CancellationToken? cancellationtoken = null)
		{
			SetBasicAuth(settings);
			string url = BuildUrl(settings, "/sensor", from, to);
			string json = await httpClient.GetStringAsync(url, cancellationtoken ?? CancellationToken.None);
			using var doc = JsonDocument.Parse(json);
			var dataarr = doc.RootElement.GetProperty("data");
			var records = new List<SensorRecord>(dataarr.GetArrayLength());
			foreach(var item in dataarr.EnumerateArray())
			{
				records.Add(new SensorRecord
				{
					Dt			= long.Parse(item.GetProperty("dt").GetString()!),
					Temp		= float.Parse(item.GetProperty("temp").GetString()!),
					Humidity	= int.Parse(item.GetProperty("humidity").GetString()!),
					Pressure	= int.Parse(item.GetProperty("pressure").GetString()!),
					Co2			= int.Parse(item.GetProperty("co2").GetString()!),
					Iaq			= int.Parse(item.GetProperty("iaq").GetString()!),
				});
			}
			return records;
		}
		public async Task<List<WeatherRecord>> FetchWeatherRecordsAsync(AppSettings settings, long? from = null, long? to = null, CancellationToken? cancellationtoken = null)
		{
			SetBasicAuth(settings);
			string url = BuildUrl(settings, "/weather", from, to);
			string json = await httpClient.GetStringAsync(url, cancellationtoken ?? CancellationToken.None);
			using var doc = JsonDocument.Parse(json);
			var dataarr = doc.RootElement.GetProperty("data");
			var records = new List<WeatherRecord>(dataarr.GetArrayLength());
			foreach(var item in dataarr.EnumerateArray())
			{
				records.Add(new WeatherRecord
				{
					Dt			= long.Parse(item.GetProperty("dt").GetString()!),
					Temp		= float.Parse(item.GetProperty("temp").GetString()!),
					Pressure	= int.Parse(item.GetProperty("pressure").GetString()!),
					Humidity	= int.Parse(item.GetProperty("humidity").GetString()!),
					Cloud		= int.Parse(item.GetProperty("cloud").GetString()!),
					WindSpeed	= float.Parse(item.GetProperty("wind_speed").GetString()!),
					WindDeg		= int.Parse(item.GetProperty("wind_deg").GetString()!),
					WeatherId	= int.Parse(item.GetProperty("weather_id").GetString()!),
					WeatherIcon	= item .GetProperty("weather_icon").GetString()!,
				});
			}
			return records;
		}
	}
}
