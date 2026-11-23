package com.binouze;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Intent;
import android.net.Uri;
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
    private static boolean isActive;
    private static String  currentState;

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
        final Activity activity = UnityPlayer.currentActivity;
        if( activity == null )
        {
            Log.e( TAG, "UnityPlayer.currentActivity is null in signIn" );
            isActive     = true;
            currentState = state;
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
                    isActive     = true;
                    currentState = state;

                    String url = buildAuthUrl( state );
                    String pkg = CustomTabsClient.getPackageName( activity, null );
                    if( pkg == null )
                    {
                        activity.startActivity( new Intent( Intent.ACTION_VIEW, Uri.parse( url ) ) );
                    }
                    else
                    {
                        bindCustomTabsService( activity, pkg, url );
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

    private static String buildAuthUrl( String state ) throws Exception
    {
        return "https://appleid.apple.com/auth/authorize?response_type=code%20id_token&response_mode=form_post"
             + "&client_id="    + ClientID
             + "&scope="        + Scope
             + "&state="        + state
             + "&redirect_uri=" + URLEncoder.encode( RedirectURI, "UTF-8" );
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
                    Send( new JSONObject().put( "result", "canceled" ).put( "state", stateOf( uri ) ).toString() );
                else
                    Send( new JSONObject().put( "result", "error" ).put( "state", stateOf( uri ) ).put( "message", error ).toString() );
                return;
            }

            String idToken = uri.getQueryParameter( "id_token" );
            if( idToken == null || idToken.isEmpty() )
            {
                Send( new JSONObject().put( "result", "error" ).put( "state", stateOf( uri ) ).put( "message", "id_token not found" ).toString() );
                return;
            }

            String code = uri.getQueryParameter( "code" );
            String user = uri.getQueryParameter( "user" );
            Send( new JSONObject()
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

    private static void SendError( String message )
    {
        try
        {
            JSONObject o = new JSONObject().put( "result", "error" ).put( "message", message == null ? "" : message );
            if( currentState != null )
                o.put( "state", currentState );
            Send( o.toString() );
        }
        catch( Exception e )
        {
            Log.e( TAG, "SendError exception: " + e.getMessage() );
        }
    }

    // sends at most one response per signIn
    private static void Send( String json )
    {
        if( !isActive )
            return;
        isActive     = false;
        currentState = null;

        try
        {
            UnityPlayer.UnitySendMessage( "[Lagoon-Utils]", "OnAppleSignInResponse", json );
        }
        catch( Exception e )
        {
            Log.e( TAG, "UnitySendMessage exception: " + e.getMessage() );
        }
        unbindCustomTabsService();
    }

    /** Abandons the pending request. Sends nothing to Unity. */
    public static void cancel()
    {
        isActive     = false;
        currentState = null;
        unbindCustomTabsService();
    }

    private static CustomTabsSession           customTabsSession;
    private static CustomTabsClient            customTabsClient;
    private static CustomTabsServiceConnection customTabsServiceConnection;
    private static Activity                    boundActivity;

    private static void bindCustomTabsService( final Activity activity, final String pkg, final String url )
    {
        customTabsServiceConnection = new CustomTabsServiceConnection()
        {
            @Override
            public void onCustomTabsServiceConnected( ComponentName name, CustomTabsClient client )
            {
                customTabsClient = client;
                try { client.warmup( 0 ); } catch( Exception ignored ) { }
                show( activity, pkg, url );
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
            show( activity, pkg, url );
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

    private static void show( Activity activity, String pkg, String url )
    {
        // cancelled or answered while the service was connecting
        if( !isActive )
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
            SendError( "CANNOT LAUNCH CUSTOMTAB: " + e.getMessage() );
        }
    }

    public static void unbindCustomTabsService()
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
