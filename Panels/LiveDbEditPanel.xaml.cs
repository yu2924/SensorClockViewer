using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Diagnostics;
using SensorClockViewer.Services;

namespace SensorClockViewer.Panels
{
	public sealed partial class LiveDbEditPanel : UserControl
	{
		// --------------------------------------------------------------------------------
		// properties
		private static readonly SolidColorBrush ToneBrushDefault = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
		public static readonly DependencyProperty ToneBrushProperty = DependencyProperty.Register("ToneBrush", typeof(Brush), typeof(LiveDbEditPanel), new PropertyMetadata(ToneBrushDefault));
		public Brush ToneBrush
		{
			get { return (Brush)GetValue(ToneBrushProperty); }
			set { SetValue(ToneBrushProperty, value); }
		}
		public static readonly DependencyProperty TargetTaskBusyProperty = DependencyProperty.Register("TargetTaskBusy", typeof(bool), typeof(LiveDbEditPanel), new PropertyMetadata(false));
		public bool TargetTaskBusy
		{
			get { return (bool)GetValue(TargetTaskBusyProperty); }
			set { SetValue(TargetTaskBusyProperty, value); }
		}
		public static readonly DependencyProperty OtherTaskBusyProperty = DependencyProperty.Register("OtherTaskBusy", typeof(bool), typeof(LiveDbEditPanel), new PropertyMetadata(false));
		public bool OtherTaskBusy
		{
			get { return (bool)GetValue(OtherTaskBusyProperty); }
			set { SetValue(OtherTaskBusyProperty, value); }
		}
		public static readonly DependencyProperty TaskProgressProperty = DependencyProperty.Register("TaskProgress", typeof(double), typeof(LiveDbEditPanel), new PropertyMetadata(0.0));
		public double TaskProgress
		{
			get { return (double)GetValue(TaskProgressProperty); }
			set { SetValue(TaskProgressProperty, value); }
		}
		public static readonly DependencyProperty TaskIndeterminateProperty = DependencyProperty.Register("TaskIndeterminate", typeof(bool), typeof(LiveDbEditPanel), new PropertyMetadata(false));
		public bool TaskIndeterminate
		{
			get { return (bool)GetValue(TaskIndeterminateProperty); }
			set { SetValue(TaskIndeterminateProperty, value); }
		}
		public string Title
		{
			get => TitleTextBlock.Text;
			set => TitleTextBlock.Text = value;
		}
		public enum PlotTypes { None, Sensor, Weather, };
		public PlotTypes PlotType
		{
			get;
			set;
		} = PlotTypes.None;
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
		private enum TargetRange { Selected = 0, Entire, }
		// --------------------------------------------------------------------------------
		public LiveDbEditPanel()
		{
			InitializeComponent();
			UpdateEnabledState();
		}
		private void UpdateEnabledState()
		{
			bool dbavail = dbOperateService != null;
			bool idle = !TargetTaskBusy && !OtherTaskBusy;
			SelFromTextBox.IsEnabled = dbavail;
			SelToTextBox.IsEnabled = dbavail;
			TargetRadio.IsEnabled = dbavail && idle;
			DeleteButton.IsEnabled = dbavail && idle;
			FetchButton.IsEnabled = dbavail && idle;
		}
		private void ReplaceDbOpService(DbOperateService? dos)
		{
			if(dbOperateService != null) { dbOperateService.PropertyChanged -= OnDbOpPropertyChanged; }
			dbOperateService = dos;
			if(dbOperateService != null) { dbOperateService.PropertyChanged += OnDbOpPropertyChanged; }
			UpdateEnabledState();
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
					switch(PlotType)
					{
						case PlotTypes.Sensor:
							TargetTaskBusy = (tstat & DbOperateService.TaskFlags.Sensor) != DbOperateService.TaskFlags.Idle;
							OtherTaskBusy = (tstat & ~DbOperateService.TaskFlags.Sensor) != DbOperateService.TaskFlags.Idle;
							break;
						case PlotTypes.Weather:
							TargetTaskBusy = (tstat & DbOperateService.TaskFlags.Weather) != DbOperateService.TaskFlags.Idle;
							OtherTaskBusy = (tstat & ~DbOperateService.TaskFlags.Weather) != DbOperateService.TaskFlags.Idle;
							break;
					}
					UpdateEnabledState();
				});
			}
			if((e.PropertyName == null) || (e.PropertyName == nameof(Services.DbOperateService.TaskProgress)))
			{
				DispatcherQueue.TryEnqueue(() =>
				{
					var tstat = dbOperateService?.TaskStatus;
					bool istarget = PlotType switch
					{
						PlotTypes.Sensor => (tstat & DbOperateService.TaskFlags.Sensor) != DbOperateService.TaskFlags.Idle,
						PlotTypes.Weather => (tstat & DbOperateService.TaskFlags.Weather) != DbOperateService.TaskFlags.Idle,
						_ => false,
					};
					if(istarget) { double pv = dbOperateService?.TaskProgress ?? -1; TaskIndeterminate = pv < 0; TaskProgress = double.Max(0, pv); }
				});
			}
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
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			dbOperateService?.CancelAsyncTask();
		}
		private async void Fetch_Click(object sender, RoutedEventArgs e)
		{
			if(dbOperateService == null) { Debug.Print("ERROR: dbOperateService is null"); return; }
			bool selonly = TargetRadio.SelectedIndex == (int)TargetRange.Selected;
			DateTime? dtf = selonly ? selection?.Range.From : null;
			DateTime? dtt = selonly ? selection?.Range.To : null;
			DbOperateService.TaskResult r = PlotType switch
			{
#if TEST_ASYNCTASK
				PlotTypes.Sensor => await dbOperateService.DebugDummyTaskAsync(DbOperateService.TaskFlags.Sensor),
				PlotTypes.Weather => await dbOperateService.DebugDummyTaskAsync(DbOperateService.TaskFlags.Weather),
#else
				PlotTypes.Sensor => await dbOperateService.FetchSensorAsync(dtf, dtt),
				PlotTypes.Weather => await dbOperateService.FetchWeatherAsync(dtf, dtt),
#endif
				_ => DbOperateService.TaskResult.Failed,
			};
			if(r == DbOperateService.TaskResult.Failed) { await MessageBoxes.ShowAsync(this, "Error", "Failed to fetch data."); }
		}
		private async void Delete_Click(object sender, RoutedEventArgs e)
		{
			if(dbOperateService == null) { Debug.Print("ERROR: dbOperateService is null"); return; }
			bool selonly = TargetRadio.SelectedIndex == (int)TargetRange.Selected;
			var rconf = await MessageBoxes.ShowConfirmAsync(this, "Delete", string.Format("Are you sure to delete the data for the {0} range?", selonly ? "selected" : "entire"), "Delete");
			if(rconf != ContentDialogResult.Primary) { return; }
			DateTime? dtf = selonly ? selection?.Range.From : null;
			DateTime? dtt = selonly ? selection?.Range.To : null;
			DbOperateService.TaskResult r = PlotType switch
			{
#if TEST_ASYNCTASK
				PlotTypes.Sensor => await dbOperateService.DebugDummyTaskAsync(DbOperateService.TaskFlags.Sensor),
				PlotTypes.Weather => await dbOperateService.DebugDummyTaskAsync(DbOperateService.TaskFlags.Weather),
#else
				PlotTypes.Sensor => await dbOperateService.DeleteSensorRecordsAsync(dtf, dtt),
				PlotTypes.Weather => await dbOperateService.DeleteWeatherRecordsAsync(dtf, dtt),
#endif
				_ => DbOperateService.TaskResult.Failed,
			};
			if(r == DbOperateService.TaskResult.Failed) { await MessageBoxes.ShowAsync(this, "Error", "Failed to delete data."); }
		}
	}
}
