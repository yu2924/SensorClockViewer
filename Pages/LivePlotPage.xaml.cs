using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SensorClockViewer.Controls;
using SensorClockViewer.Services;

namespace SensorClockViewer.Pages
{
	public sealed partial class LivePlotPage : Page
	{
		private DbOperateService dbOperateService = ((App)Application.Current).LiveDbOperateService;
		public LivePlotPage()
		{
			InitializeComponent();
			SensorEditPanel.DbOperateService = dbOperateService;
			WeatherEditPanel.DbOperateService = dbOperateService;
			CreateArchPanel.DbOperateService = dbOperateService;
			SensorEditPanel.Selection = PlotArrayCtrl.SensorSelection;
			WeatherEditPanel.Selection = PlotArrayCtrl.WeatherSelection;
			CreateArchPanel.Selection = PlotArrayCtrl.SensorSelection;
			Loaded += (s, e) => OnLoaded();
			dbOperateService.DataChanged += (s, e) => DispatcherQueue.TryEnqueue(() => RefreshPlot(e.TaskChanged));
		}
		private void OnLoaded()
		{
			SelectOpPane(0);
			RefreshPlot(DbOperateService.TaskFlags.Sensor | DbOperateService.TaskFlags.Weather);
		}
		private void SelectOpPane(int i)
		{
			SensorEditPanel?.Visibility = (i == 0) ? Visibility.Visible : Visibility.Collapsed;
			WeatherEditPanel?.Visibility = (i == 0) ? Visibility.Visible : Visibility.Collapsed;
			CreateArchPanel?.Visibility = (i == 1) ? Visibility.Visible : Visibility.Collapsed;
			TabListView?.SelectedIndex = i;
			if(i == 1) { EditSelection.MakeLink(PlotArrayCtrl.SensorSelection, PlotArrayCtrl.WeatherSelection); }
			else { EditSelection.BreakLink(PlotArrayCtrl.SensorSelection, PlotArrayCtrl.WeatherSelection); }
		}
		private void RefreshPlot(DbOperateService.TaskFlags tf)
		{
			if((tf & DbOperateService.TaskFlags.Sensor) != 0) { PlotArrayCtrl.SetSensorData(dbOperateService.GetSensorRecords()); }
			if((tf & DbOperateService.TaskFlags.Weather) != 0) { PlotArrayCtrl.SetWeatherData(dbOperateService.GetWeatherRecords()); }
			PlotArrayCtrl.DefaultScaleAxes();
		}
		// --------------------------------------------------------------------------------
		// ui event handlers
		private void TabListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SelectOpPane(TabListView.SelectedIndex);
		}
	}
}
