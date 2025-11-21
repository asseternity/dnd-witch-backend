public enum NationNameParts
{
    prefix,
    suffix,
    noun,
    of,
    generated_proper_noun,
}

public enum NationNamePatterns
{
    // Single-word patterns
    Prefix, // "The Holy"
    Suffix, // "Of The Great Beyond"
    Noun, // "Kingdom"
    GeneratedProperNoun, // "Arcadia"

    // Two-word patterns
    Prefix_Noun, // "The Holy Kingdom"
    Prefix_GeneratedProperNoun, // "The Holy Arcadia"
    Noun_Suffix, // "Kingdom Of The Great Beyond"
    GeneratedProperNoun_Suffix, // "Arcadia Of The Great Beyond"

    // Three-word patterns
    Noun_Of_GeneratedProperNoun, // "Kingdom of Arcadia"
    Prefix_Noun_Suffix, // "The Holy Kingdom Of The Great Beyond"
    Prefix_GeneratedProperNoun_Suffix, // "The Holy Arcadia Of The Great Beyond"

    // Four-word patterns
    Prefix_Noun_Of_GeneratedProperNoun, // "The Holy Kingdom of Arcadia"
    Noun_Of_GeneratedProperNoun_Suffix, // "Kingdom of Arcadia Of The Great Beyond"

    // Five-word patterns
    Prefix_Noun_Of_GeneratedProperNoun_Suffix, // "The Holy Kingdom of Arcadia Of The Great Beyond"
}
