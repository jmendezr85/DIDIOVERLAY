package com.didioverlay.app;


public class OrderAccessibilityService
	extends android.accessibilityservice.AccessibilityService
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onServiceConnected:()V:GetOnServiceConnectedHandler\n" +
			"n_onInterrupt:()V:GetOnInterruptHandler\n" +
			"n_onAccessibilityEvent:(Landroid/view/accessibility/AccessibilityEvent;)V:GetOnAccessibilityEvent_Landroid_view_accessibility_AccessibilityEvent_Handler\n" +
			"n_onUnbind:(Landroid/content/Intent;)Z:GetOnUnbind_Landroid_content_Intent_Handler\n" +
			"";
		mono.android.Runtime.register ("DidiOverlay.Platforms.Android.Services.OrderAccessibilityService, DidiOverlay", OrderAccessibilityService.class, __md_methods);
	}


	public OrderAccessibilityService ()
	{
		super ();
		if (getClass () == OrderAccessibilityService.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.OrderAccessibilityService, DidiOverlay", "", this, new java.lang.Object[] {  });
		}
	}


	public void onServiceConnected ()
	{
		n_onServiceConnected ();
	}

	private native void n_onServiceConnected ();


	public void onInterrupt ()
	{
		n_onInterrupt ();
	}

	private native void n_onInterrupt ();


	public void onAccessibilityEvent (android.view.accessibility.AccessibilityEvent p0)
	{
		n_onAccessibilityEvent (p0);
	}

	private native void n_onAccessibilityEvent (android.view.accessibility.AccessibilityEvent p0);


	public boolean onUnbind (android.content.Intent p0)
	{
		return n_onUnbind (p0);
	}

	private native boolean n_onUnbind (android.content.Intent p0);

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
