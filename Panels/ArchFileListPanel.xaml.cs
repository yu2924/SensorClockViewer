using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using SensorClockViewer.Services;

namespace SensorClockViewer.Panels
{
	public sealed partial class ArchFileListPanel : UserControl
	{
		// --------------------------------------------------------------------------------
		// properties
		public static readonly DependencyProperty IsSelectedOneProperty = DependencyProperty.Register("IsSelectedOne", typeof(bool), typeof(ArchFileListPanel), new PropertyMetadata(false));
		public bool IsSelectedOne
		{
			get { return (bool)GetValue(IsSelectedOneProperty); }
			set { SetValue(IsSelectedOneProperty, value); }
		}
		public static readonly DependencyProperty IsSelectedAnyProperty = DependencyProperty.Register("IsSelectedAny", typeof(bool), typeof(ArchFileListPanel), new PropertyMetadata(false));
		public bool IsSelectedAny
		{
			get { return (bool)GetValue(IsSelectedAnyProperty); }
			set { SetValue(IsSelectedAnyProperty, value); }
		}
		public static readonly DependencyProperty FilenameSortIconProperty = DependencyProperty.Register("FilenameSortIcon", typeof(string), typeof(ArchFileListPanel), new PropertyMetadata(string.Empty));
		public string FilenameSortIcon
		{
			get { return (string)GetValue(FilenameSortIconProperty); }
			set { SetValue(FilenameSortIconProperty, value); }
		}
		public static readonly DependencyProperty TimestampSortIconProperty = DependencyProperty.Register("TimestampSortIcon", typeof(string), typeof(ArchFileListPanel), new PropertyMetadata(string.Empty));
		public string TimestampSortIcon
		{
			get { return (string)GetValue(TimestampSortIconProperty); }
			set { SetValue(TimestampSortIconProperty, value); }
		}
		public enum FileCommand { Invalid, Open, Delete, }
		public class FileCommandEventArgs : EventArgs
		{
			public FileCommand Command { get; }
			public string[] Items { get; }
			public FileCommandEventArgs(FileCommand cmd, string[] sl) { Command = cmd; Items = sl; }
		}
		public delegate void FileCommandEventHandler(object sender, FileCommandEventArgs e);
		public event FileCommandEventHandler? FilCommandLaunched;
		public FileListService ArchListService { get; } = ((App)Microsoft.UI.Xaml.Application.Current).ArchFileListService;
		// --------------------------------------------------------------------------------
		public ArchFileListPanel()
		{
			InitializeComponent();
			UpdateListviewHeaderVisual();
		}
		// --------------------------------------------------------------------------------
		// ui event handlers, listview header
		private void UpdateListviewHeaderVisual()
		{
			string glyph = ArchListService.SortOrder switch
			{
				FileItem.SortDirection.Ascend => "\xe70e", // ChevronUp,
				FileItem.SortDirection.Descend => "\xe70d", // ChevronDown
				_ => string.Empty,
			};
			switch(ArchListService.SortBy)
			{
				case FileItem.SortCriteria.FileName:
					FilenameSortIcon = glyph;
					TimestampSortIcon = string.Empty;
					break;
				case FileItem.SortCriteria.Timestamp:
					FilenameSortIcon = string.Empty;
					TimestampSortIcon = glyph;
					break;
			}
		}
		private void LvHdrFilename_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if(e.GetCurrentPoint(this).Properties.IsRightButtonPressed) { return; }
			bool desc = ArchListService.SortOrder == FileItem.SortDirection.Descend;
			desc = (ArchListService.SortBy != FileItem.SortCriteria.FileName) ? false : !desc;
			ArchListService.SortBy = FileItem.SortCriteria.FileName;
			ArchListService.SortOrder = desc ? FileItem.SortDirection.Descend : FileItem.SortDirection.Ascend;
			UpdateListviewHeaderVisual();
		}
		private void LvHdrTimestamp_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if(e.GetCurrentPoint(this).Properties.IsRightButtonPressed) { return; }
			bool desc = ArchListService.SortOrder == FileItem.SortDirection.Descend;
			desc = (ArchListService.SortBy != FileItem.SortCriteria.Timestamp) ? false : !desc;
			ArchListService.SortBy = FileItem.SortCriteria.Timestamp;
			ArchListService.SortOrder = desc ? FileItem.SortDirection.Descend : FileItem.SortDirection.Ascend;
			UpdateListviewHeaderVisual();
		}
		// --------------------------------------------------------------------------------
		// ui event handlers, listview items
		private void FileListItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
		{
			if((sender is FrameworkElement fe) && (fe.DataContext is FileItem fi))
			{
				if(!FileListView.SelectedItems.Any(it => ((FileItem)it).Path == fi.Path)) { FileListView.SelectedItem = fi; }
			}
		}
		private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var sel = FileListView.SelectedItems;
			IsSelectedOne = sel.Count == 1;
			IsSelectedAny = 0 < sel.Count;
		}
		private async void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
		{
			if(FileListView.SelectedItems.Count != 1) { return; }
			int ii = FileListView.SelectedIndex;
			if(FileListView.SelectedItem is FileItem fi)
			{
				FilCommandLaunched?.Invoke(this, new FileCommandEventArgs(FileCommand.Open, new string[] { fi.Path }));
			}
		}
		private async void OpenFileListItem_Click(object sender, RoutedEventArgs e)
		{
			var targets = FileListView.SelectedItems.Select(item => ((FileItem)item).Path).ToArray();
			if((targets == null) || (targets.Length != 1)) { return; }
			FilCommandLaunched?.Invoke(this, new FileCommandEventArgs(FileCommand.Open, targets));
		}
		private async void DeleteFileListItem_Click(object sender, RoutedEventArgs e)
		{
			var targets = FileListView.SelectedItems.Select(item => ((FileItem)item).Path).ToArray();
			if((targets == null) || (targets.Length <= 0)) { return; }
			FilCommandLaunched?.Invoke(this, new FileCommandEventArgs(FileCommand.Delete, targets));
		}
	}
	// --------------------------------------------------------------------------------
	// converters
	public class DateTimeTolocalStringConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is DateTime dt) ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is string sv) ? DateTime.Parse(sv).ToUniversalTime() : new DateTime();
		}
	}
	public class SortKindToIntConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is FileItem.SortCriteria crit) ? (int)crit : 0;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is int iv) ? (FileItem.SortCriteria)Math.Max(0, iv) : FileItem.SortCriteria.FileName;
		}
	}
	public class SortDirectionToIntConverter : IValueConverter
	{
		public object Convert(object value, Type targettype, object parameter, string lang)
		{
			return (value is FileItem.SortDirection dir) ? (int)dir : 0;
		}
		public object ConvertBack(object value, Type targettype, object parameter, string lang)
		{
			return (value is int iv) ? (FileItem.SortDirection)Math.Max(0, iv) : FileItem.SortDirection.Ascend;
		}
	}
}
