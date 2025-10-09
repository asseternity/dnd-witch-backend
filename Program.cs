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
    var rolls = new List<int>();
    int modifierTotal = 0;

    foreach (var part in parts)
    {
        if (part.IsDice)
        {
            for (int i = 0; i < Math.Abs(part.Count); i++)
            {
                int roll = rnd.Next(1, part.Sides + 1);
                rolls.Add((part.Count > 0) ? roll : -roll);
            }
        }
        else
        {
            modifierTotal += part.Modifier;
        }
    }

    rolls.Sort((a, b) => b.CompareTo(a)); // descending
    int total = rolls.Sum() + modifierTotal;

    // Special case: one die, no modifiers
    if (rolls.Count == 1 && modifierTotal == 0)
    {
        return $"You rolled: {rolls[0]}";
    }

    string rollsStr = string.Join(", ", rolls);
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
// import all the jsons
var backgroundData = JObject.Parse(File.ReadAllText("data/backgrounds.json"));
var classData = JObject.Parse(File.ReadAllText("data/classes.json"));
var featsData = JObject.Parse(File.ReadAllText("data/feats.json"));
var racesData = JObject.Parse(File.ReadAllText("data/races.json"));
var alignmentsData = JObject.Parse(File.ReadAllText("data/alignments.json"));

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
string GenerateCharacter(bool LeordisChar)
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
        "Reyes Sin Lugar (new-bloods)"
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

    string finalString = "";
    if (LeordisChar)
    {
        finalString =
            $"📝 Name: {name}\n"
            + $"⚧ Sex: {sex}\n"
            + $"🧬 Race: {race}\n"
            + $"🌍 Nation: {nation}\n"
            + $"💥 Opinion on Whetu's Collision: {race}\n"
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
            + $"⚧ Sex: {sex}\n"
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

// GET routes
app.MapGet(
    "/char",
    () =>
    {
        return Results.Ok(GenerateCharacter(false));
    }
);

app.MapGet(
    "/LeordisChar",
    () =>
    {
        return Results.Ok(GenerateCharacter(true));
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
    AllowedUpdates = { } // receive all update types
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
                        response = GenerateCharacter(false);
                    }
                    else if (incoming.Equals("/leordischar", StringComparison.OrdinalIgnoreCase))
                    {
                        response = GenerateCharacter(true);
                    }
                }

                await bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: response,
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
// [_] connect backend to TG
