using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace DAQSystem.Common.UI
{
    public class DialogBase : Window
    {
        public DialogBase()
        {
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindingOperations.ClearBinding(this, ResultProperty);
            BindingOperations.ClearBinding(this, IsOpenProperty);

            if (e.NewValue == null)
                return;

            BindingOperations.SetBinding(this, ResultProperty,
                new Binding(nameof(DialogResult))
                {
                    Source = e.NewValue,
                    Mode = BindingMode.TwoWay
                });

            BindingOperations.SetBinding(this, IsOpenProperty,
                new Binding(nameof(IsActive))
                {
                    Source = e.NewValue,
                    Mode = BindingMode.TwoWay
                });
        }

        public static readonly DependencyProperty ResultProperty =
            DependencyProperty.Register(nameof(Result), typeof(bool?), typeof(DialogBase),
                new PropertyMetadata(null, OnResultPropertyChanged));

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(DialogBase),
              new PropertyMetadata(false, new PropertyChangedCallback(OnIsOpenPropertyChanged)));

        public bool? Result
        {
            get => (bool?)GetValue(ResultProperty);
            set => SetValue(ResultProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        private static void OnResultPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (DialogBase)d;
            window.DialogResult = (bool?)e.NewValue;
        }

        private static void OnIsOpenPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (DialogBase)d;
            if (!(bool)e.NewValue)
                window.Close();
        }
    }
}
