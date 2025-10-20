package com.didioverlay.app;


public class ScreenOcrService_ImageListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.media.ImageReader.OnImageAvailableListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onImageAvailable:(Landroid/media/ImageReader;)V:GetOnImageAvailable_Landroid_media_ImageReader_Handler:Android.Media.ImageReader/IOnImageAvailableListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("DidiOverlay.Platforms.Android.Services.ScreenOcrService+ImageListener, DidiOverlay", ScreenOcrService_ImageListener.class, __md_methods);
	}


	public ScreenOcrService_ImageListener ()
	{
		super ();
		if (getClass () == ScreenOcrService_ImageListener.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.ScreenOcrService+ImageListener, DidiOverlay", "", this, new java.lang.Object[] {  });
		}
	}

	public ScreenOcrService_ImageListener (com.didioverlay.app.ScreenOcrService p0, int p1, int p2)
	{
		super ();
		if (getClass () == ScreenOcrService_ImageListener.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.ScreenOcrService+ImageListener, DidiOverlay", "DidiOverlay.Platforms.Android.Services.ScreenOcrService, DidiOverlay:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
		}
	}


	public void onImageAvailable (android.media.ImageReader p0)
	{
		n_onImageAvailable (p0);
	}

	private native void n_onImageAvailable (android.media.ImageReader p0);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
