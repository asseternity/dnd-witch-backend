// this dnd bot needs to:
// 1) roll any dice code you send it, like /1d20 or /5d6, etc.
// 2) generate a character with a random name, class, ability score array, background
// (i'll use a string list with a thousand different random professions), alignment,
// starting feat, and a personal motto (piecemeal generated from three string lists with verbs, nouns,
// adjectives, etc - madlibs style)
// 3) when generating a character, it has a 15% chance to add a random + or - 1-3 bonus or penalty to each attribute rolled (min 3)

using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.SwaggerUI;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// import all the jsons
var backgroundData = JObject.Parse(File.ReadAllText("data/backgrounds.json"));
var classData = JObject.Parse(File.ReadAllText("data/classes.json"));
var featsData = JObject.Parse(File.ReadAllText("data/feats.json"));
var racesData = JObject.Parse(File.ReadAllText("data/races.json"));
var alignmentsData = JObject.Parse(File.ReadAllText("data/alignments.json"));
var godsData = JObject.Parse(File.ReadAllText("data/gods.json"));
var domainsData = JObject.Parse(File.ReadAllText("data/domains.json"));
var epithetsData = JObject.Parse(File.ReadAllText("data/epithets.json"));

var cityNamePrefixes = JObject.Parse(File.ReadAllText("data/city/cityNamePrefixes.json"));
var cityNameSuffixes = JObject.Parse(File.ReadAllText("data/city/cityNameSuffixes.json"));
var geographyCorrelations = JObject.Parse(File.ReadAllText("data/city/geographyCorrelations.json"));
var geographyTypes = JObject.Parse(File.ReadAllText("data/city/geographyTypes.json"));
var recentEvents = JObject.Parse(File.ReadAllText("data/city/recent_events.json"));
var rulerPopularities = JObject.Parse(File.ReadAllText("data/city/rulerPopularities.json"));
var rulerPersonalities = JObject.Parse(File.ReadAllText("data/city/personalities.json"));
var rulerTitles = JObject.Parse(File.ReadAllText("data/city/rulerTitles.json"));
var seats = JObject.Parse(File.ReadAllText("data/city/seats.json"));
var socialClasses = JObject.Parse(File.ReadAllText("data/city/socialClasses.json"));
var weather = JObject.Parse(File.ReadAllText("data/city/weather.json"));
var economies = JObject.Parse(File.ReadAllText("data/city/economy.json"));

var factionTypes = JObject.Parse(File.ReadAllText("data/faction/types.json"));

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<ITelegramBotClient>(
    new TelegramBotClient(Environment.GetEnvironmentVariable("TELEGRAM_TOKEN"))
);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

// ------ attributes array ------
List<int> ProduceAttributeArray()
{
    List<int> attributes = new List<int>();
    Random rnd = new Random();
    for (int i = 0; i < 6; i++)
    {
        List<int> rolls = new List<int>();
        for (int j = 0; j < 4; j++)
        {
            rolls.Add(rnd.Next(1, 7));
        }
        rolls = rolls.OrderBy(x => x).ToList();
        rolls.RemoveAt(0);
        int sum = rolls.Sum(x => x);
        attributes.Add(sum);
    }
    return attributes;
}

app.MapGet(
    "/attributes",
    () =>
    {
        return Results.Ok(String.Join(", ", ProduceAttributeArray()));
    }
);

// ------ dice rolling ------
List<DicePart> ParseDiceCode(string code)
{
    var parts = new List<DicePart>();
    var matches = Regex.Matches(code, @"([+-]?[^+-]+)");

    foreach (Match match in matches)
    {
        string part = match.Value.Trim();
        if (string.IsNullOrEmpty(part))
            continue;

        int sign = 1;
        if (part.StartsWith("+"))
            part = part.Substring(1);
        else if (part.StartsWith("-"))
        {
            sign = -1;
            part = part.Substring(1);
        }

        try
        {
            if (part.Contains("d") || part.Contains("D"))
            {
                string[] diceElements = part.Split('d', 'D');
                int count = int.Parse(diceElements[0]) * sign;
                int sides = int.Parse(diceElements[1]);
                parts.Add(new DicePart { Count = count, Sides = sides });
            }
            else
            {
                int modifier = int.Parse(part) * sign;
                parts.Add(new DicePart { Modifier = modifier });
            }
        }
        catch
        {
            // invalid part, ignore
        }
    }

    return parts;
}

string RollDice(List<DicePart> parts)
{
    Random rnd = new Random();
    var rolls = new List<RollResult>();
    int modifierTotal = 0;

    foreach (var part in parts)
    {
        if (part.IsDice)
        {
            for (int i = 0; i < Math.Abs(part.Count); i++)
            {
                int roll = rnd.Next(1, part.Sides + 1);
                RollResult rollResult = new RollResult();
                rollResult.result = roll;
                rollResult.dice = part.Sides;
                rolls.Add(rollResult);
            }
        }
        else
        {
            modifierTotal += part.Modifier;
        }
    }

    rolls.Sort((a, b) => b.result.CompareTo(a.result)); // descending
    int total = rolls.Sum(r => r.result) + modifierTotal;

    // Special case: one die, no modifiers
    if (rolls.Count == 1 && modifierTotal == 0)
    {
        return $"You rolled: {rolls[0].result}";
    }

    List<string> rollResults = new List<string>();
    rollResults = rolls.Select(r => $"{r.result}/{r.dice}").ToList();

    string rollsStr = string.Join(", ", rollResults);
    string modifierStr = (modifierTotal >= 0) ? $"+{modifierTotal}" : modifierTotal.ToString();

    return $"Total: {total} | Rolls: {rollsStr} | Modifier: {modifierStr}";
}

