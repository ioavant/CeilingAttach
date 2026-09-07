using System;
using System.Linq;
using Autodesk.Revit.DB;

public static class FloorFinder
{
    private static readonly double SpaceRayOffsetFeet =
        UnitUtils.ConvertToInternalUnits(1200, UnitTypeId.Millimeters);

    public static bool AttachToNearestFloor(
        Document doc,
        Element element,
        double maxSearchHeightFeet,
        double gapFeet,
        out double movedFeet)
    {
        movedFeet = 0;

        if (element == null) return false;

        BoundingBoxXYZ bb = element.get_BoundingBox(null);
        if (bb == null) return false;

        bool isSpace = element.Category?.Id.GetValue() ==
                       (int)BuiltInCategory.OST_MEPSpaces;

        return isSpace
            ? AttachSpace(doc, element, bb, maxSearchHeightFeet, gapFeet, out movedFeet)
            : AttachElement(doc, element, bb, maxSearchHeightFeet, gapFeet, out movedFeet);
    }

    // ─── Обычный элемент — оригинальная логика ──────────────────────────────
    private static bool AttachElement(
        Document doc, Element element, BoundingBoxXYZ bb,
        double maxSearchHeightFeet, double gapFeet, out double movedFeet)
    {
        movedFeet = 0;

        XYZ bottomCenter = new XYZ(
            (bb.Min.X + bb.Max.X) / 2.0,
            (bb.Min.Y + bb.Max.Y) / 2.0,
            bb.Min.Z);

        double elementHeight = bb.Max.Z - bb.Min.Z;

        View3D view = SearchViewManager.GetOrCreate(doc);
        SearchViewManager.UpdateSectionBox(view, element, maxSearchHeightFeet);

        ReferenceIntersector intersector = new ReferenceIntersector(
            new LogicalOrFilter(new ElementFilter[]
            {
                new ElementClassFilter(typeof(Floor)),
                new ElementClassFilter(typeof(RoofBase))
            }),
            FindReferenceTarget.Face,
            view);
        intersector.FindReferencesInRevitLinks = true;

        var hits = intersector.Find(bottomCenter, XYZ.BasisZ);
        var nearest = hits
            .Where(h => h.Proximity > 0 && h.Proximity <= maxSearchHeightFeet)
            .OrderBy(h => h.Proximity)
            .FirstOrDefault();

        if (nearest == null) return false;

        double floorZ = bottomCenter.Z + nearest.Proximity;
        double newBottomZ = floorZ - gapFeet - elementHeight;
        double moveDelta = newBottomZ - bottomCenter.Z;

        double tolerance = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Millimeters);
        if (Math.Abs(moveDelta) < tolerance) return false;

        movedFeet = moveDelta;

        ElementTransformUtils.MoveElement(doc, element.Id, new XYZ(0, 0, moveDelta));
        return true;
    }

    // ─── Space: луч из центра +1200мм, двигаем только верхний край ───────────
    private static bool AttachSpace(
        Document doc, Element element, BoundingBoxXYZ bb,
        double maxSearchHeightFeet, double gapFeet, out double movedFeet)
    {
        movedFeet = 0;

        // Стартуем на 1200мм выше низа Space чтобы не зацепить его собственную грань
        XYZ rayOrigin = new XYZ(
            (bb.Min.X + bb.Max.X) / 2.0,
            (bb.Min.Y + bb.Max.Y) / 2.0,
            bb.Min.Z + SpaceRayOffsetFeet);

        // If the Space is already taller than the configured search height, extend
        // the search to cover the Space's own height — the bounding slab cannot be
        // below the Space's current top.
        double currentSpaceHeight = bb.Max.Z - bb.Min.Z;
        double effectiveSearchHeight = Math.Max(maxSearchHeightFeet, currentSpaceHeight);

        View3D view = SearchViewManager.GetOrCreate(doc);
        SearchViewManager.UpdateSectionBox(view, element, effectiveSearchHeight);

        ReferenceIntersector intersector = new ReferenceIntersector(
            new LogicalOrFilter(new ElementFilter[]
            {
                new ElementClassFilter(typeof(Floor)),
                new ElementClassFilter(typeof(RoofBase))
            }),
            FindReferenceTarget.Face,
            view);
        intersector.FindReferencesInRevitLinks = true;

        var hits = intersector.Find(rayOrigin, XYZ.BasisZ);
        var nearest = hits
            .Where(h => h.Proximity > 0 && h.Proximity <= effectiveSearchHeight + SpaceRayOffsetFeet)
            .OrderBy(h => h.Proximity)
            .FirstOrDefault();

        if (nearest == null) return false;

        double floorZ = rayOrigin.Z + nearest.Proximity;
        double newHeight = floorZ - bb.Min.Z - gapFeet; // высота от низа Space до перекрытия

        if (newHeight <= 0) return false;

        Parameter limitOffset =
            element.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);
        if (limitOffset == null || limitOffset.IsReadOnly) return false;

        double oldHeight = limitOffset.AsDouble();
        double tolerance = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Millimeters);
        if (Math.Abs(newHeight - oldHeight) < tolerance) return false;

        movedFeet = newHeight - oldHeight;
        limitOffset.Set(newHeight);
        return true;
    }
}