using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Chisel.Core.Schedule;

namespace Chisel.App.Models;

// How each BlockColor key looks on the dark theme. Stripe is the solid left edge and the swatch.
// Fill is a subtle tint of the stripe over the card background, kept dark so block text stays readable.
public sealed record BlockSwatch(string Key, string Name, Color Stripe, Color Fill);

public static class BlockPalette
{
    public static IReadOnlyList<BlockSwatch> All { get; } = new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#7D93C4"), Color.Parse("#363D4F")),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#8DB38A"), Color.Parse("#394541")),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#62AEB0"), Color.Parse("#2F444A")),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#D0A660"), Color.Parse("#494237")),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#CC7F66"), Color.Parse("#493838")),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#CC8BA0"), Color.Parse("#493B46")),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#A693CF"), Color.Parse("#3F3D52")),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#9C978F"), Color.Parse("#3D3E42")),
    };

    private static readonly Dictionary<string, BlockSwatch> ByKey = All.ToDictionary(s => s.Key);

    // Unknown or missing keys get the default swatch.
    public static BlockSwatch Get(string? key) => ByKey[BlockColor.Normalize(key)];
}
