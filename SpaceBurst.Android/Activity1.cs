using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace SpaceBurst
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@drawable/icon",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
    public class Activity1 : AndroidGameActivity
    {
        private Game1 game;
        private View view;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            PlatformServices.Initialize(
                PlatformCapabilities.CreateAndroid(),
                PlatformServices.CreateDefaultFileStorageBackend(),
                PlatformServices.CreateDefaultTextAssetProvider(),
                PlatformServices.CreateImmediateAudioStartGate());
            game = new Game1();
            view = (View)game.Services.GetService(typeof(View));

            SetContentView(view);
            game.Run();
        }

        public override void OnBackPressed()
        {
            Input.NotifyAndroidBackPressed();
        }

        public override bool DispatchKeyEvent(KeyEvent e)
        {
            if (e != null && e.KeyCode == Keycode.Back && e.Action == KeyEventActions.Up)
            {
                Input.NotifyAndroidBackPressed();
                return true;
            }

            return base.DispatchKeyEvent(e);
        }
    }
}
