using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InputStats;

public class LineChartControl : Canvas
{
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

        var allClicks = clickData.Select(b => (double)b.Clicks).ToList();
        var allKeys = keyData.Select(b => (double)b.Keys).ToList();
        double maxValue = Math.Max(allClicks.DefaultIfEmpty(0).Max(), allKeys.DefaultIfEmpty(0).Max());
        if (maxValue < 1) maxValue = 1;

        var gridBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("BorderBrush") ?? new SolidColorBrush(Colors.Gray));
        var textBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("TextMutedBrush") ?? new SolidColorBrush(Colors.Gray));
        var clickBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("StatClickBrush") ?? new SolidColorBrush(Colors.Green));
        var keyBrush = (System.Windows.Media.Brush)(System.Windows.Application.Current.TryFindResource("StatKeyBrush") ?? new SolidColorBrush(Colors.Orange));

        // 绘制网格线和 Y 轴标签
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

        // 绘制折线
        if (clickData.Count > 0)
            DrawSeries(clickData, maxValue, clickBrush, paddingLeft, paddingTop, chartWidth, chartHeight, true);
        if (keyData.Count > 0)
            DrawSeries(keyData, maxValue, keyBrush, paddingLeft, paddingTop, chartWidth, chartHeight, false);

        // X 轴时间标签（首、中、尾）
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
    }

    private void DrawSeries(List<TimeBucket> data, double maxValue, System.Windows.Media.Brush brush,
        double paddingLeft, double paddingTop, double chartWidth, double chartHeight, bool isClick)
    {
        if (data.Count == 0) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            bool first = true;
            for (int i = 0; i < data.Count; i++)
            {
                double x = paddingLeft + (data.Count <= 1 ? chartWidth / 2 : (i / (double)(data.Count - 1)) * chartWidth);
                double value = isClick ? data[i].Clicks : data[i].Keys;
                double y = paddingTop + chartHeight - (value / maxValue) * chartHeight;

                if (first)
                {
                    ctx.BeginFigure(new System.Windows.Point(x, y), false, false);
                    first = false;
                }
                else
                {
                    ctx.LineTo(new System.Windows.Point(x, y), true, false);
                }
            }
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
        Children.Add(path);

        // 数据点圆点
        for (int i = 0; i < data.Count; i++)
        {
            double x = paddingLeft + (data.Count <= 1 ? chartWidth / 2 : (i / (double)(data.Count - 1)) * chartWidth);
            double value = isClick ? data[i].Clicks : data[i].Keys;
            double y = paddingTop + chartHeight - (value / maxValue) * chartHeight;

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
            Children.Add(hitEllipse);

            var dotEllipse = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = brush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dotEllipse, x - 2);
            Canvas.SetTop(dotEllipse, y - 2);
            Children.Add(dotEllipse);
        }
    }
}
