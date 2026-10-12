using Microsoft.UI.Windowing;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace SensorClockViewer
{
	public class AppSettings : IDisposable, INotifyPropertyChanged
	{
		public class WindowStateContainer
		{
			public double Left { get; set; } = 0;
			public double Top { get; set; } = 0;
			public double Width { get; set; } = -1;
			public double Height { get; set; } = -1;
			public AppWindowPresenterKind PresenterKind { get; set; } = AppWindowPresenterKind.Default;
			public bool IsValid() { return (0 < Width) && (0 < Height); }
		}
		private class RawProperties
		{
			public string ServerName { get; set; } = "SensorClock.local";
			public int Port { get; set; } = 80;
			public string UserName { get; set; } = "SensorClock";
			public string Password { get; set; } = "password";
			public bool IsAutoFetchEnabled { get; set; } = false;
			public int FetchIntervalSeconds { get; set; } = 300;
			public WindowStateContainer WindowState { get; set; } = new WindowStateContainer();
		}
		private RawProperties rawProperties = new RawProperties();
		public string ServerName
		{
			get => rawProperties.ServerName;
			set { if(rawProperties.ServerName != value) { rawProperties.ServerName = value; SetDirty(true); FirePropertyChanged(nameof(ServerName)); } }
		}
		public int Port
		{
			get => rawProperties.Port;
			set { if(rawProperties.Port != value) { rawProperties.Port = value; SetDirty(true); FirePropertyChanged(nameof(Port)); } }
		}
		public string UserName
		{
			get => rawProperties.UserName;
			set { if(rawProperties.UserName != value) { rawProperties.UserName = value; SetDirty(true); FirePropertyChanged(nameof(UserName)); } }
		}
		public string Password
		{
			get => rawProperties.Password;
			set { if(rawProperties.Password != value) { rawProperties.Password = value; SetDirty(true); FirePropertyChanged(nameof(Password)); } }
		}
		public bool IsAutoFetchEnabled
		{
			get => rawProperties.IsAutoFetchEnabled;
			set { if(rawProperties.IsAutoFetchEnabled != value) { rawProperties.IsAutoFetchEnabled = value; SetDirty(true); FirePropertyChanged(nameof(IsAutoFetchEnabled)); } }
		}
		public int FetchIntervalSeconds
		{
			get => rawProperties.FetchIntervalSeconds;
			set { if(rawProperties.FetchIntervalSeconds != value) { rawProperties.FetchIntervalSeconds = value; SetDirty(true); FirePropertyChanged(nameof(FetchIntervalSeconds)); } }
		}
		public WindowStateContainer WindowState
		{
			get => rawProperties.WindowState;
			set { rawProperties.WindowState = value; SetDirty(true); FirePropertyChanged(nameof(WindowStateContainer)); }
		}
		protected void FirePropertyChanged(string? propname)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
		}
		public event PropertyChangedEventHandler? PropertyChanged;
		private const int autoSaveDelayMs = 3000;
		private readonly System.Threading.Timer autoSaveTimer;
		private readonly object saveLock = new();
		private bool dirty = false;
		private string persistencePath;
		public AppSettings(string path)
		{
			persistencePath = path;
			autoSaveTimer = new Timer((state) => Save(), null, Timeout.Infinite, Timeout.Infinite);
			Load();
		}
		private void SetDirty(bool d)
		{
			lock(saveLock)
			{
				dirty = d;
				autoSaveTimer.Change(dirty ? autoSaveDelayMs : Timeout.Infinite, Timeout.Infinite);
			}
		}
		public void Load()
		{
			rawProperties = new RawProperties();
			if(System.IO.Path.Exists(persistencePath))
			{
				try
				{
					string json = File.ReadAllText(persistencePath);
					rawProperties = JsonSerializer.Deserialize<RawProperties>(json) ?? new RawProperties();
				}
				catch { Debug.Print("AppSettings: JsonSerializer.Deserialize() failed"); }
			}
			SetDirty(false);
			FirePropertyChanged(null);
		}
		public void Save()
		{
			lock(saveLock)
			{
				if(!dirty) { return; }
				string json = JsonSerializer.Serialize(rawProperties, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(persistencePath, json);
				dirty = false;
			}
		}
		public void Dispose()
		{
			Save();
			autoSaveTimer?.Dispose();
		}
	}
}
