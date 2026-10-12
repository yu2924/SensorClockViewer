using Microsoft.UI.Xaml.Controls;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;

namespace SensorClockViewer.Controls
{
	public sealed partial class PlotArrayControl : UserControl
	{
		// --------------------------------------------------------------------------------
		// public properties
		public EditSelection SensorSelection { get; } = new EditSelection();
		public EditSelection WeatherSelection { get; } = new EditSelection();
		public struct PlotRange
		{
			public DateTime from = DateTime.MinValue;
			public DateTime to = DateTime.MinValue;
			public PlotRange() { }
			public PlotRange(DateTime f, DateTime t) { from = f; to = t; }
			public bool IsNull { get => (from == DateTime.MinValue) && (to == DateTime.MinValue); }
			public bool IsEmpty { get => from == to; }
			public static PlotRange Union(PlotRange a, PlotRange b)
			{
				if(a.IsNull) { return b; }
				if(b.IsNull) { return a; }
				return new PlotRange((a.from <= b.from) ? a.from : b.from, (a.to <= b.to) ? b.to : a.to);
			}
			public static PlotRange operator |(PlotRange a, PlotRange b) { return Union(a, b); }
		}
		public PlotRange SensorPlotRange { get; private set; } = new PlotRange();
		public PlotRange WeatherPlotRange { get; private set; } = new PlotRange();
		public PlotRange OverallPlotRange { get; private set; } = new PlotRange();
		// --------------------------------------------------------------------------------
		// internals
		// the entire set of wind vectors and icons are not plotted directly, but are drawn along the ruler ticks
		private struct WeWindRecord
		{
			public double position;
			public float speed;
			public float deg;
			public WeWindRecord(double p, float s, float d) { position = p; speed = s; deg = d; }
		}
		private WeWindRecord[]? weWindRecords = null;
		private struct WeIconRecord
		{
			public double position;
			public string icon;
			public WeIconRecord(double p, string i) { position = p; icon = i; }
		}
		private WeIconRecord[]? weIconRecords = null;
		// --------------------------------------------------------------------------------
		public PlotArrayControl()
		{
			InitializeComponent();
			// ----------------------------------------
			// plot controls setup
			const int LeftMargin = 64;
			const int RightMargin = 24;
			PlotLaneControl[] selanes = [SeTempLane, SeHumLane, SePresLane, SeCo2Lane, SeIaqLane];
			PlotLaneControl[] welanes = [WeTempLane, WeHumLane, WePresLane, WeCloudsLane, WeWindsLane, WeIconsLane];
			ScottPlot.Plot[] seplots = selanes.Select(ctrl => ctrl.PlotControl.Plot).ToArray();
			ScottPlot.Plot[] weplots = welanes.Select(ctrl => ctrl.PlotControl.Plot).ToArray();
			// ruler lane
			var rulerplot = RulerLane.PlotControl.Plot;
			rulerplot.Axes.Left.LockSize(LeftMargin);
			rulerplot.Axes.Left.IsVisible = false;
			rulerplot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.EmptyTickGenerator();
			rulerplot.Axes.Right.LockSize(RightMargin);
			rulerplot.Axes.Right.IsVisible = false;
			rulerplot.Axes.Top.MaximumSize = 1;
			rulerplot.Axes.Top.IsVisible = false;
			rulerplot.Axes.DateTimeTicksBottom();
			rulerplot.Axes.Bottom.LockSize(RulerLane.OpenHeight - 4);
			RulerLane.PlotControl.Menu?.Clear();
			RulerLane.PlotControl.Menu?.Add("Autoscale", (plot) => { plot.Axes.AutoScale(); RulerLane.PlotControl.Refresh(); });
			// sensor lanes
			foreach(var plot in seplots)
			{
				plot.Grid.XAxis = rulerplot.Axes.Bottom;
				plot.Axes.Left.LockSize(LeftMargin);
				plot.Axes.Right.LockSize(RightMargin);
				plot.Axes.Top.MaximumSize = 1;
				plot.Axes.Bottom.MaximumSize = 1; // collapse but bottom edge
				plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.EmptyTickGenerator();
			}
			foreach(var lane in selanes)
			{
				lane.PlotControl.Menu?.Clear();
				lane.PlotControl.Menu?.Add("Autoscale", (plot) => { plot.Axes.AutoScale(); lane.PlotControl.Refresh(); });
				lane.PlotControl.Menu?.Add("Reset", (plot) => { DefaultScaleAxes(); });
			}
			// weather lanes
			foreach(var plot in weplots)
			{
				plot.Grid.XAxis = rulerplot.Axes.Bottom;
				plot.Axes.Left.LockSize(LeftMargin);
				plot.Axes.Right.LockSize(RightMargin);
				plot.Axes.Top.MaximumSize = 1;
				plot.Axes.Bottom.MaximumSize = 1; // collapse but bottom edge
				plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.EmptyTickGenerator();
			}
			foreach(var lane in welanes)
			{
				if((lane == WeWindsLane) || (lane == WeIconsLane)) continue;
				lane.PlotControl.Menu?.Clear();
				lane.PlotControl.Menu?.Add("Autoscale", (plot) => { plot.Axes.AutoScale(); lane.PlotControl.Refresh(); });
				lane.PlotControl.Menu?.Add("Reset", (plot) => { DefaultScaleAxes(); });
			}
			WeWindsLane.PlotControl.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.EmptyTickGenerator();
			WeIconsLane.PlotControl.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.EmptyTickGenerator();
			WeWindsLane.PlotControl.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.LockedVertical(WeWindsLane.PlotControl.Plot.Axes.Left, -1, 1));
			WeIconsLane.PlotControl.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.LockedVertical(WeIconsLane.PlotControl.Plot.Axes.Left, -1, 1));
			// link x-axes
			List<ScottPlot.Plot> lnkplots = new(seplots.Length + weplots.Length + 1);
			lnkplots.Add(rulerplot);
			lnkplots.AddRange(seplots);
			lnkplots.AddRange(weplots);
			foreach(var plota in lnkplots)
			{
				foreach(var plotb in lnkplots)
				{
					if(plota != plotb) { plota.Axes.Link(plota.Axes.Bottom, plotb.Axes.Bottom, plotb); }
				}
			}
			// inspector font
			StringBuilder units = new();
			foreach(var lane in selanes) { units.Append(lane.Unit); }
			foreach(var lane in welanes) { units.Append(lane.Unit); }
			string fontname = Fonts.Detect(units.ToString());
			foreach(var lane in selanes) { lane.InspectorAnnotation.LabelFontName = fontname; }
			foreach(var lane in welanes) { lane.InspectorAnnotation.LabelFontName = fontname; }
			// ----------------------------------------
			// edit selections
			foreach(var lane in selanes) { lane.Selection = SensorSelection; }
			foreach(var lane in welanes) { lane.Selection = WeatherSelection; }
			// ----------------------------------------
			// pick wind vectors and weather icons along the ticks
			RulerLane.PlotControl.Plot.RenderManager.RenderStarting += (s, e) =>
			{
				// custom datetime tick format
				var ticks = RulerLane.PlotControl.Plot.Axes.Bottom.TickGenerator.Ticks;
				for(int c = ticks.Length, i = 0; i < c; ++i)
				{
					bool longform = ticks[i].Label.Contains("\n");
					DateTime dt = DateTime.FromOADate(ticks[i].Position);
					ticks[i] = new ScottPlot.Tick(ticks[i].Position, dt.ToLocalTime().ToString(longform ? "yyyy-MM-dd\nHH:mm:ss" : "yyyy-MM-dd"));
				}
				// tick positions with interpolation and extrapolation
				List<double> tickpositions = new(ticks.Length * 2 + 2);
				if(0 < ticks.Length)
				{
					double xcurr = ticks.First().Position;
					tickpositions.Add(xcurr);
					for(int c = ticks.Length, i = 1; i < c; ++i)
					{
						double xnext = ticks[i].Position;
						tickpositions.Add((xcurr + xnext) * 0.5);
						tickpositions.Add(xnext);
						xcurr = xnext;
					}
					if(2 <= tickpositions.Count)
					{
						var xvisible = RulerLane.PlotControl.Plot.Axes.Bottom.GetRange();
						var dx = tickpositions[1] - tickpositions[0];
						var xl = tickpositions.First() - dx;
						var xr = tickpositions.Last() + dx;
						if(xvisible.Min <= xl) { tickpositions.Insert(0, xl); }
						if(xr <= xvisible.Max) { tickpositions.Add(xr); }
					}
				}
				// wind vectors
				WeWindsLane.PlotControl.Plot.Clear<ScottPlot.Plottables.VectorField>();
				if(weWindRecords != null)
				{
					// note about angles:
					//   VectorField: increase clockwise, 0°=>right, 90°=>bottom
					//   meteorological: increase clockwise, 0°=>blowing from north, 90°=>blowing from east cf. https://en.wikipedia.org/wiki/Wind_direction
					List<ScottPlot.RootedCoordinateVector> vectors = new(tickpositions.Count);
					int inext = 0;
					foreach(var xtick in tickpositions)
					{
						if(xtick < weWindRecords.First().position) { continue; }
						if(weWindRecords.Length <= inext) { break; }
						int i = Array.FindIndex(weWindRecords, inext, (rec) => { return xtick < rec.position; });
						if(i < 0) { continue; }
						i = Math.Max(i - 1, 0);
						inext = i;
						var rec = weWindRecords[i];
						var pt = new ScottPlot.Coordinates(xtick, 0);
						float spd = rec.speed;
						float arg = rec.deg * float.Pi / 180.0f - float.Pi * 0.5f;
						var v2 = new System.Numerics.Vector2(float.Cos(arg), float.Sin(arg)) * -spd;
						vectors.Add(new(pt, v2));
					}
					ScottPlot.IPalette palette = new ScottPlot.Palettes.Category10();
					WeWindsLane.PlotControl.Plot.Add.VectorField(vectors, palette.GetColor(4));
				}
				WeWindsLane.PlotControl.Refresh();
				// weather icons
				WeIconsLane.PlotControl.Plot.Clear<ScottPlot.Plottables.ImageMarker>();
				if(weIconRecords != null)
				{
					int inext = 0;
					foreach(var xtick in tickpositions)
					{
						if(xtick < weIconRecords.First().position) { continue; }
						if(weIconRecords.Length <= inext) { break; }
						int i = Array.FindIndex(weIconRecords, inext, (rec) => { return xtick < rec.position; });
						if(i < 0) { continue; }
						i = Math.Max(i - 1, 0);
						inext = i;
						var rec = weIconRecords[i];
						byte[]? bin = Properties.Resources.ResourceManager.GetObject(rec.icon ?? string.Empty) as byte[];
						if(bin == null) { continue; }
						ScottPlot.Coordinates pt = new(xtick, 0);
						ScottPlot.Image img = new(bin);
						if(img.Width <= 0) { continue; }
						float scale = 20.0f / (float)img.Width;
						WeIconsLane.PlotControl.Plot.Add.ImageMarker(pt, img, scale);
					}
				}
				WeIconsLane.PlotControl.Refresh();
			};
			// ----------------------------------------
			// adjust content height
			ScrollContentPanel.LayoutUpdated += (s, e) =>
			{
				double desiredheight = ScrollContentPanel.DesiredSize.Height + RulerLane.DesiredSize.Height;
				if(PlotContent.MaxHeight != desiredheight) { PlotContent.MaxHeight = desiredheight; }
			};
		}
		public void SetSensorData(IEnumerable<Models.SensorRecord>? data)
		{
			SeTempLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			SeHumLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			SePresLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			SeCo2Lane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			SeIaqLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			SensorPlotRange = new PlotRange();
			EditSelection.DateTimeRange selrange = new();
			if(data != null)
			{
				var records = data.ToList();
				if(0 < records.Count)
				{
					DateTime[] xs = records.Select(r => DateTimeOffset.FromUnixTimeSeconds(r.Dt).DateTime).ToArray();
					SensorPlotRange = new(xs.First(), xs.Last());
					selrange = new(xs.First(), xs.First());
					ScottPlot.IPalette palette = new ScottPlot.Palettes.Category10();
					SeTempLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Temp).ToArray(), palette.GetColor(0));
					SeHumLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Humidity).ToArray(), palette.GetColor(1));
					SePresLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Pressure).ToArray(), palette.GetColor(2));
					SeCo2Lane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Co2).ToArray(), palette.GetColor(3));
					SeIaqLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Iaq).ToArray(), palette.GetColor(4));
				}
			}
			SensorSelection.Range = selrange;
			UpdateRulerPlot();
			SeTempLane.PlotControl.Refresh();
			SeHumLane.PlotControl.Refresh();
			SePresLane.PlotControl.Refresh();
			SeCo2Lane.PlotControl.Refresh();
			SeIaqLane.PlotControl.Refresh();
		}
		public void SetWeatherData(IEnumerable<Models.WeatherRecord>? data)
		{
			WeTempLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			WeHumLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			WePresLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			WeCloudsLane.PlotControl.Plot.Clear<ScottPlot.Plottables.SignalXY>();
			WeatherPlotRange = new PlotRange();
			EditSelection.DateTimeRange selrange = new();
			if(data != null)
			{
				var records = data.ToList();
				if(0 < records.Count)
				{
					DateTime[] xs = records.Select(r => DateTimeOffset.FromUnixTimeSeconds(r.Dt).DateTime).ToArray();
					WeatherPlotRange = new(xs.First(), xs.Last());
					selrange = new(xs.First(), xs.First());
					ScottPlot.IPalette palette = new ScottPlot.Palettes.Category10();
					WeTempLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Temp).ToArray(), palette.GetColor(0));
					WeHumLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Humidity).ToArray(), palette.GetColor(1));
					WePresLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Pressure).ToArray(), palette.GetColor(2));
					WeCloudsLane.PlotControl.Plot.Add.SignalXY(xs, records.Select(r => r.Cloud).ToArray(), palette.GetColor(3));
					// wind vectors
					WeWindRecord[] windrecords = records.Select(r => new WeWindRecord(0, r.WindSpeed, (float)r.WindDeg)).ToArray();
					for(int c = xs.Length, i = 0; i < c; ++i) { windrecords[i].position = xs[i].ToOADate(); }
					weWindRecords = windrecords;
					// icons
					WeIconRecord[] iconrecords = records.Select(r => new WeIconRecord(0, r.WeatherIcon)).ToArray();
					for(int c = xs.Length, i = 0; i < c; ++i) { iconrecords[i].position = xs[i].ToOADate(); }
					weIconRecords = iconrecords;
				}
			}
			WeatherSelection.Range = selrange;
			UpdateRulerPlot();
			WeTempLane.PlotControl.Refresh();
			WeHumLane.PlotControl.Refresh();
			WePresLane.PlotControl.Refresh();
			WeCloudsLane.PlotControl.Refresh();
		}
		private void UpdateRulerPlot()
		{
			RulerLane.PlotControl.Plot.Clear<ScottPlot.Plottables.Marker>();
			OverallPlotRange = SensorPlotRange | WeatherPlotRange;
			PlotRange rngv = OverallPlotRange;
			if(rngv.IsNull) { var t = DateTime.UtcNow; rngv = new PlotRange(t, t); }
			var fig = RulerLane.PlotControl.Plot.Add.Markers([rngv.from, rngv.to], ScottPlot.Generate.Zeros(2));
			fig.Color = ScottPlot.Colors.Transparent;
			RulerLane.PlotControl.Refresh();
		}
		public void DefaultScaleAxes()
		{
			// note:
			// atmospheric pressure range on record: 870 - 1084.8 hPa
			PlotLaneControl[] lanes = { SeTempLane, SeHumLane, SePresLane, SeCo2Lane, SeIaqLane, WeTempLane, WeHumLane, WePresLane, WeCloudsLane, };
			Tuple<int, int>[] Limits = [new(-10, 50), new(-10, 110), new(850, 1090), new(400, 3200), new(-10, 110), new(-10, 50), new(-10, 110), new(850, 1090), new(-10, 110)];
			for(int i = 0; i < Limits.Length; ++i) { lanes[i].PlotControl.Plot.Axes.SetLimitsY(Limits[i].Item1, Limits[i].Item2); }
			RulerLane.PlotControl.Plot.Axes.AutoScale();
			RulerLane.PlotControl.Refresh();
			foreach(var lane in lanes) { lane.PlotControl.Refresh(); }
		}
	}
}
