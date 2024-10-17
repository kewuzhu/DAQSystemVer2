using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.UI.Dialog;
using DAQSystem.Application.Utility;
using DAQSystem.Common.Model;
using DAQSystem.Common.Utility;
using DAQSystem.DataAcquisition;
using NLog;
using OxyPlot;
using OxyPlot.Series;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace DAQSystem.Application.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private const string DEFAULT_DIR_NAME = "DAQSystem";
        private const string DEFAULT_RAW_DATA_OUTPUT_FILENAME = "Raw_Data.csv";
        private const string DEFAULT_PLOT_OUTPUT_FILENAME = "Plot.pdf";
        private const int TRANSLATION_ANIMAITON_DISTANCE = 500;

        private readonly OxyColor DEFAULT_PLOT_COLOR = OxyColor.FromRgb(211, 211, 211);
        private readonly OxyColor DEFAULT_FITTED_PLOT_COLOR = OxyColor.FromRgb(255, 0, 0);
        private readonly OxyColor DEFAULT_OUTPUT_PLOT_COLOR = OxyColor.FromRgb(0, 0, 0);

        public List<CommandTypes> DataAcquisitionSettings { get; } = new List<CommandTypes>() { CommandTypes.SetCollectDuration, CommandTypes.SetInitialThreshold, CommandTypes.SetSignalSign, CommandTypes.SetSignalBaseline, CommandTypes.SetTimeInterval, CommandTypes.SetGain };

        public ObservableCollection<SettingCommandValuePair> SettingCommands { get; }

        [ObservableProperty]
        private string workingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DEFAULT_DIR_NAME);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(ExportPlotToPdfCommand),
            nameof(StartCollectingCommand),
            nameof(StopAndResetCommand),
            nameof(OpenFitGaussianDialogCommand),
            nameof(OpenChannelToEnergyDialogCommand))]
        private AppStatus currentStatus;

        [ObservableProperty]
        private CommandTypes selectedSetting;

        [ObservableProperty]
        private PlotTypes selectedPlotType;

        [ObservableProperty]
        private LinearEquationParameters channelToEnergyParameters;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(ExportPlotToPdfCommand),
            nameof(OpenFitGaussianDialogCommand),
            nameof(OpenChannelToEnergyDialogCommand))]
        private int progressCounter;

        [ObservableProperty]
        private PlotModel countChannelPlotModel;

        [ObservableProperty]
        private PlotModel countEnergyPlotModel;

        [ObservableProperty]
        private SettingCommandValuePair selectedSettingCommand;

        [ObservableProperty]
        private bool isRendering;

        [ObservableProperty]
        private PlotTypes currentPlotType;

        [ObservableProperty]
        private bool canSwitchPlotType;

        [ObservableProperty]
        private bool isSettingFadePlaying;

        [ObservableProperty]
        private bool isPlotFadePlaying;

        [ObservableProperty]
        private FitGaussianDialog fitGaussianWindow;

        [ObservableProperty]
        private ChannelToEnergyDialog channelToEnergyWindow;

        private bool CanExportPlotToPdf() => CurrentStatus == AppStatus.Connected && ProgressCounter != 0 && ProgressCounter == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanExportPlotToPdf))]
        private void ExportPlotToPdf()
        {
            try
            {
                CreateWorkingDirectoryIfNotExists();

                string pdfFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{DEFAULT_PLOT_OUTPUT_FILENAME}");

                UpdatePlotColor(DEFAULT_OUTPUT_PLOT_COLOR);

                using (var stream = new FileStream(pdfFilePath, FileMode.Create))
                {
                    OxyPlot.SkiaSharp.PdfExporter.Export(CurrentPlotType == PlotTypes.CountChannel ? CountChannelPlotModel : CountEnergyPlotModel, stream, 600, 400);
                }

                UpdatePlotColor(DEFAULT_PLOT_COLOR);

                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Notice)}", $"{string.Format(Theme.GetString(Strings.SaveFileToPathMessageFormat), pdfFilePath)}", MessageType.Info);
            }
            catch (Exception ex) 
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        [RelayCommand]
        private void SelectDirectory()
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                WorkingDirectory = dialog.SelectedPath;
                logger_.Info($"Working directory has been changed to {WorkingDirectory}");
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
                    await daqControl_.Initialize(serialConfig_);

                    CurrentStatus = AppStatus.Connected;
                }
                else
                {
                    ResetAllData();

                    await daqControl_.Uninitialize();
                    logger_.Info("Uninitialize serial port");
                    CurrentStatus = AppStatus.Idle;
                }
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        private bool CanStartCollecting() => CurrentStatus == AppStatus.Connected;

        [RelayCommand(CanExecute = nameof(CanStartCollecting))]
        private async Task StartCollecting()
        {
            try
            {
                ResetAllData();

                CurrentStatus = AppStatus.Collecting;

                foreach (var cmd in SettingCommands)
                {
                    SelectedSettingCommand = cmd;
                    await Task.Delay(100);
                    await daqControl_.WriteCommand(cmd.CommandType, cmd.Value);
                }
                SelectedSettingCommand = null;

                var duration = SettingCommands?.FirstOrDefault(x => x.CommandType == CommandTypes.SetCollectDuration)?.Value;
                await daqControl_.WriteCommand(CommandTypes.StartToCollect, (duration.Value / 100));

                WriteDataToCsv();
                CurrentStatus = AppStatus.Connected;
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        private bool CanStopAndReset() => CurrentStatus != AppStatus.Idle;

        [RelayCommand(AllowConcurrentExecutions = true,
            CanExecute = nameof(CanStopAndReset))]
        private async Task StopAndReset()
        {
            try
            {
                await daqControl_.WriteCommand(CommandTypes.StopAndReset);
                ResetAllData();
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        private bool CanOpenFitGaussianDialog() => CurrentStatus == AppStatus.Connected && ProgressCounter != 0 && ProgressCounter == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanOpenFitGaussianDialog))]
        private void OpenFitGaussianDialog() 
        {
            logger_.Info($"{nameof(FitGaussianWindow)} showing.");
            if (FitGaussianWindow != null)
            {
                FitGaussianWindow.Show();
                return;
            }

            fitGaussianViewModel_ ??= new FitGaussianViewModel(CountChannelPlotModel, rawData_, fittedCountChannelPlotData_);
            
            FitGaussianWindow ??= new FitGaussianDialog
            {
                DataContext = fitGaussianViewModel_,
                Owner = UIUtils.GetActiveWindow()
            };

            fitGaussianViewModel_.DialogCloseRequested += (s, e) => 
                {
                    logger_.Info($"{nameof(FitGaussianWindow)} closing.");
                    FitGaussianWindow.Close(); 
                };

            FitGaussianWindow.ShowDialog();
        }

        private bool CanOpenChannelToEnergyDialog() => CurrentStatus == AppStatus.Connected && ProgressCounter != 0 && ProgressCounter == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanOpenChannelToEnergyDialog))]
        private void OpenChannelToEnergyDialog() 
        {
            logger_.Info($"{nameof(ChannelToEnergyWindow)} showing.");
            
            if (ChannelToEnergyWindow != null)
            {
                channelToEnergyViewModel_.LinearEquationParametersChanged += OnLinearEquationParametersChanged;
                ChannelToEnergyWindow.Show();
                return;
            }

            channelToEnergyViewModel_ ??= new ChannelToEnergyViewModel();
            ChannelToEnergyWindow ??= new ChannelToEnergyDialog
            {
                DataContext = channelToEnergyViewModel_,
                Owner = UIUtils.GetActiveWindow()
            };

            channelToEnergyViewModel_.DialogCloseRequested += (s, e) => 
                {
                    logger_.Info($"{nameof(ChannelToEnergyWindow)} closing.");
                    channelToEnergyViewModel_.LinearEquationParametersChanged -= OnLinearEquationParametersChanged;
                    ChannelToEnergyWindow.Close(); 
                };

            channelToEnergyViewModel_.LinearEquationParametersChanged += OnLinearEquationParametersChanged;
            ChannelToEnergyWindow.ShowDialog();
        }

        private void ResetAllData()
        {
            rawData_.Clear();
            ProgressCounter = 0;
            countChannelPlotData_.Points.Clear();
            fittedCountChannelPlotData_?.Points.Clear();
            countEnergyPlotData_.Points.Clear();
            fittedCountEnergyPlotData_?.Points.Clear();
        }

        public MainWindowViewModel(SerialConfiguration serialConfig, DAQConfiguration daqConfig)
        {
            serialConfig_ = serialConfig ?? throw new ArgumentNullException(nameof(serialConfig));
            daqConfig_ = daqConfig ?? throw new ArgumentNullException(nameof(daqConfig));

            CurrentStatus = AppStatus.Idle;
            SelectedSetting = DataAcquisitionSettings.First();

            SettingCommands = new()
            {
                { new SettingCommandValuePair() { CommandType = CommandTypes.SetCollectDuration, Value = daqConfig_.CollectDuration } },
                { new SettingCommandValuePair() { CommandType = CommandTypes.SetInitialThreshold, Value = daqConfig_.InitialThreshold } },
                { new SettingCommandValuePair() { CommandType = CommandTypes.SetSignalSign, Value = daqConfig_.SignalSign } },
                { new SettingCommandValuePair() { CommandType = CommandTypes.SetSignalBaseline, Value = daqConfig_.SignalBaseline } },
                { new SettingCommandValuePair() { CommandType = CommandTypes.SetTimeInterval, Value = daqConfig_.TimeInterval } },
                { new SettingCommandValuePair() { CommandType = CommandTypes.SetGain, Value = daqConfig_.Gain } }
            };

            daqControl_.FilteredDataReceived += OnFilteredDataReceived;

            CurrentPlotType = PlotTypes.CountChannel;
            InitializePlot();
            UpdatePlotColor(DEFAULT_PLOT_COLOR);
        }

        private void InitializePlot()
        {
            CountChannelPlotModel = new PlotModel()
            {
                PlotAreaBorderThickness = new OxyThickness(2),
                DefaultFontSize = 14,
            };

            CountEnergyPlotModel = new PlotModel()
            {
                PlotAreaBorderThickness = new OxyThickness(2),
                DefaultFontSize = 14,
            };

            var xAxisChannel = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "ADC Channel",
                StringFormat = "0.0"
            };

            var xAxisEnergy = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Energy",
                StringFormat = "0.0"
            };

            var yAxisChannel = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Count",
                StringFormat = "0.0"
            };

            var yAxisEnergy = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Count",
                StringFormat = "0.0"
            };

            CountChannelPlotModel.Axes.Add(xAxisChannel);
            CountChannelPlotModel.Axes.Add(yAxisChannel);
            CountEnergyPlotModel.Axes.Add(xAxisEnergy);
            CountEnergyPlotModel.Axes.Add(yAxisEnergy);

            countChannelPlotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 1
            };

            countEnergyPlotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 1
            };

            fittedCountChannelPlotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 2,
                MarkerFill = DEFAULT_FITTED_PLOT_COLOR
            };

            fittedCountEnergyPlotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 2,
                MarkerFill = DEFAULT_FITTED_PLOT_COLOR
            };

            CountChannelPlotModel.Series.Add(countChannelPlotData_);
            CountChannelPlotModel.Series.Add(fittedCountChannelPlotData_);

            CountEnergyPlotModel.Series.Add(countEnergyPlotData_);
            CountEnergyPlotModel.Series.Add(fittedCountEnergyPlotData_);
        }

        private void UpdatePlotColor(OxyColor color)
        {
            CountChannelPlotModel.PlotAreaBorderColor = color;
            CountChannelPlotModel.TextColor = color;
            CountChannelPlotModel.TitleColor = color;

            CountEnergyPlotModel.PlotAreaBorderColor = color;
            CountEnergyPlotModel.TextColor = color;
            CountEnergyPlotModel.TitleColor = color;

            for (int i = 0; i < CountChannelPlotModel.Axes.Count; i++)
            {
                CountChannelPlotModel.Axes[i].AxislineColor = color;
                CountChannelPlotModel.Axes[i].TicklineColor = color;

                CountEnergyPlotModel.Axes[i].AxislineColor = color;
                CountEnergyPlotModel.Axes[i].TicklineColor = color;
            }

            countChannelPlotData_.MarkerFill = color;
            countEnergyPlotData_.MarkerFill = color;

            CountChannelPlotModel.InvalidatePlot(true);
            CountEnergyPlotModel.InvalidatePlot(true);
        }

        public async Task CleanUp()
        {
            daqControl_.FilteredDataReceived -= OnFilteredDataReceived;
            await daqControl_.Uninitialize();
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
                lock (countChannelPlotData_)
                {
                    foreach (int d in data)
                    {
                        if (!countChannelPlotData_.Points.Any(x => x.X == d))
                        {
                            var adcCountPair = new ScatterPoint(d, 1);
                            countChannelPlotData_.Points.Add(adcCountPair);
                        }
                        else
                        {
                            var pointToUpdate = countChannelPlotData_.Points.OrderByDescending(x => x.Y).FirstOrDefault(x => x.X == d);
                            var adcCountPair = new ScatterPoint(d, (pointToUpdate.Y + 1));
                            countChannelPlotData_.Points.Add(adcCountPair);
                        }
                        syncContextProxy_.ExecuteInSyncContext(() => { ProgressCounter++; });
                    }
                    CountChannelPlotModel.InvalidatePlot(true);
                }
            });
        }

        private void OnLinearEquationParametersChanged(Object sender, LinearEquationParameters p) 
        {
            ChannelToEnergyParameters = p;

            countEnergyPlotData_.Points.Clear();

            foreach (var point in countChannelPlotData_.Points) 
            {
                var pointX = point.X * ChannelToEnergyParameters.Coefficient + ChannelToEnergyParameters.Constant;
                var pointY = point.Y;
                countEnergyPlotData_.Points.Add(new ScatterPoint(pointX, pointY));
            }

            CountEnergyPlotModel.InvalidatePlot(true);
            CanSwitchPlotType = true;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            switch (e.PropertyName)
            {
                case nameof(SelectedSetting):
                    IsSettingFadePlaying = true;
                    break;
                case nameof(ProgressCounter):
                    IsRendering = ProgressCounter != rawData_.Count;
                    break;
                case nameof(CurrentPlotType):
                    IsPlotFadePlaying = true;
                    logger_.Info($"Current Plot Type switched to {CurrentPlotType}");
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
            UserCommunication.ShowMessage($"{Theme.GetString(Strings.Notice)}", $"{string.Format(Theme.GetString(Strings.SaveFileToPathMessageFormat), csvFilePath)}", MessageType.Info);
        }

        private void CreateWorkingDirectoryIfNotExists()
        {
            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly SerialConfiguration serialConfig_ = new();
        private readonly DAQConfiguration daqConfig_ = new();
        private readonly DataAcquisitionControl daqControl_ = new();
        private readonly List<int> rawData_ = new();
        private readonly SyncContextProxy syncContextProxy_ = new();

        private ScatterSeries countChannelPlotData_;
        private ScatterSeries countEnergyPlotData_;
        private ScatterSeries fittedCountChannelPlotData_;
        private ScatterSeries fittedCountEnergyPlotData_;
        private ChannelToEnergyViewModel channelToEnergyViewModel_;
        private FitGaussianViewModel fitGaussianViewModel_;

        public partial class SettingCommandValuePair : ObservableObject
        {
            [ObservableProperty]
            private CommandTypes commandType;

            [ObservableProperty]
            private int value;
        }
    }
}
