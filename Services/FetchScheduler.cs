using System;
using System.Threading;
using System.Threading.Tasks;

namespace SensorClockViewer.Services
{
	public class FetchScheduler : IDisposable
	{
		private readonly AppSettings settings;
		public DbOperateService dbOperateService;
		private CancellationTokenSource? autoFetchCts;
		private Task? autoFetchTask;
		public FetchScheduler(AppSettings s, DbOperateService dbs)
		{
			settings = s;
			dbOperateService = dbs;
			settings.PropertyChanged += (s, e) =>
			{
				if((e.PropertyName == null) || (e.PropertyName == nameof(AppSettings.IsAutoFetchEnabled)) || (e.PropertyName == nameof(AppSettings.FetchIntervalSeconds)))
				{
					Stop();
					if(settings.IsAutoFetchEnabled) { Start(); }
				}
			};
			if(settings.IsAutoFetchEnabled) { Start(); }
		}
		public void Dispose()
		{
			Stop();
		}
		public void Start()
		{
			Stop();
			// Debug.Print("FetchScheduler: Start()");
			autoFetchCts = new CancellationTokenSource();
			autoFetchTask = RunAsync(autoFetchCts.Token);
		}
		public void Stop()
		{
			autoFetchCts?.Cancel();
			autoFetchCts?.Dispose();
			autoFetchCts = null;
			autoFetchTask = null;
			// Debug.Print("FetchScheduler: AutoFetchStop");
		}
		private async Task RunAsync(CancellationToken ct)
		{
			while(!ct.IsCancellationRequested)
			{
				int interval = settings.FetchIntervalSeconds;
				if(interval <= 0) { interval = 300; }
				try { await Task.Delay(TimeSpan.FromSeconds(interval), ct); }
				catch(OperationCanceledException) { break; }
				if(!ct.IsCancellationRequested)
				{
					// Debug.Print("FetchScheduler: invokeTask");
					await dbOperateService.FetchAllAsync();
				}
			}
		}
	}
}
