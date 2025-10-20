
#if ANDROID
using System;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Core.App;

using Com.Google.Mlkit.Vision.Common;
using Com.Google.Mlkit.Vision.Text;
using Com.Google.Mlkit.Vision.Text.Latin;

namespace DidiOverlay.Platforms.Android.Services
{
    [Service(
        Name = "com.didioverlay.app.ScreenOcrService",
        Exported = false,
        ForegroundServiceType = Android.Content.PM.ForegroundService.TypeMediaProjection)]
    public class ScreenOcrService : Service
    {
        public const string ActionStartProjection = "com.didioverlay.action.START_PROJECTION";
        public const string ActionForceOcr = "com.didioverlay.action.FORCE_OCR";
        public const string ExtraResultCode = "extra_result_code";
        public const string ExtraResultData = "extra_result_data";

        // Broadcast al OverlayService
        const string ACTION_OFFER = "com.didioverlay.ACTION_OFFER";
        const string TAG = "DidiOverlayOCR";
        const string CHANNEL_ID = "screen_ocr";
        const int NOTIF_ID = 9912;

        // Extras esperados por OverlayService
        const string EXTRA_FARE_COP = "fareCop";
        const string EXTRA_MINUTES = "minutes";
        const string EXTRA_PICKUP_KM = "pickupKm";
        const string EXTRA_TRIP_KM = "tripKm";

        // ROI relativo (ajustable)
        const float ROI_TOP = 0.50f;
        const float ROI_HEIGHT = 0.45f;
        const float ROI_LEFT = 0.05f;
        const float ROI_WIDTH = 0.90f;

        const int TargetWidth = 720;
        const int MinOcrIntervalMs = 700;

        MediaProjection? _projection;
        VirtualDisplay? _virtualDisplay;
        ImageReader? _imageReader;
        HandlerThread? _handlerThread;
        Handler? _bgHandler;
        TextRecognizer? _recognizer;
        long _lastOcrTs;
        bool _started;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            var action = intent?.Action;
            if (action == ActionStartProjection)
            {
                StartAsForeground();
                SetupProjection(intent!);
                return StartCommandResult.Sticky;
            }
            else if (action == ActionForceOcr)
            {
                _lastOcrTs = 0;
                Log.Info(TAG, "Force OCR");
                return StartCommandResult.Sticky;
            }

            return StartCommandResult.Sticky;
        }

        void StartAsForeground()
        {
            CreateChannel();
            var notif = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("OCR de pantalla activo")
                .SetContentText("Leyendo ofertas de DiDi…")
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetOngoing(true)
                .Build();

            StartForeground(NOTIF_ID, notif, Android.Content.PM.ForegroundService.MediaProjection);
        }

