using Godot;
using System.Collections.Generic;

namespace FE5.Units
{
    public partial class LevelUpResult : RefCounted
    {
        public bool LeveledUp { get; set; }
        public int NewLevel { get; set; }
        public int PreviousLevel { get; set; }
        public List<string> StatIncreases { get; set; } = new List<string>();
        public int ExcessExp { get; set; }
        public BaseStats? PreviousStats { get; set; }
        public BaseStats? NewStats { get; set; }

        public override string ToString()
        {
            if (!LeveledUp)
            {
                return $"未升级 (EXP: +0)";
            }

            string statInfo = StatIncreases.Count > 0
                ? string.Join(", ", StatIncreases)
                : "无属性提升";

            return $"升级! Lv.{PreviousLevel} → Lv.{NewLevel} | {statInfo}";
        }
    }
}
