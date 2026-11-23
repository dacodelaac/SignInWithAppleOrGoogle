
namespace com.binouze
{
    /// <summary> Information for the authenticated user.</summary>
    public class AppleSignInUser
    {
        /// <summary> Email address.</summary>
        ///<remarks> null if not requested, or if there was an error.</remarks>
        public string Email { get; set; }

        /// <summary> First Name.</summary>
        public string FirstName { get; set; }

        /// <summary> Last Name.</summary>
        public string LastName { get; set; }

        /// <summary> User ID</summary>
        public string UserId { get; set; }

        /// <summary> Apple identity token (JWT).</summary>
        public string IdToken { get; set; }

        /// <summary> Apple authorization code.</summary>
        public string AuthorizationCode { get; set; }

        /// <summary> Result status.</summary>
        public AppleSignInStatusCode Status { get; set; }

        /// <summary> Error description when Status is Error.</summary>
        public string ErrorMessage { get; set; }

        /// <summary><para>Returns a string that represents the current object.</para></summary>
        public override string ToString()
        {
            return $"{nameof( Status )}: {Status}, {nameof( Email )}: {Email}, {nameof( FirstName )}: {FirstName}, {nameof( LastName )}: {LastName}, {nameof( UserId )}: {UserId}";
        }
    }
}
