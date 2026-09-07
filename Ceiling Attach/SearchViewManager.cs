using System;
using System.Linq;
using Autodesk.Revit.DB;

public static class SearchViewManager
{
    private const string ViewName = "_CeilingAttach_SearchView";
    private const double XYMarginFeet = 10.0; // ~3000 мм

    public static View3D GetOrCreate(Document doc)
    {
        View3D view = new FilteredElementCollector(doc)
            .OfClass(typeof(View3D))
            .Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && v.Name == ViewName);

        if (view != null)
        {
            EnsureValid(view);
            return view;
        }

        return Create(doc);
    }

    private static View3D Create(Document doc)
    {
  
            ViewFamilyType vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

            View3D view = View3D.CreateIsometric(doc, vft.Id);
            view.Name = ViewName;
            view.DetailLevel = ViewDetailLevel.Fine;
            view.ViewTemplateId = ElementId.InvalidElementId;

            ConfigureCategories(doc, view);
            return view;
    }

    private static void EnsureValid(View3D view)
    {
        if (view.IsTemplate)
            throw new InvalidOperationException("Search view cannot be template.");

        if (view.ViewTemplateId != ElementId.InvalidElementId)
            view.ViewTemplateId = ElementId.InvalidElementId;
    }

    private static void ConfigureCategories(Document doc, View3D view)
    {
        foreach (Category cat in doc.Settings.Categories)
        {
            try { view.SetCategoryHidden(cat.Id, true); }
            catch { }
        }

        // Avoid BuiltInCategory[] array literal — the .NET 9 compiler encodes
        // enum constant arrays as a binary blob that .NET Framework 4.8's
        // RuntimeHelpers.InitializeArray rejects with ArgumentException.
        UnhideCategory(doc, view, BuiltInCategory.OST_Floors);
        UnhideCategory(doc, view, BuiltInCategory.OST_Roofs);
        UnhideCategory(doc, view, BuiltInCategory.OST_RvtLinks);
    }

    private static void UnhideCategory(Document doc, View3D view, BuiltInCategory bic)
    {
        Category cat = doc.Settings.Categories.get_Item(bic);
        if (cat != null)
            view.SetCategoryHidden(cat.Id, false);
    }

    // 🔥 Главная часть — динамический SectionBox
    public static void UpdateSectionBox(View3D view, Element element, double maxSearchHeightFeet)
    {
        Document doc = view.Document;

        BoundingBoxXYZ bb = element.get_BoundingBox(null);
        if (bb == null) return;

        XYZ min = bb.Min;
        XYZ max = bb.Max;

        XYZ sectionMin = new XYZ(
            min.X - XYMarginFeet,
            min.Y - XYMarginFeet,
            min.Z);

        XYZ sectionMax = new XYZ(
            max.X + XYMarginFeet,
            max.Y + XYMarginFeet,
            max.Z + maxSearchHeightFeet);

        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
        sectionBox.Min = sectionMin;
        sectionBox.Max = sectionMax;

            view.IsSectionBoxActive = true;
            view.SetSectionBox(sectionBox);
    }
}