app.MapGet(
    "/r/{diceCode}",
    (string diceCode) =>
    {
        if (diceCode != "")
        {
            List<DicePart> parsed = ParseDiceCode(diceCode);
            if (parsed == null)
                return Results.BadRequest();
            string result = RollDice(parsed);
            if (result == null)
                return Results.BadRequest();
            return Results.Ok(result);
        }
        else
        {
            return Results.BadRequest();
        }
    }
);

// ------ character generator ------
// helper:
// within a main field (jsons have just one top tier field),
// pick a random upper field,
// if it does have an inner field, pick a random one
// return a string of both of these names
string PickRandomUpperAndInner(JObject data)
{
    var rand = new Random();

    // If JSON has a single wrapper like { "classes": { ... } } or { "races": { ... } }
    JObject root = data;
    if (root.Properties().Count() == 1 && root.Properties().First().Value is JObject singleObj)
    {
        root = singleObj;
    }

    // Pick random upper (e.g., "Monk", "Centaur")
    var upperList = root.Properties().Select(p => p.Name).ToList();
    if (upperList.Count == 0)
        return "Unknown";

    var upper = upperList[rand.Next(upperList.Count)];
    var upperNode = root[upper];

    // If upper node is an array -> subclasses/subraces stored as string array
    if (upperNode is JArray arr && arr.Count > 0)
    {
        var inner = arr[rand.Next(arr.Count)].ToString();
        return $"{upper} ({inner})";
    }

    // If upper node is an object -> pick a property name as inner
    if (upperNode is JObject innerObj && innerObj.Properties().Any())
    {
        var innerList = innerObj.Properties().Select(p => p.Name).ToList();
        var inner = innerList[rand.Next(innerList.Count)];
        return $"{upper} ({inner})";
    }

    // No inner / empty array -> just upper
    return upper;
}

string PickRandomFromArray(JObject data)
{
    var rand = new Random();
    var firstProp = data.Properties().First();
    var array = (JArray)firstProp.Value;
    return array[rand.Next(array.Count)].ToString();
}

// helper function to generate attribute scores (with the above bonus / penalty)
string ProduceFunAttributeArray()
{
    List<int> attributes = new List<int>();
    Random rnd = new Random();
    for (int i = 0; i < 6; i++)
    {
        List<int> rolls = new List<int>();
        for (int j = 0; j < 4; j++)
        {
            rolls.Add(rnd.Next(1, 7));
        }
        rolls = rolls.OrderBy(x => x).ToList();
        rolls.RemoveAt(0);
        int sum = rolls.Sum(x => x);
        int chance = rnd.Next(1, 101);
        if (chance < 25)
        {
            int funModifier = rnd.Next(1, 5);
            int chance2 = rnd.Next(1, 101);
            if (chance2 < 50)
            {
                sum = sum + funModifier;
            }
            else
            {
                sum = sum - funModifier;
                if (sum < 3)
                {
                    sum = 3;
                }
            }
        }
        attributes.Add(sum);
    }
    return String.Join(", ", attributes.ToArray());
}

// helper function: name generator. make a string out of random alternating consonants and vowels
string[] LoadSuffixes(string path)
{
    return File.ReadAllLines(path);
}

string GenerateFullName()
{
    var rand = new Random();

    // Consonants and vowels for random string generation
    string consonants = "bcdfghjklmnpqrstvwxyz";
    string vowels = "aeiou";
    var suffixes = LoadSuffixes("data/suffixes.txt");

    // Helper to generate alternating consonant-vowel strings
    string GenerateRandomString(int length)
    {
        var arr = new char[length];
        for (int i = 0; i < length; i++)
            arr[i] =
                (i % 2 == 0)
                    ? consonants[rand.Next(consonants.Length)]
                    : vowels[rand.Next(vowels.Length)];
        arr[0] = char.ToUpper(arr[0]);
        return new string(arr);
    }

    // First name: 2-8 letters
    int firstNameLength = rand.Next(2, 9);
    string firstName = GenerateRandomString(firstNameLength);

    // Surname: 3-5 letters + random suffix
    int surnameBaseLength = rand.Next(3, 6);
    string surnameBase = GenerateRandomString(surnameBaseLength);
    string surnameSuffix = suffixes[rand.Next(suffixes.Length)];
    string surname = surnameBase + surnameSuffix;

    return $"{firstName} {surname}";
}

// helper method to generate a piecemeal madlibs personal motto
string GenerateMotto()
{
    var rand = new Random();

    // Load JSON once (or cache it outside this method if calling repeatedly)
    string json = File.ReadAllText("data/motto_parts.json");
    var parts = JObject.Parse(json);

    string beginning = parts["beginnings"]
        .ElementAt(rand.Next(parts["beginnings"].Count()))
        .ToString();
    string middle = parts["middles"].ElementAt(rand.Next(parts["middles"].Count())).ToString();
    string obj = parts["objects"].ElementAt(rand.Next(parts["objects"].Count())).ToString();
    string ending = parts["endings"].ElementAt(rand.Next(parts["endings"].Count())).ToString();

    return $"{beginning}, {middle} {obj} {ending}";
}

