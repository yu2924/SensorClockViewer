using Dapper;
using Microsoft.Data.Sqlite;
using SensorClockViewer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SensorClockViewer.Services
{
	public class DatabaseService : IDisposable
	{
		public string FilePath { get; }
		private string connectionString;
		public DatabaseService(string path)
		{
			FilePath = path;
			connectionString = $"Data Source={FilePath};Pooling=True;";
		}
		public void Dispose()
		{
			using var conn = new SqliteConnection(connectionString);
			SqliteConnection.ClearPool(conn);
		}
		public void InitializeDatabase(bool create)
		{
			using var conn = new SqliteConnection(connectionString);
			if(create)
			{
				conn.Execute(@"
					create table if not exists SensorRecords (
						Dt        integer primary key,
						Temp      real,
						Humidity  integer,
						Pressure  integer,
						Co2       integer,
						Iaq       integer
					);
					create table if not exists WeatherRecords (
						Dt           integer primary key,
						Temp         real,
						Pressure     integer,
						Humidity     integer,
						Cloud        integer,
						WindSpeed    real,
						WindDeg      integer,
						WeatherId    integer,
						WeatherIcon  text
					);");
			}
			else
			{
				conn.Execute(@"select name from sqlite_master where type = 'table' and (name = 'SensorRecords' or name = 'WeatherRecords');");
			}
		}
		// --------------------------------------------------------------------------------
		// upsert
		public async Task<int> UpsertSensorRecordsAsync(IEnumerable<SensorRecord> records, CancellationToken? cancellationtoken = null, IProgress<double>? progress = null)
		{
			return await Task.Run(() =>
			{
				progress?.Report(-1);
				using var conn = new SqliteConnection(connectionString);
				conn.Open();
				using var tran = conn.BeginTransaction();
				int numtotal = records.Count();
				int iadded = 0;
				try
				{
					List<SensorRecord> seg = new(100);
					var dbexecproc = ()=>
					{
						iadded += conn.Execute(@"
							insert or replace into SensorRecords (Dt, Temp, Humidity, Pressure, Co2, Iaq)
							values (@Dt, @Temp, @Humidity, @Pressure, @Co2, @Iaq);", seg, tran);
						cancellationtoken?.ThrowIfCancellationRequested();
						progress?.Report((double)iadded / (double)numtotal);
					};
					var enm = records.GetEnumerator();
					while(enm.MoveNext())
					{
						seg.Add(enm.Current);
						if(100 <= seg.Count) { dbexecproc(); seg.Clear(); }
					}
					if(0 < seg.Count) { dbexecproc(); }
					tran.Commit();
				}
				catch
				{
					tran.Rollback();
					throw;
				}
				return iadded;
			}, cancellationtoken ?? CancellationToken.None);
		}
		public async Task<int> UpsertWeatherRecordsAsync(IEnumerable<WeatherRecord> records, CancellationToken? cancellationtoken = null, IProgress<double>? progress = null)
		{
			return await Task.Run(() =>
			{
				progress?.Report(-1);
				using var conn = new SqliteConnection(connectionString);
				conn.Open();
				using var tran = conn.BeginTransaction();
				int numtotal = records.Count();
				int iadded = 0;
				try
				{
					List<WeatherRecord> seg = new(100);
					var dbexecproc = () =>
					{
						iadded += conn.Execute(@"
							insert or replace into WeatherRecords (Dt, Temp, Pressure, Humidity, Cloud, WindSpeed, WindDeg, WeatherId, WeatherIcon)
							values (@Dt, @Temp, @Pressure, @Humidity, @Cloud, @WindSpeed, @WindDeg, @WeatherId, @WeatherIcon);", seg, tran);
						cancellationtoken?.ThrowIfCancellationRequested();
						progress?.Report((double)iadded / (double)numtotal);
					};
					var enm = records.GetEnumerator();
					while(enm.MoveNext())
					{
						seg.Add(enm.Current);
						if(100 <= seg.Count) { dbexecproc(); seg.Clear(); }
					}
					if(0 < seg.Count) { dbexecproc(); }
					tran.Commit();
				}
				catch
				{
					tran.Rollback();
					throw;
				}
				return iadded;
			}, cancellationtoken ?? CancellationToken.None);
		}
		// --------------------------------------------------------------------------------
		// delete
		public async Task<int> DeleteSensorRecordsAsync(long from = 0, long to = long.MaxValue, CancellationToken? cancellationtoken = null, IProgress<double>? progress = null)
		{
			return await Task.Run(() =>
			{
				progress?.Report(-1);
				using var conn = new SqliteConnection(connectionString);
				conn.Open();
				using var tran = conn.BeginTransaction();
				int iremoved = 0;
				try
				{
					iremoved = conn.Execute("delete from SensorRecords where Dt between @from and @to;", new { from, to }, tran);
					cancellationtoken?.ThrowIfCancellationRequested();
					tran.Commit();
				}
				catch
				{
					tran.Rollback();
					throw;
				}
				return iremoved;
			});
		}
		public async Task<int> DeleteWeatherRecordsAsync(long from = 0, long to = long.MaxValue, CancellationToken? cancellationtoken = null, IProgress<double>? progress = null)
		{
			return await Task.Run(() =>
			{
				progress?.Report(-1);
				using var conn = new SqliteConnection(connectionString);
				conn.Open();
				using var tran = conn.BeginTransaction();
				int iremoved = 0;
				try
				{
					iremoved = conn.Execute("delete from WeatherRecords where Dt between @from and @to;", new { from, to }, tran);
					cancellationtoken?.ThrowIfCancellationRequested();
					tran.Commit();
				}
				catch
				{
					tran.Rollback();
					throw;
				}
				return iremoved;
			});
		}
		// --------------------------------------------------------------------------------
		// archive
		public async Task<bool> CreateArchiveAsync(DatabaseService archivedbs, long from = 0, long to = long.MaxValue, CancellationToken? cancellationtoken = null, IProgress<double>? progress = null)
		{
			double stage = 0;
			Progress<double> progcomb = new(pos => progress?.Report(stage + pos * 0.5));
			var serecords = GetSensorRecords(from, to);
			await archivedbs.UpsertSensorRecordsAsync(serecords, cancellationtoken, progcomb);
			stage += 0.5;
			var werecords = GetWeatherRecords(from, to);
			await archivedbs.UpsertWeatherRecordsAsync(werecords, cancellationtoken, progcomb);
			return true;
		}
		// --------------------------------------------------------------------------------
		// get
		public IEnumerable<SensorRecord> GetSensorRecords(long from = 0, long to = long.MaxValue)
		{
			using var conn = new SqliteConnection(connectionString);
			return conn.Query<SensorRecord>(
				"select * from SensorRecords where Dt between @from and @to order by Dt;",
				new { from, to });
		}
		public IEnumerable<WeatherRecord> GetWeatherRecords(long from = 0, long to = long.MaxValue)
		{
			using var conn = new SqliteConnection(connectionString);
			{
				return conn.Query<WeatherRecord>(
					"select * from WeatherRecords where Dt between @from and @to order by Dt;",
					new { from, to });
			}
		}
		public Tuple<long, long>? GetSensorRange(long from = 0, long to = long.MaxValue)
		{
			using var conn = new SqliteConnection(connectionString);
			var heads = conn.Query<SensorRecord>(
				"select * from SensorRecords where Dt between @from and @to order by Dt asc limit 1;",
				new { from, to });
			var tails = conn.Query<SensorRecord>(
				"select * from SensorRecords where Dt between @from and @to order by Dt desc limit 1;",
				new { from, to });
			return (heads.Any() && tails.Any()) ? new(heads.First().Dt, tails.First().Dt) : null;
		}
		public Tuple<long, long>? GetWeatherRange(long from = 0, long to = long.MaxValue)
		{
			using var conn = new SqliteConnection(connectionString);
			var heads = conn.Query<WeatherRecord>(
				"select * from WeatherRecords where Dt between @from and @to order by Dt asc limit 1;",
				new { from, to });
			var tails = conn.Query<WeatherRecord>(
				"select * from WeatherRecords where Dt between @from and @to order by Dt desc limit 1;",
				new { from, to });
			return (heads.Any() && tails.Any()) ? new(heads.First().Dt, tails.First().Dt) : null;
		}
	}
}
