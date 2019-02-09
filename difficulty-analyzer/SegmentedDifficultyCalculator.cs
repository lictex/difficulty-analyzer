using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DifficultyAnalyzer
{
    public class SegmentedDifficultyCalculator : DifficultyCalculator
    {
        private const int section_length = 400;
        private const double difficulty_multiplier = 0.0675;

        public class Result
        {
            public double StartTime;
            public List<double> AimPeaks;
            public List<double> SpeedPeaks;
            public double AimRating;
            public double SpeedRating;
            public double StarRating;
            public int MaxCombo;
            public int SectionLength;
        }

        public Result LastResult { get; private set; } = null;

        public SegmentedDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap) : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes Calculate(IBeatmap beatmap, Mod[] mods, double timeRate)
        {
            if (!beatmap.HitObjects.Any() || (!(beatmap is OsuBeatmap)))
                return new OsuDifficultyAttributes(mods, 0);

            OsuDifficultyBeatmap difficultyBeatmap = new OsuDifficultyBeatmap(beatmap.HitObjects.Cast<OsuHitObject>().ToList(), timeRate);
            Skill[] skills = new Skill[]
            {
                new Aim(),
                new Speed()
            };

            double sectionLength = section_length * timeRate;

            // The first object doesn't generate a strain, so we begin with an incremented section end
            double currentSectionEnd = Math.Ceiling(beatmap.HitObjects.First().StartTime / sectionLength) * sectionLength;

            foreach (OsuDifficultyHitObject h in difficultyBeatmap)
            {
                while (h.BaseObject.StartTime > currentSectionEnd)
                {
                    foreach (Skill s in skills)
                    {
                        s.SaveCurrentPeak();
                        s.StartNewSectionFrom(currentSectionEnd);
                    }

                    currentSectionEnd += sectionLength;
                }

                foreach (Skill s in skills)
                    s.Process(h);
            }

            // The peak strain will not be saved for the last section in the above loop
            foreach (Skill s in skills)
                s.SaveCurrentPeak();

            List<double> aimPeaks = new List<double>((List<double>)typeof(Skill).GetField("strainPeaks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).GetValue(skills[0]));
            List<double> speedPeaks = new List<double>((List<double>)typeof(Skill).GetField("strainPeaks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).GetValue(skills[1]));

            double aimRating = Math.Sqrt(skills[0].DifficultyValue()) * difficulty_multiplier;
            double speedRating = Math.Sqrt(skills[1].DifficultyValue()) * difficulty_multiplier;
            double starRating = aimRating + speedRating + Math.Abs(aimRating - speedRating) / 2;

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)(beatmap.HitObjects.First().HitWindows.Great / 2) / timeRate;
            double preempt = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.ApproachRate, 1800, 1200, 450) / timeRate;

            int maxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the head circle would be counted twice (once for the slider itself in the line above)
            maxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            LastResult = new Result()
            {
                StartTime = Math.Floor(beatmap.HitObjects[1].StartTime / sectionLength) * sectionLength,
                AimPeaks = aimPeaks,
                SpeedPeaks = speedPeaks,
                AimRating = aimRating,
                SpeedRating = speedRating,
                StarRating = starRating,
                SectionLength = section_length,
                MaxCombo = maxCombo
            };

            return new OsuDifficultyAttributes(mods, starRating)
            {
                AimStrain = aimRating,
                SpeedStrain = speedRating,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                MaxCombo = maxCombo
            };
        }
    }
}
