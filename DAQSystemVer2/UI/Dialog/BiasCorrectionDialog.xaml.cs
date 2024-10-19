using DAQSystem.Common.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DAQSystem.Application.UI.Dialog
{
    /// <summary>
    /// BiasCorrectionDialog.xaml 的交互逻辑
    /// </summary>
    public partial class BiasCorrectionDialog : DialogBase
    {
        public BiasCorrectionDialog()
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
