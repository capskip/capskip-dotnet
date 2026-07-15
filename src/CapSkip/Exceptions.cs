using System;

namespace CapSkip
{
    /// <summary>Base exception for all CapSkip SDK errors.</summary>
    public class CapSkipError : Exception
    {
        /// <summary>Create a CapSkip error with a message.</summary>
        public CapSkipError(string? message = null)
            : base(message)
        {
        }

        /// <summary>Create a CapSkip error wrapping an underlying exception.</summary>
        public CapSkipError(Exception inner)
            : base(inner.Message, inner)
        {
        }

        /// <summary>Create a CapSkip error with a message and an underlying cause.</summary>
        public CapSkipError(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>Invalid or unsupported parameters.</summary>
    public class ValidationException : CapSkipError
    {
        /// <summary>Create a validation error with a message.</summary>
        public ValidationException(string? message = null) : base(message) { }

        /// <summary>Create a validation error with a message and an underlying cause.</summary>
        public ValidationException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Connection failure or captcha not ready.</summary>
    public class NetworkException : CapSkipError
    {
        /// <summary>Create a network error with a message.</summary>
        public NetworkException(string? message = null) : base(message) { }

        /// <summary>Create a network error wrapping an underlying exception.</summary>
        public NetworkException(Exception inner) : base(inner) { }

        /// <summary>Create a network error with a message and an underlying cause.</summary>
        public NetworkException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>CapSkip API returned an error.</summary>
    public class ApiException : CapSkipError
    {
        /// <summary>Create an API error with a message.</summary>
        public ApiException(string? message = null) : base(message) { }

        /// <summary>Create an API error with a message and an underlying cause.</summary>
        public ApiException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Polling exceeded the configured timeout.</summary>
    public class TimeoutException : CapSkipError
    {
        /// <summary>Create a timeout error with a message.</summary>
        public TimeoutException(string? message = null) : base(message) { }

        /// <summary>Create a timeout error with a message and an underlying cause.</summary>
        public TimeoutException(string message, Exception inner) : base(message, inner) { }
    }
}
