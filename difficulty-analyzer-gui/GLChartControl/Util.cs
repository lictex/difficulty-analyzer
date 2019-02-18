using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace difficulty_analyzer_gui.GLChartControl
{
    static class Util
    {
        public enum Origin
        {
            Center, CenterLeft, CenterRight,
            TopCenter, TopLeft, TopRight,
            BottomCenter, BottomLeft, BottomRight,
        }

        public class FrameLimiter
        {
            private Stopwatch sw = new Stopwatch();

            public int Limit { get; set; } = 144;

            public float FrameStart()
            {
                var r = sw.ElapsedTicks / 10000f;
                sw.Restart(); return r;
            }

            public void FrameEnd()
            {
                double id = (1000.0 / Limit - sw.ElapsedTicks / 10000.0);
                if (id > 0) Thread.Sleep(TimeSpan.FromTicks((long)(id * 10000)));
            }
        }

        public struct ViewportInfo
        {
            public int X, Y;
            public int Width, Height;
            public float ScaleX, ScaleY;

            public float ScaledWidth { get => Width / ScaleX; }
            public float ScaledHeight { get => Height / ScaleY; }

            public bool IsValid => Width != 0 && Height != 0;
        }

        public class ShaderProgram
        {
            public int ID { get; private set; }

            public ShaderProgram(string vshPath, string fshPath) : this(vshPath, null, fshPath) { }
            public ShaderProgram(string vshPath, string gshPath, string fshPath)
            {
                int vsh = CreateShader(ShaderType.VertexShader, vshPath);
                int gsh = gshPath == null ? 0 : CreateShader(ShaderType.GeometryShader, gshPath);
                int fsh = CreateShader(ShaderType.FragmentShader, fshPath);

                ID = GL.CreateProgram();
                GL.AttachShader(ID, vsh);
                if (gsh != 0) GL.AttachShader(ID, gsh);
                GL.AttachShader(ID, fsh);
                GL.LinkProgram(ID);
                GL.DeleteShader(vsh);
                if (gsh != 0) GL.DeleteShader(gsh);
                GL.DeleteShader(fsh);
            }

            public int this[string s] { get => GL.GetUniformLocation(ID, s); }

            public ViewportInfo CurrentViewport { get; private set; }

            public ShaderProgram WithViewport(ViewportInfo viewportSize)
            {
                CurrentViewport = viewportSize;
                GL.Uniform1(this["viewportWidth"], viewportSize.Width);
                GL.Uniform1(this["viewportHeight"], viewportSize.Height);
                GL.Uniform1(this["scaleX"], viewportSize.ScaleX);
                GL.Uniform1(this["scaleY"], viewportSize.ScaleY);
                return this;
            }

            public static implicit operator int(ShaderProgram s) => s.ID;
        }

        public class VAO
        {
            public int ID { get; private set; }

            public int Length { get; private set; }

            private VAO() { }
            public VAO(float[] vert)
            {
                GL.GenBuffers(1, out int VBO);
                GL.GenVertexArrays(1, out int VAO);
                GL.BindVertexArray(VAO);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
                GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * vert.Length, vert, BufferUsageHint.DynamicDraw);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(float) * 9, 0);
                GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 9, sizeof(float) * 3);
                GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, sizeof(float) * 9, sizeof(float) * 7);
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindVertexArray(0);
                GL.DeleteBuffer(VBO);
                ID = VAO;
                Length = vert.Length / 9;
            }

            public static implicit operator int(VAO s) => s.ID;
            public static bool operator ==(VAO a, VAO b) => a.ID == b.ID;
            public static bool operator !=(VAO a, VAO b) => a.ID != b.ID;
            public override bool Equals(object obj) => obj as VAO == this;
            public override int GetHashCode() => ID.GetHashCode();

            public static VAO Invaild => new VAO() { ID = 0, Length = 0 };
        }

        public class Rect
        {
            private Rect() { }

            public static Rect Instance { get; private set; } = new Rect();

            private int rectVAO;

            public void Init()
            {
                rectVAO = CreateRectVAO();
            }

            public void Draw(ShaderProgram glShader, float x, float y, float w, float h, Color4 col, Origin origin = Origin.Center, float radius = 0)
            {
                float aX = x, aY = y;
                void left() => aX += w / 2f;
                void right() => aX -= w / 2f;
                void top() => aY += h / 2f;
                void bottom() => aY -= h / 2f;
                switch (origin)
                {
                    case Origin.BottomCenter: bottom(); break;
                    case Origin.BottomLeft: bottom(); left(); break;
                    case Origin.BottomRight: bottom(); right(); break;
                    case Origin.Center: break;
                    case Origin.CenterLeft: left(); break;
                    case Origin.CenterRight: right(); break;
                    case Origin.TopCenter: top(); break;
                    case Origin.TopLeft: top(); left(); break;
                    case Origin.TopRight: top(); right(); break;
                }
                GL.Uniform4(glShader["color"], col);
                GL.Uniform1(glShader["radius"], radius);
                GL.Uniform1(glShader["width"], w);
                GL.Uniform1(glShader["height"], h);
                GL.Uniform1(glShader["x"], aX);
                GL.Uniform1(glShader["y"], aY);

                GL.BindVertexArray(rectVAO);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }
        }

        public static int CreateShader(ShaderType type, string shaderLocation)
        {
            using (StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(shaderLocation)))
            {
                string shader = reader.ReadToEnd();
                int s = GL.CreateShader(type);
                GL.ShaderSource(s, shader);
                GL.CompileShader(s);
                GL.GetShader(s, ShaderParameter.CompileStatus, out int success);
                if (success == 0) throw new ArgumentException(GL.GetShaderInfoLog(s));
                return s;
            }
        }

        public static int CreateRectVAO(float texX1 = 0, float texY1 = 0, float texX2 = 0, float texY2 = 0) => new VAO(
            new float[] {
                 /* vert */ -1f  ,  1f  , 0f  , /* color */ 1f  , 1f  , 1f  , 1f  , /* texcoord */ texX1  , texY1  ,
                 /* vert */ -1f  , -1f  , 0f  , /* color */ 1f  , 1f  , 1f  , 1f  , /* texcoord */ texX1  , texY2  ,
                 /* vert */  1f  , -1f  , 0f  , /* color */ 1f  , 1f  , 1f  , 1f  , /* texcoord */ texX2  , texY2  ,
                 /* vert */ -1f  ,  1f  , 0f  , /* color */ 1f  , 1f  , 1f  , 1f  , /* texcoord */ texX1  , texY1  ,
                 /* vert */  1f  , -1f  , 0f  , /* color */ 1f  , 1f  , 1f  , 1f  , /* texcoord */ texX2  , texY2  ,
                 /* vert */  1f  ,  1f  , 0f  , /* color */ 1f  , 1f  , 1f  , 1f  , /* texcoord */ texX2  , texY1
            }
        );

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T> => val.CompareTo(min) < 0 ? min : val.CompareTo(max) > 0 ? max : val;
    }
}
