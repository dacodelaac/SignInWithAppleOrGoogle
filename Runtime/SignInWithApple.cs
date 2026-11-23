using System;
using System.Text;
using AppleAuth;
using UnityEngine;
#if UNITY_IOS
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

namespace com.binouze
{
    public class SignInWithApple : MonoBehaviour
    {
        #if UNITY_ANDROID
        // Android needs the web flow settings: without them the Apple button must not be offered
        internal static bool IsSupported
        {
            get
            {
                if( Application.isEditor )
                    return true;
                var settings = SignInWithAppleOrGoogleSettings.LoadInstance();
                return settings != null
                    && !string.IsNullOrEmpty( settings.APPLECONNECT_CLIENT_ID )
                    && !string.IsNullOrEmpty( settings.URL_APPLECONNECT_REDIRECT );
            }
        }
        #elif UNITY_IOS
        internal static bool IsSupported = AppleAuthManager.IsCurrentPlatformSupported;
        #else
        internal static bool IsSupported => false;
        #endif
        
        private         bool              MustUpdate;
        private         IAppleAuthManager appleAuthManager;

        private bool IsInit;

        private static void Log( string str )
        {
            PluginLogger.Log( $"[Apple] {str}" );
        }

        private void Init()
        {
            if( IsInit )
                return;
            IsInit = true;


            #if UNITY_IOS
            // If the current platform is supported
            if( IsSupported && !Application.isEditor )
            {
                // Creates a default JSON deserializer, to transform JSON Native responses to C# instances
                var deserializer = new PayloadDeserializer();
                // Creates an Apple Authentication manager with the deserializer
                appleAuthManager = new AppleAuthManager(deserializer);
                MustUpdate       = true;

                appleAuthManager.SetCredentialsRevokedCallback(result =>
                {
                    // deco -> reset game
                    SignInWithAppleOrGoogle.OnTokenRevoked?.Invoke();
                });
            }
            #else
            var settings = SignInWithAppleOrGoogleSettings.LoadInstance();
            if( settings == null )
            {
                Debug.LogException( new Exception("[SignInWithAppleOrGoogle] APPLE Fail to load settings file") );
                return;
            }

            AppleSignIn.GetInstance().Init( settings.URL_APPLECONNECT_REDIRECT );
            #endif
        }

        internal static void ResetStatics()
        {
            #if UNITY_ANDROID
            AppleSignIn.ResetStatics();
            AppleSignIn.GetInstance().CloseDialog();
            #endif
        }

        #if UNITY_IOS
        private void Update()
        {
            // Updates the AppleAuthManager instance to execute
            // pending callbacks inside Unity's execution loop
            if( MustUpdate )
            {
                appleAuthManager.Update();
            }
        }
        #endif

        private static AppleSignInUser NewUser( AppleSignInStatusCode status, string errorMessage = null )
        {
            return new AppleSignInUser { Status = status, ErrorMessage = errorMessage };
        }

        internal static void Connect( Action<AppleSignInUser> OnComplete )
        {
            GetInstance()._Connect( OnComplete );
        }
        internal static void Disconnect( Action OnComplete )
        {
            GetInstance()._Disconnect( OnComplete );
        }

        internal static void GetCredentialState( string userId, Action<AppleCredentialState> OnComplete )
        {
            GetInstance()._GetCredentialState( userId, OnComplete );
        }

        private void _Disconnect( Action OnComplete )
        {
            OnComplete?.Invoke();
        }

        /// <summary>
        /// Lancer la connection a Apple ID, OnComplete est appele une seule fois avec un user non null
        /// </summary>
        /// <param name="OnComplete"></param>
        private void _Connect( Action<AppleSignInUser> OnComplete )
        {
            Log( "Connect" );

            void Complete( AppleSignInUser user )
            {
                user ??= NewUser( AppleSignInStatusCode.Error, "no result" );
                Log( $"ConnectComplete {user?.Status}" );
                OnComplete?.Invoke( user );
            }

            if( Application.isEditor )
            {
                EditorHelper.ShowInputDialog( "connecte apple ?", "oui", "non",
                    fuid =>
                    {
                        AppleSignIn.FAKE_UID = fuid;
                        AppleSignIn.GetInstance().SignIn( Complete );
                    },
                    () =>
                    {
                        Complete( NewUser( AppleSignInStatusCode.Canceled ) );
                    } );
                return;
            }

            #if UNITY_ANDROID
            AppleSignIn.GetInstance().SignIn( Complete );
            #elif UNITY_IOS
            if( !IsSupported || appleAuthManager == null )
            {
                Log( "Connect IOS => NOT SUPPORTED" );
                Complete( NewUser( AppleSignInStatusCode.NotSupported ) );
                return;
            }

            DoConnect( Complete );
            #else
            Log( "Connect PLATEFORM NOT SUPPORTED" );
            Complete( NewUser( AppleSignInStatusCode.NotSupported ) );
            #endif
        }

