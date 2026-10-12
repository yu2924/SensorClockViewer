using System;
using System.ComponentModel;
using static SensorClockViewer.Controls.PlotArrayControl;

namespace SensorClockViewer.Controls
{
	public class EditSelection : INotifyPropertyChanged
	{
		public struct DateTimeRange
		{
			public DateTime From = DateTime.UtcNow;
			public DateTime To = DateTime.UtcNow;
			public DateTimeRange() {}
			public DateTimeRange(DateTime f, DateTime t) { From = (f <= t) ? f : t; To = (f <= t) ? t : f; }
			public DateTimeRange Normalized() { return new((From <= To) ? From : To, (From <= To) ? To : From); }
			public static DateTimeRange Union(DateTimeRange a, DateTimeRange b) { return new DateTimeRange((a.From <= b.From) ? a.From : b.From, (a.To <= b.To) ? b.To : a.To); }
			public static DateTimeRange operator |(DateTimeRange a, DateTimeRange b) { return Union(a, b); }
		}
		private DateTimeRange range = new DateTimeRange();
		public DateTimeRange Range
		{
			get => range;
			set
			{
				InternalSetRange(value);
				linkedSelection?.InternalSetRange(value);
			}
		}
		private void InternalSetRange(DateTimeRange v)
		{
			var vn = v.Normalized();
			if((range.From != vn.From) || (range.To != vn.To)) { range = vn; FirePropertyChanged(nameof(Range)); }
		}
		protected void FirePropertyChanged(string? propname)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
		}
		public event PropertyChangedEventHandler? PropertyChanged;
		public bool IsEmpty
		{
			get => Range.From == Range.To;
		}
		public bool IsNotEmpty
		{
			get => Range.From < Range.To;
		}
		private EditSelection? linkedSelection = null;
		public EditSelection()
		{
		}
		public static void MakeLink(EditSelection a, EditSelection b)
		{
			a.linkedSelection = b;
			b.linkedSelection = a;
			DateTimeRange rng = a.Range | b.Range;
			a.InternalSetRange(rng);
			b.InternalSetRange(rng);
		}
		public static void BreakLink(EditSelection a, EditSelection b)
		{
			a.linkedSelection = null;
			b.linkedSelection = null;
		}
	}
}
