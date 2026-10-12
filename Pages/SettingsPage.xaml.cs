using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SensorClockViewer.Pages
{
	public sealed partial class SettingsPage : Page
	{
		public AppSettings Settings { get => ((App)Application.Current).Settings; }
		public string AppName { get; } = AppInfo.AppName;
		public string AppVersion { get; } = AppInfo.AppVersion;
		public string CompanyName { get; } = AppInfo.CompanyName;
		public string Copyright { get; } = AppInfo.Copyright;
		public SettingsPage()
		{
			InitializeComponent();
		}
	}
}
