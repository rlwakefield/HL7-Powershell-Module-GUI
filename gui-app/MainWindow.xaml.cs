using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HL7ToolsGUI
{
    public partial class MainWindow : Window, IPanelHost
    {
        private readonly List<ICmdletPanel> panels = new List<ICmdletPanel>();

        public MainWindow()
        {
            InitializeComponent();

            foreach (Func<ICmdletPanel> factory in PanelRegistry.Factories)
            {
                ICmdletPanel panel = factory();
                panel.Initialize(this);
                panels.Add(panel);
            }
            CmdletList.ItemsSource = panels;
            if (panels.Count > 0)
            {
                CmdletList.SelectedIndex = 0;
            }
        }

        Window IPanelHost.Window
        {
            get { return this; }
        }

        private void CmdletList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ICmdletPanel panel = CmdletList.SelectedItem as ICmdletPanel;
            PanelHost.Content = panel == null ? null : panel.View;
            SetStatus("Ready");
        }

        public void Log(string text)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(text + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }

        public void ClearLog()
        {
            Dispatcher.Invoke(() => LogTextBox.Clear());
        }

        public void SetStatus(string text)
        {
            Dispatcher.Invoke(() => StatusText.Text = text);
        }

        void IPanelHost.Close()
        {
            Close();
        }

        public async Task RunPanelAsync(ICmdletPanel panel, Control runButton)
        {
            runButton.IsEnabled = false;
            SetStatus("Running ...");
            try
            {
                await panel.RunAsync(this);
                SetStatus("Completed");
            }
            catch (InputValidationException ex)
            {
                MessageBox.Show(this, ex.Message, "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Validation failed");
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                SetStatus("Failed");
            }
            finally
            {
                runButton.IsEnabled = true;
            }
        }
    }
}