        void CreateChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var nm = (NotificationManager)GetSystemService(NotificationService)!;
                var ch = new NotificationChannel(CHANNEL_ID, "Screen OCR", NotificationImportance.Low)
                {
                    Description = "Servicio para OCR de pantalla"
                };
                nm.CreateNotificationChannel(ch);
            }
        }

        void SetupProjection(Intent dataIntent)
        {
            try
            {
                int resultCode = dataIntent.GetIntExtra(ExtraResultCode, (int)Result.Canceled);
                var resultData = (Intent?)dataIntent.GetParcelableExtra(ExtraResultData);
                if (resultCode != (int)Result.Ok || resultData == null) { StopSelf(); return; }

                var mpm = (MediaProjectionManager)GetSystemService(MediaProjectionService)!;
                _projection = mpm.GetMediaProjection(resultCode, resultData);

                _handlerThread = new HandlerThread("ocr-bg");
                _handlerThread.Start();
                _bgHandler = new Handler(_handlerThread.Looper!);

                var dm = Resources!.DisplayMetrics!;
                int width = dm.WidthPixels;
                int height = dm.HeightPixels;
                int density = (int)dm.DensityDpi;

                _imageReader = ImageReader.NewInstance(width, height, ImageFormatType.Rgba8888, 2);
                _imageReader.SetOnImageAvailableListener(new ImageAvailableListener(this), _bgHandler);

                _virtualDisplay = _projection.CreateVirtualDisplay(
                    "DidiOverlayVirtualDisplay",
                    width, height, density, DisplayFlags.AutoMirror,
                    _imageReader.Surface, null, _bgHandler);

                _recognizer = TextRecognition.GetClient(new TextRecognizerOptions.Builder().Build());

                _started = true;
                Log.Info(TAG, $"VirtualDisplay {width}x{height} creado");
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"SetupProjection error: {ex}");
                StopSelf();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            try { _virtualDisplay?.Release(); } catch { }
            try { _imageReader?.Close(); } catch { }
            try { _projection?.Stop(); } catch { }
            try { _recognizer?.Close(); } catch { }
            _bgHandler?.Looper?.QuitSafely();
            _handlerThread?.QuitSafely();
            Log.Info(TAG, "ScreenOcrService destruido");
        }

        async Task HandleImageAsync(Image image)
        {
            long now = Java.Lang.JavaSystem.CurrentTimeMillis();
            if (now - _lastOcrTs < MinOcrIntervalMs) { image.Close(); return; }
            _lastOcrTs = now;

            try
            {
                var plane = image.GetPlanes()[0];
                var buffer = plane.Buffer;
                int pixelStride = plane.PixelStride;
                int rowStride = plane.RowStride;
                int width = image.Width;
                int height = image.Height;

                int paddedWidth = rowStride / pixelStride;
                var fullBmp = Bitmap.CreateBitmap(paddedWidth, height, Bitmap.Config.Argb8888);
                buffer.Rewind();
                fullBmp.CopyPixelsFromBuffer(buffer);
                var cleanBmp = Bitmap.CreateBitmap(fullBmp, 0, 0, width, height);
                fullBmp.Recycle();

                var roi = new Rect(
                    (int)(width * ROI_LEFT),
                    (int)(height * ROI_TOP),
                    (int)(width * (ROI_LEFT + ROI_WIDTH)),
                    (int)(height * (ROI_TOP + ROI_HEIGHT))
                );

                int w = roi.Width();
                int h = roi.Height();
                if (w <= 0 || h <= 0) { cleanBmp.Recycle(); image.Close(); return; }

                var roiBmp = Bitmap.CreateBitmap(cleanBmp, roi.Left, roi.Top, w, h);
                cleanBmp.Recycle();

                if (roiBmp.Width > TargetWidth)
                {
                    int scaledH = (int)(roiBmp.Height * (TargetWidth / (float)roiBmp.Width));
                    var scaled = Bitmap.CreateScaledBitmap(roiBmp, TargetWidth, Math.Max(1, scaledH), true);
                    roiBmp.Recycle();
                    roiBmp = scaled;
                }

                var input = InputImage.FromBitmap(roiBmp, 0);
                var result = await _recognizer!.Process(input);
                roiBmp.Recycle();

                var text = result?.Text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    Log.Debug(TAG, $"OCR → {text.Replace('\n',' ')}");
                    if (Ocr.OcrOfferParser.TryParse(text!, out var fare, out var minutes, out var kmTotal))
                    {
                        // Heurística: asumir pickup 1/3 y trip 2/3 del total si no tenemos separación.
                        double pickupKm = Math.Round(kmTotal * 0.35, 2);
                        double tripKm   = Math.Round(kmTotal - pickupKm, 2);

                        var broadcast = new Intent(ACTION_OFFER)
                            .PutExtra(EXTRA_FARE_COP, fare)
                            .PutExtra(EXTRA_MINUTES, minutes)
                            .PutExtra(EXTRA_PICKUP_KM, pickupKm)
                            .PutExtra(EXTRA_TRIP_KM, tripKm);
                        SendBroadcast(broadcast);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"OCR frame error: {ex.Message}");
            }
            finally
            {
                try { image.Close(); } catch { }
            }
        }

        class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            readonly ScreenOcrService _svc;
            public ImageAvailableListener(ScreenOcrService svc) => _svc = svc;
            public void OnImageAvailable(ImageReader reader)
            {
                if (!_svc._started) return;
                try
                {
                    using var img = reader.AcquireLatestImage();
                    if (img == null) return;
                    _ = _svc.HandleImageAsync(img);
                }
                catch (Exception ex)
                {
                    Log.Warn(TAG, $"OnImageAvailable: {ex.Message}");
                }
            }
        }
    }
}
#endif
