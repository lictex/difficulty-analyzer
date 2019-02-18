using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using difficulty_analyzer_gui.GLChartControl.Fonts;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using static difficulty_analyzer_gui.GLChartControl.Util;

namespace difficulty_analyzer_gui.GLChartControl
{
    /// <summary>
    /// GLChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class GLChartControl : UserControl
    {
        private OpenTK.GLControl GLControl;

        public List<double> _data;
        public List<double> Data { get => _data; set { _data = value; ReloadData(Data); ChartScale = 1; ChartOffset = 0; } }
        public int SectionLength { get; set; }
        public int StartTime { get; set; }

        private float _chartScale = 1;
        private float ChartScale
        {
            get => _chartScale; set
            {
                _chartScale = value < 1 ? 1 : value;

                if (Data != null && _chartScale > Data.Count) _chartScale = Data.Count;
            }
        }

        private float _chartOffset = 0;
        private float ChartOffset
        {
            get => _chartOffset; set
            {
                _chartOffset = value > 0 ? 0 : value;
                if (ChartScale * viewportInfo.ScaledWidth + _chartOffset < viewportInfo.ScaledWidth) _chartOffset = (float)((1 - ChartScale) * viewportInfo.ScaledWidth);
            }
        }

        private float InfoAlpha { get; set; } = 0;
        private float InfoBackgroundRGB { get; set; } = 140 / 255f;

        public GLChartControl()
        {
            InitializeComponent();
            if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime) return;

            GLControl = new OpenTK.GLControl();
            wHost.Child = GLControl;
            GLControl.Load += GLControl_Load;
        }

        private Thread renderThread;
        private ConcurrentQueue<Action> tQueue = new ConcurrentQueue<Action>();
        private void GLControl_Load(object sender, EventArgs e)
        {
            Prepare();
            GLControl_Resize(sender, e);
            GLControl.Resize += GLControl_Resize;
            GLControl.MouseMove += GLControl_MouseMove;
            GLControl.MouseWheel += GLControl_MouseWheel;
            GLControl.MouseEnter += GLControl_MouseEnter;
            GLControl.MouseLeave += GLControl_MouseLeave;
            GLControl.MouseClick += GLControl_MouseClick;
            GLControl.VSync = false;

            GLControl.Context.MakeCurrent(null);
            renderThread = new Thread(() =>
            {
                GLControl.Context.MakeCurrent(GLControl.WindowInfo);
                while (GLControl.Visible && !GLControl.IsDisposed) { while (tQueue.TryDequeue(out Action a)) a(); FrameLoop(); }
            });
            renderThread.Start();
        }

        private ViewportInfo viewportInfo;
        private void GLControl_Resize(object sender, EventArgs e)
        {
            var p = viewportInfo.IsValid ? ((float)GLControl.Width / viewportInfo.Width) : 1;
            var source = System.Windows.PresentationSource.FromVisual(this);
            if (source != null)
            {
                viewportInfo.Width = GLControl.Width;
                viewportInfo.Height = GLControl.Height;
                viewportInfo.ScaleX = (float)source.CompositionTarget.TransformToDevice.M11;
                viewportInfo.ScaleY = (float)source.CompositionTarget.TransformToDevice.M22;
            }
            ChartOffset *= p;
            Thread.MemoryBarrier();
            _iChartOffset = ChartOffset;
        }

        private double mousePosX, mousePosY;
        private System.Drawing.Point _lastPoint;
        private string _lastTime;
        private double _lastMouseData;
        private void GLControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mousePosX = e.Location.X / viewportInfo.ScaleX; mousePosY = e.Location.Y / viewportInfo.ScaleY;

            var m = e.Location;
            if (e.Button == System.Windows.Forms.MouseButtons.Left) ChartOffset += (m.X - _lastPoint.X) / viewportInfo.ScaleX;
            _lastPoint = m;

