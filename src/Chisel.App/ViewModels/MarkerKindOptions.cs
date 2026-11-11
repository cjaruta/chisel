using System;
using System.Collections.Generic;
using Chisel.Core.Schedule;

namespace Chisel.App.ViewModels;

public static class MarkerKindOptions
{
    public static IReadOnlyList<MarkerKind> All { get; } = Enum.GetValues<MarkerKind>();
}
