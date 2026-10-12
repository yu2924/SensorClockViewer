using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace SensorClockViewer.Services
{
	public class FileListService : IDisposable, INotifyPropertyChanged
	{
		public ObservableCollection<FileItem> FileItemList { get; } = new();
		public FileItem.SortCriteria SortBy
		{
			get => itemComparer.SortBy;
			set { if(itemComparer.SortBy != value) { itemComparer.SortBy = value; FileItemList.Clear(); RefillItems(); FirePropertyChanged(nameof(SortBy)); } }
		}
		public FileItem.SortDirection SortOrder
		{
			get => itemComparer.SortOrder;
			set { if(itemComparer.SortOrder != value) { itemComparer.SortOrder = value; FileItemList.Clear(); RefillItems(); FirePropertyChanged(nameof(SortOrder)); } }
		}
		protected void FirePropertyChanged(string? propname)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
		}
		public event PropertyChangedEventHandler? PropertyChanged;
		private readonly string targetDirectory;
		private readonly string targetFilter;
		private FileSystemWatcher fileSystemWatcher = new();
		private DispatcherQueue? dispatcherQueue;
		private bool alreadyQueued = false;
		private FileItem.Comparer itemComparer = new();
		// --------------------------------------------------------------------------------
		public FileListService(string dir, string filter, bool recursive = false)
		{
			targetDirectory = dir;
			targetFilter = filter;
			fileSystemWatcher.Path = dir;
			fileSystemWatcher.Filter = filter;
			fileSystemWatcher.IncludeSubdirectories = recursive;
			fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
			fileSystemWatcher.Changed += OnFileSystemWatcherChanged;
			fileSystemWatcher.Created += OnFileSystemWatcherChanged;
			fileSystemWatcher.Deleted += OnFileSystemWatcherChanged;
			fileSystemWatcher.Renamed += OnFileSystemWatcherChanged;
		}
		public void Dispose()
		{
			DeactivateWatcher();
			fileSystemWatcher.Dispose();
		}
		public void ActivateWatcher(DispatcherQueue disp)
		{
			FileItemList.Clear();
			RefillItems();
			dispatcherQueue = disp;
			alreadyQueued = false;
			fileSystemWatcher.EnableRaisingEvents = true;
		}
		public void DeactivateWatcher()
		{
			fileSystemWatcher.EnableRaisingEvents = false;
			dispatcherQueue = null;
			alreadyQueued = false;
		}
		private void OnFileSystemWatcherChanged(object sender, FileSystemEventArgs e)
		{
			if(alreadyQueued) { return; }
			alreadyQueued = true;
			dispatcherQueue?.TryEnqueue(() => { alreadyQueued = false; RefillItems(); });
		}
		private void RefillItems()
		{
			string[] existingpaths = Directory.EnumerateFiles(targetDirectory, targetFilter, SearchOption.TopDirectoryOnly).ToArray();
			for(int i = FileItemList.Count - 1; 0 <= i; --i)
			{
				if(!existingpaths.Contains(FileItemList[i].Path)) { FileItemList.RemoveAt(i); }
			}
			foreach(var path in existingpaths)
			{
				int ii = FileItemList.FindIndexOf(path);
				if(0 <= ii) { FileItemList[ii].Update(); }
				else { FileItemList.AddSortedBinary(new FileItem(path), itemComparer); }
			}
		}
	}
	// --------------------------------------------------------------------------------
	public class FileItem
	{
		public string Path { get; set; } = string.Empty;
		public string FileName { get; set; } = string.Empty;
		public DateTime Timestamp { get; set; }
		public FileItem(string path) { Path = path; FileName = System.IO.Path.GetFileName(path); Timestamp = File.GetLastWriteTimeUtc(path); }
		public void Update() { Timestamp = File.GetLastWriteTimeUtc(Path); }
		public enum SortCriteria { FileName, Timestamp };
		public enum SortDirection { Ascend, Descend };
		public class Comparer : IComparer<FileItem>
		{
			private static readonly IComparer<string> cmpFileName = StringComparer.CurrentCultureIgnoreCase;
			private static readonly DateTime dateTime0 = new DateTime(0);
			private int sign;
			public SortCriteria SortBy
			{
				get;
				set;
			} = SortCriteria.Timestamp;
			public SortDirection SortOrder
			{
				get;
				set {
					field = value;
					sign = value switch
					{
						SortDirection.Ascend => 1,
						SortDirection.Descend => -1,
						_ => 0,
					};
				}
			} = SortDirection.Descend;
			public int Compare(FileItem? a, FileItem? b)
			{
				return SortBy switch
				{
					SortCriteria.FileName => cmpFileName.Compare(a?.FileName, b?.FileName),
					SortCriteria.Timestamp => DateTime.Compare(a?.Timestamp ?? dateTime0, b?.Timestamp ?? dateTime0),
					_ => 0,
				} * sign;
			}
		}
	}
	// --------------------------------------------------------------------------------
	internal static class ObservableCollectionExtensions
	{
		public static void AddSorted<T>(this ObservableCollection<T> collection, T item, IComparer<T> comparer)
		{
			int i = 0;
			while((i < collection.Count) && (comparer.Compare(collection[i], item) < 0)) { ++i; }
			collection.Insert(i, item);
		}
		public static void AddSortedBinary<T>(this ObservableCollection<T> collection, T item, IComparer<T> comparer)
		{
			int lower = 0, upper = collection.Count - 1;
			while(lower <= upper)
			{
				int mid = lower + (upper - lower) / 2;
				if(comparer.Compare(collection[mid], item) < 0) { lower = mid + 1; }
				else { upper = mid - 1; }
			}
			collection.Insert(lower, item);
		}
		public static int FindIndexOf(this ObservableCollection<FileItem> collection, string path)
		{
			for(int c = collection.Count, i = 0; i < c; ++i)
			{
				if(collection[i].Path == path) return i;
			}
			return -1;
		}
	}
}
