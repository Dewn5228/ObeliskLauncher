using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace ObeliskLauncher.Avalonia.Views;

public partial class LoadingSpinner : UserControl
{
    private DispatcherTimer? _rotationTimer;
    private double _currentAngle;

    public LoadingSpinner()
    {
        InitializeComponent();
        Unloaded += (s, e) => _rotationTimer?.Stop();
        StartRotation();
    }

    private void StartRotation()
    {
        _currentAngle = 0;
        _rotationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _rotationTimer.Tick += (s, e) =>
        {
            _currentAngle += 12;
            _currentAngle %= 360;

            if (this.FindControl<Ellipse>("SpinningArc") is Ellipse arc &&
                arc.RenderTransform is RotateTransform rotate)
            {
                rotate.Angle = _currentAngle;
            }
        };
        _rotationTimer.Start();
    }
}
