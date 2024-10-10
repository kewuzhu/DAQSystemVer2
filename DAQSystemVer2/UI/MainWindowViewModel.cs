using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.Model;
using DAQSystem.Common.Utility;
using DAQSystem.DataAcquisition;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
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
        private const int TRANSLATION_ANIMAITON_DISTANCE = 500;

        private readonly OxyColor DEFAULT_PLOT_COLOR = OxyColor.FromRgb(211, 211, 211);
        private readonly OxyColor DEFAULT_FITTED_PLOT_COLOR = OxyColor.FromRgb(255, 0, 0);
        private readonly OxyColor DEFAULT_OUTPUT_PLOT_COLOR = OxyColor.FromRgb(0, 0, 0);

        public List<CommandTypes> DataAcquisitionSettings { get; } = new List<CommandTypes>() { CommandTypes.SetCollectDuration, CommandTypes.SetInitialThreshold, CommandTypes.SetSignalSign, CommandTypes.SetSignalBaseline, CommandTypes.SetTimeInterval, CommandTypes.SetGain };

        public ObservableCollection<CommandControl> SettingCommands { get; }

        [ObservableProperty]
        private string workingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DEFAULT_DIR_NAME);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(ExportPlotToPdfCommand),
            nameof(StartCollectingCommand),
            nameof(StopAndResetCommand),
            nameof(CalculateGaussianCommand))]
        private AppStatus currentStatus;

        [ObservableProperty]
        private CommandTypes selectedSetting;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(ExportPlotToPdfCommand),
            nameof(CalculateGaussianCommand))]
        private int progressCounter;

        [ObservableProperty]
        private PlotModel plotModel;

        [ObservableProperty]
        private CommandControl selectedSettingCommand;

        [ObservableProperty]
        private bool isRendering;

        [ObservableProperty]
        private int gaussianRangeOnXStart;

        [ObservableProperty]
        private int gaussianRangeOnXEnd;

        [ObservableProperty]
        private double gaussianAmplitude;

        [ObservableProperty]
        private double gaussianMean;

        [ObservableProperty]
        private double gaussianSigma;

        [ObservableProperty]
        private bool isSettingFadePlaying;

        [ObservableProperty]
        private bool isCalculationFadePlaying;

        [ObservableProperty]
        private bool isGaussianTranslateInPlaying;

        [ObservableProperty]
        private bool isGaussianTranslateOutPlaying;

        [ObservableProperty]
        private int gaussianGridTranslation;

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
                    OxyPlot.SkiaSharp.PdfExporter.Export(PlotModel, stream, 600, 400);
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
                    await daq_.Initialize(serialConfig_);

                    CurrentStatus = AppStatus.Connected;
                }
                else
                {
                    ResetAllData();

                    await daq_.Uninitialize();
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
                    await daq_.WriteCommand(cmd.CommandType, cmd.Value);
                }
                SelectedSettingCommand = null;

                var duration = SettingCommands?.FirstOrDefault(x => x.CommandType == CommandTypes.SetCollectDuration)?.Value;
                await daq_.WriteCommand(CommandTypes.StartToCollect, (duration.Value / 100));

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
                await daq_.WriteCommand(CommandTypes.StopAndReset);
                ResetAllData();
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        private bool CanCalculateGaussian() => CurrentStatus == AppStatus.Connected && ProgressCounter != 0 && ProgressCounter == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanCalculateGaussian))]
        private void CalculateGaussian()
        {
            try 
            {
                Dictionary<int, int> frequencyDictionary = rawData_.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

                if (!frequencyDictionary.Any(x => x.Key == GaussianRangeOnXStart) ||
                    !frequencyDictionary.Any(x => x.Key == GaussianRangeOnXEnd) ||
                    GaussianRangeOnXStart >= GaussianRangeOnXEnd)
                {
                    UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"{Theme.GetString(Strings.Parameter)} {Theme.GetString(Strings.Error)}", MessageType.Warning);
                }

                IsCalculationFadePlaying = true;

                var xData = frequencyDictionary.Keys.Where(k => k >= GaussianRangeOnXStart && k <= GaussianRangeOnXEnd).OrderBy(x => x).ToArray();
                var yData = xData.Select(k => frequencyDictionary[k]).ToArray();

                var result = FitGaussian(xData, yData);

                GaussianAmplitude = result[0];
                GaussianMean = result[1];
                GaussianSigma = result[2];
                logger_.Info($"Fit Gaussian result: Amplitude:{GaussianAmplitude}, Mean:{GaussianMean}, Sigma:{GaussianSigma}");

                fittedPlotData_ ??= new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 2,
                    MarkerFill = DEFAULT_FITTED_PLOT_COLOR
                };

                for (int i = 0; i < xData.Length; i++)
                {
                    var fittedY = (Gaussian(xData[i], GaussianAmplitude, GaussianMean, GaussianSigma));
                    var adcCountPair = new ScatterPoint(xData[i], fittedY);
                    fittedPlotData_.Points.Add(adcCountPair);
                }

                if (!PlotModel.Series.Contains(fittedPlotData_))
                    PlotModel.Series.Add(fittedPlotData_);

                PlotModel.InvalidatePlot(true);
                logger_.Info($"Fitted plot updated");
            }
            catch (Exception ex) 
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        private void ResetAllData()
        {
            rawData_.Clear();
            ProgressCounter = 0;
            plotData_.Points.Clear();
            fittedPlotData_?.Points.Clear();
            GaussianAmplitude = 0;
            GaussianMean = 0;
            GaussianSigma = 0;
        }

        public MainWindowViewModel(SerialConfiguration serialConfig, DAQConfiguration daqConfig)
        {
            serialConfig_ = serialConfig ?? throw new ArgumentNullException(nameof(serialConfig));
            daqConfig_ = daqConfig ?? throw new ArgumentNullException(nameof(daqConfig));

            CurrentStatus = AppStatus.Idle;
            SelectedSetting = DataAcquisitionSettings.First();

            SettingCommands = new()
            {
                { new CommandControl() { CommandType = CommandTypes.SetCollectDuration, Value = daqConfig_.CollectDuration } },
                { new CommandControl() { CommandType = CommandTypes.SetInitialThreshold, Value = daqConfig_.InitialThreshold } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalSign, Value = daqConfig_.SignalSign } },
                { new CommandControl() { CommandType = CommandTypes.SetSignalBaseline, Value = daqConfig_.SignalBaseline } },
                { new CommandControl() { CommandType = CommandTypes.SetTimeInterval, Value = daqConfig_.TimeInterval } },
                { new CommandControl() { CommandType = CommandTypes.SetGain, Value = daqConfig_.Gain } }
            };

            daq_.FilteredDataReceived += OnFilteredDataReceived;

            InitializePlot();
            UpdatePlotColor(DEFAULT_PLOT_COLOR);
            GaussianGridTranslation = TRANSLATION_ANIMAITON_DISTANCE;
        }

        private void InitializePlot()
        {
            PlotModel = new PlotModel()
            {
                PlotAreaBorderThickness = new OxyThickness(2),
                DefaultFontSize = 14,
            };

            var xAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "ADC Channel",
                StringFormat = "0"
            };

            var yAxis = new OxyPlot.Axes.LinearAxis
            {
                AxislineThickness = 2,
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Count",
                StringFormat = "0.0"
            };

            PlotModel.Axes.Add(xAxis);
            PlotModel.Axes.Add(yAxis);

            plotData_ = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 1
            };

            PlotModel.Series.Add(plotData_);
        }

        private void UpdatePlotColor(OxyColor color)
        {
            PlotModel.PlotAreaBorderColor = color;
            PlotModel.TextColor = color;
            PlotModel.TitleColor = color;

            for (int i = 0; i < PlotModel.Axes.Count; i++)
            {
                PlotModel.Axes[i].AxislineColor = color;
                PlotModel.Axes[i].TicklineColor = color;
            }

            plotData_.MarkerFill = color;

            PlotModel.InvalidatePlot(true);
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
                        syncContextProxy_.ExecuteInSyncContext(() => { ProgressCounter++; });
                    }
                    PlotModel.InvalidatePlot(true);
                }
            });
        }

        private static double Gaussian(double x, double a, double b, double c)
        {
            return a * Math.Exp(-Math.Pow((x - b), 2) / (2 * Math.Pow(c, 2)));
        }

        private static double[] FitGaussian(int[] xData, int[] yData)
        {
            var a = yData.Max();
            var b = xData[Array.IndexOf(yData, a)];

            double meanX = yData.Select((y, i) => xData[i] * y).Sum() / yData.Sum();
            double c = Math.Sqrt(yData.Select((y, i) => y * Math.Pow(xData[i] - meanX, 2)).Sum() / yData.Sum());

            Func<Vector<double>, double> targetFunction = p =>
            {
                double amplitude = p[0];
                double mean = p[1];
                double sigma = p[2];
                double residual = 0.0;

                for (int i = 0; i < xData.Length; i++)
                {
                    double predicted = Gaussian(xData[i], amplitude, mean, sigma);
                    residual += Math.Pow(yData[i] - predicted, 2);
                }
                return residual;
            };

            var initialGuess = Vector<double>.Build.DenseOfArray(new double[] { a, b, c });

            var optimizer = new NelderMeadSimplex(1e-6, 2000);
            var result = optimizer.FindMinimum(ObjectiveFunction.Value(targetFunction), initialGuess);

            return result.MinimizingPoint.ToArray();
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
                case nameof(CurrentStatus):
                    if (CurrentStatus == AppStatus.Connected && rawData_.Count == 0)
                    {
                        IsGaussianTranslateInPlaying = true;
                        GaussianGridTranslation = 0;
                    }
                    else if(CurrentStatus == AppStatus.Idle)
                    {
                        IsGaussianTranslateOutPlaying = true;
                        GaussianGridTranslation = TRANSLATION_ANIMAITON_DISTANCE;
                    }
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
        private readonly DataAcquisitionControl daq_ = new();
        private readonly List<int> rawData_ = new();
        private readonly SyncContextProxy syncContextProxy_ = new();

        private ScatterSeries plotData_;
        private ScatterSeries fittedPlotData_;

        public partial class CommandControl : ObservableObject
        {
            [ObservableProperty]
            private CommandTypes commandType;

            [ObservableProperty]
            private int value;
        }
    }
}
