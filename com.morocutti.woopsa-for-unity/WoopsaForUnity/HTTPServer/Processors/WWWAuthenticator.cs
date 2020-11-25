﻿using System;
using System.Text;

namespace Woopsa
{
    public abstract class BaseAuthenticator : PostRouteProcessor, IRequestProcessor
    {
        [ThreadStatic]
        private static string _currentUserName;

        /// <summary>
        /// This property returns the username provided to authenticate a request within the current 
        /// webserver thread, or null if none. The value is specific to the calling thread, the property
        /// returns a different value in each thread.
        /// </summary>
        public static string CurrentUserName { get { return _currentUserName; } }

        public BaseAuthenticator(string realm)
        {
            Realm = realm;
        }

        public string Realm { get; private set; }

        public virtual bool Process(HTTPRequest request, HTTPResponse response)
        {
            string username;
            string password;
            bool authenticated;
            _currentUserName = null;
            if (request.Headers.ContainsKey(HTTPHeader.Authorization))
            {
                string authString = request.Headers[HTTPHeader.Authorization].Split(' ')[1];
                authString = Encoding.GetEncoding("ISO-8859-1").GetString(Convert.FromBase64String(authString));
                string[] parts = authString.Split(':');
                username = parts[0];
                password = parts[1];
            }
            else
            {
                username = null;
                password = null;
            }
            authenticated = Authenticate(request, username, password);
            if (authenticated)
                _currentUserName = username;
            else
            {
                response.SetHeader(HTTPHeader.WWWAuthenticate, "Basic Realm=\"" + Realm + "\"");
                response.WriteError(HTTPStatusCode.Unauthorized, "Unauthorized");
            }
            return authenticated;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username">null if no Authorization received in HTTP headers</param>
        /// <param name="password">null if no Authorization received in HTTP headers</param>
        /// <returns></returns>
        protected abstract bool Authenticate(HTTPRequest request, string username, string password);

    }

    public class AuthenticationCheckEventArgs : EventArgs
    {
        public AuthenticationCheckEventArgs(HTTPRequest request, string username, string password)
        {
            Request = request;
            Username = username;
            Password = password;
        }
        public HTTPRequest Request { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public bool IsAuthenticated { get; set; }
    }

    public delegate void AuthenticationCheck(object sender, AuthenticationCheckEventArgs e);

    public class SimpleAuthenticator : BaseAuthenticator
    {
        public SimpleAuthenticator(string realm, AuthenticationCheck authenticationCheck) :
            base(realm)
        {
            if (authenticationCheck != null)
                AuthenticationCheck = authenticationCheck;
            else
                throw new NotImplementedException("No DoCheck delegate was specified for WWWAuthenticator.");
        }

        protected override bool Authenticate(HTTPRequest request, string username, string password)
        {
            AuthenticationCheckEventArgs eventArgs = new AuthenticationCheckEventArgs(request, 
                username, password);
            eventArgs.IsAuthenticated = false;
            AuthenticationCheck(this, eventArgs);
            return eventArgs.IsAuthenticated;
        }

        private AuthenticationCheck AuthenticationCheck;
    }

}
