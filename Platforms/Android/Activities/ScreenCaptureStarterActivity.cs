#if ANDROID
using global::Android.App;
using global::Android.Content;
using global::Android.OS;
using global::Android.Runtime;               // GeneratedEnum
using AToast = global::Android.Widget.Toast; // Alias a la clase Toast
using AToastLength = global::Android.Widget.ToastLength;

namespace DidiOverlay.Platforms.Android;

[Activity(
    Name = "com.didioverlay.app.ScreenCaptureStarterActivity",
    Theme = "@android:style/Theme.Translucent.NoTitleBar",
    Exported = false,
    TaskAffinity = "",
    ExcludeFromRecents = true,
    NoHistory = true
)]
public class ScreenCaptureStarterActivity : Activity
{
    const int REQ_MEDIA_PROJ = 5001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            var mgr = (global::Android.Media.Projection.MediaProjectionManager?)
                      GetSystemService(MediaProjectionService);
            if (mgr == null)
            {
                AToast.MakeText(this, "No se pudo obtener MediaProjectionManager", AToastLength.Long).Show();
                Finish(); return;
            }
            var intent = mgr.CreateScreenCaptureIntent();
            StartActivityForResult(intent, REQ_MEDIA_PROJ);
        }
        catch
        {
            AToast.MakeText(this, "Error al solicitar permiso de captura", AToastLength.Long).Show();
            Finish();
        }
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] global::Android.App.Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == REQ_MEDIA_PROJ)
        {
            if (resultCode == global::Android.App.Result.Ok && data != null)
            {
                var svc = new Intent(this, typeof(DidiOverlay.Platforms.Android.Services.ScreenOcrService));
                svc.PutExtra("mp.resultCode", (int)resultCode);
                svc.PutExtra("mp.data", data);
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O) StartForegroundService(svc);
                else StartService(svc);

                AToast.MakeText(this, "OCR de pantalla iniciado", AToastLength.Short).Show();
            }
            else
            {
                AToast.MakeText(this, "Permiso de captura cancelado", AToastLength.Long).Show();
            }
        }
        Finish();
    }
}
#endif
