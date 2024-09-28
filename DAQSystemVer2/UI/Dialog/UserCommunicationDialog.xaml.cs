using DAQSystem.Common.UI;
using System;
using System.Windows;

namespace DAQSystem.Application.UI
{
    public partial class UserCommunicationDialog : DialogBase
    {
        public UserCommunicationDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AffirmativeButton.Focus();
        }
    }
}
