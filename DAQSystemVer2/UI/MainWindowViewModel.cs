using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.Model;
using DAQSystem.DataAcquisition;
using NLog;
using OxyPlot;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;

namespace DAQSystem.Application.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private const string DEFAULT_DIR_NAME = "DAQSystem";

        public List<CommandTypes> DataAcquisitionSettings { get; } = new List<CommandTypes>() { CommandTypes.SetCollectDuration, CommandTypes.SetInitialThreshold, CommandTypes.SetSignalSign, CommandTypes.SetSignalBaseline, CommandTypes.SetTimeInterval, CommandTypes.SetGain };

        public ObservableCollection<CommandControl> SettingCommands { get; } = new()
            {
                { new CommandControl() { CommandType = CommandTypes.SetCollectDuration, IsModified = false, Value = 2000000 } },
                { new CommandControl() { CommandType = CommandTypes.SetInitialThreshold, IsModified = false, Value = 8191 } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalSign, IsModified = false, Value = 1 } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalBaseline, IsModified = false, Value = 1050 } },
                { new CommandControl() { CommandType = CommandTypes.SetTimeInterval, IsModified = false, Value = 100 } },
                { new CommandControl() { CommandType = CommandTypes.SetGain, IsModified = false, Value = 1340 } }
            };

        [ObservableProperty]
        private string workingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DEFAULT_DIR_NAME);

        [ObservableProperty]
        private bool isAnimationPlaying;

        [ObservableProperty]
        private AppStatus currentStatus;

        [ObservableProperty]
        private CommandTypes selectedSetting;

        [ObservableProperty]
        private PlotModel plotModel;

        [RelayCommand]
        private void SelectDirectory()
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                WorkingDirectory = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private async Task ToggleConnect()
        {
            try
            {
                if (CurrentStatus == AppStatus.Idle)
                {
                    CurrentStatus = AppStatus.Connected;
                    //await daq_.Initialize(serialConfig_);
                }
                else
                {
                    //dataAcquisitionControl_.Uninitialize();
                    CurrentStatus = AppStatus.Idle;
                }


            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", ex.Message, MessageType.Warning);
            }

        }

        [RelayCommand]
        private void StartCollecting()
        {
            CurrentStatus = AppStatus.Collecting;
        }

        [RelayCommand]
        private void StopAndReset()
        {
            CurrentStatus = AppStatus.Connected;
        }

        public MainWindowViewModel(SerialConfiguration serialConfig)
        {
            serialConfig_ = serialConfig ?? throw new ArgumentNullException(nameof(serialConfig));

            CurrentStatus = AppStatus.Idle;
            SelectedSetting = DataAcquisitionSettings.First();

            InitializePlot();
        }

        private void InitializePlot()
        {
            PlotModel = new PlotModel()
            {
                PlotAreaBorderColor = OxyColor.FromRgb(211, 211, 211),
                PlotAreaBorderThickness = new OxyThickness(2),
                TextColor = OxyColor.FromRgb(211, 211, 211),
                TitleColor = OxyColor.FromRgb(211, 211, 211),
                DefaultFontSize = 14,
            };

            var xAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                AxislineColor = OxyColor.FromRgb(211, 211, 211),
                TicklineColor = OxyColor.FromRgb(211, 211, 211),
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "ADC Channel",
                StringFormat = "0"
            };

            var yAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                AxislineColor = OxyColor.FromRgb(211, 211, 211),
                TicklineColor = OxyColor.FromRgb(211, 211, 211),
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Count",
                StringFormat = "0.0"
            };

            PlotModel.Axes.Add(xAxis);
            PlotModel.Axes.Add(yAxis);
        }

        public async Task CleanUp()
        {

        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            switch (e.PropertyName)
            {
                case nameof(SelectedSetting):
                    IsAnimationPlaying = true;
                    break;
            }
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly SerialConfiguration serialConfig_ = new();
        private readonly DataAcquisitionControl daq_ = new();

        public partial class CommandControl : ObservableObject
        {
            [ObservableProperty]
            private CommandTypes commandType;

            [ObservableProperty]
            private bool isModified;

            [ObservableProperty]
            private int value;
        }
    }
}
