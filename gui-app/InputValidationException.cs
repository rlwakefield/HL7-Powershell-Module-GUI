using System;

namespace HL7ToolsGUI
{
    /// <summary>
    /// Thrown by a panel when the user input is invalid.
    /// </summary>
    public class InputValidationException : Exception
    {
        public InputValidationException(string message) : base(message)
        {
        }
    }
}
