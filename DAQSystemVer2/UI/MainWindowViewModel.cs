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
using System.Collections.Generic;
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

        public List<int> MergedParameters { get; } = new List<int> { 2, 4, 6, 8 };

        public ObservableCollection<SettingCommandValuePair> SettingCommands { get; }

        [ObservableProperty]
        private string workingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DEFAULT_DIR_NAME);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(StartCollectingCommand),
            nameof(StopAndResetCommand))]
        private AppStatus currentStatus;

        [ObservableProperty]
        private CommandTypes selectedSetting;

        [ObservableProperty]
        private int selectedMergedParameter;

        [ObservableProperty]
        private LinearEquationParameters channelToEnergyParameters;

        [ObservableProperty]
        private int progressCounter;

        [ObservableProperty]
        private PlotModel mainPlotModel;

        [ObservableProperty]
        private SettingCommandValuePair selectedSettingCommand;

        [ObservableProperty]
        private bool isRendering;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenFitGaussianDialogCommand))]
        private PlotTypes currentPlotType;

        [ObservableProperty]
        private bool isSettingFadePlaying;

        [ObservableProperty]
        private bool isPlotFadePlaying;

        [ObservableProperty]
        private FitGaussianDialog fitGaussianWindow;

        [ObservableProperty]
        private ChannelToEnergyDialog channelToEnergyWindow;

        [ObservableProperty]
        private BiasCorrectionDialog biasCorrectionWindow;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(ExportPlotToPdfCommand),
            nameof(OpenFitGaussianDialogCommand),
            nameof(OpenChannelToEnergyDialogCommand),
            nameof(OpenBiasCorrectionDialogCommand))]
        private bool canSwitchPlot;

        [RelayCommand(CanExecute = nameof(CanSwitchPlot))]
        private void ExportPlotToPdf()
        {
            try
            {
                CreateWorkingDirectoryIfNotExists();

                string pdfFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{CurrentPlotType}{DEFAULT_PLOT_OUTPUT_FILENAME}");

                UpdatePlotColor(DEFAULT_OUTPUT_PLOT_COLOR);

                using (var stream = new FileStream(pdfFilePath, FileMode.Create))
                {
                    OxyPlot.SkiaSharp.PdfExporter.Export(MainPlotModel, stream, 600, 400);
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

                var frequencyDictionary = rawData_.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
                channelCountDictionary_ = new SortedDictionary<int, int>(frequencyDictionary);

                WriteDataToCsv();
                CurrentStatus = AppStatus.Connected;
                CanSwitchPlot = true;
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

        private bool CanOpenFitGaussianDialog() => CanSwitchPlot && CurrentPlotType != PlotTypes.EnergyChannel;

        [RelayCommand(CanExecute = nameof(CanOpenFitGaussianDialog))]
        private void OpenFitGaussianDialog()
        {
            logger_.Info($"{nameof(FitGaussianWindow)} showing.");
            if (FitGaussianWindow != null)
            {
                FitGaussianWindow.Show();
                return;
            }

            fitGaussianViewModel_ ??= new FitGaussianViewModel(MainPlotModel, plotDataDictionary_[CurrentPlotType], fittedPlotData_);

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

        [RelayCommand(CanExecute = nameof(CanSwitchPlot))]
        private void OpenChannelToEnergyDialog()
        {
            logger_.Info($"{nameof(ChannelToEnergyWindow)} showing.");

            if (ChannelToEnergyWindow != null)
            {
                channelToEnergyViewModel_.LinearEquationParametersChanged += OnChannelToEnergyParametersChanged;
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
                    channelToEnergyViewModel_.LinearEquationParametersChanged -= OnChannelToEnergyParametersChanged;
                    ChannelToEnergyWindow.Close();
                };

            channelToEnergyViewModel_.LinearEquationParametersChanged += OnChannelToEnergyParametersChanged;
            ChannelToEnergyWindow.ShowDialog();
        }

        [RelayCommand(CanExecute = nameof(CanSwitchPlot))]
        private void OpenBiasCorrectionDialog()
        {
            logger_.Info($"{nameof(BiasCorrectionWindow)} showing.");

            if (BiasCorrectionWindow != null)
            {
                biasCorrectionViewModel_.BiasCorrectionParametersChanged += OnBiasCorrectionParametersChanged;
                BiasCorrectionWindow.Show();
                return;
            }

            biasCorrectionViewModel_ ??= new BiasCorrectionViewModel();
            BiasCorrectionWindow ??= new BiasCorrectionDialog
            {
                DataContext = biasCorrectionViewModel_,
                Owner = UIUtils.GetActiveWindow()
            };

            biasCorrectionViewModel_.DialogCloseRequested += (s, e) =>
            {
                logger_.Info($"{nameof(BiasCorrectionWindow)} closing.");
                biasCorrectionViewModel_.BiasCorrectionParametersChanged -= OnBiasCorrectionParametersChanged;
                BiasCorrectionWindow.Close();
            };

            biasCorrectionViewModel_.BiasCorrectionParametersChanged += OnBiasCorrectionParametersChanged;
            BiasCorrectionWindow.ShowDialog();
        }

        private void ResetAllData()
        {
            CurrentPlotType = PlotTypes.CountChannel;
            CanSwitchPlot = false;
            rawData_.Clear();
            ProgressCounter = 0;
            plotData_.Points.Clear();
            fittedPlotData_.Points.Clear();
            foreach (var key in plotDataDictionary_.Keys)
            {
                plotDataDictionary_[key].Clear();
            }
            channelCountDictionary_?.Clear();
        }

        public MainWindowViewModel(SerialConfiguration serialConfig, DAQConfiguration daqConfig)
        {
            serialConfig_ = serialConfig ?? throw new ArgumentNullException(nameof(serialConfig));
            daqConfig_ = daqConfig ?? throw new ArgumentNullException(nameof(daqConfig));

            CurrentStatus = AppStatus.Idle;
            SelectedSetting = DataAcquisitionSettings.First();
            SelectedMergedParameter = MergedParameters.First();

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

            InitializePlot();
            UpdatePlotColor(DEFAULT_PLOT_COLOR);
            CurrentPlotType = PlotTypes.CountChannel;
        }

        private void InitializePlot()
        {
            MainPlotModel = new PlotModel()
            {
                PlotAreaBorderThickness = new OxyThickness(2),
                DefaultFontSize = 14,
            };

            var xAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "ADC Channel",
                StringFormat = "0.0"
            };

            var yAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Count",
                StringFormat = "0.0"
            };

            MainPlotModel.Axes.Add(xAxis);
            MainPlotModel.Axes.Add(yAxis);

            plotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 1
            };

            fittedPlotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 2,
                MarkerFill = DEFAULT_FITTED_PLOT_COLOR
            };

            MainPlotModel.Series.Add(plotData_);
            MainPlotModel.Series.Add(fittedPlotData_);
        }

        private void UpdatePlotColor(OxyColor color)
        {
            MainPlotModel.PlotAreaBorderColor = color;
            MainPlotModel.TextColor = color;
            MainPlotModel.TitleColor = color;

            for (int i = 0; i < MainPlotModel.Axes.Count; i++)
            {
                MainPlotModel.Axes[i].AxislineColor = color;
                MainPlotModel.Axes[i].TicklineColor = color;
            }

            plotData_.MarkerFill = color;

            MainPlotModel.InvalidatePlot(true);
        }

        public async Task CleanUp()
        {
            daqControl_.FilteredDataReceived -= OnFilteredDataReceived;
            await daqControl_.Uninitialize();
        }

        private async void OnFilteredDataReceived(object sender, List<int> data)
        {
            rawData_.AddRange(data);
            await UpdatePlotOnDataReceived(data);
        }

        private async Task UpdatePlotOnDataReceived(List<int> data)
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
                            plotDataDictionary_[PlotTypes.CountChannel].Add(adcCountPair);
                        }
                        else
                        {
                            var pointToUpdate = plotData_.Points.OrderByDescending(x => x.Y).FirstOrDefault(x => x.X == d);
                            var adcCountPair = new ScatterPoint(d, (pointToUpdate.Y + 1));
                            plotData_.Points.Add(adcCountPair);
                            plotDataDictionary_[PlotTypes.CountChannel].Add(adcCountPair);
                        }
                        syncContextProxy_.ExecuteInSyncContext(() => { ProgressCounter++; });
                    }
                    MainPlotModel.InvalidatePlot(true);
                }
            });
        }

        private async void OnChannelToEnergyParametersChanged(Object sender, LinearEquationParameters p)
        {
            ChannelToEnergyParameters = p;
            logger_.Info($"{nameof(ChannelToEnergyParameters)} has been set, coefficient{ChannelToEnergyParameters.Coefficient}, constant{ChannelToEnergyParameters.Constant}");

            if (plotDataDictionary_[PlotTypes.CountChannel].Count == 0)
                return;

            plotDataDictionary_[PlotTypes.EnergyChannel].Clear();

            foreach (var point in plotDataDictionary_[PlotTypes.CountChannel])
            {
                await Task.Run(() =>
                {
                    lock (plotDataDictionary_[PlotTypes.CountChannel])
                    {
                        var pointX = point.X;
                        var pointY = point.X * ChannelToEnergyParameters.Coefficient + ChannelToEnergyParameters.Constant;
                        plotDataDictionary_[PlotTypes.EnergyChannel].Add(new ScatterPoint(pointX, pointY));
                    }
                });
            }

            if (CurrentPlotType == PlotTypes.EnergyChannel)
                await UpdatePlot();
        }

        private async void OnBiasCorrectionParametersChanged(Object sender, LinearEquationParameters p)
        {
            biasCorrectionParameters_ = p;
            logger_.Info($"{nameof(biasCorrectionParameters_)} has been set, coefficient{biasCorrectionParameters_.Coefficient}, constant{biasCorrectionParameters_.Constant}");

            if (plotDataDictionary_[PlotTypes.CountChannel].Count == 0)
                return;

            plotDataDictionary_[PlotTypes.BiasCorrection].Clear();

            foreach (var point in plotDataDictionary_[PlotTypes.CountChannel])
            {
                await Task.Run(() =>
                {
                    lock (plotDataDictionary_[PlotTypes.BiasCorrection])
                    {
                        var pointX = point.X;
                        var corrected = point.Y - (point.X * biasCorrectionParameters_.Coefficient + biasCorrectionParameters_.Constant);
                        var pointY = corrected < 0 ? 0 : corrected;
                        plotDataDictionary_[PlotTypes.BiasCorrection].Add(new ScatterPoint(pointX, pointY));
                    }
                });
            }

            if (CurrentPlotType == PlotTypes.BiasCorrection)
                await UpdatePlot();
        }

        protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
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
                    logger_.Info($"Current Plot Type switched to {CurrentPlotType}");
                    IsPlotFadePlaying = true;
                    await UpdatePlot();
                    break;
                case nameof(SelectedMergedParameter):
                    logger_.Info($"{nameof(SelectedMergedParameter)} has been set to {SelectedMergedParameter}");
                    await UpdatePlot();
                    break;
            }
        }

        private async Task UpdatePlot()
        {
            if (MainPlotModel == null)
                return;
            await Task.Run(() =>
            {
                switch (CurrentPlotType)
                {
                    case PlotTypes.CountChannel:
                        MainPlotModel.Axes[0].Title = "ADC Channel";
                        MainPlotModel.Axes[1].Title = "Count";
                        break;
                    case PlotTypes.EnergyChannel:
                        MainPlotModel.Axes[0].Title = "ADC Channel";
                        MainPlotModel.Axes[1].Title = "Energy";
                        break;
                    case PlotTypes.ToLn:
                        MainPlotModel.Axes[0].Title = "LnChannel";
                        MainPlotModel.Axes[1].Title = "Count";
                        CalculateChannelToLn();
                        break;
                    case PlotTypes.MergedChannel:
                        MainPlotModel.Axes[0].Title = "Merged ADC Channel";
                        MainPlotModel.Axes[1].Title = "Count";
                        ConvertToMergedChannel();
                        break;
                    case PlotTypes.BiasCorrection:
                        MainPlotModel.Axes[0].Title = "ADC Channel";
                        MainPlotModel.Axes[1].Title = "Bias Corrected Count";
                        break;
                }
                plotData_.Points.Clear();
                fittedPlotData_.Points.Clear();
                plotData_.Points.AddRange(plotDataDictionary_[CurrentPlotType]);
                MainPlotModel.InvalidatePlot(true);
            });
        }

        private void CalculateChannelToLn()
        {
            if (plotDataDictionary_[PlotTypes.CountChannel].Count == 0)
            {
                logger_.Info($"Unable to calculate channel to ln");
                return;
            }

            plotDataDictionary_[PlotTypes.ToLn].Clear();

            plotDataDictionary_[PlotTypes.ToLn].AddRange(
                plotDataDictionary_[PlotTypes.CountChannel]
                .Select(point => new ScatterPoint(Math.Log(point.X), point.Y))
            );
        }

        private void ConvertToMergedChannel()
        {
            if (channelCountDictionary_ == null || channelCountDictionary_.Count == 0)
            {
                logger_.Info($"Unable to convert to merged channel");
                return;
            }

            plotDataDictionary_[PlotTypes.MergedChannel].Clear();

            var mergedDict = new SortedDictionary<double, int>();
            var mergedKeys = new HashSet<int>();
            var keys = new List<int>(channelCountDictionary_.Keys);

            for (int i = 0; i <= keys.Count - SelectedMergedParameter; i++)
            {
                bool isMerged = keys.GetRange(i, SelectedMergedParameter).Any(key => mergedKeys.Contains(key));

                if (!isMerged)
                {
                    var currentKeys = keys.GetRange(i, SelectedMergedParameter);

                    double newKey = currentKeys.Average();
                    int newValue = currentKeys.Sum(key => channelCountDictionary_[key]);

                    mergedDict[newKey] = newValue;

                    foreach (var key in currentKeys)
                    {
                        mergedKeys.Add(key);
                    }
                }
            }

            foreach (var key in mergedDict.Keys)
            {
                for (int i = 0; i <= mergedDict[key]; i++)
                {
                    var pointX = key;
                    var pointY = i;
                    plotDataDictionary_[PlotTypes.MergedChannel].Add(new ScatterPoint(key, i));
                }
            }
        }

        private void WriteDataToCsv()
        {
            if (channelCountDictionary_.Count == 0)
                return;

            CreateWorkingDirectoryIfNotExists();

            string csvFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{DEFAULT_RAW_DATA_OUTPUT_FILENAME}");

            using (var writer = new StreamWriter(csvFilePath))
            {
                writer.WriteLine("Key,Value");

                foreach (var kvp in channelCountDictionary_)
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
        private readonly Dictionary<PlotTypes, List<ScatterPoint>> plotDataDictionary_ = new()
        {
            { PlotTypes.CountChannel, new List<ScatterPoint>() },
            { PlotTypes.EnergyChannel, new List<ScatterPoint>() },
            { PlotTypes.ToLn, new List<ScatterPoint>() },
            { PlotTypes.MergedChannel, new List<ScatterPoint>() },
            { PlotTypes.BiasCorrection, new List<ScatterPoint>() }
        };

        private SortedDictionary<int, int> channelCountDictionary_;
        private ScatterSeries plotData_;
        private ScatterSeries fittedPlotData_;
        private FitGaussianViewModel fitGaussianViewModel_;
        private ChannelToEnergyViewModel channelToEnergyViewModel_;
        private BiasCorrectionViewModel biasCorrectionViewModel_;
        private LinearEquationParameters biasCorrectionParameters_;

        public partial class SettingCommandValuePair : ObservableObject
        {
            [ObservableProperty]
            private CommandTypes commandType;

            [ObservableProperty]
            private int value;
        }
    }
}
