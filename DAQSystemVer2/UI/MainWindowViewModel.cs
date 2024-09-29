using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.Model;
using DAQSystem.DataAcquisition;
using NLog;
using OxyPlot;

namespace DAQSystem.Application.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        public List<CommandTypes> DataAcquisitionSettings { get; } = new List<CommandTypes>() { CommandTypes.SetCollectDuration, CommandTypes.SetInitialThreshold, CommandTypes.SetSignalSign, CommandTypes.SetSignalBaseline, CommandTypes.SetTimeInterval, CommandTypes.SetGain };

        [ObservableProperty]
        private CommandTypes selectedSetting;

        [ObservableProperty]
        private PlotModel plotModel;

        [RelayCommand]
        private void ToggleConnect()
        {
            try
            {
                if (!dataAcquisitionControl_.IsInitialized)
                    dataAcquisitionControl_.Initialize(serialConfig_);
                else
                    dataAcquisitionControl_.Uninitialize();

            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", ex.Message, MessageType.Warning);
            }

        }

        public MainWindowViewModel(SerialConfiguration serialConfig)
        {
            serialConfig_ = serialConfig ?? throw new ArgumentNullException(nameof(serialConfig));

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

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly SerialConfiguration serialConfig_ = new();
        private readonly DataAcquisitionControl dataAcquisitionControl_ = new();
    }
}
