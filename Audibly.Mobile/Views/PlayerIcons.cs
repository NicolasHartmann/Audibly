using Microsoft.Maui.Controls.Shapes;

namespace Audibly.Mobile.Views;

/// <summary>
///     The play and pause symbols as shapes. Text glyphs for them render as coloured emoji on some
///     phones, so the symbols are drawn instead.
/// </summary>
public static class PlayerIcons
{
    // both are drawn in a 24 x 24 box; the triangle sits slightly right so it looks centred in a circle
    public static readonly Geometry Play = Parse("M7,3 L7,21 L22,12 Z");
    public static readonly Geometry Pause = Parse("M5,3 H10 V21 H5 Z M14,3 H19 V21 H14 Z");

    private static Geometry Parse(string data) =>
        (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;
}
