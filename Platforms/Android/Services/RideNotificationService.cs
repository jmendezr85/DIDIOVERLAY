#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Android.Util;

namespace DidiOverlay.Platforms.Android.Services;

[Service(
    Exported = false,
    Name = "com.didioverlay.RideNotificationService",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE"
)]
public class RideNotificationService : NotificationListenerService
{
    const string TAG = "DidiOverlayNotif";

    // Acción y extras que tu OverlayService ya consume
    public const string ActionOffer = "com.didioverlay.ACTION_OFFER";
    public const string ExtraFareCop = "fareCop";
    public const string ExtraMinutes = "minutes";
    public const string ExtraPickupKm = "pickupKm";
    public const string ExtraTripKm = "tripKm";

    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        Log.Info(TAG, "Listener conectado ✅");
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        base.OnNotificationPosted(sbn!);

        try
        {
            if (sbn == null) return;

            var pkg = sbn.PackageName ?? "(sin pkg)";
            var n = sbn.Notification;

            string? title = n?.Extras?.GetString(Notification.ExtraTitle);
            string? text = n?.Extras?.GetCharSequence(Notification.ExtraText)?.ToString();
            string? bigText = n?.Extras?.GetCharSequence(Notification.ExtraBigText)?.ToString();
            string? channel = n?.ChannelId;

            Log.Info(TAG, $"POSTED | pkg={pkg} | ch={channel} | title={title} | text={(text ?? bigText) ?? "(sin texto)"}");

            // Si es nuestra notificación de prueba, emite una oferta “dummy” al overlay
            if (channel == "didi_overlay_test")
            {
                EmitDummyOfferBroadcast();
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn(TAG, $"OnNotificationPosted error: {ex.Message}");
        }
    }

    public override void OnNotificationRemoved(StatusBarNotification? sbn)
    {
        base.OnNotificationRemoved(sbn!);
        try
        {
            if (sbn == null) return;
            var pkg = sbn.PackageName ?? "(sin pkg)";
            Log.Info(TAG, $"REMOVED | pkg={pkg}");
        }
        catch { /* noop */ }
    }

    void EmitDummyOfferBroadcast()
    {
        try
        {
            // Valores de ejemplo para que tu OverlayService muestre la tarjeta
            var intent = new Intent(ActionOffer)
                .PutExtra(ExtraFareCop, 6500)   // COP
                .PutExtra(ExtraMinutes, 8)      // minutos
                .PutExtra(ExtraPickupKm, 1.2)   // km hasta recoger
                .PutExtra(ExtraTripKm, 3.4);    // km del viaje

            SendBroadcast(intent);
            Log.Info(TAG, "Broadcast ACTION_OFFER enviado (dummy).");
        }
        catch (System.Exception ex)
        {
            Log.Warn(TAG, $"EmitDummyOfferBroadcast error: {ex.Message}");
        }
    }
}
#endif
