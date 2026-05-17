using Godot;
using System;
using FE5.Units;
using FE5.Map;

namespace FE5.Combat
{
    public static class CombatCalculator
    {
        private static Random _random = new Random();

        public static int CalculateHitRate(Unit attacker, Unit defender, TerrainData terrain)
        {
            int baseHit = attacker.Stats.CalculateHitRate();
            int avoid = defender.Stats.CalculateAvoidRate(terrain.AvoidBonus);
            int hitRate = baseHit - avoid;

            return Mathf.Clamp(hitRate, 0, 100);
        }

        public static int CalculateCritRate(Unit attacker, Unit defender)
        {
            int critRate = attacker.Stats.CalculateCritRate();
            int critAvoid = defender.Stats.Luck;

            return Mathf.Clamp(critRate - critAvoid, 0, 100);
        }

        public static int CalculateDamage(Unit attacker, Unit defender, TerrainData terrain, bool isMagic = false)
        {
            int attackPower = attacker.Stats.CalculateAttackPower(isMagic);
            int defense = defender.Stats.CalculateDefense(isMagic) + terrain.DefenseBonus;
            int damage = attackPower - defense;

            return Mathf.Max(1, damage);
        }

        public static bool CheckHit(int hitRate)
        {
            int roll = _random.Next(1, 101);
            return roll <= hitRate;
        }

        public static bool CheckCrit(int critRate)
        {
            int roll = _random.Next(1, 101);
            return roll <= critRate;
        }

        public static int CalculateAttackCount(Unit attacker, Unit defender)
        {
            return attacker.Stats.GetAttackCount(defender.Stats);
        }

        public static string GetFollowUpInfo(Unit attacker, Unit defender)
        {
            int attackerAS = attacker.Stats.CalculateAttackSpeed();
            int defenderAS = defender.Stats.CalculateAttackSpeed();
            int asDiff = attackerAS - defenderAS;

            if (asDiff >= 4)
            {
                return $"追击! ({attacker.UnitName} AS:{attackerAS} vs {defender.UnitName} AS:{defenderAS}, 差值:{asDiff})";
            }
            else if (asDiff <= -4)
            {
                return $"被追击风险 ({defender.UnitName} 可能追击)";
            }
            else
            {
                return $"无追击 (AS差:{asDiff})";
            }
        }
    }
}