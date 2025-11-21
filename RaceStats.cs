using System.Collections.Generic;

public static class RaceStats
{
    public static readonly Dictionary<
        string,
        (int minHeight, int maxHeight, int minWeight, int maxWeight)
    > Stats = new Dictionary<string, (int, int, int, int)>
    {
        { "Dragonborn", (190, 210, 90, 120) },
        { "Fairy", (30, 60, 2, 10) },
        { "Pixie", (30, 60, 2, 10) },
        { "Triton", (150, 190, 50, 90) },
        { "Dwarf", (120, 150, 45, 80) },
        { "Elf", (150, 190, 45, 75) },
        { "Gnome", (90, 120, 20, 45) },
        { "Half-Elf", (150, 190, 45, 85) },
        { "Half-Orc", (170, 200, 70, 110) },
        { "Halfling", (90, 120, 20, 40) },
        { "Human", (150, 200, 50, 100) },
        { "Tiefling", (150, 190, 45, 85) },
        { "Aarakocra", (140, 170, 30, 60) },
        { "Aasimar", (160, 200, 50, 90) },
        { "Firbolg", (240, 300, 120, 200) },
        { "Genasi", (150, 200, 45, 90) },
        { "Gith", (160, 190, 50, 80) },
        { "Goliath", (210, 240, 90, 160) },
        { "Qudanti", (240, 300, 120, 200) },
        { "Kenku", (140, 160, 30, 50) },
        { "Satyr", (160, 180, 50, 80) },
        { "Tabaxi", (150, 180, 45, 70) },
        { "Tortle", (180, 200, 90, 150) },
        { "Bugbear", (200, 240, 80, 150) },
        { "Centaur", (240, 300, 250, 450) },
        { "Goblin", (90, 120, 20, 40) },
        { "Grung", (60, 90, 5, 20) },
        { "Hobgoblin", (160, 190, 50, 90) },
        { "Kobold", (70, 100, 10, 20) },
        { "Lizardfolk", (170, 200, 70, 100) },
        { "Minotaur", (210, 250, 120, 180) },
        { "Orc", (160, 200, 70, 110) },
        { "Yuan-Ti", (150, 180, 45, 90) },
        { "Warforged", (180, 210, 80, 150) },
    };
}
