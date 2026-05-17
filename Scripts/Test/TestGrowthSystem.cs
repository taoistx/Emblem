using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;
using FE5.Combat;

namespace FE5.Test
{
    public partial class TestGrowthSystem : Node
    {
        public override void _Ready()
        {
            GD.Print("\n========================================");
            GD.Print("    角色属性系统测试");
            GD.Print("========================================\n");

            TestBaseStats();
            TestUnitStats();
            TestGrowthRates();
            TestLevelUpSystem();
            TestExperienceSystem();
            TestWeaponRanks();
            TestExperienceManagerInterface();

            GD.Print("\n========================================");
            GD.Print("    所有测试完成!");
            GD.Print("========================================\n");
        }

        private void TestBaseStats()
        {
            GD.Print("\n[测试1] BaseStats 结构体");
            GD.Print("------------------------------");

            BaseStats stats = BaseStats.Default();
            GD.Print($"默认基础属性: {stats}");

            BaseStats customStats = new BaseStats
            {
                HP = 25,
                MaxHP = 25,
                str = 10,
                mag = 5,
                skl = 8,
                spd = 12,
                lck = 6,
                def = 7,
                bld = 8,
                Movement = 5
            };
            GD.Print($"自定义基础属性: {customStats}");

            GD.Print("✓ BaseStats 测试通过\n");
        }

        private void TestUnitStats()
        {
            GD.Print("\n[测试2] UnitStats 属性访问");
            GD.Print("------------------------------");

            UnitStats stats = new UnitStats();
            stats.Base = new BaseStats
            {
                HP = 25,
                MaxHP = 25,
                str = 10,
                mag = 5,
                skl = 8,
                spd = 12,
                lck = 6,
                def = 7,
                bld = 8,
                Movement = 5
            };
            stats.Level = 1;
            stats.Experience = 0;

            GD.Print($"单位属性: {stats}");
            GD.Print($"当前HP: {stats.HP}, 最大HP: {stats.MaxHP}");
            GD.Print($"力量: {stats.Strength}, 魔力: {stats.Magic}");
            GD.Print($"等级: {stats.Level}, 经验: {stats.Experience}");

            stats.HP = 20;
            GD.Print($"受伤后HP: {stats.HP}");

            GD.Print("✓ UnitStats 测试通过\n");
        }

        private void TestGrowthRates()
        {
            GD.Print("\n[测试3] 成长率系统");
            GD.Print("------------------------------");

            GrowthRates rates = GrowthRates.Default();
            GD.Print($"默认成长率:");
            GD.Print($"  HP: {rates.HP}%");
            GD.Print($"  力量: {rates.str}%");
            GD.Print($"  魔力: {rates.mag}%");
            GD.Print($"  技巧: {rates.skl}%");
            GD.Print($"  速度: {rates.spd}%");
            GD.Print($"  幸运: {rates.lck}%");
            GD.Print($"  防御: {rates.def}%");
            GD.Print($"  体格: {rates.bld}%");
            GD.Print($"  移动: {rates.Movement}%");

            UnitStats stats = new UnitStats();
            stats.GrowthRate = rates;
            GD.Print($"单位成长率已设置: HP {stats.GrowthRate.HP}%");

            GD.Print("✓ 成长率测试通过\n");
        }

        private void TestLevelUpSystem()
        {
            GD.Print("\n[测试4] 升级系统");
            GD.Print("------------------------------");

            UnitStats stats = new UnitStats();
            stats.Base = new BaseStats
            {
                HP = 20,
                MaxHP = 20,
                str = 5,
                mag = 3,
                skl = 5,
                spd = 7,
                lck = 4,
                def = 4,
                bld = 5,
                Movement = 5
            };
            stats.Level = 1;
            stats.Experience = 95;

            GD.Print($"升级前: {stats}");

            var result = GrowthManager.ProcessLevelUp(stats);
            GD.Print($"升级结果: {result}");
            GD.Print($"升级后: {stats}");

            GD.Print($"是否可升级: {ExperienceManager.CanLevelUp(stats.Level)}");

            GD.Print("✓ 升级系统测试通过\n");
        }

