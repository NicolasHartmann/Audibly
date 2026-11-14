using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Audibly.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        // the folder dialog of the library importer
        if (requestCode == AndroidLibraryImporter.PickFolderRequest)
            AndroidLibraryImporter.OnFolderPicked(resultCode == Result.Ok ? data?.Data : null);
    }
}
