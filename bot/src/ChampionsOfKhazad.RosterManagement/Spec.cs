namespace ChampionsOfKhazad.RosterManagement;

public class Spec
{
    public Role Role { get; }

    private Spec(Role role)
    {
        Role = role;
    }

    public static Spec DeathKnightBlood => new(Role.Tank);
    public static Spec DeathKnightFrost => new(Role.Melee);
    public static Spec DeathKnightUnholy => new(Role.Melee);
    public static Spec DruidGuardian => new(Role.Tank);
    public static Spec DruidFeral => new(Role.Melee);
    public static Spec DruidBalance => new(Role.Ranged);
    public static Spec DruidRestoration => new(Role.Healer);
    public static Spec HunterBeastMastery => new(Role.Ranged);
    public static Spec HunterMarksmanship => new(Role.Ranged);
    public static Spec HunterSurvival => new(Role.Melee);
    public static Spec MageArcane => new(Role.Ranged);
    public static Spec MageFire => new(Role.Ranged);
    public static Spec MageFrost => new(Role.Ranged);
    public static Spec WarriorProtection => new(Role.Tank);
    public static Spec WarriorArms => new(Role.Melee);
    public static Spec WarriorFury => new(Role.Melee);
    public static Spec PaladinProtection => new(Role.Tank);
    public static Spec PaladinRetribution => new(Role.Melee);
    public static Spec PaladinHoly => new(Role.Healer);
    public static Spec PriestShadow => new(Role.Ranged);
    public static Spec PriestDiscipline => new(Role.Healer);
    public static Spec PriestHoly => new(Role.Healer);
    public static Spec RogueAssassination => new(Role.Melee);
    public static Spec RogueCombat => new(Role.Melee);
    public static Spec RogueSubtlety => new(Role.Melee);
    public static Spec ShamanEnhancement => new(Role.Melee);
    public static Spec ShamanElemental => new(Role.Ranged);
    public static Spec ShamanRestoration => new(Role.Healer);
    public static Spec WarlockAffliction => new(Role.Ranged);
    public static Spec WarlockDemonology => new(Role.Ranged);
    public static Spec WarlockDestruction => new(Role.Ranged);
}
