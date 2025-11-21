class Relationships
{
    public static readonly string[] relationshipsWithHighness = new[]
    {
        "loyal to the throne of the Highness",
        "barely tolerated by the Highness",
        "secretly plotting against the Highness",
        "in open dispute with the Highness' policy",
        "publicly devoted to the Highness but privately resentful",
        "bound to the Highness by old oaths",
        "fearful of displeasing the Highness",
        "manipulated into obedience by the Highness",
        "treated as a favored ally of the Highness",
        "trusted more by the Highness than they probably should be",
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
