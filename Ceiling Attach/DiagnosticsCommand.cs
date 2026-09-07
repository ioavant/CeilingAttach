using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace CeilingAttach
{
    [Transaction(TransactionMode.Manual)]
    public class DiagnosticsCommand : IExternalCommand
    {
        private const string SearchViewName = "_CeilingAttach_SearchView";
        private static readonly double SpaceRayOffsetFeet =
            UnitUtils.ConvertToInternalUnits(1200, UnitTypeId.Millimeters);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Use pre-selected single element or ask to pick one
            Element element = null;
            var preSelected = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(SelectionService.IsAttachableElement)
                .ToList();

            if (preSelected.Count == 1)
            {
                element = preSelected[0];
            }
            else if (preSelected.Count > 1)
            {
                TaskDialog.Show("Diagnostics", "Выбери ровно ОДИН элемент для диагностики.");
                return Result.Cancelled;
            }
            else
            {
                try
                {
                    var r = uidoc.Selection.PickObject(ObjectType.Element, "Кликни элемент для диагностики");
                    element = doc.GetElement(r);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            string report = BuildReport(doc, element);
            ShowReport(commandData.Application.MainWindowHandle, report);
            return Result.Succeeded;
        }

        private static string BuildReport(Document doc, Element element)
        {
            var sb = new StringBuilder();

            // ── Unit helpers ──────────────────────────────────────────────────────
            FormatOptions fo = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
            ForgeTypeId unit = fo.GetUnitTypeId();
            string ul = LabelUtils.GetLabelForUnit(unit);
            string F(double feet) =>
                $"{UnitUtils.ConvertFromInternalUnits(feet, unit):G5} {ul}";

            // ── Element ───────────────────────────────────────────────────────────
            sb.AppendLine("═══ ЭЛЕМЕНТ ═══════════════════════════════════════════════════");
            sb.AppendLine($"Имя:       {element.Name}");
            sb.AppendLine($"Id:        {element.Id.GetValue()}");
            sb.AppendLine($"Категория: {element.Category?.Name ?? "null"}");
            bool isSpace = element.Category?.Id.GetValue() == (int)BuiltInCategory.OST_MEPSpaces;
            sb.AppendLine($"Тип:       {(isSpace ? "Space (MEP)" : "Обычный элемент")}");

            BoundingBoxXYZ bb = element.get_BoundingBox(null);
            if (bb == null)
            {
                sb.AppendLine("ОШИБКА: BoundingBox == null. Элемент не может быть обработан.");
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("═══ BOUNDING BOX ══════════════════════════════════════════════");
            sb.AppendLine($"Min Z:  {F(bb.Min.Z)}");
            sb.AppendLine($"Max Z:  {F(bb.Max.Z)}");
            sb.AppendLine($"Высота: {F(bb.Max.Z - bb.Min.Z)}");

            // ── Search settings ───────────────────────────────────────────────────
            double maxSearch = App.SearchHeightInternalUnits;
            double gap       = App.GapInternalUnits;
            double spaceHeight = bb.Max.Z - bb.Min.Z;
            double effectiveSearch = isSpace ? Math.Max(maxSearch, spaceHeight) : maxSearch;
            double rayZ = isSpace ? bb.Min.Z + SpaceRayOffsetFeet : bb.Min.Z;
            double maxProximity = effectiveSearch + (isSpace ? SpaceRayOffsetFeet : 0);

            sb.AppendLine();
            sb.AppendLine("═══ ПАРАМЕТРЫ ПОИСКА ══════════════════════════════════════════");
            sb.AppendLine($"Зазор:                  {F(gap)}");
            sb.AppendLine($"Настроенная высота:     {F(maxSearch)}");
            sb.AppendLine($"Эффективная высота:     {F(effectiveSearch)}" +
                          (isSpace && effectiveSearch > maxSearch ? "  ← расширено по высоте Space" : ""));
            sb.AppendLine($"Старт луча Z:           {F(rayZ)}");
            sb.AppendLine($"Макс. proximity:        {F(maxProximity)}");
            sb.AppendLine($"Перекрытие ожидается ≤ {F(rayZ + maxProximity)}");

            // ── Document contents ─────────────────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine("═══ СОДЕРЖИМОЕ ДОКУМЕНТА ══════════════════════════════════════");
            int floorCount = new FilteredElementCollector(doc).OfClass(typeof(Floor)).GetElementCount();
            int roofCount  = new FilteredElementCollector(doc).OfClass(typeof(RoofBase)).GetElementCount();
            int linkCount  = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).GetElementCount();
            sb.AppendLine($"Перекрытия (Floor):  {floorCount}");
            sb.AppendLine($"Кровли   (RoofBase): {roofCount}");
            sb.AppendLine($"Связанные файлы:     {linkCount}");

            // ── Search view + intersector (inside transaction) ────────────────────
            using (var tx = new Transaction(doc, "[Diag] CeilingAttach diagnostic"))
            {
                tx.Start();

                // --- View state ---
                sb.AppendLine();
                sb.AppendLine("═══ SEARCH VIEW ════════════════════════════════════════════════");
                View3D view = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate && v.Name == SearchViewName);

                if (view == null)
                {
                    sb.AppendLine("View не существует — будет создан сейчас.");
                    view = SearchViewManager.GetOrCreate(doc);
                    sb.AppendLine($"Создан: Id={view.Id.GetValue()}");
                }
                else
                {
                    sb.AppendLine($"View существует: Id={view.Id.GetValue()}");
                }

                sb.AppendLine($"ViewTemplateId: {view.ViewTemplateId.GetValue()} (должен быть -1)");
                if (view.ViewTemplateId != ElementId.InvalidElementId)
                {
                    sb.AppendLine("  ПРОБЛЕМА: применён ViewTemplate — снимаю.");
                    view.ViewTemplateId = ElementId.InvalidElementId;
                }

                // Check category visibility
                bool floorsHidden, roofsHidden, linksHidden;
                try
                {
                    var catFloors = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Floors);
                    var catRoofs  = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Roofs);
                    var catLinks  = doc.Settings.Categories.get_Item(BuiltInCategory.OST_RvtLinks);
                    floorsHidden = catFloors != null && view.GetCategoryHidden(catFloors.Id);
                    roofsHidden  = catRoofs  != null && view.GetCategoryHidden(catRoofs.Id);
                    linksHidden  = catLinks  != null && view.GetCategoryHidden(catLinks.Id);
                    sb.AppendLine($"Floors скрыты в view: {floorsHidden}");
                    sb.AppendLine($"Roofs  скрыты в view: {roofsHidden}");
                    sb.AppendLine($"Links  скрыты в view: {linksHidden}");
                    if (floorsHidden || roofsHidden || linksHidden)
                    {
                        sb.AppendLine("  ПРОБЛЕМА: категории скрыты — восстанавливаю.");
                        if (floorsHidden && catFloors != null) view.SetCategoryHidden(catFloors.Id, false);
                        if (roofsHidden  && catRoofs  != null) view.SetCategoryHidden(catRoofs.Id,  false);
                        if (linksHidden  && catLinks  != null) view.SetCategoryHidden(catLinks.Id,  false);
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Не удалось проверить видимость категорий: {ex.Message}");
                }

                // How many floors/roofs are visible in the view (before section box)
                int floorsVisible = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Floor)).GetElementCount();
                int roofsVisible = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(RoofBase)).GetElementCount();
                sb.AppendLine($"Floors видимых в view (без section box): {floorsVisible}");
                sb.AppendLine($"Roofs  видимых в view (без section box): {roofsVisible}");
                if (floorsVisible == 0 && roofsVisible == 0 && (floorCount + roofCount) > 0)
                    sb.AppendLine("  ПРОБЛЕМА: в документе есть перекрытия/кровли, но в view они не видны!");

                // --- Section box ---
                SearchViewManager.UpdateSectionBox(view, element, effectiveSearch);
                BoundingBoxXYZ sbox = view.GetSectionBox();
                sb.AppendLine();
                sb.AppendLine("═══ SECTION BOX ════════════════════════════════════════════════");
                sb.AppendLine($"Min: ({F(sbox.Min.X)}, {F(sbox.Min.Y)}, {F(sbox.Min.Z)})");
                sb.AppendLine($"Max: ({F(sbox.Max.X)}, {F(sbox.Max.Y)}, {F(sbox.Max.Z)})");

                // --- Intersector ---
                var intersector = new ReferenceIntersector(
                    new LogicalOrFilter(new ElementFilter[]
                    {
                        new ElementClassFilter(typeof(Floor)),
                        new ElementClassFilter(typeof(RoofBase))
                    }),
                    FindReferenceTarget.Face,
                    view) { FindReferencesInRevitLinks = true };

                XYZ origin = new XYZ(
                    (bb.Min.X + bb.Max.X) / 2.0,
                    (bb.Min.Y + bb.Max.Y) / 2.0,
                    rayZ);

                var hits = intersector.Find(origin, XYZ.BasisZ);

                sb.AppendLine();
                sb.AppendLine("═══ INTERSECTOR HITS ══════════════════════════════════════════");
                sb.AppendLine($"Луч: origin=({F(origin.X)}, {F(origin.Y)}, {F(origin.Z)}), dir=+Z");
                sb.AppendLine($"Всего hits: {hits?.Count ?? 0}");

                if (hits != null && hits.Count > 0)
                {
                    sb.AppendLine();
                    foreach (var hit in hits.OrderBy(h => h.Proximity))
                    {
                        bool positive = hit.Proximity > 0;
                        bool inRange  = positive && hit.Proximity <= maxProximity;
                        string status = !positive ? "✗ proximity ≤ 0 (пропущен)" :
                                        inRange   ? "✓ В ДИАПАЗОНЕ" :
                                                    $"✗ вне диапазона (макс {F(maxProximity)})";

                        Element hitElem = null;
                        try { hitElem = doc.GetElement(hit.GetReference().ElementId); } catch { }
                        string elemInfo = hitElem != null
                            ? $"{hitElem.GetType().Name} \"{hitElem.Name}\" Id:{hitElem.Id.GetValue()}"
                            : "(элемент в связанном файле или не найден)";

                        sb.AppendLine($"  proximity={F(hit.Proximity)}  hitZ={F(origin.Z + hit.Proximity)}  {status}");
                        sb.AppendLine($"    {elemInfo}");
                    }
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("Hits не найдены. Возможные причины:");
                    sb.AppendLine("  • Перекрытие/кровля не попадает в section box по XY");
                    sb.AppendLine("  • Section box слишком мал по Z");
                    sb.AppendLine("  • Категория Floor/Roof скрыта в search view");
                    sb.AppendLine("  • Перекрытие в связанном файле, который не загружен");
                    sb.AppendLine("  • Элемент находится вне BIM-модели");
                }

                tx.Commit();
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            return sb.ToString();
        }

        private static void ShowReport(IntPtr owner, string report)
        {
            var tb = new System.Windows.Controls.TextBox
            {
                Text = report,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                Margin = new Thickness(8)
            };

            var win = new Window
            {
                Title  = "Ceiling Attach — Диагностика",
                Width  = 750,
                Height = 650,
                Content = tb,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            new WindowInteropHelper(win).Owner = owner;
            win.ShowDialog();
        }
    }
}
