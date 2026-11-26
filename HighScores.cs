using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CrossyRoadBase;

public class ScoreEntry
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public string Place { get; set; } = "";       // which city or municipality the run ended in
    public string Character { get; set; } = "";   // Boy, Girl or Chicken
    public string Date { get; set; } = "";
    public long Id { get; set; }
}

/// <summary>
/// The local high score table. It is saved as JSON in Godot's user:// folder
/// (Project > Open User Data Folder > highscores.json), so it survives restarts.
/// </summary>
public static class HighScores
{
    const string SavePath = "user://highscores.json";
    public const int ShownCount = 10;               // rows on the board (and what it takes to qualify)
    const int KeptCount = 50;                       // how many scores are remembered in total
    const int MaxNameLength = 12;

    public static string LastName = "";             // prefilled in the name box next time
    public static long LastAddedId = -1;            // the board highlights this entry once

    static List<ScoreEntry> cache;

    public static List<ScoreEntry> All()
    {
        Load();
        return cache;
    }

    public static List<ScoreEntry> Top(int count = ShownCount) => All().Take(count).ToList();

    public static int Best()
    {
        var all = All();
        return all.Count > 0 ? all[0].Score : 0;
    }

    /// <summary>Does this score earn a spot on the board?</summary>
    public static bool Qualifies(int score)
    {
        if (score <= 0) return false;
        var all = All();
        return all.Count < ShownCount || score > all[ShownCount - 1].Score;
    }

    /// <summary>Adds a score and returns its rank on the board (1 = first place).</summary>
    public static int Add(string name, int score, string place, string character)
    {
        Load();

        name = (name ?? "").Trim();
        if (name.Length == 0) name = "Anonymous";
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);

        var entry = new ScoreEntry
        {
            Name = name,
            Score = score,
            Place = place,
            Character = character,
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            Id = DateTime.Now.Ticks
        };

        cache.Add(entry);
        cache = Sorted(cache).Take(KeptCount).ToList();

        LastName = name;
        LastAddedId = entry.Id;
        Save();
        return cache.FindIndex(e => e.Id == entry.Id) + 1;
    }

    static IEnumerable<ScoreEntry> Sorted(IEnumerable<ScoreEntry> list) =>
        list.OrderByDescending(e => e.Score).ThenBy(e => e.Id);   // equal scores: the earlier one stays ahead

    static void Load()
    {
        if (cache != null) return;
        cache = new List<ScoreEntry>();

        try
        {
            if (!FileAccess.FileExists(SavePath)) return;
            var list = JsonSerializer.Deserialize<List<ScoreEntry>>(FileAccess.GetFileAsString(SavePath));
            if (list != null) cache = Sorted(list).ToList();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[HighScores] couldn't read {SavePath}: {ex.Message}");
            cache = new List<ScoreEntry>();
        }
    }

    static void Save()
    {
        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[HighScores] couldn't write {SavePath}");
                return;
            }
            file.StoreString(JsonSerializer.Serialize(cache));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[HighScores] couldn't save: {ex.Message}");
        }
    }
}
