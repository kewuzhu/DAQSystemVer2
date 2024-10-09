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
using System.ComponentModel;
using System.IO;

namespace DAQSystem.Application.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private const string DEFAULT_DIR_NAME = "DAQSystem";
        private const string DEFAULT_RAW_DATA_OUTPUT_FILENAME = "Raw_Data.csv";
        private const string DEFAULT_PLOT_OUTPUT_FILENAME = "Plot.pdf";
        private const int DEFAULT_COLLECTION_DURATION = 20000;
        private const int DEFAULT_INITIAL_THRESHOLD = 1000;
        private const int DEFAULT_SIGNAL_SIGN = 1;
        private const int DEFAULT_SIGNAL_BASELINE = 1050;
        private const int DEFAULT_TIME_INTERVAL = 1000;
        private const int DEFAULT_GAIN = 1340;

        private readonly OxyColor DEFAULT_COLOR = OxyColor.FromRgb(211, 211, 211);

        public List<CommandTypes> DataAcquisitionSettings { get; } = new List<CommandTypes>() { CommandTypes.SetCollectDuration, CommandTypes.SetInitialThreshold, CommandTypes.SetSignalSign, CommandTypes.SetSignalBaseline, CommandTypes.SetTimeInterval, CommandTypes.SetGain };

        public ObservableCollection<CommandControl> SettingCommands { get; } = new()
            {
                { new CommandControl() { CommandType = CommandTypes.SetCollectDuration, Value = DEFAULT_COLLECTION_DURATION } },
                { new CommandControl() { CommandType = CommandTypes.SetInitialThreshold, Value = DEFAULT_INITIAL_THRESHOLD } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalSign, Value = DEFAULT_SIGNAL_SIGN } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalBaseline, Value = DEFAULT_SIGNAL_BASELINE } },
                { new CommandControl() { CommandType = CommandTypes.SetTimeInterval, Value = DEFAULT_TIME_INTERVAL } },
                { new CommandControl() { CommandType = CommandTypes.SetGain, Value = DEFAULT_GAIN } }
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

        private bool CanExportPlotToPdf() => plotData_.Points.Count != 0 && progressCounter_ == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanExportPlotToPdf))]
        private void ExportPlotToPdf()
        {
            CreateWorkingDirectoryIfNotExists();

            string pdfFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{DEFAULT_PLOT_OUTPUT_FILENAME}");

            lock (PlotModel) 
            {
                using (var stream = new FileStream(pdfFilePath, FileMode.Create))
                {
                    OxyPlot.SkiaSharp.PdfExporter.Export(PlotModel, stream, 600, 400);
                }
            }

            UserCommunication.ShowMessage("null", $"{string.Format(Theme.GetString(Strings.SaveFileToPathMessageFormat), pdfFilePath)}", MessageType.Info);
        }

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

                    CurrentStatus = AppStatus.Connected;
                }
                else
                {
                    await daq_.Uninitialize();
                    logger_.Info("Uninitialize serial port");
                    CurrentStatus = AppStatus.Idle;
                }


            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nSource:{ex.Source}", MessageType.Warning);
            }

        }

        [RelayCommand]
        private async Task StartCollecting()
        {
            try
            {
                ResetAllData();

                CurrentStatus = AppStatus.Collecting;

                foreach (var cmd in SettingCommands)
                {
                    await daq_.WriteCommand(cmd.CommandType, cmd.Value);
                }

                var duration = SettingCommands?.FirstOrDefault(x => x.CommandType == CommandTypes.SetCollectDuration)?.Value;
                await daq_.WriteCommand(CommandTypes.StartToCollect, (duration.Value / 100));

                WriteDataToCsv();
                CurrentStatus = AppStatus.Connected;
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nSource:{ex.Source}", MessageType.Warning);
            }

        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task StopAndReset()
        {
            try
            {
                await daq_.WriteCommand(CommandTypes.StopAndReset);
                ResetAllData();
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nSource:{ex.Source}", MessageType.Warning);
            }
        }

        private void ResetAllData() 
        {
            rawData_.Clear();
            progressCounter_ = 0;
            plotData_.Points.Clear();
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

        private async void OnFilteredDataReceived(object sender, List<int> data)
        {
            rawData_.AddRange(data);
            await UpdatePlot(data);
        }

        private async Task UpdatePlot(List<int> data)
        {
            await Task.Run(() =>
            {
                lock (plotData_)
                {
                    foreach (int d in data) 
                    {
                        if (!plotData_.Points.Any(x => x.X == d))
                        {
                            var adcCountPair = new ScatterPoint(d, 1);
                            plotData_.Points.Add(adcCountPair);
                        }
                        else
                        {
                            var pointToUpdate = plotData_.Points.OrderByDescending(x => x.Y).FirstOrDefault(x => x.X == d);
                            var adcCountPair = new ScatterPoint(d, (pointToUpdate.Y + 1));
                            plotData_.Points.Add(adcCountPair);
                        }
                    }
                    PlotModel.InvalidatePlot(true);
                }
            });
            await App.Current.Dispatcher.BeginInvoke(new Action(() => { progressCounter_ += data.Count; }));
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

            CreateWorkingDirectoryIfNotExists();

            string csvFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{DEFAULT_RAW_DATA_OUTPUT_FILENAME}");

            using (var writer = new StreamWriter(csvFilePath))
            {
                writer.WriteLine("Key,Value");

                foreach (var kvp in frequencyDictionary)
                {
                    writer.WriteLine($"{kvp.Key},{kvp.Value}");
                }
            }
            UserCommunication.ShowMessage("null", $"{string.Format(Theme.GetString(Strings.SaveFileToPathMessageFormat),csvFilePath)}", MessageType.Info);
        }

        private void CreateWorkingDirectoryIfNotExists()
        {
            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly SerialConfiguration serialConfig_ = new();
        private readonly DataAcquisitionControl daq_ = new();
        private readonly List<int> rawData_ = new();

        private ScatterSeries plotData_;
        private int progressCounter_;

        public partial class CommandControl : ObservableObject
        {
            [ObservableProperty]
            private CommandTypes commandType;

            [ObservableProperty]
            private int value;
        }
    }
}
