using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp.Views.Windows;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace SensorClockViewer.Controls
{
	public sealed partial class PlotLaneControl : UserControl
	{
		// --------------------------------------------------------------------------------
		// dependency properties
		private const bool IsOpenDefault = true;
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(PlotLaneControl), new PropertyMetadata(IsOpenDefault, (d, e) => { if(d is PlotLaneControl pl) { pl.OnIsOpenDidChange(); } }));
		public bool IsOpen
		{
			get { return (bool)GetValue(IsOpenProperty); }
			set { SetValue(IsOpenProperty, value); }
		}
		private static readonly SolidColorBrush ToneBrushDefault = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
		public static readonly DependencyProperty ToneBrushProperty = DependencyProperty.Register("ToneBrush", typeof(Brush), typeof(PlotLaneControl), new PropertyMetadata(ToneBrushDefault, (d, e) => { if(d is PlotLaneControl pl) { pl.OnToneBrushDidChange(); } }));
		public Brush ToneBrush
		{
			get { return (Brush)GetValue(ToneBrushProperty); }
			set { SetValue(ToneBrushProperty, value); }
		}
		private const bool HasTitleDefault = true;
		public static readonly DependencyProperty HasTitleProperty = DependencyProperty.Register("HasTitle", typeof(bool), typeof(PlotLaneControl), new PropertyMetadata(HasTitleDefault));
		public bool HasTitle
		{
			get { return (bool)GetValue(HasTitleProperty); }
			set { SetValue(HasTitleProperty, value); }
		}
		private const bool IsInspectorEnabledDefault = false;
		public static readonly DependencyProperty IsInspectorEnabledProperty = DependencyProperty.Register("IsInspectorEnabled", typeof(bool), typeof(PlotLaneControl), new PropertyMetadata(IsInspectorEnabledDefault));
		public bool IsInspectorEnabled
		{
			get { return (bool)GetValue(IsInspectorEnabledProperty); }
			set { SetValue(IsInspectorEnabledProperty, value); }
		}
		// --------------------------------------------------------------------------------
		// simple properties
		public const int OpenHeightDefault = 80;
		public int OpenHeight
		{
			get;
			set { field = value; OnIsOpenDidChange(); }
		} = OpenHeightDefault;
		public string Title
		{
			get { return TitleText.Text; }
			set { TitleText.Text = value; }
		}
		public string Description
		{
			get { return DescriptionText.Text; }
			set { DescriptionText.Text = value; }
		}
		public string Format
		{
			get; set;
		} = "g";
		public string Unit
		{
			get; set;
		} = string.Empty;
		public ScottPlot.WinUI.WinUIPlot PlotControl
		{
			get { return PlotCtrl; }
		}
		public ScottPlot.Plottables.Crosshair InspectorCrosshair
		{
			get;
		}
		public ScottPlot.Plottables.Annotation InspectorAnnotation
		{
			get;
		}
		private EditSelection? selection = null;
		public EditSelection? Selection
		{
			get => selection;
			set => ReplaceSelectionObject(value);
		}
		// --------------------------------------------------------------------------------
		// internals
		private Pointer? draggingPointer = null;
		private DateTime dragAnchorDateTime;
		private ScottPlot.Plottables.HorizontalSpan? selectionSpan;
		// --------------------------------------------------------------------------------
		public PlotLaneControl()
		{
			InitializeComponent();
			InspectorCrosshair = PlotCtrl.Plot.Add.Crosshair(0, 0);
			InspectorCrosshair.MarkerShape = ScottPlot.MarkerShape.OpenCircle;
			InspectorCrosshair.MarkerSize = 13;
			InspectorCrosshair.IsVisible = false;
			InspectorAnnotation = PlotCtrl.Plot.Add.Annotation("", ScottPlot.Alignment.MiddleCenter);
			InspectorAnnotation.IsVisible = false;
			OnIsOpenDidChange();
			Loaded += (s, e) => ApplyThemeColors();
			ActualThemeChanged += (s, e) => ApplyThemeColors();
			PlotCtrl.PointerPressed		+= PlotCtrl_PointerPressed;
			PlotCtrl.PointerReleased	+= PlotCtrl_PointerReleased;
			PlotCtrl.PointerMoved		+= PlotCtrl_PointerMoved;
			PlotCtrl.PointerExited		+= PlotCtrl_PointerExited;
			PlotCtrl.PointerCaptureLost	+= PlotCtrl_PointerCaptureLost;
		}
		// --------------------------------------------------------------------------------
		// dependency property change handlers
		private void OnIsOpenDidChange()
		{
			ContentPanel.Height = IsOpen ? OpenHeight : OpenStateCheck.Height;
			DescriptionText.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
			PlotCtrl.Plot.Axes.Left.TickLabelStyle.IsVisible = IsOpen;
			ApplyThemeColors();
		}
		private void OnToneBrushDidChange()
		{
			ApplyThemeColors();
		}
		// --------------------------------------------------------------------------------
		// theme related
		private void ApplyThemeColors()
		{
			ElementTheme theme = ActualTheme;
			ResourceDictionary? themedic = Resources[theme.ToString()] as ResourceDictionary;
			if(themedic == null) { Debug.Print("PlotLaneControl: theme dictionary no found"); return; }
			ScottPlot.Color figurebackgroundcolor = ConvertColorFromBrush(Background);
			ScottPlot.Color tonecolor = ConvertColorFromBrush(ToneBrush);
			ScottPlot.Color databackgroundcolor = ConvertColorFromDic(themedic["DataBackgroundColor"]).MixedWith(tonecolor, (double)tonecolor.A / 255.0);
			ScottPlot.Color databackgrounddimcolor = ConvertColorFromDic(themedic["DataBackgroundDimColor"]);
			ScottPlot.Color axescolor = ConvertColorFromDic(themedic["AxesColor"]);
			ScottPlot.Color majorlinecolor = ConvertColorFromDic(themedic["MajorLineColor"]);
			ScottPlot.Color crosshairmarkercolor = ConvertColorFromDic(themedic["CrosshairMarkerColor"]);
			PlotCtrl.Plot.FigureBackground.Color = figurebackgroundcolor;
			PlotCtrl.Plot.Grid.MajorLineColor = majorlinecolor;
			PlotCtrl.Plot.DataBackground.Color = IsOpen ? databackgroundcolor : databackgrounddimcolor;
			PlotCtrl.Plot.Axes.Color(IsOpen ? axescolor : databackgrounddimcolor);
			InspectorCrosshair.MarkerColor = crosshairmarkercolor;
			PlotCtrl.Refresh();
		}
		private ScottPlot.Color ConvertColorFromBrush(Brush? br)
		{
			if(br is SolidColorBrush sbr) { return ScottPlot.Color.FromSKColor(sbr.Color.ToSKColor()); }
			return ScottPlot.Colors.Transparent;
		}
		private ScottPlot.Color ConvertColorFromDic(object? obj)
		{
			if(obj is Windows.UI.Color clr) { return ScottPlot.Color.FromSKColor(clr.ToSKColor()); }
			return ScottPlot.Colors.Transparent;
		}
		// ----------------------------------------
		// selection related
		private void ReplaceSelectionObject(EditSelection? newsel)
		{
			if(selection != null) { selection.PropertyChanged -= OnSelectionPropertyChanged; }
			selection = newsel;
			if(selection != null) { selection.PropertyChanged += OnSelectionPropertyChanged; }
			ReflectSelectedRangeToSpan();
		}
		private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if((e.PropertyName == null) || (e.PropertyName == nameof(EditSelection.Range)))
			{
				ReflectSelectedRangeToSpan();
			}
		}
		private void ReflectSelectedRangeToSpan()
		{
			if((selection != null) && !selection.IsEmpty && IsOpen)
			{
				if(selectionSpan == null)
				{
					ScottPlot.IPalette palette = new ScottPlot.Palettes.Category10();
					selectionSpan = PlotCtrl.Plot.Add.HorizontalSpan(selection.Range.From.ToOADate(), selection.Range.To.ToOADate());
					selectionSpan.FillColor = palette.GetColor(7).WithAlpha(0.2);
					selectionSpan.IsDraggable = false;
					selectionSpan.IsResizable = false;
					selectionSpan.IsVisible = true;
				}
				selectionSpan.X1 = selection.Range.From.ToOADate();
				selectionSpan.X2 = selection.Range.To.ToOADate();
				PlotCtrl.Refresh();
			}
			else if(selectionSpan != null)
			{
				PlotCtrl.Plot.Remove(selectionSpan);
				selectionSpan = null;
				PlotCtrl.Refresh();
			}
		}
		// --------------------------------------------------------------------------------
		// mouse event handlers
		private ScottPlot.DataPoint? FindNearestFromMouse(ScottPlot.Coordinates mousecoords)
		{
			var plottable = PlotCtrl.Plot.PlottableList.Find((ScottPlot.IPlottable pl) => { return pl is ScottPlot.Plottables.SignalXY; });
			if(plottable is ScottPlot.Plottables.SignalXY sigxy)
			{
				ScottPlot.DataPoint nearest = sigxy.Data.GetNearestX(mousecoords, PlotCtrl.Plot.LastRender);
				if(nearest.IsReal) { return nearest; }
			}
			return null;
		}
		private void PlotCtrl_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if(e.GetCurrentPoint(this).Properties.IsRightButtonPressed) { return; }
			if((selection != null) && (draggingPointer == null) && IsOpen)
			{
				// begin selection drag
				var mousepos = e.GetCurrentPoint(PlotCtrl).Position;
				ScottPlot.Coordinates mousecoords = PlotCtrl.Plot.GetCoordinates(new ScottPlot.Pixel(mousepos.X, mousepos.Y));
				CapturePointer(e.Pointer);
				PlotCtrl.UserInputProcessor.Disable();
				draggingPointer = e.Pointer;
				ScottPlot.DataPoint? nearest = FindNearestFromMouse(mousecoords);
				dragAnchorDateTime = DateTime.FromOADate((nearest != null) ? nearest.Value.X : mousecoords.X);
				selection?.Range = new(dragAnchorDateTime, dragAnchorDateTime);
				ReflectSelectedRangeToSpan();
			}
		}
		private void PlotCtrl_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			if(draggingPointer != null)
			{
				ReleasePointerCapture(e.Pointer);
			}
		}
		private void PlotCtrl_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
		{
			var mousepos = e.GetCurrentPoint(PlotCtrl).Position;
			if(draggingPointer != null)
			{
				// end selection drag
				PlotCtrl.UserInputProcessor.Enable();
				draggingPointer = null;
			}
		}
		private void PlotCtrl_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			var mousepos = e.GetCurrentPoint(PlotCtrl).Position;
			ScottPlot.Coordinates mousecoords = PlotCtrl.Plot.GetCoordinates(new ScottPlot.Pixel(mousepos.X, mousepos.Y));
			ScottPlot.DataPoint? nearest = FindNearestFromMouse(mousecoords);
			DateTime dtmouse = DateTime.FromOADate((nearest != null) ? nearest.Value.X : mousecoords.X);
			if(draggingPointer != null)
			{
				// move selection drag
				selection?.Range = new((dtmouse <= dragAnchorDateTime) ? dtmouse : dragAnchorDateTime, (dragAnchorDateTime <= dtmouse) ? dtmouse : dragAnchorDateTime);
				ReflectSelectedRangeToSpan();
			}
			if(IsInspectorEnabled && IsOpen)
			{
				// move inspector
				if(nearest != null)
				{
					InspectorCrosshair.Position = nearest.Value.Coordinates;
					InspectorCrosshair.IsVisible = true;
					InspectorAnnotation.Text = string.Format("{0}\n{1} {2}", dtmouse.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), nearest.Value.Y.ToString(Format), Unit);
					InspectorAnnotation.IsVisible = true;
					PlotCtrl.Refresh();
				}
				else if(InspectorCrosshair.IsVisible)
				{
					InspectorCrosshair.IsVisible = false;
					InspectorAnnotation.IsVisible = false;
					InspectorAnnotation.Text = "";
					PlotCtrl.Refresh();
				}
			}
		}
		private void PlotCtrl_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			if(InspectorCrosshair.IsVisible == true)
			{
				InspectorCrosshair.IsVisible = false;
				InspectorAnnotation.IsVisible = false;
				InspectorAnnotation.Text = "";
				PlotCtrl.Refresh();
			}
		}
	}
}
