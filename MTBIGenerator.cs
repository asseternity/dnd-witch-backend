public static class MBTIGenerator
{
    public static string GetRandomMBTI()
    {
        var types = new (string, string)[]
        {
            ("INTJ", "Architect"),
            ("INTP", "Logician"),
            ("ENTJ", "Commander"),
            ("ENTP", "Debater"),
            ("INFJ", "Advocate"),
            ("INFP", "Mediator"),
            ("ENFJ", "Protagonist"),
            ("ENFP", "Campaigner"),
            ("ISTJ", "Logistician"),
            ("ISFJ", "Defender"),
            ("ESTJ", "Executive"),
            ("ESFJ", "Consul"),
            ("ISTP", "Virtuoso"),
            ("ISFP", "Adventurer"),
            ("ESTP", "Entrepreneur"),
            ("ESFP", "Entertainer")
        };

        var rng = new Random();
        var pick = types[rng.Next(types.Length)];
        return $"{pick.Item1} ({pick.Item2})";
    }
}
