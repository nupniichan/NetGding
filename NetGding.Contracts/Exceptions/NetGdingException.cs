using System;

namespace NetGding.Contracts.Exceptions;

public sealed class NetGdingException : Exception
{
    public string ErrorCode { get; }
    public string Location { get; }

    public NetGdingException(string errorCode, string location, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Location = location;
    }
}
