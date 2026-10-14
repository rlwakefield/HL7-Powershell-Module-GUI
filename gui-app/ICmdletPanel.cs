using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HL7ToolsGUI
{
    /// <summary>
    /// A panel providing the GUI for a single cmdlet.
    /// </summary>
    public interface ICmdletPanel
    {
        /// <summary>Name shown in the navigation list.</summary>
        string DisplayName { get; }

        /// <summary>The view hosted in the main window.</summary>
        UserControl View { get; }

        /// <summary>Called once when the panel is created, to connect it to the main window.</summary>
        void Initialize(IPanelHost host);

        /// <summary>Validate the input and run the command. Invoked off the UI thread's critical path via the host.</summary>
        Task RunAsync(IPanelHost host);
    }

    /// <summary>
    /// Services the main window provides to panels.
    /// </summary>
    public interface IPanelHost
    {
        Window Window { get; }

        /// <summary>Append a line to the log box (safe to call from any thread).</summary>
        void Log(string text);

        void ClearLog();

        void SetStatus(string text);

        void Close();

        /// <summary>
        /// Run the panel asynchronously, disabling the run button while it executes.
        /// Reports validation and runtime errors in the log and status bar.
        /// </summary>
        Task RunPanelAsync(ICmdletPanel panel, Control runButton);
    }
}
