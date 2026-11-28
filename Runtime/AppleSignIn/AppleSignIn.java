package com.binouze;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Intent;
import android.net.Uri;
import android.os.Looper;
import android.text.TextUtils;
import android.util.Log;

import androidx.browser.customtabs.CustomTabsCallback;
import androidx.browser.customtabs.CustomTabsClient;
import androidx.browser.customtabs.CustomTabsIntent;
import androidx.browser.customtabs.CustomTabsServiceConnection;
import androidx.browser.customtabs.CustomTabsSession;

import com.unity3d.player.UnityPlayer;

import org.json.JSONObject;

import java.net.URLEncoder;

public class AppleSignIn
{
    private static final String TAG = "AppleSignIn";

    private static String ClientID;
    private static String RedirectURI;
    private static String Scope;
    private static String UrlScheme;

    // request in flight (set by signIn, cleared when the response is sent or cancelled)
    private static final Object LOCK = new Object();
    private static boolean isActive;
    private static String  currentState;

    private static boolean isCurrent( String state )
    {
        synchronized( LOCK )
        {
            return isActive && state != null && state.equals( currentState );
        }
    }

    public static void init( String clientID, String redirectURI, String scope, String urlScheme )
    {
        Log.i( TAG, "init" );

        ClientID    = clientID;
        RedirectURI = redirectURI;
        Scope       = scope;
        UrlScheme   = urlScheme;
    }

    public static void signIn( final String state )
    {
        signIn( state, null );
    }

    /** redirectURI: Return URL for this request only, null/empty = the one given to init. */
    public static void signIn( final String state, final String redirectURI )
    {
        final String redirect = TextUtils.isEmpty( redirectURI ) ? RedirectURI : redirectURI;

        // set synchronously (Unity thread) so a cancel() issued right after is ordered correctly
        synchronized( LOCK )
        {
            isActive     = true;
            currentState = state;
        }

        final Activity activity = UnityPlayer.currentActivity;
        if( activity == null )
        {
            Log.e( TAG, "UnityPlayer.currentActivity is null in signIn" );
            SendError( "no current activity" );
            return;
        }

        activity.runOnUiThread( new Runnable()
        {
            @Override
            public void run()
            {
                try
                {
                    unbindCustomTabsService();

                    // cancelled or superseded before this runnable ran
                    if( !isCurrent( state ) )
                        return;

                    String url = buildAuthUrl( state, redirect );
                    String pkg = CustomTabsClient.getPackageName( activity, null );
                    if( pkg == null )
                    {
                        activity.startActivity( new Intent( Intent.ACTION_VIEW, Uri.parse( url ) ) );
                    }
                    else
                    {
                        bindCustomTabsService( activity, pkg, url, state );
                    }
                }
                catch( Exception e )
                {
                    Log.e( TAG, "signIn exception: " + e.getMessage() );
                    SendError( "EXCEPTION: " + e.getMessage() );
                }
            }
        } );
    }

    private static String buildAuthUrl( String state, String redirect ) throws Exception
    {
        return "https://appleid.apple.com/auth/authorize?response_type=code%20id_token&response_mode=form_post"
             + "&client_id="    + ClientID
             + "&scope="        + Scope
             + "&state="        + state
             + "&redirect_uri=" + URLEncoder.encode( redirect, "UTF-8" );
    }

    /** Called by AppleSignInActivity with the deep link opened by the redirect page. */
    public static void OnSignInResponse( String url )
    {
        try
        {
            if( url == null )
            {
                SendError( "empty response" );
                return;
            }

            Uri uri = Uri.parse( url );

            String error = uri.getQueryParameter( "error" );
            if( error != null && !error.isEmpty() )
            {
                if( error.equals( "user_cancelled_authorize" ) )
                    Send( stateOf( uri ), new JSONObject().put( "result", "canceled" ).put( "state", stateOf( uri ) ).toString() );
                else
                    Send( stateOf( uri ), new JSONObject().put( "result", "error" ).put( "state", stateOf( uri ) ).put( "message", error ).toString() );
                return;
            }

            String idToken = uri.getQueryParameter( "id_token" );
            if( idToken == null || idToken.isEmpty() )
            {
                Send( stateOf( uri ), new JSONObject().put( "result", "error" ).put( "state", stateOf( uri ) ).put( "message", "id_token not found" ).toString() );
                return;
            }

            String code = uri.getQueryParameter( "code" );
            String user = uri.getQueryParameter( "user" );
            Send( stateOf( uri ), new JSONObject()
                    .put( "result",   "success" )
                    .put( "state",    stateOf( uri ) )
                    .put( "id_token", idToken )
                    .put( "code",     code == null ? "" : code )
                    .put( "user",     user == null ? "" : user )
                    .toString() );
        }
        catch( Exception e )
        {
            Log.e( TAG, "OnSignInResponse exception: " + e.getMessage() );
            SendError( "EXCEPTION: " + e.getMessage() );
        }
    }

