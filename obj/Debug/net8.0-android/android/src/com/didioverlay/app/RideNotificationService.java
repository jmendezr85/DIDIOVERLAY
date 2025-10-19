package com.didioverlay.app;


public class RideNotificationService
	extends android.service.notification.NotificationListenerService
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onListenerConnected:()V:GetOnListenerConnectedHandler\n" +
			"n_onListenerDisconnected:()V:GetOnListenerDisconnectedHandler\n" +
			"n_onNotificationPosted:(Landroid/service/notification/StatusBarNotification;)V:GetOnNotificationPosted_Landroid_service_notification_StatusBarNotification_Handler\n" +
			"";
		mono.android.Runtime.register ("DidiOverlay.Platforms.Android.Services.RideNotificationService, DidiOverlay", RideNotificationService.class, __md_methods);
	}


	public RideNotificationService ()
	{
		super ();
		if (getClass () == RideNotificationService.class) {
			mono.android.TypeManager.Activate ("DidiOverlay.Platforms.Android.Services.RideNotificationService, DidiOverlay", "", this, new java.lang.Object[] {  });
		}
	}


	public void onListenerConnected ()
	{
		n_onListenerConnected ();
	}

	private native void n_onListenerConnected ();


	public void onListenerDisconnected ()
	{
		n_onListenerDisconnected ();
	}

	private native void n_onListenerDisconnected ();


	public void onNotificationPosted (android.service.notification.StatusBarNotification p0)
	{
		n_onNotificationPosted (p0);
	}

	private native void n_onNotificationPosted (android.service.notification.StatusBarNotification p0);

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
