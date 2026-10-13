using System;
using System.Collections.Generic;
using System.Linq;

namespace HL7ToolsGUI.Cmdlets
{
    public enum SendSourceMode
    {
        Path,
        LiteralPath,
        MessageText,
    }

    /// <summary>
    /// Validated input for the Send HL7 Message panel. Contains no UI dependencies.
    /// </summary>
    public class SendHL7Request
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public int Delay { get; set; }
        public SendSourceMode Mode { get; set; }
        public string[] Paths { get; set; }
        public string MessageText { get; set; }
        public string Encoding { get; set; }
        public bool NoAck { get; set; }
        public bool UseTls { get; set; }
        public bool SkipCertificateCheck { get; set; }

        /// <summary>
        /// Validate the raw input values, using the same rules as the PowerShell GUI.
        /// </summary>
        public static SendHL7Request Parse(string hostName, string port, string delay, SendSourceMode mode,
            string paths, string messageText, string encoding, bool noAck, bool useTls, bool skipCertificateCheck)
        {
            hostName = (hostName ?? string.Empty).Trim();
            if (hostName.Length == 0)
            {
                throw new InputValidationException("Enter a host name or IP address.");
            }

            int portValue;
            if (!int.TryParse((port ?? string.Empty).Trim(), out portValue) || portValue < 1 || portValue > 65535)
            {
                throw new InputValidationException("Port must be a whole number from 1 through 65535.");
            }

            int delayValue;
            if (!int.TryParse((delay ?? string.Empty).Trim(), out delayValue) || delayValue < 0 || delayValue > 600)
            {
                throw new InputValidationException("Delay must be a whole number from 0 through 600 seconds.");
            }

            SendHL7Request request = new SendHL7Request
            {
                HostName = hostName,
                Port = portValue,
                Delay = delayValue,
                Mode = mode,
                Encoding = string.IsNullOrEmpty(encoding) ? "UTF-8" : encoding,
                NoAck = noAck,
                UseTls = useTls,
                SkipCertificateCheck = useTls && skipCertificateCheck,
            };

            if (mode == SendSourceMode.MessageText)
            {
                if (string.IsNullOrWhiteSpace(messageText))
                {
                    throw new InputValidationException("Enter the HL7 message text to send.");
                }
                request.MessageText = messageText;
            }
            else
            {
                request.Paths = (paths ?? string.Empty)
                    .Split(new[] { ',', ';' })
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToArray();
                if (request.Paths.Length == 0)
                {
                    throw new InputValidationException("Enter or browse to at least one message file.");
                }
            }

            return request;
        }

        /// <summary>
        /// Convert message text to a HL7 message string, with segments delimited by CR.
        /// </summary>
        public static string ToHL7Text(string messageText)
        {
            string[] lines = messageText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            return string.Concat(lines.Select(line => line + (char)0x0D));
        }
    }
}
