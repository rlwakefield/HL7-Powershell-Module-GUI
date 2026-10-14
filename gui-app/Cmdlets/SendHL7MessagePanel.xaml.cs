using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HL7Tools;

namespace HL7ToolsGUI.Cmdlets
{
    public partial class SendHL7MessagePanel : UserControl, ICmdletPanel
    {
        private IPanelHost host;

        public SendHL7MessagePanel()
        {
            InitializeComponent();
        }

        public string DisplayName
        {
            get { return "Send HL7 Message"; }
        }

        public UserControl View
        {
            get { return this; }
        }

        public void Initialize(IPanelHost host)
        {
            this.host = host;
        }

        private void UseTlsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SkipCertificateCheckBox.IsEnabled = UseTlsCheckBox.IsChecked == true;
            if (UseTlsCheckBox.IsChecked != true)
            {
                SkipCertificateCheckBox.IsChecked = false;
            }
        }

        private void SourceMode_Checked(object sender, RoutedEventArgs e)
        {
            // fires during InitializeComponent before all controls exist
            if (PathInputPanel == null || MessageTextBox == null)
            {
                return;
            }
            bool isMessage = MessageRadioButton.IsChecked == true;
            PathInputPanel.Visibility = isMessage ? Visibility.Collapsed : Visibility.Visible;
            MessageTextBox.Visibility = isMessage ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Title = "Select HL7 message file(s)",
            };
            if (dialog.ShowDialog(host == null ? null : host.Window) == true)
            {
                PathTextBox.Text = string.Join(";", dialog.FileNames);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await host.RunPanelAsync(this, SendButton);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            HostNameTextBox.Clear();
            PortTextBox.Clear();
            PathTextBox.Clear();
            MessageTextBox.Clear();
            DelayTextBox.Text = "0";
            EncodingComboBox.SelectedIndex = 0;
            NoAckCheckBox.IsChecked = false;
            UseTlsCheckBox.IsChecked = false;
            SkipCertificateCheckBox.IsChecked = false;
            PathRadioButton.IsChecked = true;
            host.ClearLog();
            host.SetStatus("Ready");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            host.Close();
        }

        private SendHL7Request BuildRequest()
        {
            SendSourceMode mode = MessageRadioButton.IsChecked == true ? SendSourceMode.MessageText
                : LiteralPathRadioButton.IsChecked == true ? SendSourceMode.LiteralPath
                : SendSourceMode.Path;
            ComboBoxItem encoding = EncodingComboBox.SelectedItem as ComboBoxItem;
            return SendHL7Request.Parse(HostNameTextBox.Text, PortTextBox.Text, DelayTextBox.Text, mode,
                PathTextBox.Text, MessageTextBox.Text, encoding == null ? null : (string)encoding.Content,
                NoAckCheckBox.IsChecked == true, UseTlsCheckBox.IsChecked == true, SkipCertificateCheckBox.IsChecked == true);
        }

        public Task RunAsync(IPanelHost host)
        {
            // read the UI on the UI thread, then send on a background thread
            SendHL7Request request = BuildRequest();
            return Task.Run(() => Send(request, host));
        }

        private static void Send(SendHL7Request request, IPanelHost host)
        {
            HL7MessageSender sender = new HL7MessageSender
            {
                HostName = request.HostName,
                Port = request.Port,
                EncodingName = request.Encoding,
                NoACK = request.NoAck,
                UseTls = request.UseTls,
                SkipCertificateCheck = request.SkipCertificateCheck,
                Log = host.Log,
            };

            if (request.Mode == SendSourceMode.MessageText)
            {
                SendOne(sender, SendHL7Request.ToHL7Text(request.MessageText), null, host);
                return;
            }

            var files = HL7FileResolver.ResolveFiles(request.Paths, request.Mode == SendSourceMode.Path);
            if (files.Count == 0)
            {
                host.Log("ERROR: No matching files found.");
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                string contents;
                try
                {
                    if (!File.Exists(file))
                    {
                        host.Log("ERROR: File not found: " + file);
                        return;
                    }
                    contents = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    host.Log("ERROR: Unable to open file " + file + ": " + ex.Message);
                    return;
                }

                if (!SendOne(sender, contents, file, host))
                {
                    return;
                }

                if (request.Delay > 0 && i < files.Count - 1)
                {
                    host.Log("Waiting " + request.Delay + " second(s) ...");
                    System.Threading.Thread.Sleep(request.Delay * 1000);
                }
            }
        }

        private static bool SendOne(HL7MessageSender sender, string content, string filePath, IPanelHost host)
        {
            try
            {
                SendHL7MessageResult result = sender.Send(content, filePath);
                host.Log("Status:      " + result.Status);
                host.Log("Time sent:   " + result.TimeSent.ToString("yyyy-MM-dd HH:mm:ss"));
                host.Log("Remote host: " + result.RemoteHost + ":" + result.Port);
                if (!string.IsNullOrEmpty(result.Filename))
                {
                    host.Log("File:        " + result.Filename);
                }
                host.Log("Elapsed:     " + result.ElapsedSeconds.ToString("0.000") + " s");
                if (result.ACKMessage != null)
                {
                    host.Log("ACK:");
                    foreach (string line in result.ACKMessage)
                    {
                        host.Log("  " + line);
                    }
                }
                host.Log(string.Empty);
                return true;
            }
            catch (ArgumentException)
            {
                host.Log(filePath != null
                    ? "ERROR: The file does not appear to be a valid HL7 v2 message: " + filePath
                    : "ERROR: The message text does not appear to be a valid HL7 v2 message.");
            }
            catch (SocketException ex)
            {
                host.Log("ERROR: Connection error (" + sender.HostName + ":" + sender.Port + "): " + ex.Message);
            }
            catch (Exception ex)
            {
                host.Log("ERROR: " + ex.Message);
            }
            return false;
        }
    }
}
