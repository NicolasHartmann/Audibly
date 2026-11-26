using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace Audibly.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBarIcons();
    }

    // the activity stays when the phone switches between light and dark (ConfigChanges.UiMode)
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplySystemBarIcons();
    }

    // The pages reach under the status and navigation bars (edge to edge, enforced from Android 15),
    // so the bars show the page's background: dark icons on the light theme, light ones on the dark.
    private void ApplySystemBarIcons()
    {
        if (Window is not { } window) return;
        var dark = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        controller.AppearanceLightStatusBars = !dark;
        controller.AppearanceLightNavigationBars = !dark;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        // the folder dialog of the library importer
        if (requestCode == AndroidLibraryImporter.PickFolderRequest)
            AndroidLibraryImporter.OnFolderPicked(resultCode == Result.Ok ? data?.Data : null);

        // the folder dialog of the sync settings
        if (requestCode == AndroidSyncFolderAccess.PickFolderRequest)
            AndroidSyncFolderAccess.OnFolderPicked(resultCode == Result.Ok ? data?.Data : null);
    }
}
