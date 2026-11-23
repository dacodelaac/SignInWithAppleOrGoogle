using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;

namespace com.binouze
{
    public class AppleSignIn : MonoBehaviour
    {
        public static string FAKE_UID;
        public static string CLIENT_ID;
        public static string SCOPE;

        // time to let the redirect deep link arrive after the player comes back to the game
        private const float RETURN_GRACE_SECONDS = 1.5f;

        private static AppleSignInFlow _flow = new AppleSignInFlow();

        private bool IsInit;

        // focus based cancel (browser closed without finishing)
        private bool _leftApp;
        private int  _requestId;

        private static void Log( string val )
        {
            PluginLogger.Log( $"[AppleSignIn] {val}" );
        }
        private static void LogError( string val )
        {
            PluginLogger.LogError( $"[AppleSignIn] {val}" );
        }

        /// <summary>Drops the pending request without calling its callback.</summary>
        public static void ResetStatics()
        {
            _flow = new AppleSignInFlow();
        }

        public void Init( string redirectURI )
        {
            if( IsInit )
                return;

            #if UNITY_ANDROID && !UNITY_EDITOR
            using var cls = new AndroidJavaClass("com.binouze.AppleSignIn");
            cls.CallStatic("init", CLIENT_ID, redirectURI, SCOPE, URL_SCHEME);
            #endif

            IsInit = true;
        }

        public void SignIn( Action<AppleSignInUser> onComplete )
        {
            if( !IsInit )
            {
                // callback exactly once even when not initialized
                var tmp = new AppleSignInFlow();
                tmp.Begin( onComplete );
                tmp.Fail( "not initialized" );
                return;
            }

            var state = _flow.Begin( onComplete );

            // Begin notifies the superseded callback, which may start a newer SignIn re-entrantly:
            // then this call is obsolete and must not reach Java with a stale state.
            if( !_flow.IsCurrent( state ) )
                return;

            _requestId++;
            _leftApp = false;

            #if UNITY_EDITOR
            _flow.HandleResponse( BuildEditorResponse( state ) );
            #elif UNITY_ANDROID
            try
            {
                using var cls = new AndroidJavaClass("com.binouze.AppleSignIn");
                cls.CallStatic("signIn", state);
            }
            catch( Exception e )
            {
                LogError( $"signIn exception {e.Message}" );
                _flow.Fail( e.Message );
            }
            #else
            _flow.Fail( "not supported" );
            #endif
        }

        public void CloseDialog()
        {
            CancelPending( "closed" );
        }

        private void CancelPending( string reason )
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass("com.binouze.AppleSignIn");
                cls.CallStatic("cancel");
            }
            catch( Exception e )
            {
                LogError( $"cancel exception {e.Message}" );
            }
            #endif
            _flow.Cancel( reason );
        }

        public void OnAppleSignInResponse( string response )
        {
            // the response carries id_token / code / user: never log it
            Log( $"OnAppleSignInResponse received ({response?.Length ?? 0} chars)" );
            _flow.HandleResponse( response );
        }

        #if UNITY_EDITOR
        private static string BuildEditorResponse( string state )
        {
            var payload = Json.Serialize( new Dictionary<string,object> { { "sub", FAKE_UID ?? "fake" } } );
            var b64     = Convert.ToBase64String( Encoding.UTF8.GetBytes( payload ) ).TrimEnd( '=' ).Replace( '+', '-' ).Replace( '/', '_' );
            var user    = Json.Serialize( new Dictionary<string,object>
            {
                { "email", "fake_user@gmail.com" },
                { "name",  new Dictionary<string,object> { { "firstName", "User" }, { "lastName", "FAKE" } } },
            } );
            return Json.Serialize( new Dictionary<string,object>
            {
                { "result",   "success" },
                { "state",    state },
                { "id_token", "h." + b64 + ".s" },
                { "code",     "editor" },
                { "user",     user },
            } );
        }
        #endif

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void OnApplicationFocus( bool hasFocus )
        {
            if( !_flow.IsPending )
            {
                _leftApp = false;
                return;
            }

            if( !hasFocus )
            {
                _leftApp = true;
                return;
            }

            if( _leftApp )
            {
                _leftApp = false;
                StartCoroutine( CancelIfStillPending( _requestId ) );
            }
        }

        private IEnumerator CancelIfStillPending( int requestId )
        {
            yield return new WaitForSecondsRealtime( RETURN_GRACE_SECONDS );

            // a newer request may have started meanwhile
            if( requestId == _requestId && _flow.IsPending )
                CancelPending( "closed" );
        }
        #endif

        private static string      URL_SCHEME;
        private static AppleSignIn _instance;
        public static AppleSignIn GetInstance() 
        {
            if (_instance == null)
            {
                _instance = (AppleSignIn)FindObjectOfType( typeof(AppleSignIn) );
                if( _instance == null ) 
                {
                    const string goName = "[Lagoon-Utils]";          

                    var go = GameObject.Find( goName );
                    if( go == null ) 
                    {
                        go = new GameObject {name = goName};
                        DontDestroyOnLoad( go );
                    }
                    _instance = go.AddComponent<AppleSignIn>();
                    
                    var settings = SignInWithAppleOrGoogleSettings.LoadInstance();
                    URL_SCHEME = settings == null ? "" : settings.APP_URL_SCHEME;
                    CLIENT_ID  = settings == null ? "" : settings.APPLECONNECT_CLIENT_ID;
                    SCOPE      = settings == null ? "" : WebUtility.UrlEncode(string.Join(' ',settings.APPLECONNECT_SCOPE));
                }
            }
            return _instance;
        }
    }
}