// main functions
string GenerateCharacter(bool LeordisChar, bool makeYouChooses)
{
    Random rnd = new Random();

    var name = GenerateFullName();
    var motto = GenerateMotto();
    var attributes = ProduceFunAttributeArray();
    string class_ = PickRandomUpperAndInner(classData);
    string race = PickRandomUpperAndInner(racesData);
    string background = PickRandomFromArray(backgroundData);
    string feat = PickRandomFromArray(featsData);
    string alignment = PickRandomFromArray(alignmentsData);
    string MBTI = MBTIGenerator.GetRandomMBTI();

    string mainRace = race.Split(" ")[0];
    var physicalStats = RaceStats.Stats[mainRace];
    string height =
        rnd.Next(physicalStats.minHeight, physicalStats.maxHeight + 1).ToString() + " cm";
    string weight =
        rnd.Next(physicalStats.minWeight, physicalStats.maxWeight + 1).ToString() + " kg";

    string sex = "Female";
    int chance = rnd.Next(1, 101);
    if (chance < 40)
    {
        sex = "Male";
    }
    else if (chance < 60)
    {
        sex = "Non-Binary";
    }

    if (makeYouChooses)
    {
        List<string> allFields = new List<string>();
        allFields.Add(class_);
        allFields.Add(race);
        allFields.Add(background);
        allFields.Add(feat);
        allFields.Add(alignment);
        allFields.Add(sex);
        allFields.Add(height);
        allFields.Add(weight);
        // possibly replace some with "You choose"
        for (int i = 0; i < allFields.Count; i++)
        {
            int chance2 = rnd.Next(1, 101);
            if (chance2 < 20) // ~19% chance
            {
                allFields[i] = "You choose";
            }
        }
        // write modified values back (so finalString reflects changes)
        class_ = allFields[0];
        race = allFields[1];
        background = allFields[2];
        feat = allFields[3];
        alignment = allFields[4];
        sex = allFields[5];
        height = allFields[6];
        weight = allFields[7];
    }

    // optional Leordis char stuff
    string[] nations =
    [
        "Horde of Urshani",
        "Nuu Confederation",
        "Wilds of Agrestia (Emperor-aligned)",
        "Wilds of Agrestia (M&S Aligned)",
        "Umbra Aligned",
        "Singhana Naya",
        "Reyes Sin Lugar (old-bloods)",
        "Reyes Sin Lugar (new-bloods)",
    ];
    string nation = nations[rnd.Next(0, nations.Length)];

    string[] CollisionTakes =
    [
        "Whetu is a Hero. We are now protected by Ngati and the Lovers.",
        "Whetu doomed the world to be devoured by the Great Old Ones.",
        "Whetu meant well, but the Gods are tyrants, and we have to undo it.",
        "I just want resurrections to be possible again.",
        "Whatever dangers are out there, no natural disasters or diseases is worth it.",
        "Whetu is a Hero of Humanoid Civilizations. Speakers are stronger than ever, civilization is thriving.",
        "I am supporting the Nuu. Fuck everybody else. This is payback for trying to colonize Nuu.",
        "Nobody should have been able to make that kind of decision. Whetu is a monster.",
    ];
    string collisionTake = CollisionTakes[rnd.Next(0, CollisionTakes.Length)];

    string god = "";
    if (class_.StartsWith("Paladin") || class_.StartsWith("Cleric"))
    {
        god = PickRandomFromArray(godsData);
    }

    string finalString = "";
    if (LeordisChar)
    {
        if (god == "")
        {
            finalString =
                $"📝 Name: {name}\n"
                + $"⚧ Gender: {sex}\n"
                + $"🧬 Race: {race}\n"
                + $"🌍 Nation: {nation}\n"
                + $"💥 Opinion on Whetu's Collision: {collisionTake}\n"
                + $"📏 Height: {height}\n"
                + $"⚖️ Weight: {weight}\n"
                + $"🗡️ Class: {class_}\n"
                + $"💭 MBTI: {MBTI}\n"
                + $"✨ Starting Feat: {feat}\n"
                + $"🏞️ Background: {background}\n"
                + $"🗣️ Motto: {motto}\n"
                + $"📊 Attributes: {attributes}";
        }
        else
        {
            finalString =
                $"📝 Name: {name}\n"
                + $"⚧ Gender: {sex}\n"
                + $"🧬 Race: {race}\n"
                + $"🌍 Nation: {nation}\n"
                + $"💥 Opinion on Whetu's Collision: {collisionTake}\n"
                + $"📏 Height: {height}\n"
                + $"⚖️ Weight: {weight}\n"
                + $"🗡️ Class: {class_}\n"
                + $"⛪ Worships: {god}\n"
                + $"💭 MBTI: {MBTI}\n"
                + $"✨ Starting Feat: {feat}\n"
                + $"🏞️ Background: {background}\n"
                + $"🗣️ Motto: {motto}\n"
                + $"📊 Attributes: {attributes}";
        }
    }
    else
    {
        finalString =
            $"📝 Name: {name}\n"
            + $"⚧ Gender: {sex}\n"
            + $"🧬 Race: {race}\n"
            + $"📏 Height: {height}\n"
            + $"⚖️ Weight: {weight}\n"
            + $"🗡️ Class: {class_}\n"
            + $"💭 MBTI: {MBTI}\n"
            + $"✨ Starting Feat: {feat}\n"
            + $"🏞️ Background: {background}\n"
            + $"🗣️ Motto: {motto}\n"
            + $"📊 Attributes: {attributes}";
    }

    return finalString;
}

