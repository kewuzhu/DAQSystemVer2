using DAQSystem.Common.UI;
using System.ComponentModel;
using System.Windows;

namespace DAQSystem.Application.UI.Dialog
{
    /// <summary>
    /// ChannelToEnergyDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ChannelToEnergyDialog : DialogBase
    {
        public ChannelToEnergyDialog()
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
