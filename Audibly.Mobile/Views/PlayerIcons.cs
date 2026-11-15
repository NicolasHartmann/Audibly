using Microsoft.Maui.Controls.Shapes;

namespace Audibly.Mobile.Views;

/// <summary>
///     The player's symbols as shapes. Text glyphs for them render as coloured emoji on some
///     phones, so the symbols are drawn instead. All are drawn in a 24 x 24 box unless noted.
/// </summary>
public static class PlayerIcons
{
    // the triangle sits slightly right so it looks centred in a circle
    public static readonly Geometry Play = Parse("M7,3 L7,21 L22,12 Z");
    public static readonly Geometry Pause = Parse("M5,3 H10 V21 H5 Z M14,3 H19 V21 H14 Z");

    // three dots in a column, for the menu of a book in the library
    public static readonly Geometry More = Parse(
        "M10,4 A2,2 0 1 0 14,4 A2,2 0 1 0 10,4 Z M10,12 A2,2 0 1 0 14,12 A2,2 0 1 0 10,12 Z M10,20 A2,2 0 1 0 14,20 A2,2 0 1 0 10,20 Z");

    public static readonly Geometry Menu = Parse("M3,6 H21 V8 H3 Z M3,11 H21 V13 H3 Z M3,16 H21 V18 H3 Z");
    public static readonly Geometry PreviousChapter = Parse("M19,4 L9,12 L19,20 Z M5,4 H7.5 V20 H5 Z");
    public static readonly Geometry NextChapter = Parse("M5,4 L15,12 L5,20 Z M16.5,4 H19 V20 H16.5 Z");
    public static readonly Geometry Rewind = Parse("M21,5 L13,12 L21,19 Z M12,5 L4,12 L12,19 Z");
    public static readonly Geometry Forward = Parse("M3,5 L11,12 L3,19 Z M12,5 L20,12 L12,19 Z");

    // 28 wide: three triangles for the long jump
    public static readonly Geometry RewindFar = Parse("M27,5 L19,12 L27,19 Z M18,5 L10,12 L18,19 Z M9,5 L1,12 L9,19 Z");
    public static readonly Geometry ForwardFar = Parse("M1,5 L9,12 L1,19 Z M10,5 L18,12 L10,19 Z M19,5 L27,12 L19,19 Z");

    // three books on a shelf
    public static readonly Geometry Library =
        Parse("M4,4 H7 V18 H4 Z M9,4 H12 V18 H9 Z M14.2,5 L17,4.3 L20.5,17.6 L17.7,18.3 Z M3,20 H21 V22 H3 Z");

    // drawn as an outline: a clock face with its hands
    public static readonly Geometry Timer = Parse("M12,3 A9,9 0 1 1 11.99,3 M12,7 V12 L15.5,14");

    private static Geometry Parse(string data) =>
        (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;
}
