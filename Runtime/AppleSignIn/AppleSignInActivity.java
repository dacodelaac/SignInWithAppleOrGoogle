package com.binouze;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

public class AppleSignInActivity extends Activity
{
    private static final String TAG = "AppleSignInActivity";

    @Override
    protected void onCreate( Bundle savedInstanceState )
    {
        super.onCreate( savedInstanceState );
        HandleLink( getIntent() );
    }

    @Override
    protected void onNewIntent( Intent intent )
    {
        super.onNewIntent( intent );
        setIntent( intent );
        HandleLink( intent );
    }

    private void HandleLink( Intent intent )
    {
        try
        {
            String url = intent == null ? null : intent.getDataString();
            Log.d( TAG, "HandleLink " + url );
            AppleSignIn.OnSignInResponse( url );
        }
        catch( Exception e )
        {
            Log.e( TAG, "HandleLink exception: " + e.getMessage() );
        }

        // bring the game task back to the front
        try
        {
            Intent main = getPackageManager().getLaunchIntentForPackage( getPackageName() );
            if( main != null )
            {
                main.addFlags( Intent.FLAG_ACTIVITY_REORDER_TO_FRONT | Intent.FLAG_ACTIVITY_SINGLE_TOP );
                startActivity( main );
            }
        }
        catch( Exception e )
        {
            Log.e( TAG, "return to app exception: " + e.getMessage() );
        }

        finish();
    }
}
