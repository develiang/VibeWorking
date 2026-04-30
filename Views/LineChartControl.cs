using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace InputStats;

public class LineChartControl : Canvas
{
    private Canvas? _seriesLayer;
    private double _baselineY;

    public static readonly DependencyProperty ClickDataProperty =
        DependencyProperty.Register(nameof(ClickData), typeof(IEnumerable<TimeBucket>), typeof(LineChartControl),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty KeyDataProperty =
        DependencyProperty.Register(nameof(KeyData), typeof(IEnumerable<TimeBucket>), typeof(LineChartControl),
            new PropertyMetadata(null, OnDataChanged));

    public IEnumerable<TimeBucket>? ClickData
    {
        get => (IEnumerable<TimeBucket>?)GetValue(ClickDataProperty);
        set => SetValue(ClickDataProperty, value);
    }

    public IEnumerable<TimeBucket>? KeyData
    {
        get => (IEnumerable<TimeBucket>?)GetValue(KeyDataProperty);
        set => SetValue(KeyDataProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LineChartControl control)
        {
            control.DrawChart();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        DrawChart();
    }

    public void DrawChart()
    {
        Children.Clear();
        Clip = null;
        _seriesLayer = null;

        var clickData = ClickData?.ToList() ?? new List<TimeBucket>();
        var keyData = KeyData?.ToList() ?? new List<TimeBucket>();

        if (clickData.Count == 0 && keyData.Count == 0)
            return;

        double paddingLeft = 48;
        double paddingRight = 16;
        double paddingTop = 20;
        double paddingBottom = 28;

        double chartWidth = Math.Max(1, ActualWidth - paddingLeft - paddingRight);
        double chartHeight = Math.Max(1, ActualHeight - paddingTop - paddingBottom);
        _baselineY = paddingTop + chartHeight;

        var allClicks = clickData.Select(b => (double)b.Clicks).ToList();
        var allKeys = keyData.Select(b => (double)b.Keys).ToList();
        double maxValue = Math.Max(allClicks.DefaultIfEmpty(0).Max(), allKeys.DefaultIfEmpty(0).Max());
        if (maxValue < 1) maxValue = 1;

        var gridBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("BorderBrush") ?? new SolidColorBrush(Colors.Gray));
        var textBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("TextMutedBrush") ?? new SolidColorBrush(Colors.Gray));
        var clickBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("StatClickBrush") ?? new SolidColorBrush(Colors.Green));
        var keyBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("StatKeyBrush") ?? new SolidColorBrush(Colors.Orange));

        // 网格线和 Y 轴标签（不参与动画）
        for (int i = 0; i <= 4; i++)
        {
            double t = i / 4.0;
            double y = paddingTop + chartHeight * (1 - t);
            double value = maxValue * t;

            var line = new Line
            {
                X1 = paddingLeft,
                Y1 = y,
                X2 = paddingLeft + chartWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            Children.Add(line);

            var label = new TextBlock
            {
                Text = ((int)value).ToString(),
                FontSize = 10,
                Foreground = textBrush
            };
            Canvas.SetRight(label, ActualWidth - paddingLeft + 4);
            Canvas.SetTop(label, y - 6);
            Children.Add(label);
        }

        // X 轴时间标签（首、中、尾）——不参与动画
        var data = clickData.Count > 0 ? clickData : keyData;
        if (data.Count > 0)
        {
            int[] indices = data.Count == 1
                ? new[] { 0 }
                : data.Count == 2
                    ? new[] { 0, 1 }
                    : new[] { 0, data.Count / 2, data.Count - 1 };

            bool spansMultipleDays = data.Count > 1 && (data[^1].StartTime.Date != data[0].StartTime.Date);
            string timeFormat = spansMultipleDays ? "MM-dd" : "HH:mm";

            foreach (int idx in indices)
            {
                double x = paddingLeft + (data.Count <= 1 ? chartWidth / 2 : (idx / (double)(data.Count - 1)) * chartWidth);
                var timeLabel = new TextBlock
                {
                    Text = data[idx].StartTime.ToString(timeFormat),
                    FontSize = 10,
                    Foreground = textBrush
                };
                Canvas.SetLeft(timeLabel, x - 16);
                Canvas.SetTop(timeLabel, paddingTop + chartHeight + 4);
                Children.Add(timeLabel);
            }
        }

        // 折线/圆点单独一层，供动画使用
        _seriesLayer = new Canvas { IsHitTestVisible = true };
        Children.Add(_seriesLayer);

        if (clickData.Count > 0)
            DrawSeries(clickData, maxValue, clickBrush, paddingLeft, paddingTop, chartWidth, chartHeight, true);
        if (keyData.Count > 0)
            DrawSeries(keyData, maxValue, keyBrush, paddingLeft, paddingTop, chartWidth, chartHeight, false);
    }

    public void AnimateChart()
    {
        if (_seriesLayer == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        // ScaleY 0→1，缩放原点放在基线（图表底部），视觉等价于“从 0 向上长出”
        var scale = new ScaleTransform(1, 0, 0, _baselineY);
        _seriesLayer.RenderTransform = scale;

        var scaleAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        // 圆点 Opacity 同步淮入，避免初期被压扁在基线
        var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        foreach (UIElement child in _seriesLayer.Children)
        {
            if (child is Ellipse e)
                e.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }
    }

    private void DrawSeries(List<TimeBucket> data, double maxValue, System.Windows.Media.Brush brush,
        double paddingLeft, double paddingTop, double chartWidth, double chartHeight, bool isClick)
    {
        if (data.Count == 0 || _seriesLayer == null) return;

        var pts = new List<System.Windows.Point>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            double x = paddingLeft + (data.Count <= 1 ? chartWidth / 2 : (i / (double)(data.Count - 1)) * chartWidth);
            double value = isClick ? data[i].Clicks : data[i].Keys;
            double y = paddingTop + chartHeight - (value / maxValue) * chartHeight;
            pts.Add(new System.Windows.Point(x, y));
        }

        if (pts.Count >= 2)
        {
            Geometry geometry;
            if (pts.Count == 2)
            {
                var pf = new PathFigure { StartPoint = pts[0], IsClosed = false, IsFilled = false };
                pf.Segments.Add(new LineSegment(pts[1], true));
                var pg = new PathGeometry();
                pg.Figures.Add(pf);
                geometry = pg;
            }
            else
            {
                geometry = BuildSmoothGeometry(pts);
            }
            geometry.Freeze();

            var path = new Path
            {
                Data = geometry,
                Stroke = brush,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            path.MouseEnter += (_, _) => path.StrokeThickness = 3.5;
            path.MouseLeave += (_, _) => path.StrokeThickness = 2;
            _seriesLayer.Children.Add(path);
        }

        // 数据点圆点
        for (int i = 0; i < pts.Count; i++)
        {
            double x = pts[i].X;
            double y = pts[i].Y;
            double value = isClick ? data[i].Clicks : data[i].Keys;

            string label = isClick ? "点击" : "按键";
            string tooltipText = $"{data[i].StartTime:HH:mm} {label}: {value}";

            var hitEllipse = new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = System.Windows.Media.Brushes.Transparent,
                ToolTip = new System.Windows.Controls.ToolTip
                {
                    Content = tooltipText,
                    Background = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("WindowBgBrush") ?? System.Windows.Media.Brushes.White),
                    Foreground = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("TextPrimaryBrush") ?? System.Windows.Media.Brushes.Black),
                    BorderBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("BorderBrush") ?? System.Windows.Media.Brushes.Gray),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 4, 6, 4)
                }
            };
            Canvas.SetLeft(hitEllipse, x - 12);
            Canvas.SetTop(hitEllipse, y - 12);
            _seriesLayer.Children.Add(hitEllipse);

            var dotEllipse = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = brush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dotEllipse, x - 2);
            Canvas.SetTop(dotEllipse, y - 2);
            _seriesLayer.Children.Add(dotEllipse);
        }
    }

    /// <summary>
    /// Catmull-Rom 转三次贝塞尔的平滑曲线，张力 0.5。
    /// 调用前需保证 pts.Count >= 3。
    /// </summary>
    private static PathGeometry BuildSmoothGeometry(List<System.Windows.Point> pts)
    {
        var figure = new PathFigure { StartPoint = pts[0], IsClosed = false, IsFilled = false };
        var bez = new PolyBezierSegment { IsStroked = true };

        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p0 = i == 0 ? pts[i] : pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i + 2 < pts.Count ? pts[i + 2] : pts[i + 1];

            var c1 = new System.Windows.Point(
                p1.X + (p2.X - p0.X) / 6.0,
                p1.Y + (p2.Y - p0.Y) / 6.0);
            var c2 = new System.Windows.Point(
                p2.X - (p3.X - p1.X) / 6.0,
                p2.Y - (p3.Y - p1.Y) / 6.0);

            // 把控制点 Y 钳制到当前段两端点的 Y 区间内，防止曲线过冲
            // （例如某点为 0 时不会跌破基线/坐标轴）
            double minY = Math.Min(p1.Y, p2.Y);
            double maxY = Math.Max(p1.Y, p2.Y);
            c1.Y = Math.Clamp(c1.Y, minY, maxY);
            c2.Y = Math.Clamp(c2.Y, minY, maxY);

            bez.Points.Add(c1);
            bez.Points.Add(c2);
            bez.Points.Add(p2);
        }

        figure.Segments.Add(bez);
        var pg = new PathGeometry();
        pg.Figures.Add(figure);
        return pg;
    }
}
