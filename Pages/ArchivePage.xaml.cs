using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SensorClockViewer.Panels;
using SensorClockViewer.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SensorClockViewer.Pages
{
	public sealed partial class ArchivePage : Page
	{
		private DbOperateService? dbOperateService = null;
		public ArchivePage()
		{
			InitializeComponent();
			ArchDbSensorTrimPanel.Selection = PlotArrayCtrl.SensorSelection;
			ArchDbWeatherTrimPanel.Selection = PlotArrayCtrl.WeatherSelection;
			Loaded += (s, e) => OnLoaded();
		}
		private void OnLoaded()
		{
			SelectOpPane(0);
		}
		private void SelectOpPane(int i)
		{
			ArchFileListPanel?.Visibility = (i == 0) ? Visibility.Visible : Visibility.Collapsed;
			ArchDbTrimPanel?.Visibility = (i == 1) ? Visibility.Visible : Visibility.Collapsed;
			TabListView?.SelectedIndex = i;
		}
		private void RefreshPlot(DbOperateService.TaskFlags tf)
		{
			if((tf & DbOperateService.TaskFlags.Sensor) != 0) { PlotArrayCtrl.SetSensorData(dbOperateService?.GetSensorRecords()); }
			if((tf & DbOperateService.TaskFlags.Weather) != 0) { PlotArrayCtrl.SetWeatherData(dbOperateService?.GetWeatherRecords()); }
		}
		private bool OpenItem(string? path)
		{
			if(dbOperateService != null) { dbOperateService.Dispose(); dbOperateService = null; }
			bool r = true;
			if(path != null)
			{
				Debug.Print("OpenItem file={0}", path);
				dbOperateService = new(path, null);
				if(dbOperateService.IsValid)
				{
					dbOperateService.DataChanged += (s, e) => DispatcherQueue.TryEnqueue(() => RefreshPlot(e.TaskChanged));
				}
				else
				{
					dbOperateService.Dispose();
					dbOperateService = null;
					r = false;
				}
			}
			ArchDbSensorTrimPanel.DbOperateService = dbOperateService;
			ArchDbWeatherTrimPanel.DbOperateService = dbOperateService;
			RefreshPlot(DbOperateService.TaskFlags.Sensor | DbOperateService.TaskFlags.Weather);
			DispatcherQueue.TryEnqueue(() => PlotArrayCtrl.DefaultScaleAxes());
			return r;
		}
		private void CloseItem() => OpenItem(null);
		private bool DeleteItems(string[] paths)
		{
			if((dbOperateService != null) && (dbOperateService.DbService != null) && paths.Contains(dbOperateService.DbService.FilePath)) { CloseItem(); }
			Debug.Print("DeleteItems numfiles={0}", paths.Length);
			bool r = true;
			try { foreach(var path in paths) { Debug.Print("deleting... \"{0}\"", path); System.IO.File.Delete(path); } }
			catch(Exception e) { r = false; Debug.Print("File.Delete() failed, {0}", e); }
			return r;
		}
		// --------------------------------------------------------------------------------
		// ui event handlers
		private void TabListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SelectOpPane(TabListView.SelectedIndex);
		}
		private void ArchList_FilCommandLaunched(object sender, Panels.ArchFileListPanel.FileCommandEventArgs e)
		{
			switch(e.Command)
			{
				case ArchFileListPanel.FileCommand.Open:
					HandleFileCommandOpen(e.Items);
					break;
				case ArchFileListPanel.FileCommand.Delete:
					HandleFileCommandDelete(e.Items);
					break;
			}
		}
		private void HandleFileCommandOpen(string[] targets)
		{
			if(targets is null) return;
			if(targets.Length != 1) return;
			OpenItem(targets.First());
		}
		private async void HandleFileCommandDelete(string[] targets)
		{
			if(targets is null) return;
			StringBuilder fnlist = new();
			for(int c = Math.Min(3, targets.Length), i = 0; i < c; ++i)
			{
				if(0 < i) { fnlist.Append("\n"); }
				fnlist.Append(System.IO.Path.GetFileName(targets[i]));
			}
			if(3 < targets.Length) { fnlist.Append("\n..."); }
			var rconf = await MessageBoxes.ShowConfirmAsync(this, "Confirm", string.Format("Are you sure to delete the selected {0} file(s)?\n\n{1}", targets.Length, fnlist), "Delete");
			if(rconf != ContentDialogResult.Primary) { return; }
			bool r = DeleteItems(targets);
			if(!r) { await MessageBoxes.ShowAsync(this, "Error", "Some error occured."); }
		}
	}
}
