/* Filename:    HL7MessageSender.cs
 *
 * Notes:       PowerShell-independent logic for sending a HL7 v2 message via TCP (framed using MLLP).
 *              Shared by the Send-HL7Message cmdlet and the HL7 Tools GUI application.
 */

namespace HL7Tools
{
    using System;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    /// <summary>
    /// Sends HL7 messages to a remote host using MLLP framing, with optional TLS.
    /// </summary>
    public class HL7MessageSender
    {
        /// <summary>Remote hostname or IP address</summary>
        public string HostName { get; set; }

        /// <summary>Remote TCP port</summary>
        public int Port { get; set; }

        /// <summary>Text encoding name (e.g. UTF-8, ISO-8859-1)</summary>
        public string EncodingName { get; set; } = "UTF-8";

        /// <summary>Do not wait for an ACK response</summary>
        public bool NoACK { get; set; }

        /// <summary>Secure the connection using TLS</summary>
        public bool UseTls { get; set; }

        /// <summary>Ignore TLS certificate errors</summary>
        public bool SkipCertificateCheck { get; set; }

        /// <summary>Optional callback for diagnostic (verbose) messages</summary>
        public Action<string> Log { get; set; }

        private void WriteLog(string text)
        {
            if (Log != null)
            {
                Log(text);
            }
        }

        /// <summary>
        /// Send a single message. Throws ArgumentException if the content is not a valid HL7 v2 message,
        /// and SocketException if the connection fails.
        /// </summary>
        public SendHL7MessageResult Send(string messageContent, string filePath = null)
        {
            TcpClient tcpConnection = new TcpClient();
            tcpConnection.SendTimeout = 10000;
            tcpConnection.ReceiveTimeout = 10000;
            try
            {
                System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();

                // this will validate the content is a HL7 v2 message.
                HL7Message message = new HL7Message(messageContent);
                WriteLog("Connecting to " + HostName + ":" + Port);

                timer.Start();
                tcpConnection.Connect(HostName, Port);

                string[] ackLines = UseTls
                    ? SendMessageTLS(tcpConnection, message)
                    : SendMessage(tcpConnection, message);

                timer.Stop();
                SendHL7MessageResult result = new SendHL7MessageResult("Successful", ackLines, DateTime.Now, message.ToString().Split((char)0x0D), HostName, Port, filePath, timer.Elapsed.TotalMilliseconds / 1000);
                WriteLog("Closing TCP session\n");
                return result;
            }
            finally
            {
                tcpConnection.Close();
            }
        }

        private string[] SendMessageTLS(TcpClient connection, HL7Message message)
        {
            Encoding encoder = Encoding.GetEncoding(EncodingName);
            WriteLog("Encoding: " + encoder.EncodingName);

            WriteLog("Using TLS");
            SslStream sslStream;
            if (!SkipCertificateCheck)
            {
                WriteLog("Enforcing certificate validation");
                sslStream = new SslStream(connection.GetStream());
            }
            else
            {
                WriteLog("Ignoring certificate validation errors");
                sslStream = new SslStream(connection.GetStream(), false, new RemoteCertificateValidationCallback(SkipServerCertificateValidation), null);
            }
            sslStream.AuthenticateAsClient(HostName);

            byte[] writeBuffer = encoder.GetBytes(message.GetMLLPFramedMessage());
            sslStream.Write(writeBuffer, 0, writeBuffer.Length);
            sslStream.Flush();
            WriteLog("Message sent");

            string[] ackLines = null;
            if (!NoACK)
            {
                WriteLog("Waiting for ACK ...");
                byte[] readBuffer = new byte[4096];
                int bytesRead = sslStream.Read(readBuffer, 0, 4096);
                ackLines = StripMLLPFrame(encoder.GetString(readBuffer, 0, bytesRead));
            }
            sslStream.Close();
            return ackLines;
        }

        private string[] SendMessage(TcpClient connection, HL7Message message)
        {
            Encoding encoder = Encoding.GetEncoding(EncodingName);
            WriteLog("Encoding: " + encoder.EncodingName);

            NetworkStream tcpStream = connection.GetStream();

            byte[] writeBuffer = encoder.GetBytes(message.GetMLLPFramedMessage());
            tcpStream.Write(writeBuffer, 0, writeBuffer.Length);
            tcpStream.Flush();
            WriteLog("Message sent");

            string[] ackLines = null;
            if (!NoACK)
            {
                WriteLog("Waiting for ACK ...");
                byte[] readBuffer = new byte[4096];
                int bytesRead = tcpStream.Read(readBuffer, 0, 4096);
                ackLines = StripMLLPFrame(encoder.GetString(readBuffer, 0, bytesRead));
            }
            tcpStream.Close();
            return ackLines;
        }

        /// <summary>
        /// Strip the MLLP framing from the message string
        /// </summary>
        public static string[] StripMLLPFrame(string mllpFramedMessage)
        {
            string[] messageLines = null;
            int start = mllpFramedMessage.IndexOf((char)0x0B);
            if (start >= 0)
            {
                int end = mllpFramedMessage.IndexOf((char)0x1C);
                if (end > start)
                {
                    string ackString = mllpFramedMessage.Substring(start + 1, end - 1);
                    if (ackString.Length > 0 && ackString[ackString.Length - 1] == (char)0x0D)
                    {
                        ackString = ackString.Substring(0, ackString.Length - 1);
                    }
                    messageLines = ackString.Split((char)0x0D);
                }
            }
            return messageLines;
        }

        /// <summary>
        /// Used by the RemoteCertificateValidationCallback. Always return true, to ignore certificate errors.
        /// </summary>
        public static bool SkipServerCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
