using System;

namespace HRD.Api.Exceptions
{
    public class HRDException : Exception
    {
        public HRDException(string message) : base(message) { }
        public HRDException(string message, Exception inner) : base(message, inner) { }
    }

    public class HRDApiCommunicationException : HRDException
    {
        public string Details { get; }
        public HRDApiCommunicationException(string message, string details = null) : base(message) 
        {
            Details = details;
        }
    }

    public class HRDApiIncorrectDataException : HRDException
    {
        public HRDApiIncorrectDataException(string message) : base(message) { }
    }

    public class HRDApiIncorrectDataReceivedException : HRDException
    {
        public HRDApiIncorrectDataReceivedException(string message) : base(message) { }
    }
}
