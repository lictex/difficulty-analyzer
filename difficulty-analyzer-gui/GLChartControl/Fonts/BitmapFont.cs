using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using static difficulty_analyzer_gui.GLChartControl.Util;

namespace difficulty_analyzer_gui.GLChartControl.Fonts
{
    /// <summary>
    /// broken implementation, but enough for here..?
    /// </summary>
    class BitmapFont
    {
        private BitmapFont(string fnt, string page0) { fntPath = fnt; pagePath = new string[] { page0 }; }

        public static BitmapFont Default { get; private set; } = new BitmapFont("difficulty_analyzer_gui.GLChartControl.Fonts.sFont.fnt", "difficulty_analyzer_gui.GLChartControl.Fonts.sFont.png");

        private string fntPath;
        private string[] pagePath;

        private int[] textures = new int[1];
        private uint textureWidth, textureHeight, fontSize, lineHeight;

        private int GetVAO(Char ch) => VAOArray[Array.IndexOf(CharArray, ch)];
        private bool TryGetChar(char ch, out Char c)
        {
            var cr = CharArray.Where(o => o.Value == ch);
            c = cr.FirstOrDefault();
            return cr.Count() != 0;
        }

        private int[] VAOArray;
        private Char[] CharArray;

        private struct Char
        {
            public char Value;
            public uint[] Coord;
            public uint Width;
            public uint Height;
            public int XOffset;
            public int YOffset;
            public int XAdvance;
            public KeyValuePair<char, int>[] Kernings;
        }

        private void ProcessFontFile()
        {
            using (StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(fntPath)))
            {
                var CharList = new List<Char>();
                string line;
                line = reader.ReadLine();
                {
                    fontSize = uint.Parse(Regex.Match(line, "size=[0-9]*").Value.Split('=')[1]);
                }
                line = reader.ReadLine();
                {
                    textureWidth = uint.Parse(Regex.Match(line, "scaleW=[0-9]*").Value.Split('=')[1]);
                    textureHeight = uint.Parse(Regex.Match(line, "scaleH=[0-9]*").Value.Split('=')[1]);
                    lineHeight = uint.Parse(Regex.Match(line, "lineHeight=[0-9]*").Value.Split('=')[1]);
                }
                //skipped some pages info
                while (!(line = reader.ReadLine()).StartsWith("chars count=")) ;
                int ccount = int.Parse(line.Split('=')[1]);
                for (int i = 0; i < ccount; i++)
                {
                    line = reader.ReadLine();
                    CharList.Add(new Char()
                    {
                        Value = (char)int.Parse(Regex.Match(line, "char id=[0-9]*").Value.Split('=')[1]),
                        Coord = new uint[]{
                                uint.Parse(Regex.Match(line, "x=[0-9]*").Value.Split('=')[1]),
                                uint.Parse(Regex.Match(line, "y=[0-9]*").Value.Split('=')[1])
                            },
                        Width = uint.Parse(Regex.Match(line, "width=[0-9]*").Value.Split('=')[1]),
                        Height = uint.Parse(Regex.Match(line, "height=[0-9]*").Value.Split('=')[1]),
                        XOffset = int.Parse(Regex.Match(line, "xoffset=-{0,1}[0-9]*").Value.Split('=')[1]),
                        YOffset = int.Parse(Regex.Match(line, "yoffset=-{0,1}[0-9]*").Value.Split('=')[1]),
                        XAdvance = int.Parse(Regex.Match(line, "xadvance=-{0,1}[0-9]*").Value.Split('=')[1])
                    });
                }

                try { while (!(line = reader.ReadLine()).StartsWith("kernings count=")) ; } catch { line = "kernings count=0"; }
                var kerningsList = new List<KeyValuePair<char, KeyValuePair<char, int>>>();
                int kcount = int.Parse(line.Split('=')[1]);
                for (int i = 0; i < kcount; i++)
                {
                    line = reader.ReadLine();
                    kerningsList.Add(new KeyValuePair<char, KeyValuePair<char, int>>(
                        (char)int.Parse(Regex.Match(line, "second=[0-9]*").Value.Split('=')[1]),
                        new KeyValuePair<char, int>(
                            (char)int.Parse(Regex.Match(line, "first=[0-9]*").Value.Split('=')[1]),
                             int.Parse(Regex.Match(line, "amount=-{0,1}[0-9]*").Value.Split('=')[1])
                        )
                    ));
                }
                for (int i = 0; i < CharList.Count; i++)
                {
                    KeyValuePair<char, int>[] keyValuePair = kerningsList.Where(o => o.Key == CharList[i].Value).Select(o => o.Value).ToArray();
                    var a = CharList[i];
                    a.Kernings = keyValuePair.Count() == 0 ? new KeyValuePair<char, int>[0] : keyValuePair;
                    CharList[i] = a;
                };
                CharArray = CharList.ToArray();
            }
        }

