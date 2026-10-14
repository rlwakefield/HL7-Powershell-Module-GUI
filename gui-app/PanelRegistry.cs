using System;
using System.Collections.Generic;
using HL7ToolsGUI.Cmdlets;

namespace HL7ToolsGUI
{
    /// <summary>
    /// The list of available cmdlet panels. Add new panels here.
    /// </summary>
    public static class PanelRegistry
    {
        public static IReadOnlyList<Func<ICmdletPanel>> Factories { get; } = new List<Func<ICmdletPanel>>
        {
            () => new SendHL7MessagePanel(),
        };
    }
}
