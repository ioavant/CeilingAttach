using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CeilingAttach
{
    [Transaction(TransactionMode.Manual)]
    public class AttachToFloorCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            double maxHeightFeet = App.SearchHeightInternalUnits;
            double gapFeet       = App.GapInternalUnits;

            // Format the max search height in the project's length unit for error messages.
            FormatOptions fo = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
            ForgeTypeId displayUnit = fo.GetUnitTypeId();
            string unitLabel = LabelUtils.GetLabelForUnit(displayUnit);
            string searchDisplay =
                $"{UnitUtils.ConvertFromInternalUnits(maxHeightFeet, displayUnit):G6} {unitLabel}";

            // Pre-selected elements → batch mode (one transaction, summary dialog).
            // Nothing selected → click loop (one transaction per element, auto-toast on success).
            var preSelected = uidoc.Selection.GetElementIds();
            if (preSelected.Count > 0)
            {
                var batch = preSelected
                    .Select(id => doc.GetElement(id))
                    .Where(SelectionService.IsAttachableElement)
                    .ToList();
                RunBatch(doc, batch, maxHeightFeet, gapFeet, searchDisplay, displayUnit, unitLabel);
            }
            else
            {
                RunClickLoop(uidoc, doc, maxHeightFeet, gapFeet, searchDisplay, displayUnit, unitLabel);
            }

            return Result.Succeeded;
        }

        // ── Batch mode ───────────────────────────────────────────────────────────
        // One transaction for all elements; shows a summary at the end.

        private static void RunBatch(Document doc, IList<Element> selectedElements,
            double maxHeightFeet, double gapFeet,
            string searchDisplay, ForgeTypeId displayUnit, string unitLabel)
        {
            var movedLines  = new List<string>();
            var failedLines = new List<string>();

            using (Transaction tx = new Transaction(doc, "Attach Elements To Floor"))
            {
                tx.Start();
                try
                {
                    foreach (Element element in selectedElements)
                    {
                        bool attached = FloorFinder.AttachToNearestFloor(
                            doc, element, maxHeightFeet, gapFeet, out double movedFeet);

                        if (attached)
                            doc.Regenerate();

                        string label = $"{element.Name} (Id: {element.Id.GetValue()})";

                        if (attached)
                        {
                            double movedDisplay = UnitUtils.ConvertFromInternalUnits(movedFeet, displayUnit);
                            string direction = movedFeet > 0 ? "lifted" : "lowered";
                            movedLines.Add($"{label}  →  {direction} by {Math.Abs(movedDisplay):G4} {unitLabel}");
                        }
                        else
                        {
                            failedLines.Add(label);
                        }
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    TaskDialog.Show("Ceiling Attach — Error", ex.Message);
                    return;
                }
            }

            if (movedLines.Count > 0)
                TaskDialog.Show("Ceiling Attach — Done",
                    $"Successfully attached {movedLines.Count} of {selectedElements.Count} elements:\n\n" +
                    string.Join("\n", movedLines));

            if (failedLines.Count > 0)
                TaskDialog.Show("Ceiling Attach — Warning",
                    $"Could not attach {failedLines.Count} of {selectedElements.Count} elements.\n" +
                    $"No floor found within {searchDisplay} search height.\n\n" +
                    string.Join("\n", failedLines));
        }

        // ── Click loop ───────────────────────────────────────────────────────────
        // One element at a time. Success → auto-closing 3-second toast, then next pick.
        // Failure → modal dialog requiring OK, then next pick. Esc → exit.

        private static void RunClickLoop(UIDocument uidoc, Document doc,
            double maxHeightFeet, double gapFeet,
            string searchDisplay, ForgeTypeId displayUnit, string unitLabel)
        {
            while (true)
            {
                Element element = SelectionService.PickOneElement(uidoc);
                if (element == null) break; // Esc pressed

                bool attached;
                double movedFeet = 0;

                using (Transaction tx = new Transaction(doc, "Attach To Floor"))
                {
                    tx.Start();
                    try
                    {
                        attached = FloorFinder.AttachToNearestFloor(
                            doc, element, maxHeightFeet, gapFeet, out movedFeet);

                        if (attached)
                            doc.Regenerate();

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        TaskDialog.Show("Ceiling Attach — Error", ex.Message);
                        continue;
                    }
                }

                if (attached)
                {
                    double movedDisplay = UnitUtils.ConvertFromInternalUnits(movedFeet, displayUnit);
                    string dir = movedFeet > 0 ? "lifted" : "lowered";
                    ToastWindow.Show(
                        "Ceiling Attach — Done",
                        $"{element.Name}\n→ {dir} by {Math.Abs(movedDisplay):G4} {unitLabel}",
                        autoCloseSeconds: 1);
                }
                else
                {
                    TaskDialog.Show("Ceiling Attach — Not found",
                        $"No floor or roof found within {searchDisplay} above:\n" +
                        $"{element.Name} (Id: {element.Id.GetValue()})");
                }
            }
        }
    }
}
