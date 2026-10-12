using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SensorClockViewer
{
	public static class AppInfo
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.U4)]
		private static extern uint GetCurrentPackageId(ref int pBufferLength, out byte pBuffer);
		private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
		private static bool IsAppPackaged()
		{
			int bufferLength = 0;
			byte byteBuffer = 0;
			return GetCurrentPackageId(ref bufferLength, out byteBuffer) != APPMODEL_ERROR_NO_PACKAGE;
		}
		public static bool IsPackaged { get; } = IsAppPackaged();
		public static string AppName { get; } = IsAppPackaged()
			? Windows.ApplicationModel.Package.Current.DisplayName
			: typeof(App).Assembly.GetName().Name ?? "unknown";
		public static string AppVersion { get; } = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unknown";
		public static string CompanyName { get; } = (typeof(App).Assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute)).First() as AssemblyCompanyAttribute)?.Company ?? "unknown";
		public static string Copyright { get; } = (typeof(App).Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute)).First() as AssemblyCopyrightAttribute)?.Copyright ?? "unknown";
		public static string UserDataDirectory { get; } = IsAppPackaged()
			? Windows.Storage.ApplicationData.Current.LocalFolder.Path
			: System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyName, AppName);
	}
}
