using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAQSystem.Application.UI.Dialog
{
    internal partial class BiasCorrectionViewModel : DialogViewModelBase
    {
        public event EventHandler DialogCloseRequested;

        public event EventHandler<LinearEquationParameters> BiasCorrectionParametersChanged;

        [ObservableProperty]
        private LinearEquationParameters biasCorrectionParameters;

        [RelayCommand]
        private void SetParameters()
        {
            BiasCorrectionParametersChanged?.Invoke(this, BiasCorrectionParameters);
            UserCommunication.ShowMessage($"{Theme.GetString(Strings.Notice)}", $"{Theme.GetString(Strings.BiasCorrection)}{Theme.GetString(Strings.Parameter)}{Theme.GetString(Strings.Set)}{Theme.GetString(Strings.Success)}", MessageType.Info);
        }

        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
            DialogCloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public BiasCorrectionViewModel()
        {
            BiasCorrectionParameters = new LinearEquationParameters();
        }
    }
}
