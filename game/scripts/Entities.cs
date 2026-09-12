using Godot;

namespace PatientZero
{
    public class Player
    {
        public Vector2 Pos = new(0, 6);
        public Vector2 Aim = new(0, -1);
        public float Hp = Config.PlayerMaxHp;
        public float FireCd, MeleeCd, PurgeCd, Invuln, HitFlash;
        public Vector2 Vel;
    }

    public class Enemy
    {
        public int Id;
        public EnemyType Type;
        public Vector2 Pos;
        public Vector2 Vel;
        public float Hp, MaxHp, Speed, Damage, Radius;
        public float AttackCd, Flash, SpawnT = 0.55f;
        public bool Dead;

        public static Enemy Create(EnemyType t, Vector2 pos)
        {
            var cfg = Config.Enemies[t];
            return new Enemy
            {
                Type = t,
                Pos = pos,
                Hp = cfg.hp,
                MaxHp = cfg.hp,
                Speed = cfg.speed,
                Damage = cfg.dmg,
                Radius = cfg.radius,
            };
        }
    }

    public class Projectile
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life = Config.BulletLife;
        public bool Dead;
    }

    public class Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life, MaxLife;
        public Color Color;
        public float Size;

        public static Particle Burst(Vector2 pos, Color c, float speed, float size)
        {
            float a = (float)GD.Randf() * Mathf.Tau;
            float sp = speed * (0.4f + (float)GD.Randf());
            float life = 0.4f + (float)GD.Randf() * 0.25f;
            return new Particle
            {
                Pos = pos,
                Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp,
                Life = life,
                MaxLife = life,
                Color = c,
                Size = size * (0.7f + (float)GD.Randf() * 0.6f),
            };
        }
    }
}