// generate deity
string RandomDeityAttribute()
{
    Random rnd = new Random();
    return rnd.Next(3, 31).ToString();
}
string GenerateDeity()
{
    Random rnd = new Random();
    string sex = "Female";
    int chance = rnd.Next(1, 101);
    if (chance < 40)
    {
        sex = "Male";
    }
    else if (chance < 60)
    {
        sex = "Non-Binary";
    }

    List<string> attributes = new List<string>();
    for (int i = 0; i < 6; i++)
    {
        string roll = RandomDeityAttribute();
        attributes.Add(roll);
    }
    string finalAttributes = String.Join(", ", attributes.ToArray());
    string name = GenerateFullName().Split(" ")[0];
    string epithet = PickRandomFromArray(epithetsData);
    var motto = GenerateMotto();
    string domain1 = PickRandomFromArray(domainsData);
    string domain2 = PickRandomFromArray(domainsData);
    if (domain2 == domain1)
    {
        domain2 = PickRandomFromArray(domainsData);
    }
    string domain3 = PickRandomFromArray(domainsData);
    if (domain3 == domain2 || domain3 == domain1)
    {
        domain3 = PickRandomFromArray(domainsData);
    }
    string finalDomain = domain1 + ", " + domain2 + " and " + domain3;
    string class_ = PickRandomUpperAndInner(classData);
    string race = PickRandomUpperAndInner(racesData);
    string MBTI = MBTIGenerator.GetRandomMBTI();

    string finalString =
        $"📝 Name: {name}\n"
        + $"⚧ Gender: {sex}\n"
        + $"✨ Epithet: {epithet}\n"
        + $"🧬 Domain: Deity of {finalDomain}\n"
        + $"🗡️ Class in Life: {class_}\n"
        + $"🧝 Race in Life: {race}\n"
        + $"📜 Commandment: {motto}\n"
        + $"💭 MBTI: {MBTI}\n"
        + $"📊 Attributes: {finalAttributes}";

    return finalString;
}

// city generator
string GenerateCity()
{
    Random rnd = new Random();

    // name
    string suffix = PickRandomFromArray(cityNameSuffixes);
    string prefix = PickRandomFromArray(cityNamePrefixes);
    string name = GenerateFullName().Split(" ")[0];
    int chance = rnd.Next(0, 100);
    if (chance < 50)
    {
        name = name + suffix;
    }
    if (chance < 30)
    {
        name = prefix + " " + name;
    }

    // economy
    string economy = PickRandomFromArray(economies);

    // size
    string[] sizes =
    {
        "a small",
        "a large",
        "an average",
        "the biggest and most important",
        "a tiny",
        "a quaint",
        "a mid-sized",
        "a regionally important",
        "a key strategic",
        "a cozy",
    };
    int random1 = rnd.Next(0, sizes.Length); // corrected to avoid out-of-bounds
    string size = sizes[random1];

    // population
    int minPop = 0;
    int maxPop = 1;
    switch (size)
    {
        case "a small":
            minPop = 2000;
            maxPop = 8000;
            break;
        case "a large":
            minPop = 100000;
            maxPop = 1000000;
            break;
        case "an average":
            minPop = 10000;
            maxPop = 100000;
            break;
        case "the biggest and most important":
            minPop = 5000000;
            maxPop = 50000000;
            break;
        case "a tiny":
            minPop = 300;
            maxPop = 2000;
            break;
        case "a quaint":
            minPop = 1000;
            maxPop = 5000;
            break;
        case "a mid-sized":
            minPop = 10000;
            maxPop = 50000;
            break;
        case "a regionally important":
            minPop = 200000;
            maxPop = 1000000;
            break;
        case "a key strategic":
            minPop = 1000000;
            maxPop = 6000000;
            break;
        case "a cozy":
            minPop = 1000;
            maxPop = 25000;
            break;
    }
    int population = rnd.Next(minPop, maxPop);

    // weather
    string weatherString = PickRandomFromArray(weather);

    // geography
    string geographyCorrelation = PickRandomFromArray(geographyCorrelations);
    string geographyName = GenerateFullName().Split(" ")[0];
    string geographyType = PickRandomFromArray(geographyTypes);

    // social classes
    List<SocialClass> selectedSocialClasses = new List<SocialClass>();
    for (int i = 0; i < 3; i++)
    {
        string socialClassName = PickRandomFromArray(socialClasses);
        int percentage = 1;
        if (i == 0)
        {
            percentage = rnd.Next(1, 61);
        }
        else if (i == 1)
        {
            percentage = rnd.Next(1, 31);
        }
        else
        {
            percentage =
                100
                - selectedSocialClasses[0].percentage
                - selectedSocialClasses[1].percentage
                - 15;
            if (percentage < 0)
            {
                percentage =
                    100 - selectedSocialClasses[0].percentage - selectedSocialClasses[1].percentage;
            }
        }
        SocialClass newSocialClass = new SocialClass();
        newSocialClass.name = socialClassName;
        newSocialClass.percentage = percentage;
        selectedSocialClasses.Add(newSocialClass);
    }
    selectedSocialClasses = selectedSocialClasses.OrderByDescending(sc => sc.percentage).ToList();

    // ruler
    string rulerTitle = PickRandomFromArray(rulerTitles);
    string rulerPersonality = PickRandomFromArray(rulerPersonalities);
    string rulerPopularity = PickRandomFromArray(rulerPopularities);
    string seat = PickRandomFromArray(seats);

    // palace / seat name
    string palaceName = GenerateFullName().Split(" ")[0];

    // recent news
    string recentNews = PickRandomFromArray(recentEvents);

    // city stats
    int coziness = rnd.Next(1, 101);
    int infrastructure = rnd.Next(1, 101);
    int richness = rnd.Next(1, 101);

    // final return with all variables filled
    return $"The city of {name} is {size} {economy} town of {population} people, located {geographyCorrelation} {weatherString} {geographyName} {geographyType}. "
        + $"The main social classes are: {selectedSocialClasses[0].name} ({selectedSocialClasses[0].percentage.ToString() + "%"}), "
        + $"{selectedSocialClasses[1].name} ({selectedSocialClasses[1].percentage.ToString() + "%"}), {selectedSocialClasses[2].name} ({selectedSocialClasses[2].percentage.ToString() + "%"}). "
        + $"The city is ruled by {rulerPersonality} {rulerTitle}, {rulerPopularity} the citizens, out of the {palaceName} {seat}. "
        + $"Recently, the town is talking about {recentNews}. City's stats: coziness of {coziness.ToString()}% | infrastructure of {infrastructure.ToString()}% | richness of {richness.ToString()}%.";
}