        private void _GetCredentialState( string userId, Action<AppleCredentialState> OnComplete )
        {
            #if UNITY_IOS
            if( !Application.isEditor && IsSupported && appleAuthManager != null && !string.IsNullOrEmpty( userId ) )
            {
                appleAuthManager.GetCredentialState(
                    userId,
                    state =>
                    {
                        Log( $"GetCredentialState {state}" );
                        switch( state )
                        {
                            case CredentialState.Authorized: OnComplete?.Invoke( AppleCredentialState.Authorized ); break;
                            case CredentialState.Revoked:    OnComplete?.Invoke( AppleCredentialState.Revoked );    break;
                            case CredentialState.NotFound:   OnComplete?.Invoke( AppleCredentialState.NotFound );   break;
                            default:                         OnComplete?.Invoke( AppleCredentialState.Unknown );    break;
                        }
                    },
                    error =>
                    {
                        Log( $"GetCredentialState ERROR {error?.LocalizedDescription}" );
                        OnComplete?.Invoke( AppleCredentialState.Unknown );
                    });
                return;
            }
            #endif

            OnComplete?.Invoke( AppleCredentialState.Unknown );
        }

        #if UNITY_IOS
        private void DoConnect( Action<AppleSignInUser> OnComplete )
        {
            var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);
            appleAuthManager.LoginWithAppleId(
                loginArgs,
                credential =>
                {
                    // Obtained credential, cast it to IAppleIDCredential
                    if( credential is IAppleIDCredential appleIdCredential )
                    {
                        var user = new AppleSignInUser
                        {
                            Status            = AppleSignInStatusCode.Success,
                            UserId            = appleIdCredential.User,
                            Email             = appleIdCredential.Email,
                            FirstName         = appleIdCredential.FullName?.GivenName ?? "",
                            LastName          = appleIdCredential.FullName?.FamilyName ?? "",
                            IdToken           = appleIdCredential.IdentityToken      != null ? Encoding.UTF8.GetString( appleIdCredential.IdentityToken )      : null,
                            AuthorizationCode = appleIdCredential.AuthorizationCode != null ? Encoding.UTF8.GetString( appleIdCredential.AuthorizationCode ) : null,
                        };
                        Log( $"DoConnect OK {user.UserId}" );
                        OnComplete( user );
                    }
                    else
                    {
                        Log( $"DoConnect FAIL {credential}" );
                        OnComplete( NewUser( AppleSignInStatusCode.Error, "unexpected credential type" ) );
                    }
                },
                error =>
                {
                    Log( $"DoConnect ERROR {error?.LocalizedDescription ?? "NO ERROR DESC"}" );

                    var code = error != null ? error.GetAuthorizationErrorCode() : AuthorizationErrorCode.Unknown;
                    Log( $"DoConnect ERROR code: {code}" );
                    if( code == AuthorizationErrorCode.Canceled )
                        OnComplete( NewUser( AppleSignInStatusCode.Canceled ) );
                    else
                        OnComplete( NewUser( AppleSignInStatusCode.Error, error?.LocalizedDescription ?? code.ToString() ) );
                });
        }
        #endif

        private void OnDestroy()
        {
            _instance = null;
        }

        private static SignInWithApple _instance;
        private static SignInWithApple GetInstance() 
        {
            if( _instance == null ) 
            {
                _instance = (SignInWithApple)FindObjectOfType( typeof(SignInWithApple) );
                if( _instance == null ) 
                {
                    const string goName = "[Lagoon-Utils]";          

                    var go = GameObject.Find( goName );
                    if( go == null ) 
                    {
                        go = new GameObject {name = goName};
                        DontDestroyOnLoad( go );
                    }
                    _instance = go.AddComponent<SignInWithApple>();     
                    _instance.Init();
                }
            }
            return _instance;
        }
    }
}