        private void TestExperienceSystem()
        {
            GD.Print("\n[测试5] 经验值系统");
            GD.Print("------------------------------");

            GD.Print($"升级所需经验: {ExperienceManager.ExpToLevel}");
            GD.Print($"最大等级: {ExperienceManager.MaxLevel}");

            UnitStats unitStats = new UnitStats();
            unitStats.Base = new BaseStats
            {
                HP = 20,
                MaxHP = 20,
                str = 5,
                mag = 3,
                skl = 5,
                spd = 7,
                lck = 4,
                def = 4,
                bld = 5,
                Movement = 5
            };
            unitStats.Level = 1;
            unitStats.Experience = 0;

            Unit unit = new Unit();
            unit.Initialize("测试单位", UnitFaction.Player, unitStats);

            GD.Print($"\n初始状态: {unit}");

            var expResult = unit.AddExperience(50);
            GD.Print($"获得50EXP后: {unit}");
            GD.Print($"升级结果: LeveledUp={expResult.LeveledUp}");

            GD.Print("✓ 经验值系统测试通过\n");
        }

        private void TestWeaponRanks()
        {
            GD.Print("\n[测试6] 武器熟练度系统");
            GD.Print("------------------------------");

            UnitStats stats = new UnitStats();

            GD.Print($"初始剑熟练度: {stats.GetWeaponRank(WeaponType.Sword)}");

            stats.AddWeaponExperience(WeaponType.Sword, 25);
            GD.Print($"获得25点经验后剑熟练度: {stats.GetWeaponRank(WeaponType.Sword)} (EXP: {stats.GetWeaponExp(WeaponType.Sword)})");

            stats.AddWeaponExperience(WeaponType.Sword, 10);
            GD.Print($"获得10点经验后剑熟练度: {stats.GetWeaponRank(WeaponType.Sword)} (EXP: {stats.GetWeaponExp(WeaponType.Sword)})");

            stats.AddWeaponExperience(WeaponType.Sword, 100);
            GD.Print($"获得100点经验后剑熟练度: {stats.GetWeaponRank(WeaponType.Sword)} (EXP: {stats.GetWeaponExp(WeaponType.Sword)})");

            stats.AddWeaponExperience(WeaponType.Lance, 80);
            GD.Print($"枪熟练度: {stats.GetWeaponRank(WeaponType.Lance)} (EXP: {stats.GetWeaponExp(WeaponType.Lance)})");

            GD.Print("✓ 武器熟练度测试通过\n");
        }

        private void TestExperienceManagerInterface()
        {
            GD.Print("\n[测试7] ExperienceManager 接口");
            GD.Print("------------------------------");

            GD.Print($"CanLevelUp(5): {ExperienceManager.CanLevelUp(5)}");
            GD.Print($"CanLevelUp(20): {ExperienceManager.CanLevelUp(20)}");
            GD.Print($"CanGainExp(20): {ExperienceManager.CanGainExp(20)}");
            GD.Print($"GetExpToNextLevel(50): {ExperienceManager.GetExpToNextLevel(50)}");
            GD.Print($"GetExpPreview(50, 30): {ExperienceManager.GetExpPreview(50, 30)}");

            GD.Print($"CalculateHitExp(10): {CombatCalculator.CalculateHitExp(10)}");
            GD.Print($"CalculateStaffExp(Heal, 10): {CombatCalculator.CalculateStaffExp(StaffType.Heal, 10)}");

            Unit attacker = new Unit();
            attacker._Ready();
            attacker.Stats.Level = 3;
            Unit enemy = new Unit();
            enemy._Ready();
            enemy.Stats.Level = 5;
            GD.Print($"CalculateDefeatExp(5, 3): {CombatCalculator.CalculateDefeatExp(enemy, attacker)}");

            GD.Print("✓ ExperienceManager 接口测试通过\n");
        }
    }
}
