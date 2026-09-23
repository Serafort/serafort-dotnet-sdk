using System;

namespace Serafort.SDK
{
    public class SerafortException : Exception
    {
        public SerafortException(string message) : base(message) { }
        public SerafortException(string message, Exception innerException) : base(message, innerException) { }
    }
}
