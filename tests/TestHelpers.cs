using GdUnit4;
using static GdUnit4.Assertions;

public static class TestHelpers
{
    public static Character CreateTestCharacter() => new Character
    {
        Name             = "TestHero",
        Level            = 1,
        MaxHealth        = 100,
        CurrentHealth    = 100,
        Attack           = 20,
        Defense          = 10,
        Speed            = 15,
        Experience       = 0,
        ExperienceToNext = 100,
        Gold             = 0,
    };
}
