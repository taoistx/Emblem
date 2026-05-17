using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;

namespace FE5.Combat
{
    public class AttackResult
    {
        public bool IsHit { get; set; }
        public bool IsCrit { get; set; }
        public int Damage { get; set; }
        public int RemainingHP { get; set; }
        public bool IsFollowUp { get; set; }
        public string? Description { get; set; }

        public override string ToString()
        {
            string attackType = IsFollowUp ? "追击" : "攻击";
            if (!IsHit)
                return $"[{attackType}] 未命中";

            string critStr = IsCrit ? " 必杀!" : "";
            return $"[{attackType}] 命中, 造成 {Damage} 点伤害{critStr}, 敌方剩余 HP: {RemainingHP}";
        }
    }

    public class BattleResult
    {
        public Unit Attacker { get; set; } = null!;
        public Unit Defender { get; set; } = null!;
        public List<AttackResult> AttackerAttacks { get; set; } = new List<AttackResult>();
        public List<AttackResult> DefenderAttacks { get; set; } = new List<AttackResult>();
        public bool IsAttackerDead => Attacker.Stats.HP <= 0;
        public bool IsDefenderDead => Defender.Stats.HP <= 0;
        public int TotalAttackerDamage { get; set; }
        public int TotalDefenderDamage { get; set; }

        public void PrintResult()
        {
            GD.Print("\n========== 战斗结果 ==========");
            GD.Print($"攻击方: {Attacker.UnitName} (HP: {Attacker.Stats.HP}/{Attacker.Stats.MaxHP})");
            GD.Print($"防御方: {Defender.UnitName} (HP: {Defender.Stats.HP}/{Defender.Stats.MaxHP})");
            GD.Print("------------------------------");

            GD.Print("攻击方回合:");
            foreach (var attack in AttackerAttacks)
            {
                GD.Print($"  {attack}");
            }

            if (DefenderAttacks.Count > 0)
            {
                GD.Print("防御方反击:");
                foreach (var attack in DefenderAttacks)
                {
                    GD.Print($"  {attack}");
                }
            }

            GD.Print("------------------------------");
            GD.Print($"总伤害 - 攻击方:{TotalAttackerDamage}, 防御方:{TotalDefenderDamage}");

            if (IsDefenderDead)
                GD.Print($"结果: {Defender.UnitName} 被击败!");
            else if (IsAttackerDead)
                GD.Print($"结果: {Attacker.UnitName} 被反杀!");
            else
                GD.Print($"结果: 双方存活");

            GD.Print("==============================\n");
        }
    }
}