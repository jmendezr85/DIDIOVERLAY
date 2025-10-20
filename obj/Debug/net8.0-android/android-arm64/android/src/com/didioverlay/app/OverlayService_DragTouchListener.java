package com.didioverlay.app;


public class OverlayService_DragTouchListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.view.View.OnTouchListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onTouch:(Landroid/view/View;Landroid/view/MotionEvent;)Z:GetOnTouch_Landroid_view_View_Landroid_view_MotionEvent_Handler:Android.Views.View/IOnTouchListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("DidiOverlay.Platforms.Android.Services.OverlayService+DragTouchListener, DidiOverlay", OverlayService_DragTouchListener.class, __md_methods);
	}


	public OverlayService_DragTouchListener ()
	{
		super ();
		if (getClass () == OverlayService_DragTouchListener.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.OverlayService+DragTouchListener, DidiOverlay", "", this, new java.lang.Object[] {  });
		}
	}


	public boolean onTouch (android.view.View p0, android.view.MotionEvent p1)
	{
		return n_onTouch (p0, p1);
	}

	private native boolean n_onTouch (android.view.View p0, android.view.MotionEvent p1);

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