// start
string StartString()
{
    return "🧙‍♀️ Welcome, wanderer.\n"
        + "I see your fate is tangled with dice and destiny.\n"
        + "Roll, create, or summon. ⚔️✨\n"
        + "Use /help if your path is unclear.\n";
}

// help
string HelpString()
{
    return "📜 Commands you may dare:\n"
        + "/rXdY → Roll dice (e.g., /1d20+3). 🎲\n"
        + "/char → A random adventurer appears. 🧝‍♂️\n"
        + "/char_no_blanks → No 'you choose' fields. ⚔️\n"
        + "/deity → Summon a deity of your making. ✨\n"
        + "/city → Reveal a city with secrets. 🏰\n"
        + "/faction → Create a faction for your world. 🐎\n";
}

// Generate a faction
string GenerateFaction(bool justName)
{
    Random rand = new Random();

    // name
    string json = File.ReadAllText("data/faction/name_parts.json");
    var parts = JObject.Parse(json);
    string generated_proper_noun = GenerateFullName().Split(" ")[0];
    string prefix = parts["prefixes"].ElementAt(rand.Next(parts["prefixes"].Count())).ToString();
    string first_word = parts["first_words"]
        .ElementAt(rand.Next(parts["first_words"].Count()))
        .ToString();
    string second_word = parts["second_words"]
        .ElementAt(rand.Next(parts["second_words"].Count()))
        .ToString();
    string suffix = parts["suffixes"].ElementAt(rand.Next(parts["suffixes"].Count())).ToString();

    var patterns = Enum.GetValues(typeof(FactionNamePatterns));
    FactionNamePatterns pattern = (FactionNamePatterns)
        patterns.GetValue(rand.Next(patterns.Length));

    string factionName = "";

    switch (pattern)
    {
        // Single-part names
        case FactionNamePatterns.Prefix:
            bool apostrophe = prefix.Substring(0, prefix.Length - 1).Contains("'");
            if (apostrophe)
                factionName = $"{prefix} {first_word}";
            else
                factionName = $"{prefix}";
            break;
        case FactionNamePatterns.FirstWord:
            factionName = $"{first_word}";
            break;
        case FactionNamePatterns.SecondWord:
            factionName = $"{second_word}";
            break;
        case FactionNamePatterns.Suffix:
            factionName = $"{first_word} {suffix}";
            break;
        case FactionNamePatterns.GeneratedProperNoun:
            factionName = $"{generated_proper_noun}";
            break;

        // Two-part combinations
        case FactionNamePatterns.Prefix_FirstWord:
            factionName = $"{prefix} {first_word}";
            break;
        case FactionNamePatterns.Prefix_SecondWord:
            factionName = $"{prefix} {second_word}";
            break;
        case FactionNamePatterns.Prefix_Suffix:
            factionName = $"{prefix} {suffix}";
            break;
        case FactionNamePatterns.Prefix_GeneratedProperNoun:
            factionName = $"{prefix} {generated_proper_noun}";
            break;
        case FactionNamePatterns.FirstWord_SecondWord:
            factionName = $"{first_word} {second_word}";
            break;
        case FactionNamePatterns.FirstWord_Suffix:
            factionName = $"{first_word} {suffix}";
            break;
        case FactionNamePatterns.FirstWord_GeneratedProperNoun:
            factionName = $"{first_word} {generated_proper_noun}";
            break;
        case FactionNamePatterns.SecondWord_Suffix:
            factionName = $"{second_word} {suffix}";
            break;
        case FactionNamePatterns.SecondWord_GeneratedProperNoun:
            factionName = $"{second_word} {generated_proper_noun}";
            break;
        case FactionNamePatterns.GeneratedProperNoun_Suffix:
            factionName = $"{generated_proper_noun} {suffix}";
            break;

        // Three-part combinations
        case FactionNamePatterns.Prefix_FirstWord_SecondWord:
            factionName = $"{prefix} {first_word} {second_word}";
            break;
        case FactionNamePatterns.Prefix_FirstWord_Suffix:
            factionName = $"{prefix} {first_word} {suffix}";
            break;
        case FactionNamePatterns.Prefix_FirstWord_GeneratedProperNoun:
            factionName = $"{prefix} {first_word} {generated_proper_noun}";
            break;
        case FactionNamePatterns.Prefix_SecondWord_Suffix:
            factionName = $"{prefix} {second_word} {suffix}";
            break;
        case FactionNamePatterns.Prefix_SecondWord_GeneratedProperNoun:
            factionName = $"{prefix} {second_word} {generated_proper_noun}";
            break;
        case FactionNamePatterns.Prefix_GeneratedProperNoun_Suffix:
            factionName = $"{prefix} {generated_proper_noun} {suffix}";
            break;
        case FactionNamePatterns.FirstWord_SecondWord_Suffix:
            factionName = $"{first_word} {second_word} {suffix}";
            break;
        case FactionNamePatterns.FirstWord_SecondWord_GeneratedProperNoun:
            factionName = $"{first_word} {second_word} {generated_proper_noun}";
            break;
        case FactionNamePatterns.FirstWord_GeneratedProperNoun_Suffix:
            factionName = $"{first_word} {generated_proper_noun} {suffix}";
            break;
        case FactionNamePatterns.SecondWord_GeneratedProperNoun_Suffix:
            factionName = $"{second_word} {generated_proper_noun} {suffix}";
            break;

        // Four-part combinations
        case FactionNamePatterns.Prefix_FirstWord_SecondWord_Suffix:
            factionName = $"{prefix} {first_word} {second_word} {suffix}";
            break;
        case FactionNamePatterns.Prefix_FirstWord_SecondWord_GeneratedProperNoun:
            factionName = $"{prefix} {first_word} {second_word} {generated_proper_noun}";
            break;
        case FactionNamePatterns.Prefix_FirstWord_GeneratedProperNoun_Suffix:
            factionName = $"{prefix} {first_word} {generated_proper_noun} {suffix}";
            break;
        case FactionNamePatterns.Prefix_SecondWord_GeneratedProperNoun_Suffix:
            factionName = $"{prefix} {second_word} {generated_proper_noun} {suffix}";
            break;
        case FactionNamePatterns.FirstWord_SecondWord_GeneratedProperNoun_Suffix:
            factionName = $"{first_word} {second_word} {generated_proper_noun} {suffix}";
            break;

        // All five parts
        case FactionNamePatterns.Prefix_FirstWord_SecondWord_GeneratedProperNoun_Suffix:
            factionName = $"{prefix} {first_word} {second_word} {generated_proper_noun} {suffix}";
            break;

        // Fallback
        default:
            factionName = $"{prefix} {first_word} {second_word}";
            break;
    }

    // size
    int number = rand.Next(0, 200000);
    int cities = rand.Next(0, 50);

    // symbol
    string json2 = File.ReadAllText("data/faction/symbols.json");
    var parts2 = JObject.Parse(json2);
    string symbol_word_1 = parts2["first_words"]
        .ElementAt(rand.Next(parts2["first_words"].Count()))
        .ToString();
    string symbol_word_2 = parts2["second_words"]
        .ElementAt(rand.Next(parts2["second_words"].Count()))
        .ToString();
    string symbol = $"{symbol_word_1} {symbol_word_2}";

    // type
    string type = PickRandomFromArray(factionTypes);
    char firstChar = char.ToLower(type[0]);
    string article = "a";
    if ("aeiou".IndexOf(firstChar) >= 0)
    {
        article = "an";
    }

    // leader
    string rulerTitle = PickRandomFromArray(rulerTitles);
    string rulerPersonality = PickRandomFromArray(rulerPersonalities);
    string rulerPopularity = PickRandomFromArray(rulerPopularities);
    string seat = PickRandomFromArray(seats);
    string baseName = GenerateFullName().Split(" ")[0];

    // classes
    List<SocialClass> selectedSocialClasses = new List<SocialClass>();
    for (int i = 0; i < 3; i++)
    {
        string socialClassName = PickRandomFromArray(socialClasses);
        int percentage = 1;
        if (i == 0)
        {
            percentage = rand.Next(1, 61);
        }
        else if (i == 1)
        {
            percentage = rand.Next(1, 31);
        }
        else
        {
            percentage =
                100
                - selectedSocialClasses[0].percentage
                - selectedSocialClasses[1].percentage
                - 15;
            if (percentage < 0)
            {
                percentage =
                    100 - selectedSocialClasses[0].percentage - selectedSocialClasses[1].percentage;
            }
        }
        SocialClass newSocialClass = new SocialClass();
        newSocialClass.name = socialClassName;
        newSocialClass.percentage = percentage;
        selectedSocialClasses.Add(newSocialClass);
    }
    selectedSocialClasses = selectedSocialClasses.OrderByDescending(sc => sc.percentage).ToList();

    string finalString =
        $"{factionName} are {article} {type} of {number.ToString()} members across {cities.ToString()} cities. "
        + $"Their base is the {baseName} {seat}. "
        + $"They use the symbol of {symbol}, and are led by {rulerPersonality} {rulerTitle}, {rulerPopularity} the members. "
        + $"Their members are: {selectedSocialClasses[0].name} ({selectedSocialClasses[0].percentage.ToString() + "%"}), "
        + $"{selectedSocialClasses[1].name} ({selectedSocialClasses[1].percentage.ToString() + "%"}), "
        + $"{selectedSocialClasses[2].name} ({selectedSocialClasses[2].percentage.ToString() + "%"}). ";

    if (justName)
    {
        finalString = factionName;
    }

    return finalString;
}

