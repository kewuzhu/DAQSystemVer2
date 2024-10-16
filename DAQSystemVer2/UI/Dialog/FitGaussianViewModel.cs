using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.UI;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using NLog;
using OxyPlot;
using OxyPlot.Series;
using System.Collections.Generic;

namespace DAQSystem.Application.UI.Dialog
{
    internal partial class FitGaussianViewModel : DialogViewModelBase
    {
        public event EventHandler DialogCloseRequested;

        public PlotModel PlotModel { get; }

        [ObservableProperty]
        private int maxAt;

        [ObservableProperty]
        private int maxCount;

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

        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
            DialogCloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void CalculateFitGaussianAndUpdatePlot()
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

                GetMaximumPairInfo(frequencyDictionary);

                var xData = frequencyDictionary.Keys.Where(k => k >= GaussianRangeOnXStart && k <= GaussianRangeOnXEnd).OrderBy(x => x).ToArray();
                var yData = xData.Select(k => frequencyDictionary[k]).ToArray();

                var result = FitGaussian(xData, yData);

                GaussianAmplitude = result[0];
                GaussianMean = result[1];
                GaussianSigma = result[2];
                logger_.Info($"Fit Gaussian result: Amplitude:{GaussianAmplitude}, Mean:{GaussianMean}, Sigma:{GaussianSigma}");

                for (int i = 0; i < xData.Length; i++)
                {
                    var fittedY = (Gaussian(xData[i], GaussianAmplitude, GaussianMean, GaussianSigma));
                    var adcCountPair = new ScatterPoint(xData[i], fittedY);
                    fittedPlotData_.Points.Add(adcCountPair);
                }

                PlotModel.InvalidatePlot(true);
                logger_.Info($"Fitted plot updated");
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", $"Message:{ex.Message}\nStackTrace:{ex.StackTrace}", MessageType.Warning);
            }
        }

        public FitGaussianViewModel(PlotModel plotModel, List<int> rawData, ScatterSeries fittedPlotData)
        {
            PlotModel = plotModel ?? throw new ArgumentNullException(nameof(plotModel));
            rawData_ = rawData ?? throw new ArgumentNullException(nameof(rawData));
            fittedPlotData_ = fittedPlotData ?? throw new ArgumentNullException(nameof(fittedPlotData));
        }

        private void GetMaximumPairInfo(Dictionary<int, int> map)
        {
            var maxPair = map.Aggregate((x, y) => x.Value > y.Value ? x : y);
            MaxAt = maxPair.Key;
            MaxCount = maxPair.Value;
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

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();

        private List<int> rawData_;
        private ScatterSeries fittedPlotData_;
    }
}