            InfoBackgroundRGB = mousePosY < viewportInfo.ScaledHeight - 2 && mousePosY > viewportInfo.ScaledHeight - 24 ? 70 / 255f : 140 / 255f;

            var n = GetDataIndex((float)mousePosX);
            var ts = new TimeSpan(((long)n * SectionLength + StartTime) * 10000);
            _lastTime = string.Format("{0}:{1}:{2}", Math.Floor(ts.TotalMinutes), ts.Seconds.ToString().PadLeft(2, '0'), ts.Milliseconds.ToString().PadLeft(3, '0'));
            _lastMouseData = Data == null ? 0 : Data[n.Clamp(0, Data.Count - 1)];
        }

        private void GLControl_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (mousePosY < viewportInfo.ScaledHeight - 2 && mousePosY > viewportInfo.ScaledHeight - 24) OpenOsuEditor(_lastTime);
        }

        private void GLControl_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var s = (float)e.Delta / Mouse.MouseWheelDeltaForOneLine * 1.25f;
            var p = (float)e.Location.X / GLControl.Width;
            var pp = (e.Location.X / viewportInfo.ScaleX - ChartOffset) / (viewportInfo.ScaledWidth * ChartScale);
            if (s > 0)
            {
                ChartScale *= s;
            }
            else
            {
                ChartScale /= -s;
            }

            ChartOffset = e.Location.X / viewportInfo.ScaleX - (viewportInfo.ScaledWidth * ChartScale) * pp;
        }

        private void GLControl_MouseLeave(object sender, EventArgs e) => InfoAlpha = 0;

        private void GLControl_MouseEnter(object sender, EventArgs e) => InfoAlpha = 1;

        private void ReloadData(List<double> l)
        {
            if (l == null) { tQueue.Enqueue(() => LoadChartData(new float[0])); return; }
            var mult = CalculateMultiplier(l);
            float[] d = new float[l.Count * 9];
            for (int i = 0; i < d.Length; i += 9)
            {
                d[i] = (float)(i / 9) / (d.Length / 9 - 1) * 2 - 1;
                d[i + 1] = (float)(l[i / 9] * mult) * 2 - 1;
                d[i + 2] = 0;
                d[i + 3] = d[i + 4] = d[i + 5] = d[i + 6] = 1;
                d[i + 7] = d[i + 8] = 0;
            }
            tQueue.Enqueue(() => LoadChartData(d));
        }

        private int GetDataIndex(float screenX) => Data == null ? 0 : (int)Math.Round((screenX - ChartOffset) / (viewportInfo.ScaledWidth * ChartScale) * (Data.Count - 1), MidpointRounding.AwayFromZero);

        private static double CalculateMultiplier(List<double> data)
        {
            if (data == null) return 0;
            var x = data.Average();
            data.ForEach(d => { if (d * 0.5 > x) x = d / 2; });
            return 0.5 / x;
        }

        private static void OpenOsuEditor(string time) => Process.Start(@"osu://edit/" + time);

        #region Render

        private VAO chartVAO = VAO.Invaild;
        private ShaderProgram chartShader, uiTexturedShader, uiShader;

        private void Prepare()
        {
            chartShader = new ShaderProgram("difficulty_analyzer_gui.GLChartControl.Shaders.UI.vert", "difficulty_analyzer_gui.GLChartControl.Shaders.Chart.geom", "difficulty_analyzer_gui.GLChartControl.Shaders.Rect.frag");
            uiTexturedShader = new ShaderProgram("difficulty_analyzer_gui.GLChartControl.Shaders.UI.vert", "difficulty_analyzer_gui.GLChartControl.Shaders.TexturedRect.frag");
            uiShader = new ShaderProgram("difficulty_analyzer_gui.GLChartControl.Shaders.UI.vert", "difficulty_analyzer_gui.GLChartControl.Shaders.Rect.frag");

            ReloadData(null);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            BitmapFont.Default.Init();
            Rect.Instance.Init();
        }

        private FrameLimiter limiter = new FrameLimiter() { Limit = 144 };
        private void FrameLoop()
        {
            try
            {
                float d = limiter.FrameStart();
                Render(d);
                GLControl.SwapBuffers();
                GL.Finish();
                limiter.FrameEnd();
            }
            catch { }
        }

        private void LoadChartData(float[] l)
        {
            if (chartVAO != VAO.Invaild) { GL.DeleteVertexArray(chartVAO); chartVAO = VAO.Invaild; }
            if (l.Length == 0) return;
            chartVAO = new VAO(l);
        }

        private float _iChartScale = 1;
        private float _iChartOffset = 1;
        private float _iInfoAlpha = 0;
        private float _iInfoBackgroundRGB = 140 / 255f;
        private const float _i = 6f;
        private void Render(float delta)
        {
            _iChartScale += (ChartScale - _iChartScale) / _i * Math.Min(delta / (1000f / 144f), _i);
            _iChartOffset += (ChartOffset - _iChartOffset) / _i * Math.Min(delta / (1000f / 144f), _i);
            _iInfoAlpha += (InfoAlpha - _iInfoAlpha) / _i * Math.Min(delta / (1000f / 144f), _i);
            _iInfoBackgroundRGB += (InfoBackgroundRGB - _iInfoBackgroundRGB) / _i * Math.Min(delta / (1000f / 144f), _i);

            GL.ClearColor(Color4.White);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Viewport(viewportInfo.X, viewportInfo.Y, viewportInfo.Width, viewportInfo.Height);

            var lX = mousePosX.Clamp(64 + 2, viewportInfo.ScaledWidth - 64 - 2);
            GL.UseProgram(uiShader.WithViewport(viewportInfo));
            Rect.Instance.Draw(uiShader, (float)mousePosX, 0, 3f, viewportInfo.ScaledHeight - 24f, new Color4(180 / 255f, 180 / 255f, 180 / 255f, _iInfoAlpha), Origin.TopCenter);
            Rect.Instance.Draw(uiShader, 0, viewportInfo.ScaledHeight - 24f, viewportInfo.ScaledWidth, 2f, new Color4(70 / 255f, 70 / 255f, 70 / 255f, 1f), Origin.BottomLeft);
            Rect.Instance.Draw(uiShader, (float)lX, viewportInfo.ScaledHeight - 10f - 2f, 128f, 20f, new Color4(_iInfoBackgroundRGB, _iInfoBackgroundRGB, _iInfoBackgroundRGB, _iInfoAlpha), Origin.Center, 10);

            GL.UseProgram(uiTexturedShader.WithViewport(viewportInfo));
            BitmapFont.Default.DrawText(uiTexturedShader, string.Format("{0} - {1}", _lastTime, Math.Round(_lastMouseData, 2, MidpointRounding.AwayFromZero).ToString("00.00")), (float)lX, viewportInfo.ScaledHeight - 8.5f - 2f, 17, new Color4(1f, 1f, 1f, _iInfoAlpha));

            GL.UseProgram(chartShader.WithViewport(viewportInfo));
            GL.Uniform4(chartShader["color"], new Color4(70, 70, 70, 255));
            GL.Uniform1(chartShader["radius"], 0);
            GL.Uniform1(chartShader["width"], viewportInfo.ScaledWidth * _iChartScale);
            GL.Uniform1(chartShader["height"], viewportInfo.ScaledHeight - 24f);
            GL.Uniform1(chartShader["x"], _iChartOffset + (viewportInfo.ScaledWidth * _iChartScale) / 2f);
            GL.Uniform1(chartShader["y"], viewportInfo.ScaledHeight / 2 - 12f);

            GL.BindVertexArray(chartVAO);
            GL.DrawArrays(PrimitiveType.Points, 0, chartVAO.Length);
        }
        #endregion
    }
}
