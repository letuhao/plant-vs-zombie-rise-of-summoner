namespace FusionRpg.Core.Stats;

public enum ModifierOp
{
    Flat = 0,
    Increased = 1,
    More = 2,
    Override = 3
}

public static class StatChannels
{
    public const string Hp = "hp";
    public const string MaxHp = "maxHp";
    public const string Atk = "atk";
    public const string Defense = "defense";
    public const string Arm1 = "arm1";
    public const string Arm1Max = "arm1Max";
    public const string Arm2 = "arm2";
    public const string Arm2Max = "arm2Max";
}

public enum StatSide
{
    Plant = 0,
    Zombie = 1
}
