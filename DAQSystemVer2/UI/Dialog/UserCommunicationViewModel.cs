using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Common.UI;

namespace DAQSystem.Application.UI
{
    public enum MessageType
    {
        Info,
        Warning,
        Confirmation,
        Critical
    }

    public enum ButtonType
    {
        None,
        OK,
        Cancel,
        YesNo
    }

    internal partial class UserCommunicationViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string caption;

        [ObservableProperty]
        private string message;

        [ObservableProperty]
        private MessageType messageType;

        [ObservableProperty]
        private ButtonType buttonType;

        [ObservableProperty]
        private bool isButtonsEnabled;

        [RelayCommand]
        private void Cancel() => DialogResult = false;

        [RelayCommand]
        private void OK() => DialogResult = true;

        public UserCommunicationViewModel(string message)
        {
            Message = message;
        }

        public UserCommunicationViewModel(string caption, string message, MessageType messageType, ButtonType buttonType, bool isButtonsEnabled) : this(message)
        {
            Caption = caption;
            MessageType = messageType;
            ButtonType = buttonType;
            IsButtonsEnabled = isButtonsEnabled;
        }
    }
}
