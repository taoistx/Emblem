using Godot;
using System;
using System.Collections.Generic;

namespace FE5.Units
{
    public static class GrowthManager
    {
        private static Random _random = new Random();

        public static bool CheckGrowth(int growthRate)
        {
            if (growthRate >= 100)
                return true;
            if (growthRate <= 0)
                return false;

            int roll = _random.Next(1, 101);
            return roll <= growthRate;
        }

        public static LevelUpResult ProcessLevelUp(UnitStats stats)
        {
            var result = new LevelUpResult
            {
                LeveledUp = true,
                PreviousLevel = stats.Level,
                NewLevel = stats.Level + 1,
                PreviousStats = stats.Base.Clone(),
                StatIncreases = new List<string>()
            };

            if (CheckGrowth(stats.GrowthRate.HP))
            {
                stats.Base.MaxHP += 1;
                stats.Base.HP = Mathf.Max(stats.Base.HP, stats.Base.MaxHP);
                result.StatIncreases.Add("HP+1");
            }

            if (CheckGrowth(stats.GrowthRate.str))
            {
                stats.Strength += 1;
                result.StatIncreases.Add("力+1");
            }

            if (CheckGrowth(stats.GrowthRate.mag))
            {
                stats.Magic += 1;
                result.StatIncreases.Add("魔+1");
            }

            if (CheckGrowth(stats.GrowthRate.skl))
            {
                stats.Skill += 1;
                result.StatIncreases.Add("技+1");
            }

            if (CheckGrowth(stats.GrowthRate.spd))
            {
                stats.Speed += 1;
                result.StatIncreases.Add("速+1");
            }

            if (CheckGrowth(stats.GrowthRate.lck))
            {
                stats.Luck += 1;
                result.StatIncreases.Add("运+1");
            }

            if (CheckGrowth(stats.GrowthRate.def))
            {
                stats.Defense += 1;
                result.StatIncreases.Add("守+1");
            }

            if (CheckGrowth(stats.GrowthRate.bld))
            {
                stats.Build += 1;
                result.StatIncreases.Add("体格+1");
            }

            if (CheckGrowth(stats.GrowthRate.Movement))
            {
                stats.Movement += 1;
                result.StatIncreases.Add("移+1");
            }

            stats.Level += 1;
            stats.Experience = 0;
            result.NewStats = stats.Base.Clone();

            return result;
        }

        public static List<string> GetGrowthResults(UnitStats stats, GrowthRates rates)
        {
            var results = new List<string>();

            if (CheckGrowth(rates.HP)) results.Add("HP");
            if (CheckGrowth(rates.str)) results.Add("力量");
            if (CheckGrowth(rates.mag)) results.Add("魔力");
            if (CheckGrowth(rates.skl)) results.Add("技巧");
            if (CheckGrowth(rates.spd)) results.Add("速度");
            if (CheckGrowth(rates.lck)) results.Add("幸运");
            if (CheckGrowth(rates.def)) results.Add("防御");
            if (CheckGrowth(rates.bld)) results.Add("体格");
            if (CheckGrowth(rates.Movement)) results.Add("移动");

            return results;
        }

        public static void SimulateLevelUp(UnitStats stats, int iterations = 100)
        {
            Dictionary<string, int> totalGrowths = new Dictionary<string, int>
            {
                ["HP"] = 0, ["力量"] = 0, ["魔力"] = 0, ["技巧"] = 0,
                ["速度"] = 0, ["幸运"] = 0, ["防御"] = 0, ["体格"] = 0, ["移动"] = 0
            };

            for (int i = 0; i < iterations; i++)
            {
                var results = GetGrowthResults(stats, stats.GrowthRate);
                foreach (var r in results)
                {
                    totalGrowths[r]++;
                }
            }

            GD.Print($"\n=== 模拟 {iterations} 次升级的属性成长统计 ===");
            foreach (var kvp in totalGrowths)
            {
                double percentage = (kvp.Value * 100.0) / iterations;
                GD.Print($"{kvp.Key}: {percentage:F1}% (预计每 {iterations / Math.Max(1, kvp.Value)} 次升级成长一次)");
            }
        }
    }
}
