using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

public static class SelectionService
{
    public static IList<Element> PickElements(UIDocument uidoc)
    {
        IList<Reference> refs;

        try
        {
            refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new ModelElementFilter(),
                "Select elements to attach to floor");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return null;
        }

        return refs
            .Select(r => uidoc.Document.GetElement(r))
            .Where(e => e != null)
            .ToList();
    }

    public static Element PickOneElement(UIDocument uidoc)
    {
        Reference r;
        try
        {
            r = uidoc.Selection.PickObject(
                ObjectType.Element,
                new ModelElementFilter(),
                "Click element to attach — Esc to finish");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return null;
        }
        return uidoc.Document.GetElement(r);
    }

    public static bool IsAttachableElement(Element elem)
    {
        if (elem == null) return false;
        Category cat = elem.Category;
        if (cat == null) return false;
        if (cat.CategoryType != CategoryType.Model) return false;
        if (cat.Id.GetValue() == (int)BuiltInCategory.OST_Lines) return false;
        return true;
    }

    private class ModelElementFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => SelectionService.IsAttachableElement(elem);
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}