using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenNDOF.App.Controls;

/// <summary>
/// A bi-directional axis bar showing a –1…+1 value with label and numeric readout.
/// </summary>
public partial class AxisBar : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(AxisBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(AxisBar),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(AxisBar), new PropertyMetadata("0.000"));

    public static readonly DependencyProperty BarColorProperty =
        DependencyProperty.Register(nameof(BarColor), typeof(Brush), typeof(AxisBar),
            new PropertyMetadata(Brushes.DodgerBlue));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public Brush BarColor
    {
        get => (Brush)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    public AxisBar() => InitializeComponent();

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisBar bar)
            bar.UpdateBar((double)e.NewValue);
    }

    private void UpdateBar(double v)
    {
        v = Math.Clamp(v, -1.0, 1.0);
        double half = TrackGrid.ColumnDefinitions.Count >= 3
            ? TrackGrid.ColumnDefinitions[0].ActualWidth
            : TrackGrid.ActualWidth / 2.0;

        PosBar.Width = v > 0 ? v * half : 0;
        NegBar.Width = v < 0 ? -v * half : 0;
    }

    private double HalfWidth => TrackGrid.ActualWidth / 2.0;

    private void TrackGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateBar(Value);
}
