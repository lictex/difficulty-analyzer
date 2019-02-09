using Newtonsoft.Json.Linq;
using osu.Game.Rulesets.Osu;
using PerformanceCalculator;
using System.IO;
using static System.Console;

namespace DifficultyAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                WriteLine("osu!std difficulty analyzer");
                WriteLine("usage: difficulty-analyzer [.osu file path]");
                return;
            }

            var calc = new SegmentedDifficultyCalculator(new OsuRuleset(), new ProcessorWorkingBeatmap(args[0]));
            calc.Calculate();
            var result = calc.LastResult;
            if (result == null)
            {
                WriteLine("failed.");
                return;
            }

            var json = new JObject();
            {
                json["starRating"] = result.StarRating;
                json["maxCombo"] = result.MaxCombo;

                var skills = new JObject();
                {
                    skills["sectionLength"] = result.SectionLength;
                    skills["startTime"] = result.StartTime;

                    var aim = new JObject();
                    {
                        aim["rating"] = result.AimRating;
                        aim["data"] = JArray.FromObject(result.AimPeaks);
                    }
                    skills["aim"] = aim;

                    var speed = new JObject();
                    {
                        speed["rating"] = result.SpeedRating;
                        speed["data"] = JArray.FromObject(result.SpeedPeaks);
                    }
                    skills["speed"] = speed;
                }
                json["skills"] = skills;
            }

            WriteLine(json.ToString());
        }
    }
}
