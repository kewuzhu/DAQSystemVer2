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

        private bool CanExportPlotToPdf() => CurrentStatus == AppStatus.Connected && ProgressCounter != 0 && ProgressCounter == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanExportPlotToPdf))]
        private void ExportPlotToPdf()
        {
            CreateWorkingDirectoryIfNotExists();

            string pdfFilePath = Path.Combine(WorkingDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{DEFAULT_PLOT_OUTPUT_FILENAME}");

            using (var stream = new FileStream(pdfFilePath, FileMode.Create))
            {
                OxyPlot.SkiaSharp.PdfExporter.Export(PlotModel, stream, 600, 400);
            }

            UserCommunication.ShowMessage($"{Theme.GetString(Strings.Notice)}", $"{string.Format(Theme.GetString(Strings.SaveFileToPathMessageFormat), pdfFilePath)}", MessageType.Info);
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
                    ResetAllData();

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
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nSource:{ex.Source}", MessageType.Warning);
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
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nSource:{ex.Source}", MessageType.Warning);
            }
        }

        private bool CanCalculateGaussian() => CurrentStatus == AppStatus.Connected && ProgressCounter != 0 && ProgressCounter == rawData_.Count;

        [RelayCommand(CanExecute = nameof(CanCalculateGaussian))]

        private void CalculateGaussian()
        {
            Dictionary<int, int> frequencyDictionary = rawData_.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            if (!frequencyDictionary.Any(x => x.Key == GaussianRangeOnXStart) ||
                !frequencyDictionary.Any(x => x.Key == GaussianRangeOnXEnd) ||
                GaussianRangeOnXStart >= GaussianRangeOnXEnd)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"{Theme.GetString(Strings.Parameter)} {Theme.GetString(Strings.Error)}", MessageType.Warning);
            }

            IsAnimationPlaying = true;

            var xData = frequencyDictionary.Keys.Where(k => k >= GaussianRangeOnXStart && k <= GaussianRangeOnXEnd).ToArray();
            var yData = xData.Select(k => frequencyDictionary[k]).ToArray();

            var result = FitGaussian(xData, yData);

            GaussianAmplitude = result[0];
            GaussianMean = result[1];
            GaussianSigma = result[2];
        }

        private void ResetAllData()
        {
            rawData_.Clear();
            ProgressCounter = 0;
            plotData_.Points.Clear();
            GaussianAmplitude = 0;
            GaussianMean = 0;
            GaussianSigma = 0;
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
                    IsAnimationPlaying = true;
                    break;
                case nameof(ProgressCounter):
                    IsRendering = ProgressCounter != rawData_.Count;
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
        private readonly DataAcquisitionControl daq_ = new();
        private readonly List<int> rawData_ = new();
        private readonly SyncContextProxy syncContextProxy_ = new();

        private ScatterSeries plotData_;

        public partial class CommandControl : ObservableObject
        {
            [ObservableProperty]
            private CommandTypes commandType;

            [ObservableProperty]
            private int value;
        }
    }
}
