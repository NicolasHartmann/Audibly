using Audibly.Mobile.Services;

namespace Audibly.Mobile;

public partial class MainPage : ContentPage
{
    private readonly AppData _data;

    public MainPage(AppData data)
    {
        InitializeComponent();
        _data = data;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var books = (await _data.Repository.Audiobooks.GetAsync()).ToList();
            StatusLabel.Text = books.Count == 1 ? "1 audiobook" : $"{books.Count} audiobooks";
        }
        catch (Exception e)
        {
            StatusLabel.Text = "The library could not be opened: " + e.Message;
        }
    }
}
