namespace WildsDeck.Memory;

internal static class WildsKnowledgeBase
{
    private static readonly string[] MonsterNames =
    [
        "Rathian", "Rathalos", "Guardian Rathalos", "Gravios", "Yian Kut-Ku", "Gypceros", "Congalala",
        "Blangonga", "Lagiacrus", "Nerscylla", "Gore Magala", "Seregios", "Gogmazios", "Mizutsune",
        "Guardian Fulgur Anjanath", "Guardian Ebony Odogaron", "Doshaguma", "Guardian Doshaguma", "Balahara",
        "Chatacabra", "Quematrice", "Lala Barina", "Rompopolo", "Rey Dau", "Uth Duna", "Nu Udra", "Ajarakan",
        "Arkveld", "Guardian Arkveld", "Hirabami", "Jin Dahaad", "Xu Wu", "Zoh Shia",
        "High Purrformance Barrel Puncher", "Omega Planetes"
    ];

    private static readonly IReadOnlyDictionary<int, string> AilmentNames = new Dictionary<int, string>
    {
        [0] = "Enrage", [1] = "Exhaust", [3] = "Poison", [4] = "Poison", [5] = "Paralysis", [6] = "Paralysis",
        [7] = "Sleep", [8] = "Sleep", [9] = "Blast", [11] = "Blast", [13] = "Mount", [14] = "Exhaust",
        [15] = "Stun", [16] = "Stun", [17] = "Tranquilize", [18] = "Flash", [19] = "Flash", [21] = "Dung",
        [25] = "Offset", [28] = "Power Clash"
    };

    private static readonly string[] Weapons =
    [
        "Great Sword", "Sword & Shield", "Dual Blades", "Long Sword", "Hammer", "Hunting Horn", "Lance",
        "Gunlance", "Switch Axe", "Charge Blade", "Insect Glaive", "Bow", "Heavy Bowgun", "Light Bowgun"
    ];

    public static string MonsterName(int id) => id >= 0 && id < MonsterNames.Length ? MonsterNames[id] : $"Monster {id}";
    public static string AilmentName(int id) => AilmentNames.TryGetValue(id, out string? name) ? name : $"Ailment {id}";
    public static string? WeaponName(byte id) => id < Weapons.Length ? Weapons[id] : null;
}
