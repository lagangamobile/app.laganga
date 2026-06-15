using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace com.laganga.app
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
            ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, true);
            /*
            // Para Android 5.0+, respetar los insets de la barra de estado
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Window?.ClearFlags(WindowManagerFlags.TranslucentStatus);
                Window?.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
            }
            */
        }
    }
}