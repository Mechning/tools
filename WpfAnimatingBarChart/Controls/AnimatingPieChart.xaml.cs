﻿using LovettSoftware.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LovettSoftware.Charts
{
    /// <summary>
    /// Interaction logic for AnimatingPieChart.xaml
    /// </summary>
    public partial class AnimatingPieChart : UserControl
    {
        DelayedActions actions = new DelayedActions();
        List<PieSlice> slices = new List<PieSlice>();
        int tipSlice = -1;
        Point movePos;
        PieSlice inside;
        bool mouseOverAnimationCompleted = false;

        public AnimatingPieChart()
        {
            InitializeComponent();

            this.HoverDelayMilliseconds = 250;
            this.AnimationGrowthMilliseconds = 250;
            this.AnimationColorMilliseconds = 250;
            this.IsVisibleChanged += OnVisibleChanged;
        }

        public int HoverDelayMilliseconds { get; set; }

        /// <summary>
        /// Time to animate growth of the columns.
        /// </summary>
        public int AnimationGrowthMilliseconds { get; set; }

        /// <summary>
        /// Time to animate the slice color.
        /// </summary>
        public int AnimationColorMilliseconds { get; set; }


        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            OnDelayedUpdate();
            return base.ArrangeOverride(arrangeBounds);
        }


        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            OnDelayedUpdate();
        }

        public List<ChartDataValue> Series
        {
            get { return (List<ChartDataValue>)GetValue(PieSeriesProperty); }
            set { SetValue(PieSeriesProperty, value); }
        }

        public static readonly DependencyProperty PieSeriesProperty =
            DependencyProperty.Register("PieSeries", typeof(List<ChartDataValue>), typeof(AnimatingPieChart), new PropertyMetadata(null, OnSeriesChanged));

        private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatingPieChart)d).OnSeriesChanged(e.NewValue);
        }

        private void OnSeriesChanged(object newValue)
        {
            if (newValue == null || this.Series.Count == 0)
            {
                ResetVisuals();
            }
            else
            {
                OnDelayedUpdate();
            }
        }

        private void ResetVisuals()
        {
            ChartCanvas.Children.Clear();
            slices.Clear();
            tipSlice = -1;
            inside = null;
            mouseOverAnimationCompleted = false;
        }

        private void OnDelayedUpdate()
        {
            actions.StartDelayedAction("update", UpdateChart, TimeSpan.FromMilliseconds(10));
        }

        public event EventHandler<ChartDataValue> PieSliceHover;
        public event EventHandler<ChartDataValue> PieSliceClicked;

        private void UpdateChart()
        {
            if (double.IsNaN(this.ActualWidth))
            {
                return;
            }
            double w = this.ActualWidth - (this.Padding.Left + this.Padding.Right);
            double h = this.ActualHeight - (this.Padding.Top + this.Padding.Bottom);
            if (w < 0 || h < 0 || this.Visibility != Visibility.Visible)
            {
                return;
            }

            double m = Math.Min(w, h);
            double c = Math.Floor(m / 2);

            double total = (from d in this.Series select d.Value).Sum();

            Point center = new Point(c + (w - m) / 2, c + (h - m) / 2);
            Size size = new Size(c, c);
            Storyboard sb = new Storyboard();

            double sum = 0;
            int i = 0;
            double oldStart = 0;
            double oldEnd = 0;
            foreach (var item in this.Series)
            {
                PieSlice slice = null;
                if (i < slices.Count)
                {
                    slice = slices[i];
                    oldStart = slice.StartAngle;
                    oldEnd = slice.EndAngle;
                    slice.Center = center;
                    slice.Size = size;
                }
                else
                {
                    slice = new PieSlice(ChartCanvas, Colors.Transparent, center, size);
                    slices.Add(slice);
                }

                slice.Color = item.Color;
                double start = (sum * 360) / total;
                double end = ((sum + item.Value) * 360) / total;
                AnimateSlice(sb, slice, oldStart, start, oldEnd, end, item.Color);
                sum += item.Value;
                i++;
            }

            while (slices.Count > i)
            {
                ChartCanvas.Children.Remove(slices[i].Path);
                slices.RemoveAt(i);
            }

            this.BeginStoryboard(sb);
        }

        void AnimateSlice(Storyboard sb, PieSlice slice, double a1, double a2, double a3, double a4, Color c)
        {
            var duration1 = new Duration(TimeSpan.FromMilliseconds(AnimationGrowthMilliseconds));

            var animation1 = new DoubleAnimation() { 
                From = a1, 
                To = a2, 
                Duration = duration1, 
                EasingFunction = new ExponentialEase() { Exponent = 0.6, EasingMode = EasingMode.EaseIn } 
            };
            sb.Children.Add(animation1);
            Storyboard.SetTarget(animation1, slice);
            Storyboard.SetTargetProperty(animation1, new PropertyPath(PieSlice.StartAngleProperty)); // start of the slice

            var animation2 = new DoubleAnimation() { 
                From = a3, 
                To = a4, 
                Duration = duration1,
                EasingFunction = new ExponentialEase() { Exponent = 0.6, EasingMode = EasingMode.EaseIn } 
            };
            sb.Children.Add(animation2);
            Storyboard.SetTarget(animation2, slice);
            Storyboard.SetTargetProperty(animation2, new PropertyPath(PieSlice.EndAngleProperty)); // end of the slice.

            var duration2 = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
            SolidColorBrush stroke = (SolidColorBrush)slice.Path.Stroke;
            SolidColorBrush fill = (SolidColorBrush)slice.Path.Fill;
            stroke.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, duration2));
            fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, duration2));
        }

        private Color GetMouseOverColor(Color c)
        {
            var hls = new HlsColor(c);
            hls.Lighten(0.25f);
            return hls.Color;
        }

        public ToolTipGenerator ToolTipGenerator { get; set; }


        protected override void OnMouseLeave(MouseEventArgs e)
        {
            actions.CancelDelayedAction("hover");
            tipSlice = -1;
            OnExitSlice();
            base.OnMouseLeave(e);
        }

        private void OnHover()
        {
            var i = this.tipSlice;
            if (i < 0 || i >= Series.Count)
            {
                return;
            }

            ChartDataValue value = Series[i];
            var tip = this.ToolTip as ToolTip;
            var content = ToolTipGenerator != null ? ToolTipGenerator(value) : new TextBlock() { Text = value.Label + "\r\n" + value.Value };
            if (tip == null)
            {
                tip = new ToolTip()
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                    Content = content,
                    IsOpen = true
                };
                this.ToolTip = tip;
            }
            else
            {
                tip.Content = content;
                tip.IsOpen = true;
            }
            tip.Measure(new Size(100, 100));
            tip.HorizontalOffset = 0;
            tip.VerticalOffset = -tip.ActualHeight;

            // notify any interested listeners
            var h = this.PieSliceHover;
            if (h != null)
            {
                h(this, value);
            }

        }

        int FindSlice(Point pos)
        {
            for (int i = 0, n = slices.Count; i < n; i++)
            {
                var p = this.slices[i].Path;
                if (p.Data.FillContains(pos))
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(ChartCanvas);
            var i = FindSlice(pos);
            if (i >= 0 && i < Series.Count)
            {
                OnEnterSlice(i);
                var tip = this.ToolTip as ToolTip;
                if (tip != null)
                {
                    tip.IsOpen = false;
                    this.ToolTip = null;
                }
                this.movePos = pos;
                this.tipSlice = i;
                actions.StartDelayedAction("hover", () =>
                {
                    OnHover();
                }, TimeSpan.FromMilliseconds(HoverDelayMilliseconds));
            }
            else
            {
                this.tipSlice = -1;
                OnExitSlice();
            }
            base.OnPreviewMouseMove(e);
        }

        private void OnEnterSlice(int i)
        {
            if (i < slices.Count)
            {
                var slice = slices[i];
                var brush = (SolidColorBrush)slice.Path.Fill;
                var color = slice.Color;
                if (inside == null || slice != inside)
                {
                    if (inside != null)
                    {
                        OnExitSlice();
                    }

                    var duration = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
                    var highlight = GetMouseOverColor(color);
                    var mouseOverAnimation = new ColorAnimation() { To = highlight, Duration = duration };
                    mouseOverAnimation.Completed += (s, e) =>
                    {
                        this.mouseOverAnimationCompleted = true;
                        if (slice != inside)
                        {
                            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = color, Duration = duration });
                        }
                    };
                    this.mouseOverAnimationCompleted = false;
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, mouseOverAnimation);
                    inside = slice;
                }
            }
        }

        void OnExitSlice()
        {
            if (inside != null && this.mouseOverAnimationCompleted)
            {
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
                var brush = (SolidColorBrush)inside.Path.Fill;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = inside.Color, Duration = duration });
            }
            inside = null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ChartCanvas);
            var i = FindSlice(pos);
            if (i >= 0 && i < Series.Count)
            {
                ChartDataValue value = Series[i];
                if (PieSliceClicked != null)
                {
                    PieSliceClicked(this, value);
                }
            }
        }


        class PieSlice : DependencyObject
        {
            Path path;
            PathFigure figure;
            LineSegment line1;
            LineSegment line2;
            ArcSegment arc;

            public PieSlice(Canvas owner, Color color, Point center, Size size)
            {
                this.Color = color;
                this.path = new Path();
                this.path.Stroke = new SolidColorBrush(color);
                this.path.Fill = new SolidColorBrush(color);
                this.path.StrokeThickness = 1;
                owner.Children.Add(this.path);
                this.Size = size;
                this.Center = center;
            }

            public Color Color { get; set; }

            public Path Path => path;

            public double StartAngle
            {
                get { return (double)GetValue(StartAngleProperty); }
                set { SetValue(StartAngleProperty, value); }
            }

            // Using a DependencyProperty as the backing store for StartAngle.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty StartAngleProperty =
                DependencyProperty.Register("StartAngle", typeof(double), typeof(PieSlice), new PropertyMetadata(0.0, new PropertyChangedCallback(OnPropertyChanged)));

            private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                ((PieSlice)d).OnPropertyChanged();
            }

            private void OnPropertyChanged()
            {
                var center = this.Center;
                var size = this.Size;
                double x1 = center.X + Math.Cos(StartAngle * Math.PI / 180) * size.Width;
                double y1 = center.Y + Math.Sin(StartAngle * Math.PI / 180) * size.Height;
                double x2 = center.X + Math.Cos(EndAngle * Math.PI / 180) * size.Width;
                double y2 = center.Y + Math.Sin(EndAngle * Math.PI / 180) * size.Height;
                var p1 = new Point(x1, y1);
                var p2 = new Point(x2, y2);

                if (line1 == null)
                {
                    PathGeometry g = new PathGeometry();
                    this.line1 = new LineSegment(p1, true);
                    this.arc = new ArcSegment(p2, size, 0, false, SweepDirection.Clockwise, true);
                    this.line2 = new LineSegment(center, true);
                    this.figure = new PathFigure(center, new PathSegment[] {
                        line1,
                        arc,
                        line2
                    }, true);
                    g.Figures.Add(figure);
                    path.Data = g;
                }
                else
                {
                    figure.StartPoint = center;
                    line1.Point = p1;
                    line2.Point = center;
                    arc.Point = p2;
                }

                arc.Size = size;
                arc.IsLargeArc = (EndAngle - StartAngle) > 180;
            }

            public double EndAngle
            {
                get { return (double)GetValue(EndAngleProperty); }
                set { SetValue(EndAngleProperty, value); }
            }

            public static readonly DependencyProperty EndAngleProperty =
                DependencyProperty.Register("EndAngle", typeof(double), typeof(PieSlice), new PropertyMetadata(0.0, new PropertyChangedCallback(OnPropertyChanged)));

            public Point Center
            {
                get { return (Point)GetValue(CenterProperty); }
                set { SetValue(CenterProperty, value); }
            }

            public static readonly DependencyProperty CenterProperty =
                DependencyProperty.Register("Center", typeof(Point), typeof(PieSlice), new PropertyMetadata(new Point(0,0), new PropertyChangedCallback(OnPropertyChanged)));

            public Size Size
            {
                get { return (Size)GetValue(SizeProperty); }
                set { SetValue(SizeProperty, value); }
            }

            public static readonly DependencyProperty SizeProperty =
                DependencyProperty.Register("Size", typeof(Size), typeof(PieSlice), new PropertyMetadata(new Size(0,0), new PropertyChangedCallback(OnPropertyChanged)));


        }
    }
}