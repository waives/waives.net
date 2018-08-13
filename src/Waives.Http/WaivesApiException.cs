using System;

namespace Waives.Http
{
    public class WaivesApiException : Exception
    {
        public WaivesApiException()
        {
        }

        public WaivesApiException(string message) : base(message)
        {
        }

        public WaivesApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}