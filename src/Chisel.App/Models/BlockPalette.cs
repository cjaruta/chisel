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

    // More looks, all on the same eight keys; fills are a tint of the stripe over the dark card.
    public static BlockPalette Pastel { get; } = new("pastel", "Pastel", Color.Parse("#D4D7DD"), new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#9DB4E8"), Color.Parse("#40485B"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#A9D8A0"), Color.Parse("#435149"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#8FDCD6"), Color.Parse("#3C5257"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#F2D48A"), Color.Parse("#565043"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#F2A58E"), Color.Parse("#564444"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#F2A9C4"), Color.Parse("#564552"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#CDB4F0"), Color.Parse("#4C485D"), Color.Parse("#E6E8EC")),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#CFCAC2"), Color.Parse("#4D4E52"), Color.Parse("#E6E8EC")),
    });

    public static BlockPalette Earth { get; } = new("earth", "Earth", Color.Parse("#CFC7BC"), new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#8A9BB0"), Color.Parse("#3F4652"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#8F9F6B"), Color.Parse("#41483E"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#5F9E8F"), Color.Parse("#324748"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#C9964A"), Color.Parse("#524534"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#C0693F"), Color.Parse("#4F3730"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#BE7C72"), Color.Parse("#4F3D40"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#9A859F"), Color.Parse("#44404D"), Color.Parse("#ECE6DF")),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#A39A8A"), Color.Parse("#474647"), Color.Parse("#ECE6DF")),
    });

    public static BlockPalette Coastal { get; } = new("coastal", "Coastal", Color.Parse("#C4D0D8"), new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#5B8DB8"), Color.Parse("#314255"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#6BB59A"), Color.Parse("#364E4C"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#3FB3C7"), Color.Parse("#294E59"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#E3C46A"), Color.Parse("#5A533D"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#E08A6B"), Color.Parse("#59413E"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#D98AA8"), Color.Parse("#574150"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#7F8FD6"), Color.Parse("#3C435E"), Color.Parse("#EAF0F4")),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#8FA3B0"), Color.Parse("#414952"), Color.Parse("#EAF0F4")),
    });

    public static BlockPalette Jewel { get; } = new("jewel", "Jewel", Color.Parse("#D2D4DC"), new[]
    {
        new BlockSwatch(BlockColor.Slate, "Slate blue", Color.Parse("#5B84E6"), Color.Parse("#33436A"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Sage, "Sage green", Color.Parse("#34B37A"), Color.Parse("#265345"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Teal, "Dusty teal", Color.Parse("#1FB5B0"), Color.Parse("#1F5458"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Amber, "Muted amber", Color.Parse("#E6A817"), Color.Parse("#635024"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Terracotta, "Terracotta", Color.Parse("#E0533D"), Color.Parse("#613330"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Rose, "Dusty rose", Color.Parse("#D9468F"), Color.Parse("#5E2E4C"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Lavender, "Lavender", Color.Parse("#9A5BE0"), Color.Parse("#493568"), Color.Parse("#F2F2F6")),
        new BlockSwatch(BlockColor.Gray, "Warm gray", Color.Parse("#9AA0B0"), Color.Parse("#494D58"), Color.Parse("#F2F2F6")),
    });

    public static IReadOnlyList<BlockPalette> All { get; } = new[] { Minimal, Saturated, Pastel, Earth, Coastal, Jewel };

    // Unknown or missing ids get Default Minimal.
    public static BlockPalette Get(string? id) => All.FirstOrDefault(p => p.Id == id) ?? Minimal;
}
