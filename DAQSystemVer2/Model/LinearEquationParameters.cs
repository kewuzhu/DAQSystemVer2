using CommunityToolkit.Mvvm.ComponentModel;

namespace DAQSystem.Application.Model
{
    public partial class LinearEquationParameters : ObservableObject
    {
        [ObservableProperty]
        private double coefficient;

        [ObservableProperty]
        private double constant;
    }
}
