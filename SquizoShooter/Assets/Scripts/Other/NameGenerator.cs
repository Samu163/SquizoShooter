using System; // Necesario para System.Random
using System.Collections.Generic;

public static class NameGenerator
{
    private static List<string> adjectives = new List<string> { "Fast", "Crazy", "Silent", "Brave", "Sad", "Happy", "Mega", "Cyber", "Toxic", "Neon", "Dark", "Holy" };
    private static List<string> nouns = new List<string> { "Cube", "Warrior", "Ninja", "Tank", "Sniper", "Ghost", "Robot", "Agent", "Tiger", "Pilot", "Wizard", "Knight" };

    private static System.Random rng = new System.Random();

    public static string GetRandomName()
    {
        lock (rng)
        {
            string adj = adjectives[rng.Next(adjectives.Count)];
            string noun = nouns[rng.Next(nouns.Count)];
            int num = rng.Next(10, 99);
            return $"{adj}{noun}{num}";
        }
    }
}