// Generate a nation --- name, two cities, capital, deity, monarch, factions
string GenerateNation()
{
    Random rand = new Random();

    // name
    string json = File.ReadAllText("data/nation/name_parts.json");
    var parts = JObject.Parse(json);
    string generated_proper_noun = GenerateFullName().Split(" ")[0];
    string prefix = parts["prefixes"].ElementAt(rand.Next(parts["prefixes"].Count())).ToString();
    string noun = parts["nouns"].ElementAt(rand.Next(parts["nouns"].Count())).ToString();
    string suffix = parts["suffixes"].ElementAt(rand.Next(parts["suffixes"].Count())).ToString();
    var patterns = Enum.GetValues(typeof(NationNamePatterns));
    NationNamePatterns pattern = (NationNamePatterns)patterns.GetValue(rand.Next(patterns.Length));
    string nationName = "";
    switch (pattern)
    {
        // Single-part names
        case NationNamePatterns.Prefix:
            bool apostrophe = prefix.Substring(0, prefix.Length - 1).Contains("'");
            if (apostrophe)
            {
                nationName = $"{prefix} {noun}";
            }
            else
            {
                if (prefix.EndsWith("'"))
                    nationName = $"{prefix} {noun}";
                else
                    nationName = $"{prefix}";
                break;
            }
            break;

        case NationNamePatterns.Suffix:
            nationName = $"{noun} {suffix}";
            break;

        case NationNamePatterns.Noun:
            nationName = $"{noun}";
            break;

        case NationNamePatterns.GeneratedProperNoun:
            nationName = $"{generated_proper_noun}";
            break;

        // Two-part combinations
        case NationNamePatterns.Prefix_Noun:
            nationName = $"{prefix} {noun}";
            break;

        case NationNamePatterns.Prefix_GeneratedProperNoun:
            nationName = $"{prefix} {generated_proper_noun}";
            break;

        case NationNamePatterns.Noun_Suffix:
            nationName = $"{noun} {suffix}";
            break;

        case NationNamePatterns.GeneratedProperNoun_Suffix:
            nationName = $"{generated_proper_noun} {suffix}";
            break;

        // Three-part combinations
        case NationNamePatterns.Noun_Of_GeneratedProperNoun:
            nationName = $"{noun} of {generated_proper_noun}";
            break;

        case NationNamePatterns.Prefix_Noun_Suffix:
            nationName = $"{prefix} {noun} {suffix}";
            break;

        case NationNamePatterns.Prefix_GeneratedProperNoun_Suffix:
            nationName = $"{prefix} {generated_proper_noun} {suffix}";
            break;

        // Four-part combinations
        case NationNamePatterns.Prefix_Noun_Of_GeneratedProperNoun:
            nationName = $"{prefix} {noun} of {generated_proper_noun}";
            break;

        case NationNamePatterns.Noun_Of_GeneratedProperNoun_Suffix:
            nationName = $"{noun} of {generated_proper_noun} {suffix}";
            break;

        case NationNamePatterns.Prefix_Noun_Of_GeneratedProperNoun_Suffix:
            nationName = $"{prefix} {noun} of {generated_proper_noun} {suffix}";
            break;

        // Fallback
        default:
            nationName = $"{prefix} {noun}";
            break;
    }

    // population
    int population = rand.Next(0, 50000000);
    string formattedPopulation = population.ToString("N0");

    // symbol
    string json2 = File.ReadAllText("data/faction/symbols.json");
    var parts2 = JObject.Parse(json2);
    string symbol_word_1 = parts2["first_words"]
        .ElementAt(rand.Next(parts2["first_words"].Count()))
        .ToString();
    string symbol_word_2 = parts2["second_words"]
        .ElementAt(rand.Next(parts2["second_words"].Count()))
        .ToString();
    string symbol = $"{symbol_word_1} {symbol_word_2}";

    // power level
    string powerLevel = "";

    if (population < 100000)
    {
        powerLevel = "puny";
    }
    else if (population < 500000)
    {
        powerLevel = "minor";
    }
    else if (population < 1000000)
    {
        powerLevel = "modest";
    }
    else if (population < 2000000)
    {
        powerLevel = "established";
    }
    else if (population < 5000000)
    {
        powerLevel = "notable";
    }
    else if (population < 10000000)
    {
        powerLevel = "formidable";
    }
    else if (population < 15000000)
    {
        powerLevel = "major";
    }
    else if (population < 20000000)
    {
        powerLevel = "dominant";
    }
    else if (population < 25000000)
    {
        powerLevel = "mythic";
    }
    else if (population < 30000000)
    {
        powerLevel = "titanic";
    }
    else if (population < 35000000)
    {
        powerLevel = "cataclysmic";
    }
    else if (population < 40000000)
    {
        powerLevel = "world-bending";
    }
    else if (population < 45000000)
    {
        powerLevel = "world-shaping";
    }
    else
    {
        powerLevel = "literally unstoppable";
    }

    // main economy
    string economy = PickRandomFromArray(economies);

    // geography
    string geographyCorrelation = PickRandomFromArray(geographyCorrelations);
    string geographyName = GenerateFullName().Split(" ")[0];
    string geographyType = PickRandomFromArray(geographyTypes);

    // ruler race
    string ruler_race = PickRandomUpperAndInner(racesData);

    // ruler class
    string ruler_class = PickRandomUpperAndInner(classData);

    // ruler alignment
    string ruler_alignment = PickRandomFromArray(alignmentsData);

    // ruler MBTI
    string ruler_MBTI = MBTIGenerator.GetRandomMBTI();

    // ruler name
    var ruler_name = GenerateFullName();

    // ruler_reputation
    string ruler_popularity = PickRandomFromArray(rulerPopularities);

    // ruler ability scores
    var ruler_attributes = ProduceFunAttributeArray();

    // capital name
    string capital_suffix = PickRandomFromArray(cityNameSuffixes);
    string capital_prefix = PickRandomFromArray(cityNamePrefixes);
    string capital_name = GenerateFullName().Split(" ")[0];
    int chance = rand.Next(0, 100);
    if (chance < 50)
    {
        capital_name = capital_name + capital_suffix;
    }
    if (chance < 30)
    {
        capital_name = capital_prefix + " " + capital_name;
    }

    // capital mayor name
    var mayor_name = GenerateFullName();

    // capital mayor trait
    string mayor_personality = PickRandomFromArray(rulerPersonalities);

    // three social classes
    string socialClassName1 = PickRandomFromArray(socialClasses);
    string socialClassName2 = PickRandomFromArray(socialClasses);
    string socialClassName3 = PickRandomFromArray(socialClasses);

    // two faction names
    string factionName1 = GenerateFaction(true);
    string factionName2 = GenerateFaction(true);

    // two faction types
    string type1 = PickRandomFromArray(factionTypes);
    char firstChar1 = char.ToLower(type1[0]);
    string article1 = "a";
    if ("aeiou".IndexOf(firstChar1) >= 0)
    {
        article1 = "an";
    }
    string factionType1 = $"{article1} {type1}";
    string type2 = PickRandomFromArray(factionTypes);
    char firstChar2 = char.ToLower(type2[0]);
    string article2 = "a";
    if ("aeiou".IndexOf(firstChar2) >= 0)
    {
        article2 = "an";
    }
    string factionType2 = $"{article2} {type2}";

    // three relationships with Highness
    string highnessRel1 = Relationships.PickRandomFromArray(
        Relationships.relationshipsWithHighness
    );
    string highnessRel2 = Relationships.PickRandomFromArray(
        Relationships.relationshipsWithHighness
    );
    string highnessRel3 = Relationships.PickRandomFromArray(
        Relationships.relationshipsWithHighness
    );

    // one relationship between factions
    string factionRel = Relationships.PickRandomFromArray(Relationships.factionRelationships);

    // deity name and three virtues
    string deity_name = GenerateFullName().Split(" ")[0];
    string deity_epithet = PickRandomFromArray(epithetsData);
    var deity_motto = GenerateMotto();
    string domain1 = PickRandomFromArray(domainsData);
    string domain2 = PickRandomFromArray(domainsData);
    string domain3 = PickRandomFromArray(domainsData);

    string finalString =
        $"<b>The {(nationName.StartsWith("The ") ? nationName.Substring(4) : nationName)}</b> is a {powerLevel} {economy} nation of {formattedPopulation} people "
        + $"{geographyCorrelation} {geographyType} of {geographyName}, flying under the banner of {symbol}.\n\n"
        + $"It is ruled by <b>their Highness {ruler_name}</b>, a {ruler_MBTI} {ruler_alignment} {ruler_race} {ruler_class} "
        + $"with ability scores ({ruler_attributes}), {ruler_popularity} the populace.\n\n"
        + $"The nation's capital is the city of <b>{capital_name}</b>, populated mostly by {socialClassName1} and ruled by mayor {mayor_name}, {mayor_personality} leader "
        + $"who is {highnessRel1}.\n\n"
        + $"The nation is roamed by <b>{factionName1}</b>, which is {factionType1} made up largely of {socialClassName2}. They are {highnessRel2}.\n\n"
        + $"<b>{factionName2}</b>, {factionType2} composed mostly of {socialClassName3}, is {highnessRel3}. "
        + $"These factions are currently {factionRel} with one another.\n\n"
        + $"The nation worships <b>{deity_name}, {deity_epithet}</b>, deity of {domain1}, {domain2}, and {domain3}, "
        + $"whose motto is \"{deity_motto}\"!";

    return finalString;
}

