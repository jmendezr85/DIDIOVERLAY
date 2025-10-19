package com.didioverlay.app;


public class OverlayService_ActionReceiver
	extends android.content.BroadcastReceiver
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onReceive:(Landroid/content/Context;Landroid/content/Intent;)V:GetOnReceive_Landroid_content_Context_Landroid_content_Intent_Handler\n" +
			"";
		mono.android.Runtime.register ("DidiOverlay.Platforms.Android.Services.OverlayService+ActionReceiver, DidiOverlay", OverlayService_ActionReceiver.class, __md_methods);
	}


	public OverlayService_ActionReceiver ()
	{
		super ();
		if (getClass () == OverlayService_ActionReceiver.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.OverlayService+ActionReceiver, DidiOverlay", "", this, new java.lang.Object[] {  });
		}
	}

	public OverlayService_ActionReceiver (com.didioverlay.app.OverlayService p0)
	{
		super ();
		if (getClass () == OverlayService_ActionReceiver.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.OverlayService+ActionReceiver, DidiOverlay", "DidiOverlay.Platforms.Android.Services.OverlayService, DidiOverlay", this, new java.lang.Object[] { p0 });
		}
	}


	public void onReceive (android.content.Context p0, android.content.Intent p1)
	{
		n_onReceive (p0, p1);
	}

	private native void n_onReceive (android.content.Context p0, android.content.Intent p1);

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