        public void Init()
        {
            ProcessFontFile();

            var bitmap = new System.Drawing.Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream(pagePath[0]));
            var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.GenTextures(textures.Length, textures);
            GL.BindTexture(TextureTarget.Texture2D, textures[0]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmapData.Width, bitmapData.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, bitmapData.Scan0);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            bitmap.UnlockBits(bitmapData);

            VAOArray = new int[CharArray.Length];
            for (int i = 0; i < VAOArray.Length; i++)
            {
                uint[] texcoordRaw = CharArray[i].Coord;
                float x1 = (float)texcoordRaw[0] / textureWidth;
                float y1 = (float)texcoordRaw[1] / textureHeight;
                float x2 = (float)(texcoordRaw[0] + CharArray[i].Width - 1) / textureWidth;
                float y2 = (float)(texcoordRaw[1] + CharArray[i].Height - 1) / textureHeight;

                VAOArray[i] = CreateRectVAO(x1, y1, x2, y2);
            }
        }

        public void DrawText(ShaderProgram glShader, string text, float x, float y, float size, Color4 color, Origin origin = Origin.Center)
        {
            float sizeMultiplier = size / glShader.CurrentViewport.ScaleY / fontSize;

            GL.UseProgram(glShader);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textures[0]);
            GL.Uniform1(GL.GetUniformLocation(glShader, "colortex"), 0);

            var lines = text.Split('\n');

            float dx = 0, dy = 0;
            #region dy = some_jb_things;
            switch (origin)
            {
                case Origin.Center:
                case Origin.CenterLeft:
                case Origin.CenterRight:
                    dy = -lineHeight / 2f * (lines.Length - 1) * sizeMultiplier; break;
                case Origin.BottomCenter:
                case Origin.BottomLeft:
                case Origin.BottomRight:
                    dy = lineHeight * (0.5f - lines.Length) * sizeMultiplier; break;
                case Origin.TopCenter:
                case Origin.TopLeft:
                case Origin.TopRight:
                    dy = lineHeight / 2f * sizeMultiplier; break;
                default:
                    dy = 0; break;
            }
            #endregion

            Char prev;
            foreach (string st in lines)
            {
                #region dx = some_jb_things;
                float lineWidth = 0;
                prev = new Char();
                foreach (char ch in st)
                {
                    if (!TryGetChar(ch, out Char charobj)) continue;
                    lineWidth += charobj.XAdvance + (float)charobj.Kernings.Where(o => o.Key == prev.Value).Select(o => o.Value).FirstOrDefault();
                    prev = charobj;
                }
                switch (origin)
                {
                    case Origin.Center:
                    case Origin.BottomCenter:
                    case Origin.TopCenter:
                        dx = -lineWidth / 2f * sizeMultiplier; break;
                    case Origin.CenterRight:
                    case Origin.BottomRight:
                    case Origin.TopRight:
                        dx = -lineWidth * sizeMultiplier; break;
                    case Origin.CenterLeft:
                    case Origin.BottomLeft:
                    case Origin.TopLeft:
                    default:
                        dx = 0; break;
                }
                #endregion

                prev = new Char();
                foreach (char ch in st)
                {
                    if (!TryGetChar(ch, out Char charobj)) continue;

                    float w = (float)charobj.Width / charobj.Height * size;
                    float kerning = charobj.Kernings.Where(o => o.Key == prev.Value).Select(o => o.Value).FirstOrDefault();

                    GL.Uniform1(glShader["width"], w);
                    GL.Uniform1(glShader["height"], size);
                    GL.Uniform1(glShader["x"], x + dx + (charobj.XOffset + kerning) * sizeMultiplier + (w / 2));
                    GL.Uniform1(glShader["y"], y + dy + charobj.YOffset * sizeMultiplier);
                    GL.Uniform4(glShader["color"], color);
                    GL.Uniform1(glShader["radius"], 0);

                    GL.BindVertexArray(GetVAO(charobj));
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                    dx += (charobj.XAdvance + kerning) * sizeMultiplier;
                    prev = charobj;
                }
                dy += lineHeight * sizeMultiplier;
            }
        }
    }
}
