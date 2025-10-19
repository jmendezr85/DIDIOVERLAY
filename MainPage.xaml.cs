// MainPage.xaml.cs
using System;
using Microsoft.Maui.Controls;
using DidiOverlay.Platforms.Android.Services;

#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.App;
using Android.Service.Notification;          // RequestRebind
using AndroidX.Core.App;                    // NotificationManagerCompat
#endif

namespace DidiOverlay;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    async void OnOpenOverlayPermission(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var can = Settings.CanDrawOverlays(ctx);
                if (!can)
                {
                    var i = new Intent(Settings.ActionManageOverlayPermission);
                    i.SetFlags(ActivityFlags.NewTask);
                    ctx.StartActivity(i);
                    StatusLabel.Text = "Abriendo ajustes de superposición…";
                    return;
                }
            }
            StatusLabel.Text = "Permiso de superposición OK";
        }
        catch { StatusLabel.Text = "Error al abrir ajustes de superposición"; }
#else
        await DisplayAlert("Solo Android", "Este permiso es solo en Android.", "OK");
#endif
    }

    async void OnOpenNotificationAccess(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            var i = new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
            i.SetFlags(ActivityFlags.NewTask);
            ctx.StartActivity(i);
            StatusLabel.Text = "Abre y activa DidiOverlay en Acceso a notificaciones.";
        }
        catch { StatusLabel.Text = "Error al abrir ajustes de listener"; }
#else
        await DisplayAlert("Solo Android", "Este permiso es solo en Android.", "OK");
#endif
    }

    async void OnOpenAppNotificationSettings(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            var i = new Intent(Settings.ActionAppNotificationSettings);
            i.PutExtra(Settings.ExtraAppPackage, ctx.PackageName);
            i.SetFlags(ActivityFlags.NewTask);
            ctx.StartActivity(i);
            StatusLabel.Text = "Abriendo ajustes de notificaciones de la app…";
        }
        catch { StatusLabel.Text = "No se pudieron abrir los ajustes de la app"; }
#else
        await System.Threading.Tasks.Task.CompletedTask;
#endif
    }

    void OnStartOverlay(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            var intent = new Intent(ctx, typeof(OverlayService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                ctx.StartForegroundService(intent);
            else
                ctx.StartService(intent);

            StatusLabel.Text = "Overlay iniciado";
        }
        catch { StatusLabel.Text = "Error al iniciar Overlay"; }
#endif
    }

    void OnStopOverlay(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            var intent = new Intent(ctx, typeof(OverlayService));
            ctx.StopService(intent);
            StatusLabel.Text = "Overlay detenido";
        }
        catch { StatusLabel.Text = "Error al detener Overlay"; }
#endif
    }

    async void OnOpenSettings(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    void OnCheckListenerStatus(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            var enabledPkgs = NotificationManagerCompat.GetEnabledListenerPackages(ctx);
            bool enabled = enabledPkgs.Contains(ctx.PackageName);
            StatusLabel.Text = enabled ? "Listener HABILITADO" : "Listener NO habilitado (actívalo en ajustes)";
        }
        catch { StatusLabel.Text = "No se pudo verificar el estado del listener"; }
#endif
    }

    void OnRebindListener(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context!;
            // Usa el nombre EXACTO declarado en [Service(Name="com.didioverlay.app.RideNotificationService")]
            string pkg = ctx.PackageName; // "com.didioverlay.app"
            var comp = new ComponentName(pkg, "com.didioverlay.app.RideNotificationService");
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N) // API 24+
            {
                NotificationListenerService.RequestRebind(comp);
                StatusLabel.Text = "Solicitado rebind del listener. Si no reacciona, apaga/enciende el permiso.";
            }
            else
            {
                StatusLabel.Text = "Android < 7.0: haz toggle manual del permiso (des/activar).";
            }
        }
        catch
        {
            StatusLabel.Text = "No se pudo solicitar el rebind. Haz toggle manual del permiso.";
        }
#endif
    }

    void OnSendTestNotification(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            const string CH_ID = "test_channel";
            var ctx = Android.App.Application.Context!;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var nm = (NotificationManager)ctx.GetSystemService(Context.NotificationService)!;
                var ch = new NotificationChannel(CH_ID, "Pruebas", NotificationImportance.Default);
                nm.CreateNotificationChannel(ch);
            }

            string title = "Nueva oferta";
            string body  = "COP 14.000 · 12 minutos · recogida 0,8 km · viaje 4,5 km";

            var notif = new AndroidX.Core.App.NotificationCompat.Builder(ctx, CH_ID)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle().BigText(body))
                .Build();

            NotificationManagerCompat.From(ctx).Notify(2025, notif);
            StatusLabel.Text = "Notificación de prueba enviada (nota: el Listener SOLO procesa DiDi Conductor)";
        }
        catch { StatusLabel.Text = "No se pudo enviar la notificación de prueba"; }
#endif
    }
}