// GET routes
app.MapGet(
    "/char",
    () =>
    {
        return Results.Ok(GenerateCharacter(false, true));
    }
);

app.MapGet(
    "/LeordisChar",
    () =>
    {
        return Results.Ok(GenerateCharacter(true, true));
    }
);

// Telegram bot
// Start Telegram bot long-polling
var botClient = app.Services.GetRequiredService<ITelegramBotClient>();

// CancellationToken for clean shutdown
var cts = new CancellationTokenSource();

// Receiver options
var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
{
    AllowedUpdates = { }, // receive all update types
};

// Start receiving
botClient.StartReceiving(
    updateHandler: async (ITelegramBotClient bot, Update update, CancellationToken token) =>
    {
        try
        {
            if (update?.Message?.Text != null)
            {
                string incoming = update.Message.Text.Trim();

                if (!incoming.StartsWith("/"))
                {
                    return;
                }

                string response = "The Witch does not abide by this command.";

                // ----- Dice rolling -----
                if (incoming.StartsWith("/r") || incoming.StartsWith("/"))
                {
                    string diceCode = incoming.StartsWith("/r")
                        ? incoming.Substring(2)
                        : incoming.Substring(1);

                    List<DicePart> parsed = ParseDiceCode(diceCode);
                    if (parsed.Count() > 0)
                    {
                        string result = RollDice(parsed);
                        response = result;
                    }
                    // ----- Character generator -----
                    else if (incoming.Equals("/char", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateCharacter(false, true);
                    }
                    else if (incoming.Equals("/leordischar", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateCharacter(true, true);
                    }
                    else if (incoming.Equals("/char_no_blanks", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateCharacter(false, false);
                    }
                    else if (
                        incoming.Equals(
                            "/leordischar_no_blanks",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        response = GenerateCharacter(true, false);
                    }
                    else if (incoming.Equals("/deity", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateDeity();
                    }
                    else if (incoming.Equals("/city", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateCity();
                    }
                    else if (incoming.Equals("/start", StringComparison.OrdinalIgnoreCase))
                    {
                        response = StartString();
                    }
                    else if (incoming.Equals("/help", StringComparison.OrdinalIgnoreCase))
                    {
                        response = HelpString();
                    }
                    else if (incoming.Equals("/faction", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateFaction(false);
                    }
                    else if (incoming.Equals("/nation", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateNation();
                    }
                }

                await bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: response,
                    parseMode: ParseMode.Html,
                    cancellationToken: token
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing update: {ex.Message}");
        }
    },
    errorHandler: async (ITelegramBotClient bot, Exception ex, CancellationToken token) =>
    {
        Console.WriteLine($"Telegram error: {ex.Message}");
        await Task.CompletedTask;
    },
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

app.Run();

// [v] add height and weight - varied by races
// [v] add MBTI type
// [v] a /LeordisChar route with a nation, and a "take on Whetu's Collision"
// [v] rethink - what is fun and good for creativity and what isn't (is the motto fun? is alignment?)
// [v] make a TG bot
// [v] publish on Railway
// [v] add descriptions and commands to both bots
// [v] include + and - bonuses to dice code parser
// [v] connect backend to TG
