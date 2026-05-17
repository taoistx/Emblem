namespace FE5.Units
{
    public class BaseStats
    {
        public int HP;
        public int MaxHP;
        public int str;
        public int mag;
        public int skl;
        public int spd;
        public int lck;
        public int def;
        public int bld;
        public int Movement;

        public static BaseStats Default()
        {
            return new BaseStats
            {
                HP = 0,
                MaxHP = 0,
                str = 0,
                mag = 0,
                skl = 0,
                spd = 0,
                lck = 0,
                def = 0,
                bld = 0,
                Movement = 5
            };
        }

        public override string ToString()
        {
            return $"HP:{MaxHP} 力:{str} 魔:{mag} 技:{skl} " +
                   $"速:{spd} 运:{lck} 守:{def} 体格:{bld} 移:{Movement}";
        }

        public BaseStats Clone()
        {
            return new BaseStats
            {
                HP = this.HP,
                MaxHP = this.MaxHP,
                str = this.str,
                mag = this.mag,
                skl = this.skl,
                spd = this.spd,
                lck = this.lck,
                def = this.def,
                bld = this.bld,
                Movement = this.Movement
            };
        }
    }
}
