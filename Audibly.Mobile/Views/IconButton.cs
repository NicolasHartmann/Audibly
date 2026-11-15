using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Audibly.Mobile.Views;

/// <summary>
///     A button that shows one of the drawn symbols. The touch area is larger than the symbol, and
///     the symbol follows the light or dark theme.
/// </summary>
public sealed class IconButton : ContentView
{
    private static readonly Brush Light = new SolidColorBrush(Color.FromArgb("#1F1F1F"));
    private static readonly Brush Dark = new SolidColorBrush(Colors.White);

    private readonly Path _path = new()
    {
        Aspect = Stretch.Uniform,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        HeightRequest = 26,
        WidthRequest = 26
    };

    private bool _outline;

    public IconButton()
    {
        Padding = 10;
        MinimumHeightRequest = 48;
        MinimumWidthRequest = 48;
        Content = _path;
        ApplyBrush();

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        GestureRecognizers.Add(tap);
    }

    public event EventHandler? Clicked;

    /// <summary>The symbol, one of <see cref="PlayerIcons" />.</summary>
    public Geometry? Data
    {
        get => _path.Data;
        set => _path.Data = value;
    }

    /// <summary>Height of the symbol; its width follows from its shape.</summary>
    public double IconSize
    {
        get => _path.HeightRequest;
        set
        {
            _path.HeightRequest = value;
            _path.WidthRequest = value * 1.3;
        }
    }

    /// <summary>True for symbols that are lines rather than filled shapes.</summary>
    public bool Outline
    {
        get => _outline;
        set
        {
            _outline = value;
            ApplyBrush();
        }
    }

    private void ApplyBrush()
    {
        _path.StrokeThickness = _outline ? 2 : 0;
        _path.SetAppTheme(_outline ? Shape.StrokeProperty : Shape.FillProperty, Light, Dark);
        if (_outline) _path.Fill = null;
        else _path.Stroke = null;
    }
}
