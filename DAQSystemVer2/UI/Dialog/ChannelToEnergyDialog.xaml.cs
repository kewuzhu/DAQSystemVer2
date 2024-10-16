using DAQSystem.Common.UI;
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

            var dataContext = DataContext as ChannelToEnergyViewModel;
            if (dataContext != null)
            {
                dataContext.DialogCloseRequested += (s, e) => this.Close();
            }
        }

    }
}
