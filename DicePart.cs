public class DicePart
{
    public int Count { get; set; } // Number of dice
    public int Sides { get; set; } // Number of sides per die
    public int Modifier { get; set; } // Flat modifier
    public bool IsDice => Sides > 0; // True if this part is dice
}
