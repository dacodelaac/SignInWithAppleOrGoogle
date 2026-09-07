using System.IO;
using UnityEditor;
using UnityEngine;
using Directory = UnityEngine.Windows.Directory;

namespace com.binouze
{
    [CustomEditor( typeof(SignInWithAppleOrGoogleSettings))]
    public class SignInWithAppleOrGoogleSettingsEditor : Editor
    {
        private SerializedProperty _URL_APPLECONNECT_REDIRECT;
        private SerializedProperty _APPLECONNECT_CLIENT_ID;
        private SerializedProperty _APPLECONNECT_SCOPE;
        
        private SerializedProperty _APP_URL_SCHEME;
        
        private SerializedProperty _Google_WebClientID;
        private SerializedProperty _Google_IosClientID;
        private SerializedProperty _Google_IosClientScheme;
        private SerializedProperty _Google_RequestAuthCode;
        private SerializedProperty _Google_ForceTokenRefresh;
        private SerializedProperty _Google_RequestEmail;
        private SerializedProperty _Google_RequestIdToken;
        private SerializedProperty _Google_RequestProfile;

        public static SignInWithAppleOrGoogleSettings LoadSettingsInstance()
        {
            PrebuildScript.PrepareProjectFolders();
            
            var instance = SignInWithAppleOrGoogleSettings.LoadInstance();
            // Create instance if null.
            if( instance == null )
            {
                Directory.CreateDirectory(SignInWithAppleOrGoogleSettings.SignInWithAppleOrGoogleSettingsResDir);
                instance = CreateInstance<SignInWithAppleOrGoogleSettings>();
                var assetPath = Path.Combine( SignInWithAppleOrGoogleSettings.SignInWithAppleOrGoogleSettingsResDir, SignInWithAppleOrGoogleSettings.SignInWithAppleOrGoogleSettingsFile + SignInWithAppleOrGoogleSettings.SignInWithAppleOrGoogleSettingsFileExtension);
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
            }
            return instance;
        }
        
        [MenuItem("LagoonPlugins/SignInWithAppleOrGoogle Settings")]
        public static void OpenInspector()
        {
            Selection.activeObject = LoadSettingsInstance();
        }

        public void OnEnable()
        {
            _APP_URL_SCHEME = serializedObject.FindProperty("_APP_URL_SCHEME");
            
            _URL_APPLECONNECT_REDIRECT = serializedObject.FindProperty("_URL_APPLECONNECT_REDIRECT");
            _APPLECONNECT_CLIENT_ID    = serializedObject.FindProperty("_APPLECONNECT_CLIENT_ID");
            _APPLECONNECT_SCOPE        = serializedObject.FindProperty("_APPLECONNECT_SCOPE");
            
            _Google_WebClientID     = serializedObject.FindProperty("_Google_WebClientID");
            _Google_IosClientID     = serializedObject.FindProperty("_Google_IosClientID");
            _Google_IosClientScheme = serializedObject.FindProperty("_Google_IosClientScheme");
            _Google_RequestAuthCode = serializedObject.FindProperty("_Google_RequestAuthCode");
            _Google_ForceTokenRefresh = serializedObject.FindProperty("_Google_ForceTokenRefresh");
            _Google_RequestEmail = serializedObject.FindProperty("_Google_RequestEmail");
            _Google_RequestIdToken = serializedObject.FindProperty("_Google_RequestIdToken");
            _Google_RequestProfile = serializedObject.FindProperty("_Google_RequestProfile");
        }

        public override void OnInspectorGUI()
        {
            // Make sure the Settings object has all recent changes.
            serializedObject.Update();

            var settings = (SignInWithAppleOrGoogleSettings)target;

            if( settings == null )
            {
              Debug.LogError("SignInWithAppleOrGoogleSettings is null.");
              return;
            }

            // -- GEneral
            
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            
            // -- Google
            
            EditorGUILayout.LabelField("SignIn with Google configuration:", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_Google_WebClientID,     new GUIContent("Web Client ID:"));
            EditorGUILayout.PropertyField(_Google_IosClientID,     new GUIContent("iOS Client ID:"));
            
            EditorGUI.BeginDisabledGroup( true );
            EditorGUILayout.PropertyField(_Google_IosClientScheme, new GUIContent("iOS Scheme:"));
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("Google OAuth2 Configuration:", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_Google_RequestAuthCode, new GUIContent("Request Auth Code:"));
            EditorGUILayout.PropertyField(_Google_ForceTokenRefresh, new GUIContent("Force Token Refresh:"));
            EditorGUILayout.PropertyField(_Google_RequestEmail, new GUIContent("Request Email:"));
            EditorGUILayout.PropertyField(_Google_RequestIdToken, new GUIContent("Request ID Token:"));
            EditorGUILayout.PropertyField(_Google_RequestProfile, new GUIContent("Request Profile:"));
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            
            // -- Apple
            
            EditorGUILayout.LabelField("SignIn with Apple configuration:", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            //EditorGUILayout.HelpBox( "enter your AdMost applications ids here", MessageType.Info);
            
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_APP_URL_SCHEME,            new GUIContent("Android App URL Scheme:"));
            EditorGUILayout.PropertyField(_URL_APPLECONNECT_REDIRECT, new GUIContent("Apple Connect Redirect URL:"));
            EditorGUILayout.PropertyField(_APPLECONNECT_CLIENT_ID,    new GUIContent("Apple Connect ClientID:"));
            EditorGUILayout.PropertyField(_APPLECONNECT_SCOPE,        new GUIContent("Apple Connect Scope:"));
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            

            serializedObject.ApplyModifiedProperties();
        }
    }
}