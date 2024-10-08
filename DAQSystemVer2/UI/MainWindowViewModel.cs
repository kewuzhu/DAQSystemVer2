using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.Model;
using DAQSystem.DataAcquisition;
using NLog;
using OxyPlot;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;

namespace DAQSystem.Application.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private const string DEFAULT_DIR_NAME = "DAQSystem";
        private const int DEFAULT_COLLECTION_DURATION = 100000;
        private const int DEFAULT_INITIAL_THRESHOLD = 0;
        private const int DEFAULT_SIGNAL_SIGN = 1;
        private const int DEFAULT_SIGNAL_BASELINE = 1050;
        private const int DEFAULT_TIME_INTERVAL = 1000;
        private const int DEFAULT_GAIN = 1340;

        private readonly OxyColor DEFAULT_COLOR = OxyColor.FromRgb(211, 211, 211);

        public List<CommandTypes> DataAcquisitionSettings { get; } = new List<CommandTypes>() { CommandTypes.SetCollectDuration, CommandTypes.SetInitialThreshold, CommandTypes.SetSignalSign, CommandTypes.SetSignalBaseline, CommandTypes.SetTimeInterval, CommandTypes.SetGain };

        public ObservableCollection<CommandControl> SettingCommands { get; } = new()
            {
                { new CommandControl() { CommandType = CommandTypes.SetCollectDuration, IsModified = false, Value = DEFAULT_COLLECTION_DURATION } },
                { new CommandControl() { CommandType = CommandTypes.SetInitialThreshold, IsModified = false, Value = DEFAULT_INITIAL_THRESHOLD } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalSign, IsModified = false, Value = DEFAULT_SIGNAL_SIGN } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalBaseline, IsModified = false, Value = DEFAULT_SIGNAL_BASELINE } },
                { new CommandControl() { CommandType = CommandTypes.SetTimeInterval, IsModified = false, Value = DEFAULT_TIME_INTERVAL } },
                { new CommandControl() { CommandType = CommandTypes.SetGain, IsModified = false, Value = DEFAULT_GAIN } }
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
                    logger_.Info("Initialize serial port");
                    await daq_.Initialize(serialConfig_);

                    foreach (var cmd in SettingCommands)
                    {
                        await daq_.WriteCommand(cmd.CommandType, cmd.Value);
                    }

                    CurrentStatus = AppStatus.Connected;
                }
                else
                {
                    daq_.Uninitialize();
                    CurrentStatus = AppStatus.Idle;
                }


            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", ex.Message, MessageType.Warning);
            }

        }

        [RelayCommand]
        private async Task StartCollecting()
        {
            try 
            {
                rawData_.Clear();
                plotData_.Points.Clear();
                CurrentStatus = AppStatus.Collecting;

                foreach (var cmd in SettingCommands)
                {
                    if (cmd.IsModified)
                    {
                        await daq_.WriteCommand(cmd.CommandType, cmd.Value);
                        cmd.IsModified = false;
                    }
                }
                var duration = SettingCommands.FirstOrDefault(x => x.CommandType == CommandTypes.SetCollectDuration).Value;
                await daq_.WriteCommand(CommandTypes.StartToCollect, (duration / 100));
                CurrentStatus = AppStatus.Connected;
                WriteDataToCsv();
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", ex.Message, MessageType.Warning);
            }

        }

        [RelayCommand (AllowConcurrentExecutions = true)]
        private async Task StopAndReset()
        {
            try 
            {
                await daq_.WriteCommand(CommandTypes.StopAndReset);
                rawData_.Clear();
                plotData_.Points.Clear();
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", ex.Message, MessageType.Warning);
            }
        }

        public MainWindowViewModel(SerialConfiguration serialConfig)
        {
            serialConfig_ = serialConfig ?? throw new ArgumentNullException(nameof(serialConfig));

            CurrentStatus = AppStatus.Idle;
            SelectedSetting = DataAcquisitionSettings.First();

            daq_.FilteredDataReceived += OnFilteredDataReceived;

            InitializePlot();
        }

        private void InitializePlot()
        {
            PlotModel = new PlotModel()
            {
                PlotAreaBorderColor = DEFAULT_COLOR,
                PlotAreaBorderThickness = new OxyThickness(2),
                TextColor = DEFAULT_COLOR,
                TitleColor = DEFAULT_COLOR,
                DefaultFontSize = 14,
            };

            var xAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                AxislineColor = DEFAULT_COLOR,
                TicklineColor = DEFAULT_COLOR,
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "ADC Channel",
                StringFormat = "0"
            };

            var yAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                AxislineColor = DEFAULT_COLOR,
                TicklineColor = DEFAULT_COLOR,
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Count",
                StringFormat = "0.0"
            };

            PlotModel.Axes.Add(xAxis);
            PlotModel.Axes.Add(yAxis);

            plotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 1,
                MarkerFill = DEFAULT_COLOR
            };

            PlotModel.Series.Add(plotData_);
        }

        public async Task CleanUp()
        {
            daq_.FilteredDataReceived -= OnFilteredDataReceived;
            await daq_.Uninitialize();
        }

        private async void OnFilteredDataReceived(object sender, int data)
        {
            rawData_.Add(data);
            await Task.Run(() =>
            {
                lock (plotData_)
                {
                    if (!plotData_.Points.Any(x => x.X == data))
                    {
                        var adcCountPair = new ScatterPoint(data, 1);
                        plotData_.Points.Add(adcCountPair);
                    }
                    else
                    {
                        var pointToUpdate = plotData_.Points.FirstOrDefault(x => x.X == data);
                        var adcCountPair = new ScatterPoint(data, (pointToUpdate.Y + 1));
                        plotData_.Points.Add(adcCountPair);
                    }
                    plotModel.InvalidatePlot(true);
                }
            });
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

        private void WriteDataToCsv()
        {
            Dictionary<int, int> frequencyDictionary = rawData_.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);

            string csvFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}output.csv");

            using (var writer = new StreamWriter(csvFilePath))
            {
                writer.WriteLine("Key,Value");

                foreach (var kvp in frequencyDictionary)
                {
                    writer.WriteLine($"{kvp.Key},{kvp.Value}");
                }
            }

            logger_.Info($"raw data has been saved to {csvFilePath}");
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly SerialConfiguration serialConfig_ = new();
        private readonly DataAcquisitionControl daq_ = new();
        private readonly List<int> rawData_ = new();

        private ScatterSeries plotData_;

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
