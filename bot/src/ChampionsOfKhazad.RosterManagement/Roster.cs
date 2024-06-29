namespace ChampionsOfKhazad.RosterManagement;

public class Roster
{
    public IReadOnlyCollection<Character> Raiders => _raiders;
    public IReadOnlyCollection<Character> SocialRaiders => _socialRaiders;

    private readonly List<Character> _raiders = [];
    private readonly List<Character> _socialRaiders = [];

    public void AddRaider(Character character)
    {
        _raiders.Add(character);
    }

    public void AddSocialRaider(Character character)
    {
        _socialRaiders.Add(character);
    }
}
