
#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Media.Projection;
using Android.OS;
using Android.Runtime; // GeneratedEnum
using AndroidX.Core.Content;

namespace DidiOverlay.Platforms.Android.Activities;

[Activity(
    Name = "com.didioverlay.app.ScreenCaptureStarterActivity",
    Theme = "@style/Maui.SplashTheme",
    Exported = false,
    NoHistory = true,
    ExcludeFromRecents = true)]
public class ScreenCaptureStarterActivity : Activity
{
    const int RC_CAPTURE = 1001;
    const int RC_POST_NOTIF = 2001;
    const string TAG = "DidiOverlayOCR";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                RequestPermissions(new[] { Manifest.Permission.PostNotifications }, RC_POST_NOTIF);
                return;
            }
        }

        RequestCapture();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == RC_POST_NOTIF)
        {
            // Continuar aunque el usuario niegue; el SO podría permitir mostrar la notificación de FGS igual
            RequestCapture();
        }
    }

    void RequestCapture()
    {
        try
        {
            var mpm = (MediaProjectionManager)GetSystemService(MediaProjectionService)!;
            var intent = mpm.CreateScreenCaptureIntent();
            StartActivityForResult(intent, RC_CAPTURE);
        }
        catch
        {
            Finish();
        }
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RC_CAPTURE && resultCode == Result.Ok && data != null)
        {
            var svcIntent = new Intent(this, typeof(DidiOverlay.Platforms.Android.Services.ScreenOcrService))
                .SetAction(DidiOverlay.Platforms.Android.Services.ScreenOcrService.ActionStartProjection)
                .PutExtra(DidiOverlay.Platforms.Android.Services.ScreenOcrService.ExtraResultCode, (int)resultCode)
                .PutExtra(DidiOverlay.Platforms.Android.Services.ScreenOcrService.ExtraResultData, data);

            ContextCompat.StartForegroundService(this, svcIntent);
        }

        Finish();
    }
}
#endif
