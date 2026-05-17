using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;
using FE5.Map;

namespace FE5.Combat
{
    public partial class BattleManager : Node
    {
        [Signal] public delegate void BattleStartedEventHandler(Unit attacker, Unit defender);

        private MapManager _mapManager = null!;

        public override void _Ready()
        {
            _mapManager = GetNode<MapManager>("/root/Main/MapManager");
        }

        public BattleResult ExecuteBattle(Unit attacker, Unit defender)
        {
            EmitSignal(SignalName.BattleStarted, attacker, defender);

            GD.Print($"\n>>> 战斗开始: {attacker.UnitName} vs {defender.UnitName}");

            TerrainData terrain = _mapManager.GetTerrain(defender.GridPosition);
            GD.Print($"地形: {terrain.Type}, 防守+{terrain.DefenseBonus}, 回避+{terrain.AvoidBonus}");

            string followUpInfo = CombatCalculator.GetFollowUpInfo(attacker, defender);
            GD.Print(followUpInfo);

            BattleResult result = new BattleResult
            {
                Attacker = attacker,
                Defender = defender
            };

            int attackerAttackCount = CombatCalculator.CalculateAttackCount(attacker, defender);
            int defenderAttackCount = CombatCalculator.CalculateAttackCount(defender, attacker);

            for (int i = 0; i < attackerAttackCount && defender.Stats.HP > 0; i++)
            {
                var attackResult = PerformAttack(attacker, defender, terrain, i > 0);
                result.AttackerAttacks.Add(attackResult);
                result.TotalAttackerDamage += attackResult.IsHit ? attackResult.Damage : 0;

                if (defender.Stats.HP <= 0)
                    break;
            }

            if (defender.Stats.HP > 0 && attacker.Stats.HP > 0)
            {
                for (int i = 0; i < defenderAttackCount && attacker.Stats.HP > 0; i++)
                {
                    var counterResult = PerformAttack(defender, attacker, terrain, i > 0);
                    result.DefenderAttacks.Add(counterResult);
                    result.TotalDefenderDamage += counterResult.IsHit ? counterResult.Damage : 0;

                    if (attacker.Stats.HP <= 0)
                        break;
                }
            }

            result.PrintResult();

            return result;
        }

        private AttackResult PerformAttack(Unit attacker, Unit defender, TerrainData terrain, bool isFollowUp)
        {
            int hitRate = CombatCalculator.CalculateHitRate(attacker, defender, terrain);
            int critRate = CombatCalculator.CalculateCritRate(attacker, defender);

            bool isHit = CombatCalculator.CheckHit(hitRate);

            var result = new AttackResult
            {
                IsHit = isHit,
                IsFollowUp = isFollowUp
            };

            if (isHit)
            {
                bool isCrit = CombatCalculator.CheckCrit(critRate);
                result.IsCrit = isCrit;

                int baseDamage = CombatCalculator.CalculateDamage(attacker, defender, terrain);
                result.Damage = isCrit ? baseDamage * 3 : baseDamage;

                defender.TakeDamage(result.Damage);
                result.RemainingHP = defender.Stats.HP;
            }
            else
            {
                result.Damage = 0;
                result.RemainingHP = defender.Stats.HP;
                GD.Print($"  {attacker.UnitName} 的攻击未命中! (命中率:{hitRate}%)");
            }

            return result;
        }

        public void PreviewBattle(Unit attacker, Unit defender)
        {
            TerrainData terrain = _mapManager.GetTerrain(defender.GridPosition);

            int hitRate = CombatCalculator.CalculateHitRate(attacker, defender, terrain);
            int critRate = CombatCalculator.CalculateCritRate(attacker, defender);
            int damage = CombatCalculator.CalculateDamage(attacker, defender, terrain);
            int attackCount = CombatCalculator.CalculateAttackCount(attacker, defender);

            GD.Print($"\n--- 战斗预览 ---");
            GD.Print($"{attacker.UnitName} -> {defender.UnitName}");
            GD.Print($"命中率: {hitRate}%, 必杀率: {critRate}%, 伤害: {damage}");
            GD.Print($"攻击次数: {attackCount} ({(attackCount > 1 ? "可追击" : "无追击")})");

            int counterHitRate = CombatCalculator.CalculateHitRate(defender, attacker, terrain);
            int counterDamage = CombatCalculator.CalculateDamage(defender, attacker, terrain);
            GD.Print($"反击 - 命中率: {counterHitRate}%, 伤害: {counterDamage}");
        }
    }
}