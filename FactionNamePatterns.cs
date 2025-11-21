public enum FactionNameParts
{
    prefix,
    suffix,
    first_word,
    second_word,
    generated_proper_noun,
}

public enum FactionNamePatterns
{
    // Single-part names
    Prefix,
    FirstWord,
    SecondWord,
    Suffix,
    GeneratedProperNoun,

    // Two-part combinations
    Prefix_FirstWord,
    Prefix_SecondWord,
    Prefix_Suffix,
    Prefix_GeneratedProperNoun,
    FirstWord_SecondWord,
    FirstWord_Suffix,
    FirstWord_GeneratedProperNoun,
    SecondWord_Suffix,
    SecondWord_GeneratedProperNoun,
    GeneratedProperNoun_Suffix,

    // Three-part combinations
    Prefix_FirstWord_SecondWord,
    Prefix_FirstWord_Suffix,
    Prefix_FirstWord_GeneratedProperNoun,
    Prefix_SecondWord_Suffix,
    Prefix_SecondWord_GeneratedProperNoun,
    Prefix_GeneratedProperNoun_Suffix,
    FirstWord_SecondWord_Suffix,
    FirstWord_SecondWord_GeneratedProperNoun,
    FirstWord_GeneratedProperNoun_Suffix,
    SecondWord_GeneratedProperNoun_Suffix,

    // Four-part combinations
    Prefix_FirstWord_SecondWord_Suffix,
    Prefix_FirstWord_SecondWord_GeneratedProperNoun,
    Prefix_FirstWord_GeneratedProperNoun_Suffix,
    Prefix_SecondWord_GeneratedProperNoun_Suffix,
    FirstWord_SecondWord_GeneratedProperNoun_Suffix,

    // All five parts
    Prefix_FirstWord_SecondWord_GeneratedProperNoun_Suffix,
}
