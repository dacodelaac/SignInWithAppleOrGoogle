using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace com.binouze
{
    /// <summary>Tracks one pending Apple sign-in request and turns native JSON responses into <see cref="AppleSignInUser"/>.</summary>
    public sealed class AppleSignInFlow
    {
        private string                  _state;
        private Action<AppleSignInUser> _onComplete;

        public bool IsPending => _onComplete != null;

        /// <summary>True when <paramref name="state"/> belongs to the request currently pending.</summary>
        public bool IsCurrent( string state ) => IsPending && state != null && state == _state;

        public string Begin( Action<AppleSignInUser> onComplete )
        {
            var previous = _onComplete;

            var bytes = new byte[16];
            using( var rng = RandomNumberGenerator.Create() )
                rng.GetBytes( bytes );

            var sb = new StringBuilder( 32 );
            foreach( var b in bytes )
                sb.Append( b.ToString( "x2" ) );

            // Install the new request before notifying the old one, so a re-entrant Begin
            // from the old callback supersedes this request instead of being overwritten by it.
            var state   = sb.ToString();
            _state      = state;
            _onComplete = onComplete;

            previous?.Invoke( new AppleSignInUser { Status = AppleSignInStatusCode.Error, ErrorMessage = "superseded by a new request" } );
            return state;
        }

        public void HandleResponse( string json )
        {
            if( !IsPending )
                return;

            var dict = Json.Deserialize( json ) as Dictionary<string, object>;
            if( dict == null )
            {
                Fail( "invalid response" );
                return;
            }

            var respState = dict.GetString( "state" );

            // Java cannot always know the state when it fails; keep its message.
            if( string.IsNullOrEmpty( respState ) && dict.GetString( "result" ) == "error" )
            {
                Fail( dict.GetString( "message", "unknown error" ) );
                return;
            }

            if( respState != _state )
            {
                Fail( "state mismatch" );
                return;
            }

            switch( dict.GetString( "result" ) )
            {
                case "success":
                    var idToken = dict.GetString( "id_token" );
                    if( string.IsNullOrEmpty( idToken ) )
                    {
                        Fail( "missing id_token" );
                        return;
                    }

                    var user = new AppleSignInUser
                    {
                        Status            = AppleSignInStatusCode.Success,
                        IdToken           = idToken,
                        AuthorizationCode = dict.GetString( "code" ),
                        UserId            = DecodeJwtSubject( idToken ),
                    };

                    var userJson = dict.GetString( "user" );
                    if( !string.IsNullOrEmpty( userJson ) && Json.Deserialize( userJson ) is Dictionary<string, object> u )
                    {
                        user.FirstName = u.GetStringS( null, "name", "firstName" );
                        user.LastName  = u.GetStringS( null, "name", "lastName" );
                        user.Email     = u.GetString( "email" );
                    }

                    Complete( user );
                    break;

                case "canceled":
                    Cancel( dict.GetString( "message" ) );
                    break;

                default:
                    Fail( dict.GetString( "message", "unknown error" ) );
                    break;
            }
        }

        public void Cancel( string reason )
        {
            if( !IsPending ) return;
            Complete( new AppleSignInUser { Status = AppleSignInStatusCode.Canceled, ErrorMessage = reason } );
        }

        public void Fail( string message )
        {
            if( !IsPending ) return;
            Complete( new AppleSignInUser { Status = AppleSignInStatusCode.Error, ErrorMessage = message } );
        }

        private void Complete( AppleSignInUser user )
        {
            var cb = _onComplete;
            _onComplete = null;
            _state      = null;
            cb?.Invoke( user );
        }

        public static string DecodeJwtSubject( string idToken )
        {
            try
            {
                var parts = idToken?.Split( '.' );
                if( parts == null || parts.Length < 2 ) return null;

                var s = parts[1].Replace( '-', '+' ).Replace( '_', '/' );
                switch( s.Length % 4 )
                {
                    case 2: s += "=="; break;
                    case 3: s += "=";  break;
                    case 1: return null;
                }

                var payload = Encoding.UTF8.GetString( Convert.FromBase64String( s ) );
                return ( Json.Deserialize( payload ) as Dictionary<string, object> )?.GetString( "sub" );
            }
            catch
            {
                return null;
            }
        }
    }
}
