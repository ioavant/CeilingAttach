using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;

namespace CeilingAttach
{
    public partial class SettingsWindow : Window
    {
        private readonly ForgeTypeId _lengthUnit;
        private readonly string _unitLabel;

        private double _gap;
        private double _search;

        // Values in Revit internal units (feet); SettingsCommand reads/writes these.
        public double GapInternalUnits
        {
            get => _gap;
            set { _gap = value; GapTextBox.Text = FormatForDisplay(value); }
        }

        public double SearchHeightInternalUnits
        {
            get => _search;
            set { _search = value; SearchTextBox.Text = FormatForDisplay(value); }
        }

        public SettingsWindow(Document doc)
        {
            InitializeComponent();

            FormatOptions fo = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
            _lengthUnit = fo.GetUnitTypeId();
            _unitLabel = LabelUtils.GetLabelForUnit(_lengthUnit);

            GapLabel.Text    = $"Gap between element and floor ({_unitLabel}):";
            SearchLabel.Text = $"Maximum search height ({_unitLabel}):";
        }

        private string FormatForDisplay(double internalUnits)
        {
            double display = UnitUtils.ConvertFromInternalUnits(internalUnits, _lengthUnit);
            return display.ToString("G6", CultureInfo.InvariantCulture);
        }

        private bool TryParseDisplay(string text, out double internalUnits)
        {
            string normalized = text.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double displayValue) && displayValue >= 0)
            {
                internalUnits = UnitUtils.ConvertToInternalUnits(displayValue, _lengthUnit);
                return true;
            }
            internalUnits = 0;
            return false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            bool gapOk    = TryParseDisplay(GapTextBox.Text, out double gapInternal)    && gapInternal >= 0;
            bool searchOk = TryParseDisplay(SearchTextBox.Text, out double searchInternal) && searchInternal > 0;

            if (gapOk && searchOk)
            {
                _gap    = gapInternal;
                _search = searchInternal;
                ErrorText.Visibility = System.Windows.Visibility.Collapsed;
                DialogResult = true;
            }
            else
            {
                ErrorText.Text = !gapOk
                    ? $"Gap: please enter a valid non-negative number ({_unitLabel})."
                    : $"Search height: please enter a valid positive number ({_unitLabel}).";
                ErrorText.Visibility = System.Windows.Visibility.Visible;
                (!gapOk ? GapTextBox : SearchTextBox).Focus();
            }
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            string current = textBox?.Text ?? "";
            string incoming = e.Text;

            bool isDigit    = char.IsDigit(incoming[0]);
            bool isSeparator = (incoming == "." || incoming == ",")
                               && !current.Contains(".") && !current.Contains(",");

            e.Handled = !(isDigit || isSeparator);
        }
    }
}
