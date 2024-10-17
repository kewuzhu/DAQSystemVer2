using DAQSystem.Common.UI;
using System.ComponentModel;
using System.Windows;

namespace DAQSystem.Application.UI.Dialog
{
    /// <summary>
    /// FitGaussianDialog.xaml 的交互逻辑
    /// </summary>
    public partial class FitGaussianDialog : DialogBase
    {
        public FitGaussianDialog()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
