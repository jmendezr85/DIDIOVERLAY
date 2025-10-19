#if ANDROID
using global::Android.App;        // atributos [BroadcastReceiver], [IntentFilter]
using global::Android.Content;
using global::Android.Util;
using global::Android.Widget;

namespace DidiOverlay.Platforms.Android.Receivers;

[BroadcastReceiver(Enabled = true, Exported = true)]
[global::Android.App.IntentFilter(new[] { "com.didioverlay.ACTION_OFFER" })]
public class OfferReceiver : BroadcastReceiver
{
    const string TAG = "DidiOverlayOffer";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var fare   = intent.GetIntExtra("fareCop", 0);
        var mins   = intent.GetIntExtra("minutes", 0);
        var pickup = intent.GetFloatExtra("pickupKm", 0);
        var trip   = intent.GetFloatExtra("tripKm", 0);

        var msg = $"COP {fare} · {mins} min · pick {pickup:0.##} km · trip {trip:0.##} km";
        Log.Debug(TAG, msg);
        Toast.MakeText(context, msg, ToastLength.Long).Show();

        // Aquí luego avisaremos al OverlayService/UI si hace falta.
    }
}
#endif
