using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;

namespace CeilingAttach
{
    [Transaction(TransactionMode.ReadOnly)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            var window = new SettingsWindow(doc);
            window.GapInternalUnits = App.GapInternalUnits;
            window.SearchHeightInternalUnits = App.SearchHeightInternalUnits;

            WindowInteropHelper helper = new WindowInteropHelper(window);
            helper.Owner = commandData.Application.MainWindowHandle;

            if (window.ShowDialog() == true)
            {
                App.GapInternalUnits = window.GapInternalUnits;
                App.SearchHeightInternalUnits = window.SearchHeightInternalUnits;
            }

            return Result.Succeeded;
        }
    }
}
