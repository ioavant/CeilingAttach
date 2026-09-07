using Autodesk.Revit.DB;
using System;
using System.Reflection;

public static class ElementIdExtensions
{
    private static readonly Func<ElementId, long> _getValue = BuildGetter();

    private static Func<ElementId, long> BuildGetter()
    {
        // Revit 2026+: property is called "Value" and returns long
        var valueProp = typeof(ElementId).GetProperty("Value");
        if (valueProp != null)
            return id => (long)valueProp.GetValue(id);

        // Revit 2022-2025: property is called "IntegerValue" and returns int
        var intProp = typeof(ElementId).GetProperty("IntegerValue");
        if (intProp != null)
            return id => (long)(int)intProp.GetValue(id);

        throw new InvalidOperationException(
            "Cannot find Value or IntegerValue on ElementId. Unknown Revit version.");
    }

    public static long GetValue(this ElementId id) => _getValue(id);
}