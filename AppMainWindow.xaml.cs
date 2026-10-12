using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SensorClockViewer.Pages;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Storage.Streams;
using Windows.UI;

namespace SensorClockViewer
{
	public sealed partial class AppMainWindow : Window
	{
		public AppMainWindow()
		{
			InitializeComponent();
			if(!AppInfo.IsPackaged) { AppWindow.SetIcon(WindowHelpers.LoadNativeIconFromExeModule()); }
			SetupCustomTitleBar();
			LoadWindowState();
			Closed += (s, e) => SaveWindowState();
			if(Content is FrameworkElement fe) { fe.Loaded += (s, e) => ValidateDatabase(); }
			NavView.SelectionChanged += (s, e) => NavigateToSelectedPage();
			NavView.SelectedItem = NavView.MenuItems[0];
		}
		private async void SetupCustomTitleBar()
		{
			ExtendsContentIntoTitleBar = true;
			SetTitleBar(AppTitleBar);
			CorrectNativeTitleBarButtonColors();
			if(Content is FrameworkElement fe) { fe.ActualThemeChanged += (s, e) => CorrectNativeTitleBarButtonColors(); }
			if(Properties.Resources.ResourceManager.GetObject("appicon") is byte[] b) { AppTitleBar.IconSource = WindowHelpers.CreateIconSourceFromImageBytes(b); }
		}
		private void CorrectNativeTitleBarButtonColors()
		{
			if(!AppWindowTitleBar.IsCustomizationSupported()) return;
			if(Content is FrameworkElement fe)
			{
				AppWindowTitleBar titlebar = AppWindow.TitleBar;
				bool dark = fe.ActualTheme == ElementTheme.Dark;
				Color tclr = dark ? Colors.White : Colors.Black;
				titlebar.ButtonForegroundColor = tclr;
				titlebar.ButtonHoverBackgroundColor = tclr.WithAlpha(0.5f);
			}
		}
		private void LoadWindowState()
		{
			double scale = WindowHelpers.GetDpiScaleForWindow(this);
			AppSettings settings = ((App)Application.Current).Settings;
			var ws = settings.WindowState;
			if(ws.IsValid())
			{
				AppWindow.MoveAndResize(new RectInt32((int)Math.Round(ws.Left * scale), (int)Math.Round(ws.Top * scale), (int)Math.Round(ws.Width * scale), (int)Math.Round(ws.Height * scale)));
				AppWindow.SetPresenter(ws.PresenterKind);
			}
			else
			{
				AppWindow.ResizeClient(new SizeInt32((int)Math.Round(1400.0 * scale), (int)Math.Round(800.0 * scale)));
			}
		}
		private void SaveWindowState()
		{
			double scale = WindowHelpers.GetDpiScaleForWindow(this);
			AppSettings settings = ((App)Application.Current).Settings;
			settings.WindowState = new AppSettings.WindowStateContainer
			{
				Left = (double)AppWindow.Position.X / scale,
				Top = (double)AppWindow.Position.Y / scale,
				Width = (double)AppWindow.Size.Width / scale,
				Height = (double)AppWindow.Size.Height / scale,
				PresenterKind = AppWindow.Presenter.Kind,
			};
		}
		private async void ValidateDatabase()
		{
			App app = (App)App.Current;
			if(!app.LiveDbOperateService.IsValid)
			{
				var rconf = await MessageBoxes.ShowConfirmAsync(Content, "Error", "Failed to load the database.\nDo you want to recreate the database?", "Recreate");
				if(rconf != ContentDialogResult.Primary) { return; }
				bool r = app.LiveDbOperateService.RecreateDbService(app.LiveDbPath);
				if(!r) { await MessageBoxes.ShowAsync(Content, "Error", "Failed to recreate the database.\nPlease check the file system and restart."); }
			}
		}
		private void NavigateToSelectedPage()
		{
			if(NavView.SelectedItem is NavigationViewItem item)
			{
				Type? pageType = item.Tag switch
				{
					"LivePlotPage" => typeof(LivePlotPage),
					"ArchivePage" => typeof(ArchivePage),
					"SettingsPage" => typeof(SettingsPage),
					_ => null,
				};
				if(pageType != null) { ContentFrame.Navigate(pageType); }
			}
		}
	}
	// --------------------------------------------------------------------------------
	internal static class WindowHelpers
	{
		public static double GetDpiScaleForWindow(Window window)
		{
			const double DefaultDPI = 96.0;
			nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
			double scale = (double)NativeWin32.GetDpiForWindow(hwnd) / DefaultDPI;
			if(scale <= 0) { scale = 1; }
			return scale;
		}
		public static IconId LoadNativeIconFromExeModule()
		{
			// IDI_APPLICATION = 32512
			// cf. https://devblogs.microsoft.com/oldnewthing/20250423-00/?p=111106
			IntPtr hm = NativeWin32.GetModuleHandleW(null);
			IntPtr hi = NativeWin32.LoadIconW(hm, 32512);
			return Win32Interop.GetIconIdFromIcon(hi);
		}
		public static ImageIconSource CreateIconSourceFromImageBytes(byte[] bytes)
		{
			var icostr = new InMemoryRandomAccessStream();
			using(var writer = new DataWriter(icostr.GetOutputStreamAt(0)))
			{
				writer.WriteBytes(bytes);
				writer.StoreAsync().GetAwaiter().GetResult();
			}
			var bm = new BitmapImage();
			bm.SetSource(icostr);
			return new ImageIconSource { ImageSource = bm };
		}
	}
	internal static class ColorExtension
	{
		public static Color WithAlpha(this Color clr, float alpha)
		{
			clr.A = (byte)Math.Max(0.0f, Math.Min(255.0f, alpha * 255.0f));
			return clr;
		}
	}
	internal static class NativeWin32
	{
		[DllImport("kernel32.dll")]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		public static extern IntPtr GetModuleHandleW(string? lpModuleName);
		[DllImport("user32.dll")]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		public static extern IntPtr LoadIconW(IntPtr hModule, IntPtr lpIconName);
		[DllImport("User32.dll")]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		public static extern int GetDpiForWindow(nint hwnd);
	}
}
