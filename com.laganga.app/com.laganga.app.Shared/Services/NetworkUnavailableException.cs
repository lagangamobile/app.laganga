using System;

namespace com.laganga.app.Shared.Services;


public class NetworkUnavailableException : Exception
{
    public NetworkUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}



