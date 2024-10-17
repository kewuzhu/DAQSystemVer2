using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.Utility;
using DAQSystem.Common.UI;
using MathNet.Numerics;
using NLog;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DAQSystem.Application.UI.Dialog
{
    internal partial class ChannelToEnergyViewModel : DialogViewModelBase
    {
        public event EventHandler DialogCloseRequested;

        public event EventHandler<LinearEquationParameters> LinearEquationParametersChanged;

        public ObservableCollection<ChannelEnergyPair> PairData { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
        private bool canCalculate = false;

        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
            DialogCloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanCalculate))]
        private void Calculate()
        {
            if (PairData.Count < 2)
            {
                logger_.Info("Not enuf points to fit a linear equation");
                return;
            }
                
            var channels = new List<double>();
            var energies = new List<double>();

            foreach (var d in PairData) 
            {
                logger_.Info($"Channel:{d.Channel} Energy:{d.Energy}");
                channels.Add(d.Channel);
                energies.Add(d.Energy);
            }

            var p = Fit.Line(channels.ToArray(), energies.ToArray());

            logger_.Info($"Coefficient:{p.B} Constant:{p.A}");
            LinearEquationParametersChanged?.Invoke(this, new LinearEquationParameters() { Coefficient = p.B, Constant = p.A });

            UserCommunication.ShowMessage($"{Theme.GetString(Strings.Notice)}",$"{Theme.GetString(Strings.ChannelToEnergy)}{Theme.GetString(Strings.Calculate)}{Theme.GetString(Strings.Success)}",MessageType.Info);
        }

        public ChannelToEnergyViewModel()
        {
            PairData.CollectionChanged += HandleRecordsCollectionChanged;
        }

        private void HandleRecordsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var collection = sender as ICollection<ChannelEnergyPair>;

            if (collection?.Count > 1)
                CanCalculate = true;
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();

        public partial class ChannelEnergyPair : ObservableObject
        {
            [ObservableProperty]
            private double channel;

            [ObservableProperty]
            private double energy;
        }
    }
}
