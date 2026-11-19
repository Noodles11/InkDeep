using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OctoShoots.Core.Items;

/// <summary>A treasure (data/treasures.json, docs/proposals/CROSSOVER.md §5.2): what it is called, where it lies, what it does.</summary>
public sealed class TreasureDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Biome { get; set; } = "";
    /// <summary>The depth (as the player counts it) one of whose levels hides it.</summary>
    public int Depth { get; set; }
    /// <summary>What the banner says when she finds it.</summary>
    public string Line { get; set; } = "";
    /// <summary>What it does each run.</summary>
    public string Does { get; set; } = "";
}

/// <summary>The treasures the game knows how to give, and what they give.</summary>
public static class Treasures
{
    public const string PearlAltar = "pearl_altar", SpareBell = "spare_bell", ShellUrn = "shell_urn";
    public static readonly IReadOnlyList<string> Known = new[] { PearlAltar, SpareBell, ShellUrn };
    /// <summary>The Pearl Altar's offer; the Shell Urn's shells; the Spare Bell's half pips (a whole pip).</summary>
    public const int AltarPearls = 3, UrnShells = 15, BellHalves = 2;
}

/// <summary>The treasures, in their listed order, loaded from data/treasures.json and checked.</summary>
public sealed class TreasureCatalog
{
    sealed class FileModel
    {
        public List<TreasureDef> Treasures { get; set; } = new();
    }

    readonly Dictionary<string, TreasureDef> _byId;

    TreasureCatalog(List<TreasureDef> all)
    {
        All = all;
        _byId = all.ToDictionary(t => t.Id);
    }

    public IReadOnlyList<TreasureDef> All { get; }

    public bool TryGet(string id, out TreasureDef treasure) => _byId.TryGetValue(id, out treasure!);

    /// <summary>The treasure hidden on the depth's levels (as the player counts depths), or null.</summary>
    public TreasureDef? AtDepth(int depth) => All.FirstOrDefault(t => t.Depth == depth);

    /// <summary>Reads the file and checks it: ids unique and known (Treasures.Known), depths from 1, one treasure a depth.</summary>
    public static TreasureCatalog FromJson(string json)
    {
        var model = JsonSerializer.Deserialize<FileModel>(json, ItemCatalog.Json) ?? throw new InvalidOperationException("treasures.json is empty");
        var problems = new List<string>();
        foreach (var dup in model.Treasures.GroupBy(t => t.Id).Where(g => g.Count() > 1)) problems.Add($"treasure {dup.Key} listed twice");
        foreach (var dup in model.Treasures.GroupBy(t => t.Depth).Where(g => g.Count() > 1)) problems.Add($"two treasures at depth {dup.Key}");
        foreach (var t in model.Treasures)
        {
            if (!Treasures.Known.Contains(t.Id)) problems.Add($"{t.Id}: the game does not know this treasure");
            if (t.Depth < 1) problems.Add($"{t.Id}: depth must be 1 or more");
            if (string.IsNullOrWhiteSpace(t.Title) || string.IsNullOrWhiteSpace(t.Line) || string.IsNullOrWhiteSpace(t.Does)) problems.Add($"{t.Id}: title, line and does are required");
        }
        if (problems.Count > 0) throw new InvalidOperationException("treasures.json: " + string.Join("; ", problems));
        return new TreasureCatalog(model.Treasures);
    }
}
