using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SensorClockViewer.Models;

namespace SensorClockViewer.Services
{
	public class DbOperateService : IDisposable, INotifyPropertyChanged
	{
		public enum TaskResult { Failed, Canceled, Succeeded }
		[Flags]
		public enum TaskFlags { Idle = 0, Sensor = 0x01, Weather = 0x02, Archive = 0x04 };
		public TaskFlags TaskStatus
		{
			get;
			private set { if(field != value) { field = value; FirePropertyChanged(nameof(TaskStatus)); } }
		} = TaskFlags.Idle;
		public double TaskProgress
		{
			get;
			private set { if(field != value) { field = value; FirePropertyChanged(nameof(TaskProgress)); } }
		} = 0;
		protected void FirePropertyChanged(string? propname)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
		}
		public event PropertyChangedEventHandler? PropertyChanged;
		public class DataChangedEventArgs : EventArgs
		{
			public TaskFlags TaskChanged { get; set; }
			public DataChangedEventArgs(TaskFlags tf) { TaskChanged = tf; }
		}
		public delegate void DataChangedEventHandler(object? sender, DataChangedEventArgs e);
		public event DataChangedEventHandler? DataChanged;
		public DatabaseService? DbService { get; private set; }
		public bool IsValid { get => DbService != null; }
		private CancellationTokenSource? taskCts;
		// live db staff
		private readonly AppSettings? settings;
		private readonly ServerQueryService? serverQueryService;
		public bool IsLiveDb { get => (settings != null) && (serverQueryService != null); }
		// --------------------------------------------------------------------------------
		public DbOperateService(string dbpath, AppSettings? s)
		{
			if(s != null)
			{
				settings = s;
				serverQueryService = new();
			}
			DbService = new(dbpath);
			try { DbService.InitializeDatabase(IsLiveDb); }
			catch { Debug.Print("InitializeDatabase() failed"); DbService.Dispose(); DbService = null; }
		}
		public void Dispose()
		{
			serverQueryService?.Dispose();
			DbService?.Dispose();
		}
		public bool RecreateDbService(string dbpath)
		{
			if(!IsLiveDb) { Debug.Print("WARNING: RecreateDbService() invalid call context"); return false; }
			if(DbService != null)
			{
				string path = DbService.FilePath;
				DbService.Dispose();
				DbService = null;
				if(System.IO.Path.Exists(path)) { try { System.IO.File.Delete(path); } catch { Debug.Print("File.Delete() failed"); } }
			}
			bool r = false;
			DbService = new(dbpath);
			try { DbService.InitializeDatabase(IsLiveDb); r = true; }
			catch { Debug.Print("InitializeDatabase() failed"); DbService.Dispose(); DbService = null; }
			if(!r && System.IO.Path.Exists(dbpath)) { try { System.IO.File.Delete(dbpath); } catch { Debug.Print("File.Delete() failed"); } }
			return r;
		}
		public void CancelAsyncTask()
		{
			taskCts?.Cancel();
		}
		// --------------------------------------------------------------------------------
		// fetch & store
		public async Task<TaskResult> FetchAllAsync()
		{
			if(!IsLiveDb) { Debug.Print("WARNING: FetchAllAsync() invalid call context"); return TaskResult.Failed; }
			if((DbService == null) || (serverQueryService == null) || (settings == null)) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = TaskFlags.Sensor | TaskFlags.Weather;
			TaskProgress = -1;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				try
				{
					double stage = 0;
					Progress<double> progcomb = new(pos => TaskProgress = (stage + pos * 0.5));
					var sensorRecords = await serverQueryService.FetchSensorRecordsAsync(settings, null, null, taskCts.Token);
					await DbService.UpsertSensorRecordsAsync(sensorRecords, taskCts.Token, progcomb);
					stage += 0.5;
					var weatherRecords = await serverQueryService.FetchWeatherRecordsAsync(settings, null, null, taskCts.Token);
					await DbService.UpsertWeatherRecordsAsync(weatherRecords, taskCts.Token, progcomb);
					r = TaskResult.Succeeded;
				}
				catch(OperationCanceledException) { r = TaskResult.Canceled; }
				catch { Debug.Print("FetchAllAsync() failed"); }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			DataChanged?.Invoke(this, new DataChangedEventArgs(TaskFlags.Sensor | TaskFlags.Weather));
			return r;
		}
		public async Task<TaskResult> FetchSensorAsync(DateTime? from = null, DateTime? to = null)
		{
			if(!IsLiveDb) { Debug.Print("WARNING: FetchSensorAsync() invalid call context"); return TaskResult.Failed; }
			if((DbService == null) || (serverQueryService == null) || (settings == null)) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = TaskFlags.Sensor;
			TaskProgress = -1;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				try
				{
					Progress<double> prog = new(pos => TaskProgress = pos);
					long? tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : null;
					long? tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : null;
					var records = await serverQueryService.FetchSensorRecordsAsync(settings, tf, tt, taskCts.Token);
					await DbService.UpsertSensorRecordsAsync(records, taskCts.Token, prog);
					r = TaskResult.Succeeded;
				}
				catch(OperationCanceledException) { r = TaskResult.Canceled; }
				catch { Debug.Print("FetchSensorAsync() failed"); }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			DataChanged?.Invoke(this, new DataChangedEventArgs(TaskFlags.Sensor));
			return r;
		}
		public async Task<TaskResult> FetchWeatherAsync(DateTime? from = null, DateTime? to = null)
		{
			if(!IsLiveDb) { Debug.Print("WARNING: FetchWeatherAsync() invalid call context"); return TaskResult.Failed; }
			if((DbService == null) || (serverQueryService == null) || (settings == null)) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = TaskFlags.Weather;
			TaskProgress = -1;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				try
				{
					Progress<double> prog = new(pos => TaskProgress = pos);
					long? tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : null;
					long? tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : null;
					var records = await serverQueryService.FetchWeatherRecordsAsync(settings, tf, tt, taskCts.Token);
					await DbService.UpsertWeatherRecordsAsync(records, taskCts.Token, prog);
					r = TaskResult.Succeeded;
				}
				catch(OperationCanceledException) { r = TaskResult.Canceled; }
				catch { Debug.Print("FetchWeatherAsync() failed"); }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			DataChanged?.Invoke(this, new DataChangedEventArgs(TaskFlags.Weather));
			return r;
		}
		// --------------------------------------------------------------------------------
		// delete
		public async Task<TaskResult> DeleteSensorRecordsAsync(DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = TaskFlags.Sensor;
			TaskProgress = -1;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				try
				{
					Progress<double> prog = new(pos => TaskProgress = pos);
					long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
					long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
					await DbService.DeleteSensorRecordsAsync(tf, tt, taskCts.Token, prog);
					r = TaskResult.Succeeded;
				}
				catch(OperationCanceledException) { r = TaskResult.Canceled; }
				catch { Debug.Print("DeleteSensorRecordsAsync() failed"); }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			DataChanged?.Invoke(this, new DataChangedEventArgs(TaskFlags.Sensor));
			return r;
		}
		public async Task<TaskResult> DeleteWeatherRecordsAsync(DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = TaskFlags.Weather;
			TaskProgress = -1;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				try
				{
					Progress<double> prog = new(pos => TaskProgress = pos);
					long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
					long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
					await DbService.DeleteWeatherRecordsAsync(tf, tt, taskCts.Token, prog);
					r = TaskResult.Succeeded;
				}
				catch(OperationCanceledException) { r = TaskResult.Canceled; }
				catch { Debug.Print("DeleteWeatherRecordsAsync() failed"); }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			DataChanged?.Invoke(this, new DataChangedEventArgs(TaskFlags.Weather));
			return r;
		}
		// --------------------------------------------------------------------------------
		// archive
		public async Task<TaskResult> CreateArchiveAsync(string archpath, DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = TaskFlags.Archive;
			TaskProgress = -1;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				string dir = System.IO.Path.GetDirectoryName(archpath) ?? System.IO.Directory.GetCurrentDirectory();
				string tmppath = System.IO.Path.Combine(dir, $"{Guid.NewGuid()}.tmp");
				try
				{
					Progress<double> prog = new(pos => TaskProgress = pos);
					if(System.IO.File.Exists(tmppath)) { System.IO.File.Delete(tmppath); }
					using DatabaseService archdbs = new DatabaseService(tmppath);
					archdbs.InitializeDatabase(true);
					long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
					long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
					await DbService.CreateArchiveAsync(archdbs, tf, tt, taskCts.Token, prog);
					r = TaskResult.Succeeded;
				}
				catch(OperationCanceledException) { r = TaskResult.Canceled; }
				catch { Debug.Print("CreateArchiveAsync() failed"); }
				try
				{
					if(r == TaskResult.Succeeded)
					{
						if(System.IO.File.Exists(archpath)) { System.IO.File.Delete(archpath); }
						System.IO.File.Move(tmppath, archpath);
					}
					else
					{
						if(System.IO.File.Exists(tmppath)) { System.IO.File.Delete(tmppath); }
					}
				}
				catch(Exception e) { r = TaskResult.Failed; Debug.Print("file operation failed, {0}", e.Message); }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			return r;
		}
		// --------------------------------------------------------------------------------
		// get
		public IEnumerable<SensorRecord>? GetSensorRecords(DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return null; }
			long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
			long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
			try { return DbService.GetSensorRecords(tf, tt); }
			catch { return null; }
		}
		public IEnumerable<WeatherRecord>? GetWeatherRecords(DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return null; }
			long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
			long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
			try { return DbService.GetWeatherRecords(tf, tt); }
			catch { return null; }
		}
		public Tuple<DateTime, DateTime>? GetSensorRange(DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return null; }
			long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
			long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
			try
			{
				var rng = DbService.GetSensorRange(tf, tt);
				DateTime dtf = (rng != null) ? DateTimeOffset.FromUnixTimeSeconds(rng.Item1).DateTime : DateTime.MinValue;
				DateTime dtt = (rng != null) ? DateTimeOffset.FromUnixTimeSeconds(rng.Item2).DateTime : DateTime.MinValue;
				return new(dtf, dtt);
			}
			catch { return null; }
		}
		public Tuple<DateTime, DateTime>? GetWeatherRange(DateTime? from = null, DateTime? to = null)
		{
			if(DbService == null) { return null; }
			long tf = (from != null) ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : 0;
			long tt = (to != null) ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : long.MaxValue;
			try
			{
				var rng = DbService.GetWeatherRange(tf, tt);
				DateTime dtf = (rng != null) ? DateTimeOffset.FromUnixTimeSeconds(rng.Item1).DateTime : DateTime.MinValue;
				DateTime dtt = (rng != null) ? DateTimeOffset.FromUnixTimeSeconds(rng.Item2).DateTime : DateTime.MinValue;
				return new(dtf, dtt);
			}
			catch { return null; }
		}
		// --------------------------------------------------------------------------------
		// dummy task for testing GUI behavior
#if TEST_ASYNCTASK
		public async Task<TaskResult> DebugDummyTaskAsync(TaskFlags tf)
		{
			if(DbService == null) { return TaskResult.Failed; }
			if(TaskStatus != TaskFlags.Idle) { return TaskResult.Failed; }
			TaskStatus = tf;
			TaskResult r = TaskResult.Failed;
			using(taskCts = new CancellationTokenSource())
			{
				TaskProgress = -1;
				var ct = taskCts.Token;
				for(int i = 1; i <= 10; ++i)
				{
					try { await Task.Delay(TimeSpan.FromSeconds(1), ct); TaskProgress = i * 0.1; Debug.Print("DebugDummyTask count={0}", i); }
					catch(OperationCanceledException) { r = TaskResult.Canceled; break; }
				}
				if(r != TaskResult.Canceled) { r = TaskResult.Succeeded; }
			}
			taskCts = null;
			TaskStatus = TaskFlags.Idle;
			DataChanged?.Invoke(this, new DataChangedEventArgs(tf));
			return r;
		}
#endif
	}
}
