using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace difficulty_analyzer_gui
{
    /// <summary>
    /// ChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class ChartControl : UserControl
    {
        public ChartControl() => InitializeComponent();

        private int _samplesPerScreen = 0;
        private int SamplesPerScreen
        {
            get => _samplesPerScreen; set
            {
                if (_data == null || _data.Count == 0) _samplesPerScreen = 0;
                else
                {
                    _samplesPerScreen = value;
                    if (_samplesPerScreen > _data.Count) _samplesPerScreen = _data.Count;
                    if (_samplesPerScreen < 1) _samplesPerScreen = 1;

                    if (_samplesPerScreen + _sampleStartIndex > _data.Count) _sampleStartIndex = _data.Count - _samplesPerScreen;
                }
            }
        }

        private int _sampleStartIndex = 0;
        private int SampleStartIndex
        {
            get => _sampleStartIndex; set
            {
                if (Data == null)
                {
                    _sampleStartIndex = 0;
                    return;
                }
                _sampleStartIndex = value;
                if (_samplesPerScreen + _sampleStartIndex > _data.Count) _sampleStartIndex = _data.Count - _samplesPerScreen;
                if (_sampleStartIndex < 0) _sampleStartIndex = 0;
            }
        }

        private double DataMultiplier { get; set; } = 0;

        private List<double> _data = null;
        public List<double> Data
        {
            get => _data; set
            {
                _data = value;
                SamplesPerScreen = _data == null ? 0 : _data.Count;
                SampleStartIndex = 0;
                DataMultiplier = CalculateMultiplier(_data);
                if (Data == null) CanvasUI.Visibility = Visibility.Hidden;
                else CanvasUI.Visibility = Visibility.Visible;
                Redraw();
            }
        }

        public int SectionLength { get; set; } = 400;
        public int StartTime { get; set; } = 0;

        private void Redraw()
        {
            CanvasData.Children.Clear();
            if (SamplesPerScreen == 0) return;

            var cWidth = CanvasData.ActualWidth;
            var cHeight = CanvasData.ActualHeight;

            var spacing = SamplesPerScreen > 1 ? cWidth / (SamplesPerScreen - 1) : 0;

            for (int k = 0; k < SamplesPerScreen; k++)
            {
                var d = Data[k + SampleStartIndex];

                Rectangle r = new Rectangle()
                {
                    Fill = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    Width = 4,
                    Height = cHeight * (d * DataMultiplier)
                };

                Canvas.SetLeft(r, spacing * k - r.Width / 2);
                Canvas.SetBottom(r, 0);

                CanvasData.Children.Add(r);
            }
        }

        private void Scale(int delta, Point origin)
        {
            var p = PointToDataIndex(origin);
            SamplesPerScreen += delta;
            var n = PointToDataIndex(origin);
            SampleStartIndex += p - n;
            Redraw();
        }

        private void Move(int delta)
        {
            SampleStartIndex += delta;
            Redraw();
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            BlockInfoLine.Height = (sender as FrameworkElement).ActualHeight - 24;
            _lastPoint.X *= e.NewSize.Width / e.PreviousSize.Width;
            UpdateBlockInfo();
            Redraw();
        }

        private Point _lastPoint;
        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            var m = Mouse.GetPosition((sender as FrameworkElement));
            if (e.LeftButton == MouseButtonState.Pressed) Move(PointToDataIndex(_lastPoint) - PointToDataIndex(m));
            _lastPoint = m;

            UpdateBlockInfo();
        }

        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e) => Scale(e.Delta / 4, Mouse.GetPosition(sender as FrameworkElement));

        private string _lastTime;
        private void UpdateBlockInfo()
        {
            if (Data == null) return;
            var n = PointToDataIndex(_lastPoint);
            double x = _lastPoint.X - BlockInfo.ActualWidth / 2;
            if (x < 2) x = 2;
            if (x + BlockInfo.ActualWidth > CanvasUI.ActualWidth - 2) x = CanvasUI.ActualWidth - BlockInfo.ActualWidth - 2;
            Canvas.SetLeft(BlockInfo, x);
            Canvas.SetLeft(BlockInfoLine, _lastPoint.X - BlockInfoLine.ActualWidth / 2);

            var ts = new TimeSpan(((long)n * SectionLength + StartTime) * 10000);
            _lastTime = string.Format("{0}:{1}:{2}", Math.Floor(ts.TotalMinutes), ts.Seconds.ToString().PadLeft(2, '0'), ts.Milliseconds.ToString().PadLeft(3, '0'));
            BlockInfoText.Content = string.Format("{0} - {1}", _lastTime, Math.Round(Data[n], 2, MidpointRounding.AwayFromZero).ToString("00.00"));
        }

        private void BlockInfo_MouseDown(object sender, MouseButtonEventArgs e) => OpenOsuEditor(_lastTime);

        private void BlockInfo_MouseEnter(object sender, MouseEventArgs e) => BlockInfo.Background = new SolidColorBrush(Color.FromRgb(70, 70, 70));

        private void BlockInfo_MouseLeave(object sender, MouseEventArgs e) => BlockInfo.Background = new SolidColorBrush(Color.FromRgb(140, 140, 140));

        private int PointToDataIndex(Point p) => (int)Math.Round(p.X / CanvasData.ActualWidth * (SamplesPerScreen - 1), MidpointRounding.AwayFromZero) + SampleStartIndex;

        private static void OpenOsuEditor(string time) => Process.Start(@"osu://edit/" + time);

        private static double CalculateMultiplier(List<double> data)
        {
            if (data == null) return 0;
            var x = data.Average();
            data.ForEach(d => { if (d * 0.5 > x) x = d / 2; });
            return 0.5 / x;
        }
    }
}
