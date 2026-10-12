using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using SensorClockViewer.Services;

namespace SensorClockViewer.Panels
{
	public sealed partial class LiveDbArchPanel : UserControl
	{
		// --------------------------------------------------------------------------------
		// properties
		public static readonly DependencyProperty TargetTaskBusyProperty = DependencyProperty.Register("TargetTaskBusy", typeof(bool), typeof(LiveDbArchPanel), new PropertyMetadata(false));
		public bool TargetTaskBusy
		{
			get { return (bool)GetValue(TargetTaskBusyProperty); }
			set { SetValue(TargetTaskBusyProperty, value); }
		}
		public static readonly DependencyProperty OtherTaskBusyProperty = DependencyProperty.Register("OtherTaskBusy", typeof(bool), typeof(LiveDbArchPanel), new PropertyMetadata(false));
		public bool OtherTaskBusy
		{
			get { return (bool)GetValue(OtherTaskBusyProperty); }
			set { SetValue(OtherTaskBusyProperty, value); }
		}
		private DbOperateService? dbOperateService = null;
		public DbOperateService? DbOperateService
		{
			get => dbOperateService;
			set => ReplaceDbOpService(value);
		}
		private Controls.EditSelection? selection = null;
		public Controls.EditSelection? Selection
		{
			get => selection;
			set => ReplaceSelection(value);
		}
		// --------------------------------------------------------------------------------
		public LiveDbArchPanel()
		{
			InitializeComponent();
			UpdateEnabledState();
		}
		private void UpdateEnabledState()
		{
			bool dbavail = dbOperateService != null;
			bool idle = !TargetTaskBusy && !OtherTaskBusy;
			bool userange = UseRangeCheck.IsChecked == true;
			UseRangeCheck.IsEnabled = dbavail;
			SelFromTextBox.IsEnabled = dbavail && userange;
			SelToTextBox.IsEnabled = dbavail && userange;
			RemoveRangeCheck.IsEnabled = dbavail;
			CreateButton.IsEnabled = dbavail && idle;
		}
		private void ReplaceDbOpService(DbOperateService? dcs)
		{
			if(dbOperateService != null)
			{
				dbOperateService.DataChanged -= OnDbOpDataChanged;
				dbOperateService.PropertyChanged -= OnDbOpPropertyChanged;
			}
			dbOperateService = dcs;
			if(dbOperateService != null)
			{
				dbOperateService.PropertyChanged += OnDbOpPropertyChanged;
				dbOperateService.DataChanged += OnDbOpDataChanged;
			}
			UpdateEnabledState();
			ReflectArchiveRangeToTextBoxes();
		}
		private void ReplaceSelection(Controls.EditSelection? sel)
		{
			if(selection != null) { selection.PropertyChanged -= OnSelectionPropertyChanged; }
			selection = sel;
			if(selection != null) { selection.PropertyChanged += OnSelectionPropertyChanged; }
			ReflectSelectionToTextBoxes();
		}
		private void OnDbOpPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if((e.PropertyName == null) || (e.PropertyName == nameof(Services.DbOperateService.TaskStatus)))
			{
				DispatcherQueue.TryEnqueue(() =>
				{
					var tstat = dbOperateService?.TaskStatus;
					TargetTaskBusy = (tstat & DbOperateService.TaskFlags.Archive) != DbOperateService.TaskFlags.Idle;
					OtherTaskBusy = (tstat & ~DbOperateService.TaskFlags.Archive) != DbOperateService.TaskFlags.Idle;
					UpdateEnabledState();
				});
			}
		}
		private void OnDbOpDataChanged(object? sender, DbOperateService.DataChangedEventArgs e)
		{
			DispatcherQueue.TryEnqueue(ReflectArchiveRangeToTextBoxes);
		}
		private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if((e.PropertyName == null) || (e.PropertyName == nameof(Controls.EditSelection.Range)))
			{
				DispatcherQueue.TryEnqueue(() => { ReflectSelectionToTextBoxes(); });
			}
		}
		private void ReflectSelectionToTextBoxes()
		{
			if(selection == null) { Debug.Print("ERROR: selection is null"); return; }
			SelFromTextBox.Text = selection.Range.From.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
			SelToTextBox.Text = selection.Range.To.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
		}
		private void ReflectSelectionFromTextBoxes()
		{
			if(selection == null) { Debug.Print("ERROR: selection is null"); return; }
			DateTime locdtf; bool rf = DateTime.TryParse(SelFromTextBox.Text, out locdtf);
			DateTime locdtt; bool rt = DateTime.TryParse(SelToTextBox.Text, out locdtt);
			if(rf && rt) { selection.Range = new Controls.EditSelection.DateTimeRange(locdtf.ToUniversalTime(), locdtt.ToUniversalTime()); }
		}
		private void ReflectArchiveRangeToTextBoxes()
		{
			if(dbOperateService == null) { Debug.Print("ERROR: dbOperateService is null"); return; }
			var rng = CombineDateTimeRange(dbOperateService.GetSensorRange(), dbOperateService.GetWeatherRange());
			SelFromTextBox.Text = rng.Item1.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
			SelToTextBox.Text = rng.Item2.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
		}
		// --------------------------------------------------------------------------------
		// ui event handlers
		private void SelRangeTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
		{
			if(e.Key == Windows.System.VirtualKey.Enter) { ReflectSelectionFromTextBoxes(); }
		}
		private void SelRangeTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			ReflectSelectionFromTextBoxes();
		}
		private Tuple<DateTime, DateTime> CombineDateTimeRange(Tuple<DateTime, DateTime>? a, Tuple<DateTime, DateTime>? b)
		{
			if(a == null) { return (b != null) ? b : new(DateTime.MinValue, DateTime.MinValue); }
			if(b == null) { return (a != null) ? a : new(DateTime.MinValue, DateTime.MinValue); }
			return new((a.Item1 <= b.Item1) ? a.Item1 : b.Item1, (a.Item2 <= b.Item2) ? b.Item2 : a.Item2);
		}
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			dbOperateService?.CancelAsyncTask();
		}
		private void UseRange_Click(object sender, RoutedEventArgs e)
		{
			UpdateEnabledState();
		}
		private async void ArchiveCreate_Click(object sender, RoutedEventArgs e)
		{
			if(dbOperateService == null) { Debug.Print("ERROR: dbOperateService is null"); return; }
			var app = (App)App.Current;
			if(app.MainWindow == null) { Debug.Print("ERROR: MainWindow is null"); return; }
			bool opttrim = UseRangeCheck.IsChecked == true;
			bool optremove = RemoveRangeCheck.IsChecked == true;
			var rng = CombineDateTimeRange(dbOperateService.GetSensorRange(), dbOperateService.GetWeatherRange());
			DateTime dtf = rng.Item1;
			DateTime dtt = rng.Item2;
			if(opttrim)
			{
				DateTime locdtf; bool rf = DateTime.TryParse(SelFromTextBox.Text, out locdtf);
				DateTime locdtt; bool rt = DateTime.TryParse(SelToTextBox.Text, out locdtt);
				if(!rf || !rt) { await MessageBoxes.ShowAsync(this, "Error", "Invalid datetime format."); return; }
				dtf = locdtf.ToUniversalTime();
				dtt = locdtt.ToUniversalTime();
			}
			string filename = string.Format("{0}_{1}.db", dtf.ToLocalTime().ToString("yyyy-MM-dd"), dtt.ToLocalTime().ToString("yyyy-MM-dd"));
			var savepicker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(app.MainWindow.AppWindow.Id)
			{
				SuggestedFileName = filename,
				SuggestedFolder = app.ArchiveDirectory,
				CommitButtonText = "Create",
				FileTypeChoices = { { "Database", new List<string> { ".db" } } },
				DefaultFileExtension = ".db",
				ShowOverwritePrompt = true,
			};
			var rdlg = await savepicker.PickSaveFileAsync();
			if(rdlg == null) { return; }
			string archpath = rdlg.Path;
			if(sender is Button btn) { btn.Focus(FocusState.Programmatic); }
#if TEST_ASYNCTASK
			DbOperateService.TaskResult r = await dbOperateService.DebugDummyTaskAsync(DbOperateService.TaskFlags.Archive);
#else
			DbOperateService.TaskResult r = await dbOperateService.CreateArchiveAsync(archpath, dtf, dtt);
			if((r == DbOperateService.TaskResult.Succeeded) && optremove)
			{
				await dbOperateService.DeleteSensorRecordsAsync(dtf, dtt);
				await dbOperateService.DeleteWeatherRecordsAsync(dtf, dtt);
			}
#endif
			if(r == DbOperateService.TaskResult.Failed) { await MessageBoxes.ShowAsync(this, "Error", "Failed to create a new archive."); }
		}
	}
}
