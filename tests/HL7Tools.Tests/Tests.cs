using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using HL7Tools;
using HL7ToolsGUI;
using HL7ToolsGUI.Cmdlets;
using Xunit;

namespace HL7Tools.Tests
{
    public class SendHL7RequestTests
    {
        private static SendHL7Request Parse(string host = "localhost", string port = "2575", string delay = "0",
            SendSourceMode mode = SendSourceMode.Path, string paths = "a.hl7", string message = "")
        {
            return SendHL7Request.Parse(host, port, delay, mode, paths, message, "UTF-8", false, false, false);
        }

        [Fact]
        public void ValidInputParses()
        {
            SendHL7Request r = Parse(paths: "a.hl7; b.hl7,c.hl7");
            Assert.Equal(2575, r.Port);
            Assert.Equal(new[] { "a.hl7", "b.hl7", "c.hl7" }, r.Paths);
        }

        [Theory]
        [InlineData("", "2575", "0")]
        [InlineData("host", "0", "0")]
        [InlineData("host", "65536", "0")]
        [InlineData("host", "abc", "0")]
        [InlineData("host", "2575", "601")]
        [InlineData("host", "2575", "-1")]
        public void InvalidInputThrows(string host, string port, string delay)
        {
            Assert.Throws<InputValidationException>(() => Parse(host, port, delay));
        }

        [Fact]
        public void EmptyPathsAndMessageThrow()
        {
            Assert.Throws<InputValidationException>(() => Parse(paths: " ; "));
            Assert.Throws<InputValidationException>(() => Parse(mode: SendSourceMode.MessageText, message: "  "));
        }

        [Fact]
        public void SkipCertificateRequiresTls()
        {
            SendHL7Request r = SendHL7Request.Parse("h", "1", "0", SendSourceMode.Path, "a", "", "UTF-8", false, false, true);
            Assert.False(r.SkipCertificateCheck);
        }

        [Fact]
        public void MessageTextUsesCarriageReturns()
        {
            Assert.Equal("MSH|^~\\&|A\rPID|1\r", SendHL7Request.ToHL7Text("MSH|^~\\&|A\r\nPID|1"));
        }
    }

    public class HL7FileResolverTests
    {
        [Fact]
        public void ExpandsWildcardsAndDirectories()
        {
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "a.hl7"), "x");
                File.WriteAllText(Path.Combine(dir, "b.hl7"), "x");
                File.WriteAllText(Path.Combine(dir, "c.txt"), "x");
                string childDir = Path.Combine(dir, "child");
                Directory.CreateDirectory(childDir);
                File.WriteAllText(Path.Combine(childDir, "d.hl7"), "x");
                Assert.Equal(2, HL7FileResolver.ResolveFiles(new[] { Path.Combine(dir, "*.hl7") }, true).Count);
                Assert.Single(HL7FileResolver.ResolveFiles(new[] { Path.Combine(dir, "*", "*.hl7") }, true));
                Assert.Equal(3, HL7FileResolver.ResolveFiles(new[] { dir }, false).Count);
                Assert.Single(HL7FileResolver.ResolveFiles(new[] { Path.Combine(dir, "*.hl7") }, false));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }

    public class HL7MessageSenderTests
    {
        [Fact]
        public async Task SendsFramedMessageAndReturnsAck()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string received = null;
            Task server = Task.Run(() =>
            {
                using (TcpClient client = listener.AcceptTcpClient())
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[4096];
                    int n = stream.Read(buffer, 0, buffer.Length);
                    received = Encoding.UTF8.GetString(buffer, 0, n);
                    byte[] ack = Encoding.UTF8.GetBytes("\u000bMSH|^~\\&|R|R|S|S|20200101||ACK|1|P|2.3\rMSA|AA|1\r\u001c\r");
                    stream.Write(ack, 0, ack.Length);
                }
            });

            HL7MessageSender sender = new HL7MessageSender { HostName = "127.0.0.1", Port = port };
            SendHL7MessageResult result = sender.Send("MSH|^~\\&|S|S|R|R|20200101||ADT^A01|1|P|2.3\rPID|1\r");
            await server;
            listener.Stop();

            Assert.Equal("Successful", result.Status);
            Assert.StartsWith("\u000bMSH", received);
            Assert.NotNull(result.ACKMessage);
            Assert.Contains(result.ACKMessage, l => l.StartsWith("MSA|AA"));
        }

        [Fact]
        public void InvalidMessageThrowsArgumentException()
        {
            HL7MessageSender sender = new HL7MessageSender { HostName = "127.0.0.1", Port = 1 };
            Assert.Throws<ArgumentException>(() => sender.Send("not hl7"));
        }
    }
}
