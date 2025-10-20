#if ANDROID
using System;

namespace DidiOverlay.Platforms.Android.Overlay
{
    /// <summary>
    /// Gestor de overlay para Android. Usa SIEMPRE tipos del SDK con el prefijo global::Android
    /// para evitar colisiones con el namespace DidiOverlay.Platforms.Android.
    /// </summary>
    public sealed class OverlayManager
    {
        private static readonly Lazy<OverlayManager> _instance = new(() => new OverlayManager());
        public static OverlayManager Instance => _instance.Value;

        private global::Android.Views.IWindowManager? _wm;
        private global::Android.Views.View? _overlayView;
        private global::Android.Views.WindowManagerLayoutParams? _lp;

        private OverlayManager() { }

        /// <summary>
        /// Muestra el overlay con LayoutParams por defecto (arriba, centrado).
        /// </summary>
        public void Show(global::Android.Content.Context ctx, global::Android.Views.View view)
        {
            if (_overlayView != null) return;

            _wm = (global::Android.Views.IWindowManager)ctx.GetSystemService(global::Android.Content.Context.WindowService);
            _overlayView = view;

            var type = global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O
                ? global::Android.Views.WindowManagerTypes.ApplicationOverlay
                : global::Android.Views.WindowManagerTypes.Phone;

            _lp = new global::Android.Views.WindowManagerLayoutParams(
                global::Android.Views.ViewGroup.LayoutParams.WrapContent,
                global::Android.Views.ViewGroup.LayoutParams.WrapContent,
                type,
                global::Android.Views.WindowManagerFlags.NotFocusable
                | global::Android.Views.WindowManagerFlags.NotTouchModal
                | global::Android.Views.WindowManagerFlags.LayoutNoLimits,
                global::Android.Graphics.Format.Translucent
            )
            {
                Gravity = global::Android.Views.GravityFlags.Top | global::Android.Views.GravityFlags.CenterHorizontal,
                X = 0,
                Y = 100
            };

            _wm!.AddView(_overlayView, _lp);
        }

        /// <summary>
        /// Muestra el overlay con LayoutParams personalizados.
        /// </summary>
        public void Show(global::Android.Content.Context ctx, global::Android.Views.View view, global::Android.Views.WindowManagerLayoutParams layoutParams)
        {
            if (_overlayView != null) return;

            _wm = (global::Android.Views.IWindowManager)ctx.GetSystemService(global::Android.Content.Context.WindowService);
            _overlayView = view;
            _lp = layoutParams;

            _wm!.AddView(_overlayView, _lp);
        }

        /// <summary>
        /// Actualiza la posición (x,y) del overlay.
        /// </summary>
        public void UpdatePosition(int x, int y)
        {
            if (_wm == null || _overlayView == null || _lp == null) return;
            _lp.X = x;
            _lp.Y = y;
            _wm.UpdateViewLayout(_overlayView, _lp);
        }

        /// <summary>
        /// Permite mutar los LayoutParams del overlay.
        /// </summary>
        public void UpdateLayout(Action<global::Android.Views.WindowManagerLayoutParams> mutate)
        {
            if (_wm == null || _overlayView == null || _lp == null) return;
            mutate(_lp);
            _wm.UpdateViewLayout(_overlayView, _lp);
        }

        /// <summary>
        /// Oculta y libera el overlay.
        /// </summary>
        public void Hide(global::Android.Content.Context ctx)
        {
            try
            {
                if (_wm != null && _overlayView != null)
                {
                    _wm.RemoveView(_overlayView);
                }
            }
            catch
            {
                // Ignorar excepciones al remover vistas que ya no están
            }
            finally
            {
                _overlayView = null;
                _lp = null;
                _wm = null;
            }
        }

        public bool IsShowing => _overlayView != null;

        public global::Android.Views.View? CurrentView => _overlayView;

        public global::Android.Views.WindowManagerLayoutParams? CurrentLayoutParams => _lp;
    }
}
#endif
