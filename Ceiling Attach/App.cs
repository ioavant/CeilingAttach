using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace CeilingAttach
{
    public class App : IExternalApplication
    {
        // Stored in Revit internal units (feet). Converted to/from project length
        // units only at the SettingsWindow boundary, never here.
        public static double GapInternalUnits { get; set; } = 0.0;
        public static double SearchHeightInternalUnits { get; set; } = 9.842519685; // ≈ 3000 mm

        private UIControlledApplication _uiApp;

        public Result OnStartup(UIControlledApplication application)
        {
            string tab   = Brand.RibbonTab;
            string panel = Brand.RibbonPanel;

            try { application.CreateRibbonTab(tab); } catch { /* tab exists - OK */ }

            RibbonPanel ribbonPanel = GetOrCreatePanel(application, tab, panel);
            string asmPath = Assembly.GetExecutingAssembly().Location;

            SplitButtonData sbData = new SplitButtonData("AttachBtn", "Attach\nTo Ceiling");
            SplitButton splitBtn = ribbonPanel.AddItem(sbData) as SplitButton;
            splitBtn.ToolTip = "Attach elements to nearest floor or roof";
            splitBtn.LargeImage = LoadImage("icon.png");

            PushButtonData buttonData = new PushButtonData(
                "AttachBtn", "Attach\nTo Ceiling",
                asmPath, "CeilingAttach.AttachToFloorCommand")
            {
                ToolTip = "Attach selected elements to the nearest floor or roof above them.",
                LargeImage = LoadImage("icon.png")
            };
            splitBtn.AddPushButton(buttonData);
            splitBtn.IsSynchronizedWithCurrentItem = false;

            splitBtn.AddSeparator();

            PushButtonData settingsData = new PushButtonData(
                "AttachSettings", "Settings",
                asmPath, "CeilingAttach.SettingsCommand")
            {
                ToolTip = "Set the gap between element and floor.",
                LargeImage = LoadImage("settings_32.png"),
                Image      = LoadImage("settings_16.png")
            };
            splitBtn.AddPushButton(settingsData);

            // ── About / Help button ───────────────────────────────────────────
            // Every add-in of this brand shares one "About" panel holding a single help button, so
            // only the first one loaded gets to add it. Two independent checks, because neither alone
            // covers every case:
            //   • an AppDomain data slot - all add-ins in a Revit process share one AppDomain, and this
            //     catches siblings built from the same skill regardless of load order (the Vixeldorf
            //     convention);
            //   • a scan of the live ribbon via AdWindows - RibbonPanel.GetItems() only sees items the
            //     CURRENT add-in added, so this is the only way to notice a button placed by an older
            //     add-in of the same brand (the TES convention).
            RibbonPanel aboutPanel = GetOrCreatePanel(application, tab, Brand.AboutPanel);
            bool alreadyAdded = AppDomain.CurrentDomain.GetData(Brand.AboutFlagKey) != null
                                || AboutButtonExistsOnRibbon(tab, Brand.AboutPanel);
            if (!alreadyAdded)
            {
                AppDomain.CurrentDomain.SetData(Brand.AboutFlagKey, true);
                PushButtonData webData = new PushButtonData(
                    Brand.AboutButtonId, Brand.AboutButtonText,
                    asmPath, "CeilingAttach.OpenWebPageCommand")
                {
                    ToolTip    = Brand.AboutButtonTooltip,
                    LargeImage = LoadImage("web_icon.png")
                };
                aboutPanel.AddItem(webData);
            }

            if (Brand.PinAboutPanelToEnd)
            {
                _uiApp = application;
                application.Idling += OnFirstIdling;
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private void OnFirstIdling(object sender, IdlingEventArgs e)
        {
            _uiApp.Idling -= OnFirstIdling;
            MoveAboutPanelToEnd();
        }

        private static bool AboutButtonExistsOnRibbon(string tabTitle, string panelTitle)
        {
            try
            {
                var ribbon = Autodesk.Windows.ComponentManager.Ribbon;
                if (ribbon == null) return false;

                foreach (Autodesk.Windows.RibbonTab ribbonTab in ribbon.Tabs)
                {
                    if (!string.Equals(ribbonTab.Title, tabTitle, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (Autodesk.Windows.RibbonPanel ribbonPanel in ribbonTab.Panels)
                    {
                        if (ribbonPanel.Source == null ||
                            !string.Equals(ribbonPanel.Source.Title, panelTitle, StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (Autodesk.Windows.RibbonItem item in ribbonPanel.Source.Items)
                            if (ItemTextMatchesAboutButton(item)) return true;
                    }
                }
            }
            catch { /* AdWindows unavailable - fall through and add the button */ }
            return false;
        }

        private static bool ItemTextMatchesAboutButton(Autodesk.Windows.RibbonItem item)
        {
            if (item == null) return false;
            string text = item.Text ?? string.Empty;
            if (text.IndexOf(Brand.AboutMatchText, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            Autodesk.Windows.RibbonRowPanel rowPanel = item as Autodesk.Windows.RibbonRowPanel;
            if (rowPanel != null)
                foreach (Autodesk.Windows.RibbonItem child in rowPanel.Items)
                    if (ItemTextMatchesAboutButton(child)) return true;

            return false;
        }

        private static void MoveAboutPanelToEnd()
        {
            try
            {
                var ribbon = Autodesk.Windows.ComponentManager.Ribbon;
                if (ribbon == null) return;

                foreach (Autodesk.Windows.RibbonTab tab in ribbon.Tabs)
                {
                    if (tab.Title != Brand.RibbonTab && tab.Id != Brand.RibbonTab) continue;

                    Autodesk.Windows.RibbonPanel about = null;
                    foreach (Autodesk.Windows.RibbonPanel p in tab.Panels)
                        if (p.Source != null && p.Source.Title != null &&
                            p.Source.Title.Trim().Equals(Brand.AboutPanel, StringComparison.OrdinalIgnoreCase))
                        { about = p; break; }

                    if (about != null && tab.Panels[tab.Panels.Count - 1] != about)
                    {
                        tab.Panels.Remove(about);
                        tab.Panels.Add(about);
                    }
                    break;
                }
            }
            catch { /* never let ribbon cosmetics interfere with loading */ }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tab, string name)
        {
            foreach (RibbonPanel p in app.GetRibbonPanels(tab))
                if (p.Name == name) return p;
            return app.CreateRibbonPanel(tab, name);
        }

        internal static BitmapImage LoadImage(string fileName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                System.IO.Stream stream = asm.GetManifestResourceStream("CeilingAttach.Resources." + fileName);
                if (stream == null)
                    foreach (string n in asm.GetManifestResourceNames())
                        if (n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        { stream = asm.GetManifestResourceStream(n); break; }

                if (stream == null) return null;

                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }

    // ── Help / About button ───────────────────────────────────────────────────

    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenWebPageCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Process.Start(new ProcessStartInfo(Brand.HelpUrl) { UseShellExecute = true });
            return Result.Succeeded;
        }
    }
}
