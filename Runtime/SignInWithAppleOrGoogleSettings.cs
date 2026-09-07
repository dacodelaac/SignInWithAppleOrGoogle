using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.binouze
{
    public class SignInWithAppleOrGoogleSettings : ScriptableObject
    {
        public const string SignInWithAppleOrGoogleSettingsFile          = "SignInWithAppleOrGoogleSettings";
        public const string SignInWithAppleOrGoogleSettingsResDir        = "Assets/LagoonPlugins/SignInWithAppleOrGoogle/Resources";
        public const string SignInWithAppleOrGoogleSettingsFileExtension = ".asset";

        public static SignInWithAppleOrGoogleSettings LoadInstance()
        {
            // Read from resources.
            return Resources.Load<SignInWithAppleOrGoogleSettings>(SignInWithAppleOrGoogleSettingsFile);
        }

        public bool IsValide()
        {
            return
                !string.IsNullOrEmpty( _URL_APPLECONNECT_REDIRECT ) &&
                !string.IsNullOrEmpty( _APPLECONNECT_CLIENT_ID ) &&
                _APPLECONNECT_SCOPE?.Count > 0 &&
                !string.IsNullOrEmpty( _APP_URL_SCHEME ) && _APP_URL_SCHEME.Replace( "://", "" ).Length > 0 &&
                !string.IsNullOrEmpty( _Google_WebClientID )        &&
                !string.IsNullOrEmpty( _Google_IosClientID )        &&
                !string.IsNullOrEmpty( _Google_IosClientScheme );
        }
        
        [SerializeField][TextArea]
        private string _APP_URL_SCHEME = string.Empty;
        
        // -- APPLE
        
        [SerializeField][TextArea]
        private string _URL_APPLECONNECT_REDIRECT = string.Empty;
        [SerializeField][TextArea]
        private string _APPLECONNECT_CLIENT_ID = string.Empty;
        [SerializeField]
        private List<string> _APPLECONNECT_SCOPE = new (){"name","email"};

        // -- GOOGLE
        
        [SerializeField][TextArea]
        private string _Google_WebClientID = string.Empty;
        [SerializeField][TextArea]
        private string _Google_IosClientID;
        [SerializeField][TextArea]
        private string _Google_IosClientScheme;

        [SerializeField, Tooltip("Set to true for getting an auth code when authenticating.")]
        private bool _Google_RequestAuthCode = false;

        [
            SerializeField,
            Tooltip("Set to true to request to reset the refresh token. Causes re-consent.")
        ]
        private bool _Google_ForceTokenRefresh = false;

        [SerializeField, Tooltip("Request email address, requires consent.")]
        private bool _Google_RequestEmail = false;

        [SerializeField, Tooltip("Request id token, requires consent.")]
        private bool _Google_RequestIdToken = false;

        [SerializeField, Tooltip("Request profile, requires consent.")]
        private bool _Google_RequestProfile = false;

        public string APP_URL_SCHEME
        {
            get => _APP_URL_SCHEME;
            set => _APP_URL_SCHEME = value;
        }

        public string URL_APPLECONNECT_REDIRECT
        {
            get => _URL_APPLECONNECT_REDIRECT;
            set => _URL_APPLECONNECT_REDIRECT = value;
        }

        public string APPLECONNECT_CLIENT_ID
        {
            get => _APPLECONNECT_CLIENT_ID;
            set => _APPLECONNECT_CLIENT_ID = value;
        }

        public List<string> APPLECONNECT_SCOPE
        {
            get => _APPLECONNECT_SCOPE;
            set => _APPLECONNECT_SCOPE = value;
        }

        public string Google_WebClientID
        {
            get => _Google_WebClientID;
            set => _Google_WebClientID = value;
        }

        public string Google_IosClientID
        {
            get => _Google_IosClientID;
            set => _Google_IosClientID = value;
        }

        public string Google_IosClientScheme
        {
            get => _Google_IosClientScheme;
            set => _Google_IosClientScheme = value;
        }

        public bool Google_RequestAuthCode
        {
            get => _Google_RequestAuthCode;
            set => _Google_RequestAuthCode = value;
        }

        public bool Google_ForceTokenRefresh
        {
            get => _Google_ForceTokenRefresh;
            set => _Google_ForceTokenRefresh = value;
        }

        public bool Google_RequestEmail
        {
            get => _Google_RequestEmail;
            set => _Google_RequestEmail = value;
        }

        public bool Google_RequestIdToken
        {
            get => _Google_RequestIdToken;
            set => _Google_RequestIdToken = value;
        }

        public bool Google_RequestProfile
        {
            get => _Google_RequestProfile;
            set => _Google_RequestProfile = value;
        }

        private string GetIosScheme()
        {
            if( !string.IsNullOrWhiteSpace( Google_IosClientID ) )
            {
                var ex = Google_IosClientID.Split( "." );
                Array.Reverse( ex );
                return string.Join( '.', ex );// +$".{last}";
                /*if( ex.Length >= 2 )
                {
                    // recuperer le premier element qui ira a la fin
                    var last = ex[0];
                    // supprimer le premier element de la liste
                    ex = ex[1..];
                    // reformer le string avec le premier element suivi des autres
                    return string.Join( '.', ex )+$".{last}";
                }*/
            }

            return string.Empty;
        }
        
        private string saved_Google_IosClientScheme;
        void OnValidate()
        {
            _Google_IosClientScheme = GetIosScheme();

            if( APP_URL_SCHEME.Replace( "://", "" ).Length > 0 )
            {
                if( !APP_URL_SCHEME.EndsWith( "://" ) )
                    APP_URL_SCHEME += "://";
            }
        }
    }
}