using Godot;
using System;

namespace FE5.Units
{
    public partial class UnitStats : RefCounted
    {
        public int HP { get; set; }
        public int MaxHP { get; set; }
        public int Strength { get; set; }
        public int Magic { get; set; }
        public int Skill { get; set; }
        public int Speed { get; set; }
        public int Luck { get; set; }
        public int Defense { get; set; }
        public int Build { get; set; }
        public int Movement { get; set; } = 5;

        public int WeaponWeight { get; set; }
        public int WeaponMight { get; set; }
        public int WeaponHit { get; set; }
        public int WeaponCrit { get; set; }
        public bool IsMagicWeapon { get; set; }

        public int CalculateAttackSpeed()
        {
            if (WeaponWeight <= Build)
                return Speed;
            return Mathf.Max(0, Speed - (WeaponWeight - Build));
        }

        public int CalculateHitRate()
        {
            return (Skill * 2) + WeaponHit;
        }

        public int CalculateAvoidRate(int terrainAvoidBonus = 0)
        {
            int asValue = CalculateAttackSpeed();
            return (asValue * 2) + Luck + terrainAvoidBonus;
        }

        public int CalculateCritRate()
        {
            return Skill + WeaponCrit;
        }

        public int CalculateAttackPower(bool useMagic = false)
        {
            if (useMagic || IsMagicWeapon)
            {
                return Magic + WeaponMight;
            }
            return Strength + WeaponMight;
        }

        public int CalculateDefense(bool againstMagic = false)
        {
            return againstMagic ? Magic : Defense;
        }

        public bool CanFollowUpAttack(UnitStats target)
        {
            int myAS = CalculateAttackSpeed();
            int targetAS = target.CalculateAttackSpeed();
            return (myAS - targetAS) >= 4;
        }

        public int GetAttackCount(UnitStats target)
        {
            return CanFollowUpAttack(target) ? 2 : 1;
        }

        public UnitStats Clone()
        {
            return new UnitStats
            {
                HP = this.HP,
                MaxHP = this.MaxHP,
                Strength = this.Strength,
                Magic = this.Magic,
                Skill = this.Skill,
                Speed = this.Speed,
                Luck = this.Luck,
                Defense = this.Defense,
                Build = this.Build,
                Movement = this.Movement,
                WeaponWeight = this.WeaponWeight,
                WeaponMight = this.WeaponMight,
                WeaponHit = this.WeaponHit,
                WeaponCrit = this.WeaponCrit,
                IsMagicWeapon = this.IsMagicWeapon
            };
        }

        public override string ToString()
        {
            return $"HP:{HP}/{MaxHP} 力:{Strength} 魔:{Magic} 技:{Skill} " +
                   $"速:{Speed} 运:{Luck} 守:{Defense} " +
                   $"体格:{Build} 移:{Movement} AS:{CalculateAttackSpeed()}";
        }
    }
}