    private static String stateOf( Uri uri )
    {
        String s = uri.getQueryParameter( "state" );
        return s == null ? "" : s;
    }

    // error for the current request
    private static void SendError( String message )
    {
        String st;
        synchronized( LOCK ) { st = currentState; }
        SendError( st, message );
    }

    // error for the request identified by state (null: current request, stateless)
    private static void SendError( String state, String message )
    {
        try
        {
            JSONObject o = new JSONObject().put( "result", "error" ).put( "message", message == null ? "" : message );
            if( state != null )
                o.put( "state", state );
            Send( state, o.toString() );
        }
        catch( Exception e )
        {
            Log.e( TAG, "SendError exception: " + e.getMessage() );
        }
    }

    // sends at most one response per signIn
    // a response carrying a state that is not the current one is stale and dropped;
    // an empty/null state (redirect without state) is delivered to the current request
    private static void Send( String state, String json )
    {
        synchronized( LOCK )
        {
            if( !isActive )
                return;
            if( state != null && !state.isEmpty() && !state.equals( currentState ) )
            {
                Log.w( TAG, "dropping stale response" );
                return;
            }
            isActive     = false;
            currentState = null;
        }

        try
        {
            UnityPlayer.UnitySendMessage( "[Lagoon-Utils]", "OnAppleSignInResponse", json );
        }
        catch( Exception e )
        {
            Log.e( TAG, "UnitySendMessage exception: " + e.getMessage() );
        }
        unbindOnUiThread();
    }

    /** Abandons the pending request. Sends nothing to Unity. */
    public static void cancel()
    {
        synchronized( LOCK )
        {
            isActive     = false;
            currentState = null;
        }

        unbindOnUiThread();
    }

    // service teardown must happen on the UI thread, like the rest of the tab handling
    private static void unbindOnUiThread()
    {
        if( Looper.myLooper() == Looper.getMainLooper() )
        {
            unbindCustomTabsService();
            return;
        }

        Activity activity = UnityPlayer.currentActivity;
        if( activity == null )
            return;
        activity.runOnUiThread( new Runnable()
        {
            @Override
            public void run()
            {
                unbindCustomTabsService();
            }
        } );
    }

    private static CustomTabsSession           customTabsSession;
    private static CustomTabsClient            customTabsClient;
    private static CustomTabsServiceConnection customTabsServiceConnection;
    private static Activity                    boundActivity;

    private static void bindCustomTabsService( final Activity activity, final String pkg, final String url, final String state )
    {
        customTabsServiceConnection = new CustomTabsServiceConnection()
        {
            @Override
            public void onCustomTabsServiceConnected( ComponentName name, CustomTabsClient client )
            {
                customTabsClient = client;
                try { client.warmup( 0 ); } catch( Exception ignored ) { }
                show( activity, pkg, url, state );
            }

            @Override
            public void onServiceDisconnected( ComponentName name )
            {
                customTabsClient  = null;
                customTabsSession = null;
            }
        };

        boolean ok = false;
        try
        {
            ok = CustomTabsClient.bindCustomTabsService( activity, pkg, customTabsServiceConnection );
        }
        catch( Exception e )
        {
            Log.e( TAG, "bindCustomTabsService exception: " + e.getMessage() );
        }

        if( ok )
        {
            boundActivity = activity;
        }
        else
        {
            customTabsServiceConnection = null;
            show( activity, pkg, url, state );
        }
    }

    private static CustomTabsSession getSession()
    {
        if( customTabsClient == null )
        {
            customTabsSession = null;
        }
        else if( customTabsSession == null )
        {
            customTabsSession = customTabsClient.newSession( new CustomTabsCallback() );
        }
        return customTabsSession;
    }

    private static void show( Activity activity, String pkg, String url, String state )
    {
        // cancelled or answered while the service was connecting
        // the request this URL belongs to must still be the current one
        if( !isCurrent( state ) )
            return;

        try
        {
            CustomTabsIntent customTabsIntent = new CustomTabsIntent.Builder( getSession() )
                    .setShareState( CustomTabsIntent.SHARE_STATE_OFF )
                    .build();
            customTabsIntent.intent.setPackage( pkg );
            customTabsIntent.intent.addFlags( Intent.FLAG_ACTIVITY_NO_HISTORY );
            customTabsIntent.launchUrl( activity, Uri.parse( url ) );
        }
        catch( Exception e )
        {
            Log.e( TAG, "show exception: " + e.getMessage() );
            SendError( state, "CANNOT LAUNCH CUSTOMTAB: " + e.getMessage() );
        }
    }

    // must be called on the UI thread
    private static void unbindCustomTabsService()
    {
        if( customTabsServiceConnection != null && boundActivity != null )
        {
            try
            {
                boundActivity.unbindService( customTabsServiceConnection );
            }
            catch( Exception e )
            {
                Log.e( TAG, "unbindCustomTabsService exception: " + e.getMessage() );
            }
        }
        customTabsClient            = null;
        customTabsSession           = null;
        customTabsServiceConnection = null;
        boundActivity               = null;
    }
}
