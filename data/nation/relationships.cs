class Relationships
{
    public static readonly string[] relationshipsWithHighness = new[]
    {
        "loyal to the throne",
        "barely tolerated by the Highness",
        "secretly plotting against the crown",
        "in open dispute with royal policy",
        "publicly devoted but privately resentful",
        "bound to the Highness by old oaths",
        "fearful of displeasing the royal court",
        "manipulated into obedience",
        "treated as a favored ally",
        "trusted more than they probably should be",
    };

    public static readonly string[] factionRelationships = new[]
    {
        "warring bitterly",
        "locked in uneasy negotiations",
        "competing for influence",
        "secretly cooperating",
        "forced into an awkward alliance",
        "sabotaging each other from the shadows",
        "pretending to be allies while plotting betrayal",
        "locked in a stalemate",
        "fighting proxy battles through local militias",
        "recently reconciled but still tense",
    };

    public static string PickRandomFromArray(string[] array)
    {
        Random rand = new Random();
        return array[rand.Next(array.Length)];
    }
}
