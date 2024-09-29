using CommunityToolkit.Mvvm.ComponentModel;
using DAQSystem.Common.Model;
using OxyPlot;

namespace DAQSystem.Application.UI
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private PlotModel plotModel;

        public MainWindowViewModel()
        {
            InitializePlot();
        }

        private void InitializePlot() 
        {
            PlotModel = new PlotModel();

            var xAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "ADC Channel",
                StringFormat = "0"
            };

            var yAxis = new OxyPlot.Axes.LinearAxis
            {
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
    }
}
