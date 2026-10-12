using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using SensorClockViewer.Services;
using System;
using System.Diagnostics;

namespace SensorClockViewer
{
	public partial class App : Application
	{
		public string LiveDbPath { get; }
		public string ArchiveDirectory { get; }
		public AppSettings Settings { get; }
		public DbOperateService LiveDbOperateService { get; }
		public FetchScheduler LiveDbFetchScheduler { get; }
		public FileListService ArchFileListService { get; }
		public Window? MainWindow { get; private set; }
		public App()
		{
			InitializeComponent();
			Debug.Print("UserDataDirectory={0}", AppInfo.UserDataDirectory);
			LiveDbPath = System.IO.Path.Combine(AppInfo.UserDataDirectory, "live.db");
			ArchiveDirectory = System.IO.Path.Combine(AppInfo.UserDataDirectory, "Archives");
			System.IO.Directory.CreateDirectory(AppInfo.UserDataDirectory);
			System.IO.Directory.CreateDirectory(ArchiveDirectory);
			Settings = new AppSettings(System.IO.Path.Combine(AppInfo.UserDataDirectory, "settings.json"));
			LiveDbOperateService = new DbOperateService(LiveDbPath, Settings);
			LiveDbFetchScheduler = new FetchScheduler(Settings, LiveDbOperateService);
			ArchFileListService = new FileListService(ArchiveDirectory, "*.db", false);
		}
		protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
		{
			MainWindow = new AppMainWindow();
			MainWindow.Closed += (s, e) => FinalizeObjects();
			MainWindow.Activate();
			ArchFileListService.ActivateWatcher(MainWindow.DispatcherQueue);
		}
		private void FinalizeObjects()
		{
			ArchFileListService.Dispose();
			LiveDbFetchScheduler.Dispose();
			LiveDbOperateService.Dispose();
			Settings.Dispose();
		}
	}
	// --------------------------------------------------------------------------------
	// global converters
	public class InvBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is bool bv) ? !bv : false;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is bool bv) ? !bv : false;
		}
	}
	public class NullableBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is bool bv) ? bv : false;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is bool bv) ? bv : false;
		}
	}
	public class BooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is bool bv) ? (bv ? Visibility.Visible : Visibility.Collapsed) : Visibility.Visible;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is Visibility vv) ? (vv == Visibility.Visible) : false;
		}
	}
	public class InvBooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is bool bv) ? (bv ? Visibility.Collapsed : Visibility.Visible) : Visibility.Visible;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is Visibility vv) ? (vv == Visibility.Collapsed) : false;
		}
	}
}
