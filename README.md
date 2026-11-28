# SignInWithAppleOrGoogle

Unity plugin to SignIn with Google or Apple Account for iOS and Android.

Sign in with Apple on iOS uses: https://github.com/lupidan/apple-signin-unity

## PACKAGE INSTALLATION:

- in the package manager, click on the + 
- select `add package from GIT url`
- paste the following url: `"https://github.com/binouze/SignInWithAppleOrGoogle.git"`


## SET UP GOOGLE SIGN IN:

1. Set up your Google APIs console project for Android: https://developer.android.com/identity/sign-in/credential-manager-siwg#set-google
2. Set up your Google APIs console project for iOS: https://developers.google.com/identity/sign-in/ios/start-integrating#get_an_oauth_client_id
3. Go to **LagoonPlugins > SignInWithAppleOrGoogle Settings** and fill the settings.
    - **Web Client ID:** the client Id you created on the 1st step (Web App).
    - **iOS Client ID:** the client Id you created on step 2 (iOS).
    - **iOS Scheme:** is auto generated based on the iOS Client ID.


## SET UP APPLE SIGN IN:

### iOS

1. In your Apple Developer account enable the **Sign in with Apple** capability for your App ID.
2. The plugin does not add the capability for you: the host project must add it, either manually in Xcode
   (Signing & Capabilities > + Capability > Sign in with Apple) or with its own `[PostProcessBuild]` editor script
   calling `ProjectCapabilityManager.AddSignInWithAppleWithCompatibility(unityFrameworkTargetGuid)` from `AppleAuth.Editor`:

```csharp
#if UNITY_IOS
using System.IO;
using AppleAuth.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class AppleSignInPostProcess
{
    [PostProcessBuild(int.MaxValue)]
    public static void OnPostProcessBuild( BuildTarget target, string path )
    {
        if( target != BuildTarget.iOS )
            return;

        var projPath = PBXProject.GetPBXProjectPath( path );
        var proj     = new PBXProject();
        proj.ReadFromString( File.ReadAllText( projPath ) );

        var mainTargetGuid = proj.GetUnityMainTargetGuid();
        // reuse the entitlements file of the main target if there is one
        var entitlements = proj.GetBuildPropertyForAnyConfig( mainTargetGuid, "CODE_SIGN_ENTITLEMENTS" );
        if( string.IsNullOrEmpty( entitlements ) )
            entitlements = "Unity-iPhone.entitlements";

        var manager = new ProjectCapabilityManager( projPath, entitlements, null, mainTargetGuid );
        manager.AddSignInWithAppleWithCompatibility( proj.GetUnityFrameworkTargetGuid() );
        manager.WriteToFile();
    }
}
#endif
```
3. The native part uses https://github.com/lupidan/apple-signin-unity.

### Android

Sign in with Apple on Android is a web flow, it needs a **Services ID** and a redirect page hosted on your server.

1. In your Apple Developer account create a **Services ID** (Identifiers > Services IDs) and enable **Sign in with Apple** on it.
2. Configure the Services ID with your domain and a **Return URL**. The Return URL must be HTTPS: it is the redirect page described below.
3. Go to **LagoonPlugins > SignInWithAppleOrGoogle Settings** and fill the settings.
    - **Android App URL Scheme:** the app url scheme, used to force close the login form when calling AppleSignIn.CloseDialog().
    - **Apple Connect Redirect URL:** the Return URL of the Services ID (HTTPS).
    - **Apple Connect ClientID:** the Services ID identifier.
    - **Apple Connect Scope:** the requested scopes (for example `name email`).
4. Add the redirect page, the AndroidManifest entry and the proguard rules (see **ANDROID CONFIGURATION**).

The plugin opens the sign in page in a Chrome Custom Tab (androidx.browser, the dependency is now declared by the plugin).
The default browser of the device is used when it supports Custom Tabs. If the player closes the browser without finishing,
the cancel is detected when the app gets the focus back (`Status == Canceled`).


## ANDROID CONFIGURATION:

### Apple SignIn:

#### 0. Per-call Return URL (optional)

`Apple_SignIn` accepts an optional Android-only Return URL for that request, overriding "Apple Connect Redirect URL" of the settings
(null or empty = settings value), for example to use a different server per environment.
Every URL used must be registered in the Return URLs of the Services ID.

```csharp
SignInWithAppleOrGoogle.Apple_SignIn( cb, null, "https://.../apple/callback" );
```

