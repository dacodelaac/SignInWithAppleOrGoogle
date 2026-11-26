#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace com.binouze
{
    /// <summary>
    /// Editor only: detects a pasted JWT id_token in the fake sign-in dialog and decodes its claims.
    /// Never throws, never logs the token.
    /// </summary>
    internal static class EditorIdToken
    {
        public static bool TryParse( string input, out string token, out Dictionary<string,object> claims )
        {
            token  = null;
            claims = null;
            try
            {
                if( string.IsNullOrWhiteSpace( input ) )
                    return false;

                var t     = input.Trim();
                var parts = t.Split( '.' );
                if( parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || parts[2].Length == 0 )
                    return false;

                var b64 = parts[1].Replace( '-', '+' ).Replace( '_', '/' );
                switch( b64.Length % 4 )
                {
                    case 2: b64 += "=="; break;
                    case 3: b64 += "=";  break;
                    case 1: return false;
                }

                var json = Encoding.UTF8.GetString( Convert.FromBase64String( b64 ) );
                if( Json.Deserialize( json ) is not Dictionary<string,object> dict )
                    return false;

                token  = t;
                claims = dict;
                return true;
            }
            catch
            {
                token  = null;
                claims = null;
                return false;
            }
        }

        /// <summary>claim as string, empty when missing</summary>
        public static string Claim( Dictionary<string,object> claims, string key )
        {
            return claims != null && claims.TryGetValue( key, out var v ) && v != null ? v.ToString() : "";
        }
    }
}
#endif
