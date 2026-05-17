using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;

namespace FE5.Combat
{
    public enum StaffType
    {
        Heal,
        Attack,
        Teleport,
        Rescue,
        StatusRecovery,
        Barrier,
        Torch,
        Unlock,
        Mend,
        Fortify,
        Restore,
        Hammerne,
        Warp,
        Return,
        Silence,
        Sleep,
        Berserk,
        Dance,
        Steal,
        Take,
        Summon,
        Dark,
        Light,
        Anima
    }

    public static class ExperienceManager
    {
        public const int ExpToLevel = 100;
        public const int MaxLevel = 20;

        public static int CalculateHitExp(int damage)
        {
            return 0;
        }

        public static int CalculateDefeatExp(int enemyLevel, int unitLevel)
        {
            return 0;
        }

        public static int CalculateStaffExp(StaffType staffType, int effectValue)
        {
            return 0;
        }

        public static bool CanLevelUp(int currentLevel)
        {
            return currentLevel < MaxLevel;
        }

        public static bool CanGainExp(int currentLevel)
        {
            return currentLevel < MaxLevel;
        }

        public static LevelUpResult AddExperience(Unit unit, int exp)
        {
            var result = new LevelUpResult
            {
                LeveledUp = false,
                PreviousLevel = unit.Stats.Level,
                NewLevel = unit.Stats.Level,
                ExcessExp = exp,
                StatIncreases = new List<string>()
            };

            if (!CanGainExp(unit.Stats.Level))
            {
                GD.Print($"[{unit.UnitName}] 已达到等级上限 ({MaxLevel})");
                return result;
            }

            unit.Stats.Experience += exp;

            while (unit.Stats.Experience >= ExpToLevel && CanLevelUp(unit.Stats.Level))
            {
                unit.Stats.Experience -= ExpToLevel;
                var levelUpResult = FE5.Units.GrowthManager.ProcessLevelUp(unit.Stats);
                result.LeveledUp = true;
                result.NewLevel = unit.Stats.Level;
                result.StatIncreases.AddRange(levelUpResult.StatIncreases);
                result.NewStats = levelUpResult.NewStats;
                result.ExcessExp = unit.Stats.Experience;
            }

            return result;
        }

        public static string GetExpPreview(int currentExp, int potentialExp)
        {
            int totalExp = currentExp + potentialExp;
            int levelsToGain = totalExp / ExpToLevel;

            if (levelsToGain == 0)
            {
                return $"当前EXP: {currentExp}/{ExpToLevel} (+{potentialExp})";
            }

            return $"当前EXP: {currentExp}/{ExpToLevel} (+{potentialExp}) → 可升 {levelsToGain} 级";
        }

        public static int GetExpToNextLevel(int currentExp)
        {
            return ExpToLevel - currentExp;
        }
    }
}
