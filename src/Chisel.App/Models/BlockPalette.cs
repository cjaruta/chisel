using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Chisel.Core.Schedule;

namespace Chisel.App.Models;

// How one BlockColor key looks in a palette. Stripe is the solid left edge and the swatch,
// Fill is the block background (a tint of the stripe over the card), Text is the block title.
public sealed record BlockSwatch(string Key, string Name, Color Stripe, Color Fill, Color Text);

// A set of block colors: one swatch per BlockColor key, plus the color for the block's time line.
public sealed class BlockPalette
{
    private readonly Dictionary<string, BlockSwatch> _byKey;

    public BlockPalette(string id, string name, Color timeText, IReadOnlyList<BlockSwatch> swatches)
    {
        Id = id;
        Name = name;
        TimeText = timeText;
        Swatches = swatches;
        _byKey = swatches.ToDictionary(s => s.Key);
    }

    public string Id { get; }
    public string Name { get; }
    public Color TimeText { get; }
    public IReadOnlyList<BlockSwatch> Swatches { get; }

    // Unknown or missing keys get the default color's swatch.
    public BlockSwatch Get(string? key) => _byKey[BlockColor.Normalize(key)];
}

// Every palette the user can choose from. Adding one is just adding its data here.
public static class BlockPalettes
{
    private static readonly Color MinimalText = Color.Parse("#ECEEF2");
    private static readonly Color SaturatedText = Color.Parse("#F4F5F7");

    // Muted, low-saturation colors; fills are about a 24% tint over the card background.
    public static BlockPalette Minimal { get; } = new("minimal", "Default Minimal", Color.Parse("#BCC1CA"), new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#7D93C4"), Color.Parse("#363D4F"), MinimalText),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#8DB38A"), Color.Parse("#394541"), MinimalText),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#62AEB0"), Color.Parse("#2F444A"), MinimalText),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#D0A660"), Color.Parse("#494237"), MinimalText),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#CC7F66"), Color.Parse("#493838"), MinimalText),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#CC8BA0"), Color.Parse("#493B46"), MinimalText),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#A693CF"), Color.Parse("#3F3D52"), MinimalText),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#9C978F"), Color.Parse("#3D3E42"), MinimalText),
    });

    // The same hues, more vivid: saturated stripes and about a 32% tint for fills.
    public static BlockPalette Saturated { get; } = new("saturated", "Default Saturated", Color.Parse("#CDD1D8"), new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#4C8EF2"), Color.Parse("#2D456A"), SaturatedText),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#5FC46A"), Color.Parse("#33563E"), SaturatedText),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#2EC4C4"), Color.Parse("#24565B"), SaturatedText),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#F2B53A"), Color.Parse("#63512F"), SaturatedText),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#F07148"), Color.Parse("#623B34"), SaturatedText),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#EE6FA0"), Color.Parse("#613B50"), SaturatedText),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#AE73F2"), Color.Parse("#4D3C6A"), SaturatedText),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#B3AA9C"), Color.Parse("#4E4E4E"), SaturatedText),
    });

    public static IReadOnlyList<BlockPalette> All { get; } = new[] { Minimal, Saturated };

    // Unknown or missing ids get Default Minimal.
    public static BlockPalette Get(string? id) => All.FirstOrDefault(p => p.Id == id) ?? Minimal;
}
