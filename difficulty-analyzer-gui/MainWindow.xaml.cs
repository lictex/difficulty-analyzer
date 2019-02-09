using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace difficulty_analyzer_gui
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private JObject JSONData;

        public MainWindow() => InitializeComponent();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "osu!beatmap|*.osu";
            if (dialog.ShowDialog() == true) ThreadPool.QueueUserWorkItem(a => LoadBeatmap(dialog.FileName));
        }

        private void Reset()
        {
            MainPanel.IsEnabled = false;
            Chart.Data = null;
            ComboBoxSkills.SelectedIndex = 0;
        }

        private void LoadBeatmap(string path)
        {
            Dispatcher.Invoke(Reset);
            Dispatcher.Invoke(() => Info("waiting for difficulty-analyzer..."));
            try
            {
                var s = "";

                var p = new Process();
                p.StartInfo.FileName = "difficulty-analyzer.exe";
                p.StartInfo.Arguments = $"\"{ path }\"";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.OutputDataReceived += (a, b) =>
                {
                    s += b.Data;
                };
                p.BeginOutputReadLine();
                p.WaitForExit();
                JSONData = JObject.Parse(s);
                Dispatcher.Invoke(() => Info(string.Format("Stars: {0}, Aim: {1}, Speed: {2}, MaxCombo: {3}",
                    JSONData["starRating"].Value<double>().ToString(".0000"),
                    JSONData["skills"]["aim"]["rating"].Value<double>().ToString(".0000"),
                    JSONData["skills"]["speed"]["rating"].Value<double>().ToString(".0000"),
                    JSONData["maxCombo"].Value<double>().ToString()
                )));

                Dispatcher.Invoke(() => SetChartData(Skill.Aim));

                Dispatcher.Invoke(() => MainPanel.IsEnabled = true);
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(() => Info("error: " + e.Message));
            }
        }

        private void SetChartData(Skill l)
        {
            if (JSONData == null)
            {
                Chart.Data = null;
                return;
            }
            switch (l)
            {
                case Skill.Aim: Chart.Data = JSONData["skills"]["aim"]["data"].Values<double>().ToList(); break;
                case Skill.Speed: Chart.Data = JSONData["skills"]["speed"]["data"].Values<double>().ToList(); break;
            }
            Chart.SectionLength = JSONData["skills"]["sectionLength"].Value<int>();
            Chart.StartTime = JSONData["skills"]["startTime"].Value<int>();
        }

        private void Info(string s) => TextInfo.Content = s;

        private void ComboBoxSkills_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => SetChartData((Skill)ComboBoxSkills.SelectedIndex);

        private enum Skill
        {
            Aim = 0,
            Speed = 1
        }
    }
}
