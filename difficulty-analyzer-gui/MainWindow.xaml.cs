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
            ComboBoxSkills.SelectedIndex = (int)Skill.Overall;
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

                Dispatcher.Invoke(() => SetChartData(Skill.Overall, CheckboxFilter.IsChecked == true));

                Dispatcher.Invoke(() => MainPanel.IsEnabled = true);
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(() => Info("error: " + e.Message));
            }
        }

        private void SetChartData(Skill l, bool filter)
        {
            if (JSONData == null)
            {
                Chart.Data = null;
                return;
            }
            if (filter) switch (l)
                {
                    case Skill.Aim:
                        var s = JSONData["skills"]["aim"]["data"].Values<double>().ToList();
                        Chart.Data = s.Zip(GetOrder(s), (a, b) => a * Math.Pow(0.9, b)).ToList(); break;
                    case Skill.Speed:
                        var t = JSONData["skills"]["speed"]["data"].Values<double>().ToList();
                        Chart.Data = t.Zip(GetOrder(t), (a, b) => a * Math.Pow(0.9, b)).ToList(); break;
                    case Skill.Overall:
                        var p = JSONData["skills"]["aim"]["data"].Values<double>().ToList();
                        p = p.Zip(GetOrder(p), (a, b) => a * Math.Pow(0.9, b)).ToList();
                        var q = JSONData["skills"]["speed"]["data"].Values<double>().ToList();
                        q = q.Zip(GetOrder(q), (a, b) => a * Math.Pow(0.9, b)).ToList();
                        Chart.Data = p.Zip(q, (x, y) => x + y).ToList();
                        break;

                }
            else switch (l)
                {
                    case Skill.Aim: Chart.Data = JSONData["skills"]["aim"]["data"].Values<double>().ToList(); break;
                    case Skill.Speed: Chart.Data = JSONData["skills"]["speed"]["data"].Values<double>().ToList(); break;
                    case Skill.Overall: Chart.Data = JSONData["skills"]["aim"]["data"].Values<double>().Zip(JSONData["skills"]["speed"]["data"].Values<double>(), (p, q) => p + q).ToList(); break;

                }
            Chart.SectionLength = JSONData["skills"]["sectionLength"].Value<int>();
            Chart.StartTime = JSONData["skills"]["startTime"].Value<int>();
        }

        private void Info(string s) => TextInfo.Content = s;

        private void CheckBox_Checked(object sender, RoutedEventArgs e) => SetChartData((Skill)ComboBoxSkills.SelectedIndex, CheckboxFilter.IsChecked == true);

        private void ComboBoxSkills_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => SetChartData((Skill)ComboBoxSkills.SelectedIndex, CheckboxFilter.IsChecked == true);

        private enum Skill
        {
            Aim = 0,
            Speed = 1,
            Overall = 2
        }

        public static int[] GetOrder<T>(List<T> list) where T : IComparable
        {
            var sorted = new List<T>(list); sorted.Sort((a, b) => b.CompareTo(a));
            var result = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
                for (int x = 0; x < list.Count; x++)
                    if (list[x].CompareTo(sorted[i]) == 0) result[x] = i;
            return result;
        }
    }
}