#### 1. Redirect Page

On Android, Apple sends connection data to an URL ("Apple Connect Redirect URL" in the settings, the Return URL of the Services ID) as POST variables.
This page must answer with a `302` redirect to `<scheme>://<host>?<all the POST fields URL-encoded>`
so that the app receives all the params as GET variables.

Here is an example of this page in php:

```php
// get the POST datas and sent it back to the app in GET variables
$post    = http_build_query($_POST);
$linkurl = "pbauth://applesignin/?$post";
header('Location: '.$linkurl);
```


#### 2. AndroidManifest

Add this in the AndroidManifest.xml. The `AppleSignInActivity` must be `exported`, `singleTop`, use a translucent theme and declare
a `VIEW` / `DEFAULT` / `BROWSABLE` intent-filter with the scheme and host of the redirect.

Change the {REDIRECT_SCHEME} and {REDIRECT_HOST} to what is set in your redirect url. (pbauth and applesignin in the previous exemple.)

```xml
<!-- POUR LES REDIRECT APPLE SIGNIN -->
<activity android:name="com.binouze.AppleSignInActivity" android:launchMode="singleTop" android:theme="@android:style/Theme.Translucent.NoTitleBar.Fullscreen" android:exported="true">
  <!-- POUR LES REDIRECT APPLE SIGNIN -->
  <intent-filter>
    <action android:name="android.intent.action.VIEW" />
    <category android:name="android.intent.category.DEFAULT" />
    <category android:name="android.intent.category.BROWSABLE" />
    <data android:scheme="{REDIRECT_SCHEME}" android:host="{REDIRECT_HOST}" />
  </intent-filter>
</activity>
```

Also add this `queries` block as a direct child of `<manifest>` (merge it with an existing `<queries>` if you have one). Without it, Android 11+ cannot see the Custom Tabs service and the sign-in page falls back to the default browser.

```xml
<queries>
  <intent>
    <action android:name="android.support.customtabs.action.CustomTabsService" />
  </intent>
</queries>
```


### Proguard rules to add:

If you use proguard to minify your project you should add theses rules to be sure everything works.

```
# plugins com.binouze
-keep class com.binouze.** { *; } 

# google Credential manager
-if class androidx.credentials.CredentialManager
-keep class androidx.credentials.playservices.** {
*;
}
```


## HOW TO USE

Once configured, it's pretty simple to use:

```csharp
    
    // GOOGLE
    
    // Google SignIn
    SignInWithAppleOrGoogle.Google_SignIn( (isConnected, user) =>
    {
        if( isConnected )
        {
            // user connected, get user info in user object
        }
        else
        {
            user not connected
        }
    }, 
    silent,       // if true this will not prompt the user to connect
    silentOnly ); // if false, after a silent connect fail, this wil try a non silent connection
    
    // Google SignOut
    SignInWithAppleOrGoogle.Google_SignOut( () => 
    {
        // user is signed out
    } );
    
    
    // APPLE

    // Apple SignIn (iOS and Android)
    // the callback is called exactly once, user is never null,
    // isConnected is true only if user.Status == Success and user.IdToken is not empty
    SignInWithAppleOrGoogle.Apple_SignIn( (isConnected, user) =>
    {
        if( isConnected )
        {
            // send user.IdToken (Apple JWT) to your server to authenticate the player
            // user.UserId, user.Email, user.FirstName, user.LastName, user.AuthorizationCode are also available
            // (Email, FirstName and LastName are only provided by Apple the first time)
        }
        else
        {
            // user.Status: Canceled, Error (see user.ErrorMessage), NotSupported
        }
    } );

    // Apple credential state (iOS only, other platforms return Unknown), callback on the main thread
    SignInWithAppleOrGoogle.Apple_GetCredentialState( userId, state =>
    {
        // Authorized, Revoked, NotFound or Unknown
    } );

    // Apple SignOut
    SignInWithAppleOrGoogle.Apple_SignOut( () => 
    {
        // user is signed out
    } );
    
    
```

## Editor testing

In the Unity Editor, sign-in shows a small dialog instead of the native UI.
- Paste a real `id_token` (Apple: captured from the game's `[AppleDebug]` device log; Google: from a device log) to sign in for real. Apple tokens expire after about 10 minutes.
- Any other text is used as a fake user id (a real backend will reject it).
