using DAQSystem.Application.UI;
using NLog;
using System.Media;

namespace DAQSystem.Application.Utility
{
    internal static class UserCommunication
    {
        public static event EventHandler<bool> IsShownChanged;

        private static bool isShown_;
        public static bool IsShown
        {
            get => isShown_;
            private set
            {
                isShown_ = value;
                IsShownChanged?.Invoke(typeof(UserCommunication), IsShown);
            }
        }

        public static bool ShowMessage(string caption, string userMessage, MessageType messageType, bool isButtonsEnabled = true)
            => ShowMessage(caption, userMessage, messageType, GetDefaultButtonType(messageType), isButtonsEnabled);

        public static bool ShowMessage(string caption, string userMessage, MessageType messageType, ButtonType buttonType, bool isButtonsEnabled = true)
        {
            IsShown = true;
            var result = ShowUserCommunicationDialog(caption, userMessage, messageType, buttonType, isButtonsEnabled);
            IsShown = modelessDialog_?.IsActive == true;
            return result;
        }

        public static void PlayAlarmSound() => alarmSound_.Play();

        public static void UpdateMessage(string message)
        {
            if (userCommunicationViewModel_ != null)
            {
                userCommunicationViewModel_.Message = message;
            }
        }

        public static void SetButtonsEnabled(bool isEnabled)
        {
            if (userCommunicationViewModel_ != null)
            {
                userCommunicationViewModel_.IsButtonsEnabled = isEnabled;
            }
        }

        private static ButtonType GetDefaultButtonType(MessageType messageType)
        {
            ButtonType buttonType = ButtonType.OK;
            switch (messageType)
            {
                //case MessageType.Info:
                //case MessageType.Warning:
                //case MessageType.Critical:
                //    buttonType = ButtonType.OK;
                //    break;
                case MessageType.Confirmation:
                    buttonType = ButtonType.YesNo;
                    break;
            }
            return buttonType;
        }

        private static bool ShowUserCommunicationDialog(string caption, string userMessage, MessageType messageType, ButtonType buttonType, bool isButtonsEnabled)
        {
            logger_.Info($"{userMessage}");

            userCommunicationViewModel_ = new UserCommunicationViewModel(caption, userMessage, messageType, buttonType, isButtonsEnabled);
            var userCommunicationDialog = new UserCommunicationDialog
            {
                DataContext = userCommunicationViewModel_,
                Owner = UIUtils.GetActiveWindow()
            };
            var result = userCommunicationDialog.ShowDialog() == true;
            logger_.Info($"User responded: {result}.");

            userCommunicationViewModel_ = null;
            return result;
        }

        internal static void SuspendUserInput(string userMessage)
        {
            logger_.Info($"{userMessage}");

            modelessDialog_ = new UserCommunicationDialog
            {
                DataContext = new UserCommunicationViewModel(userMessage),
                Owner = UIUtils.GetActiveWindow()
            };

            modelessDialog_.Show();
            IsShown = true;
        }

        internal static void ResumeUserInput()
        {
            modelessDialog_?.Close();
            IsShown = false;
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private static readonly SoundPlayer alarmSound_ = new SoundPlayer("C:\\Windows\\Media\\Windows Ding.wav");
        private static UserCommunicationViewModel userCommunicationViewModel_;
        private static UserCommunicationDialog modelessDialog_;
    }
}
