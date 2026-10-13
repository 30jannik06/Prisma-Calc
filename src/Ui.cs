// Prisma - UI building blocks: themed styles, helpers, session state. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Prisma
{
    public class HistoryItem { public string Expr, Result; }

    // State shared by all views: history, memory, current effective theme.
    public class Session
    {
        public readonly List<HistoryItem> History = new List<HistoryItem>();
        public readonly List<double> Memory = new List<double>();
        public EffectiveTheme Theme;
        public event Action Changed;
        public event Action ThemeChanged;
        public void RaiseTheme() { if (ThemeChanged != null) ThemeChanged(); }

        public void AddHistory(string expr, string result)
        {
            History.Insert(0, new HistoryItem { Expr = expr, Result = result });
            if (History.Count > 100) History.RemoveAt(History.Count - 1);
            Raise();
        }
        public void Raise() { if (Changed != null) Changed(); }
    }

    public interface IModeView
    {
        UIElement Root { get; }
        double PreferredWidth { get; }
        double PreferredHeight { get; }
        bool HasHistory { get; }
        bool HandleText(string text);
        bool HandleKey(Key key);
        string CopyText();
        void Paste(string text);
        void Recall(string value);
    }

    // Corner radius that follows the element height: tall keys get moderate corners, short buttons get proportionally
    // smaller ones, so nothing turns into a pill. Used from the XAML templates below.
    public class RadiusConverter : IValueConverter
    {
        public static readonly RadiusConverter Instance = new RadiusConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double h = value is double ? (double)value : 0;
            double r = Ui.Radius(h);
            if ("inner".Equals(parameter)) r = Math.Max(0, r - 1);
            return new CornerRadius(r);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotSupportedException(); }
    }

    public static class Ui
    {
        // 1.0 = "Soft" (default); "Square" and "Round" scale every radius.
        public static double CornerScale = 1.0;


        // ---- logo (same geometry as assets\prisma-logo.svg and tools\make-icon.ps1; 0..100 box) ----
        static readonly string[] LogoColors = { "FF4D6D", "FFB84D", "6CE08A", "4DC9FF", "9B6BFF" };
        static readonly double[] LogoY = { 26, 39, 52, 65, 78 };
        static readonly string[] LogoColorsSmall = { "FF4D6D", "6CE08A", "4DB8FF" };
        static readonly double[] LogoYSmall = { 28, 52, 76 };

        static Color Hex(string h, byte a)
        {
            return Color.FromArgb(a, Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
        }

        static Line LogoLine(double x1, double y1, double x2, double y2, Color c, double w)
        {
            return new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(c), StrokeThickness = w, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        }

        // The Prisma logo as a vector, any size. Small sizes use thicker lines and three beams, like the small icon entries.
        public static FrameworkElement Logo(double size)
        {
            bool simple = size <= 32;
            double ws = simple ? 1.7 : 1.0;
            var c = new Canvas { Width = 100, Height = 100 };
            c.Children.Add(new Rectangle
            {
                Width = 100, Height = 100, RadiusX = 22, RadiusY = 22,
                Fill = new LinearGradientBrush(Hex("0D1533", 255), Hex("2A2160", 255), new Point(0, 0), new Point(1, 1))
            });
            var pts = new PointCollection { new Point(50, 20), new Point(22, 72), new Point(78, 72) };
            c.Children.Add(new Polygon { Points = pts, Fill = new LinearGradientBrush(Hex("FFFFFF", 95), Hex("FFFFFF", 28), new Point(0.5, 0), new Point(0.5, 1)) });
            c.Children.Add(LogoLine(4, 57, 33.8, 50, Hex("FFFFFF", 245), 3.6 * ws));
            c.Children.Add(LogoLine(33.8, 50, 66.7, 51, Hex("FFFFFF", 140), 2.6 * ws));
            string[] cols = simple ? LogoColorsSmall : LogoColors; double[] ys = simple ? LogoYSmall : LogoY;
            for (int i = 0; i < cols.Length; i++) c.Children.Add(LogoLine(66.7, 51, 97, ys[i], Hex(cols[i], 255), 3.4 * ws));
            c.Children.Add(new Polygon { Points = pts, Fill = null, Stroke = new SolidColorBrush(Hex("FFFFFF", 235)), StrokeThickness = simple ? 5.0 : 3.2, StrokeLineJoin = PenLineJoin.Round });
            return new Viewbox { Width = size, Height = size, Child = c, Stretch = Stretch.Uniform };
        }

        public static double ScaleFor(string name) { return name == "Square" ? 0.25 : name == "Round" ? 1.7 : 1.0; }

        public static double Radius(double height)
        {
            double max = 12 * CornerScale, min = Math.Min(2, max);
            double r = height * 0.22 * CornerScale;
            return Math.Max(min, Math.Min(r, max));
        }

        // Radius for fixed-size containers (cards, panels).
        public static double R(double baseRadius) { return Math.Round(baseRadius * CornerScale, 1); }

        const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:l='clr-namespace:Prisma;assembly=Prisma'";
        const string RadB = "{Binding ActualHeight, RelativeSource={RelativeSource Self}, Converter={x:Static l:RadiusConverter.Instance}}";
        const string RadI = "{Binding ActualHeight, RelativeSource={RelativeSource Self}, Converter={x:Static l:RadiusConverter.Instance}, ConverterParameter=inner}";

        const string ButtonStyle =
            "<Style " + Ns + " TargetType='Button'>" +
            "<Setter Property='Focusable' Value='False'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>" +
            "<Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{DynamicResource Border}' BorderThickness='1' CornerRadius='" + RadB + "' Margin='3' RenderTransformOrigin='0.5,0.5'>" +
            "<Border.RenderTransform><ScaleTransform x:Name='st'/></Border.RenderTransform>" +
            "<Grid><Border CornerRadius='" + RadI + "' Background='{DynamicResource Sheen}' IsHitTestVisible='False'/>" +
            "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Grid></Border>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.86'/></Trigger>" +
            "<Trigger Property='IsPressed' Value='True'>" +
            "<Trigger.EnterActions><BeginStoryboard><Storyboard>" +
            "<DoubleAnimation Storyboard.TargetName='st' Storyboard.TargetProperty='ScaleX' To='0.93' Duration='0:0:0.07'/>" +
            "<DoubleAnimation Storyboard.TargetName='st' Storyboard.TargetProperty='ScaleY' To='0.93' Duration='0:0:0.07'/>" +
            "</Storyboard></BeginStoryboard></Trigger.EnterActions>" +
            "<Trigger.ExitActions><BeginStoryboard><Storyboard>" +
            "<DoubleAnimation Storyboard.TargetName='st' Storyboard.TargetProperty='ScaleX' To='1' Duration='0:0:0.4'><DoubleAnimation.EasingFunction><BackEase EasingMode='EaseOut' Amplitude='0.9'/></DoubleAnimation.EasingFunction></DoubleAnimation>" +
            "<DoubleAnimation Storyboard.TargetName='st' Storyboard.TargetProperty='ScaleY' To='1' Duration='0:0:0.4'><DoubleAnimation.EasingFunction><BackEase EasingMode='EaseOut' Amplitude='0.9'/></DoubleAnimation.EasingFunction></DoubleAnimation>" +
            "</Storyboard></BeginStoryboard></Trigger.ExitActions></Trigger>" +
            "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.35'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";

        const string SegStyle =
            "<Style " + Ns + " TargetType='Button'>" +
            "<Setter Property='Focusable' Value='False'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>" +
            "<Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='" + RadB + "' Margin='2'>" +
            "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.8'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";

        const string NavStyle =
            "<Style " + Ns + " TargetType='Button'>" +
            "<Setter Property='Focusable' Value='False'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>" +
            "<Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='" + RadB + "' Margin='8,1' Padding='14,7'>" +
            "<ContentPresenter HorizontalAlignment='Left' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.8'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";

        const string TextBoxStyle =
            "<Style " + Ns + " TargetType='TextBox'>" +
            "<Setter Property='Foreground' Value='{DynamicResource NumText}'/>" +
            "<Setter Property='CaretBrush' Value='{DynamicResource NumText}'/>" +
            "<Setter Property='SelectionBrush' Value='{DynamicResource AccentBg}'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'>" +
            "<Border x:Name='b' Background='{DynamicResource NumBg}' BorderBrush='{DynamicResource Border}' BorderThickness='1' CornerRadius='" + RadB + "' Padding='10,6'>" +
            "<ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/></Border>" +
            "<ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'>" +
            "<Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource AccentBg}'/></Trigger></ControlTemplate.Triggers>" +
            "</ControlTemplate></Setter.Value></Setter></Style>";

        const string ComboStyle =
            "<Style " + Ns + " TargetType='ComboBox'>" +
            "<Setter Property='Foreground' Value='{DynamicResource NumText}'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid>" +
            "<ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding Path=IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>" +
            "<ToggleButton.Template><ControlTemplate TargetType='ToggleButton'>" +
            "<Border Background='{DynamicResource NumBg}' BorderBrush='{DynamicResource Border}' BorderThickness='1' CornerRadius='" + RadB + "'/>" +
            "</ControlTemplate></ToggleButton.Template></ToggleButton>" +
            "<ContentPresenter IsHitTestVisible='False' Margin='12,6,30,6' VerticalAlignment='Center' HorizontalAlignment='Left' " +
            "Content='{TemplateBinding SelectionBoxItem}' TextElement.Foreground='{DynamicResource NumText}'/>" +
            "<Path Data='M 0 0 L 4 4 L 8 0' Stroke='{DynamicResource NumText}' StrokeThickness='1.6' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0' IsHitTestVisible='False'/>" +
            "<Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>" +
            "<Border Background='{DynamicResource PopupBg}' BorderBrush='{DynamicResource Border}' BorderThickness='1' CornerRadius='" + RadB + "' Margin='0,4,0,0' " +
            "MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='300'>" +
            "<ScrollViewer><ItemsPresenter/></ScrollViewer></Border></Popup>" +
            "</Grid></ControlTemplate></Setter.Value></Setter></Style>";

        const string ComboItemStyle =
            "<Style " + Ns + " TargetType='ComboBoxItem'>" +
            "<Setter Property='Foreground' Value='{DynamicResource NumText}'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'>" +
            "<Border x:Name='bd' Padding='12,6' CornerRadius='" + RadB + "' Margin='3' Background='Transparent'><ContentPresenter/></Border>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsHighlighted' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource FuncBg}'/></Trigger>" +
            "<Trigger Property='IsSelected' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource AccentBg}'/>" +
            "<Setter Property='Foreground' Value='{DynamicResource AccentText}'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";

        const string ScrollStyle =
            "<Style " + Ns + " TargetType='ScrollBar'>" +
            "<Setter Property='Width' Value='12'/><Setter Property='Background' Value='Transparent'/>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>" +
            "<Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'>" +
            "<Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>" +
            "<Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'>" +
            "<Border CornerRadius='4' Margin='3,0' Background='{DynamicResource SubText}' Opacity='0.5'/>" +
            "</ControlTemplate></Thumb.Template></Thumb></Track.Thumb>" +
            "<Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>" +
            "</Track></Grid></ControlTemplate></Setter.Value></Setter></Style>";

        public static Style Seg;

        public static void InstallStyles(ResourceDictionary r)
        {
            r.Add(typeof(Button), (Style)XamlReader.Parse(ButtonStyle));
            Seg = (Style)XamlReader.Parse(SegStyle);
            r.Add("Seg", Seg);
            r.Add("Nav", (Style)XamlReader.Parse(NavStyle));
            r.Add(typeof(TextBox), (Style)XamlReader.Parse(TextBoxStyle));
            r.Add(typeof(ComboBox), (Style)XamlReader.Parse(ComboStyle));
            r.Add(typeof(ComboBoxItem), (Style)XamlReader.Parse(ComboItemStyle));
            r.Add(typeof(ScrollBar), (Style)XamlReader.Parse(ScrollStyle));
        }

        // role: num | func | accent | flat
        public static Button Key(string caption, string role, Action click, double size)
        {
            var b = new Button { Content = caption, FontSize = size };
            switch (role)
            {
                case "num": b.SetResourceReference(Control.BackgroundProperty, "NumBg"); b.SetResourceReference(Control.ForegroundProperty, "NumText"); break;
                case "accent": b.SetResourceReference(Control.BackgroundProperty, "AccentFill"); b.SetResourceReference(Control.ForegroundProperty, "AccentText"); break;
                case "flat": b.Background = Brushes.Transparent; b.SetResourceReference(Control.ForegroundProperty, "Text"); break;
                default: b.SetResourceReference(Control.BackgroundProperty, "FuncBg"); b.SetResourceReference(Control.ForegroundProperty, "FuncText"); break;
            }
            if (click != null) b.Click += delegate { click(); };
            return b;
        }

        public static TextBlock Label(string text, string brushKey, double size)
        {
            var t = new TextBlock { Text = text, FontSize = size };
            t.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return t;
        }

        public static Border Card(UIElement child)
        {
            var b = new Border { CornerRadius = new CornerRadius(R(12)), BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = child };
            b.SetResourceReference(Border.BackgroundProperty, "NumBg");
            b.SetResourceReference(Border.BorderBrushProperty, "Border");
            return b;
        }

        public static UniformGrid Pad(int columns, params UIElement[] items)
        {
            var g = new UniformGrid { Columns = columns };
            foreach (UIElement e in items) g.Children.Add(e);
            return g;
        }

        public static Brush Res(FrameworkElement owner, string key, Brush fallback)
        {
            Brush b = owner.TryFindResource(key) as Brush;
            return b ?? fallback;
        }

        public static bool TryNumber(string text, out double v)
        {
            v = 0;
            if (text == null) return false;
            return double.TryParse(text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                && !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
