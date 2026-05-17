using Godot;
using System;
using System.Collections.Generic;

namespace FE5.Units
{
    public enum WeaponRank
    {
        None = 0,
        E = 30,
        D = 70,
        C = 120,
        B = 180,
        A = 250,
        S = 350
    }

    public enum WeaponType
    {
        Sword,
        Lance,
        Axe,
        Bow,
        Staff,
        FireMagic,
        ThunderMagic,
        WindMagic,
        DarkMagic
    }

    public struct GrowthRates
    {
        public int HP;
        public int str;
        public int mag;
        public int skl;
        public int spd;
        public int lck;
        public int def;
        public int bld;
        public int Movement;

        public static GrowthRates Default()
        {
            return new GrowthRates
            {
                HP = 90,
                str = 50,
                mag = 30,
                skl = 40,
                spd = 50,
                lck = 40,
                def = 30,
                bld = 10,
                Movement = 0
            };
        }
    }

    public partial class UnitStats : RefCounted
    {
        public BaseStats Base { get; set; } = BaseStats.Default();

        public int HP
        {
            get => Base.HP;
            set => Base.HP = Mathf.Min(value, MaxHP);
        }
        public int MaxHP
        {
            get => Base.MaxHP;
            set => Base.MaxHP = value;
        }
        public int Strength
        {
            get => Base.str;
            set => Base.str = value;
        }
        public int Magic
        {
            get => Base.mag;
            set => Base.mag = value;
        }
        public int Skill
        {
            get => Base.skl;
            set => Base.skl = value;
        }
        public int Speed
        {
            get => Base.spd;
            set => Base.spd = value;
        }
        public int Luck
        {
            get => Base.lck;
            set => Base.lck = value;
        }
        public int Defense
        {
            get => Base.def;
            set => Base.def = value;
        }
        public int Build
        {
            get => Base.bld;
            set => Base.bld = value;
        }
        public int Movement
        {
            get => Base.Movement;
            set => Base.Movement = value;
        }

        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public GrowthRates GrowthRate { get; set; } = GrowthRates.Default();

        private Dictionary<WeaponType, int> _weaponExperience = new Dictionary<WeaponType, int>();
        private Dictionary<WeaponType, WeaponRank> _weaponRanks = new Dictionary<WeaponType, WeaponRank>();

        public int WeaponWeight { get; set; }
        public int WeaponMight { get; set; }
        public int WeaponHit { get; set; }
        public int WeaponCrit { get; set; }
        public bool IsMagicWeapon { get; set; }

        public UnitStats()
        {
            foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
            {
                _weaponExperience[type] = 0;
                _weaponRanks[type] = WeaponRank.None;
            }
        }

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

        public void AddWeaponExperience(WeaponType type, int exp)
        {
            if (!_weaponExperience.ContainsKey(type))
                _weaponExperience[type] = 0;

            _weaponExperience[type] += exp;
            CheckWeaponRankUp(type);
        }

        private void CheckWeaponRankUp(WeaponType type)
        {
            int currentExp = _weaponExperience[type];
            WeaponRank currentRank = _weaponRanks[type];

            WeaponRank[] ranks = (WeaponRank[])Enum.GetValues(typeof(WeaponRank));
            for (int i = ranks.Length - 1; i >= 0; i--)
            {
                if (currentExp >= (int)ranks[i] && ranks[i] > currentRank)
                {
                    _weaponRanks[type] = ranks[i];
                    break;
                }
            }
        }

        public WeaponRank GetWeaponRank(WeaponType type)
        {
            return _weaponRanks.ContainsKey(type) ? _weaponRanks[type] : WeaponRank.None;
        }

        public int GetWeaponExp(WeaponType type)
        {
            return _weaponExperience.ContainsKey(type) ? _weaponExperience[type] : 0;
        }

        public UnitStats Clone()
        {
            UnitStats clone = new UnitStats
            {
                Base = this.Base,
                Level = this.Level,
                Experience = this.Experience,
                GrowthRate = this.GrowthRate,
                WeaponWeight = this.WeaponWeight,
                WeaponMight = this.WeaponMight,
                WeaponHit = this.WeaponHit,
                WeaponCrit = this.WeaponCrit,
                IsMagicWeapon = this.IsMagicWeapon
            };

            foreach (var kvp in _weaponExperience)
            {
                clone._weaponExperience[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in _weaponRanks)
            {
                clone._weaponRanks[kvp.Key] = kvp.Value;
            }

            return clone;
        }

        public override string ToString()
        {
            return $"Lv.{Level} HP:{HP}/{MaxHP} 力:{Strength} 魔:{Magic} 技:{Skill} " +
                   $"速:{Speed} 运:{Luck} 守:{Defense} 体格:{Build} 移:{Movement} AS:{CalculateAttackSpeed()}";
        }
    }
}
