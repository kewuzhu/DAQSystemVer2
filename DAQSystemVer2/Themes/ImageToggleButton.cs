using System.Windows;
using System.Windows.Controls.Primitives;

namespace DAQSystem.Application.Themes
{
    internal class ImageToggleButton : ToggleButton
    {
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(object), typeof(ImageToggleButton));

        public static readonly DependencyProperty ImageWidthProperty =
            DependencyProperty.Register(nameof(ImageWidth), typeof(double), typeof(ImageToggleButton));

        public static readonly DependencyProperty ImageHeightProperty =
            DependencyProperty.Register(nameof(ImageHeight), typeof(double), typeof(ImageToggleButton));

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ImageToggleButton));

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(nameof(TextColor), typeof(System.Windows.Media.Brush), typeof(ImageToggleButton));

        public object ImageSource
        {
            get => (string)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public double ImageWidth
        {
            get => (double)GetValue(ImageWidthProperty);
            set => SetValue(ImageWidthProperty, value);
        }

        public double ImageHeight
        {
            get => (double)GetValue(ImageHeightProperty);
            set => SetValue(ImageHeightProperty, value);
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public System.Windows.Media.Brush TextColor
        {
            get => (System.Windows.Media.Brush)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }
    }
}
