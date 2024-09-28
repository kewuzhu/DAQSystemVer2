using CommunityToolkit.Mvvm.ComponentModel;

namespace DAQSystem.Common.UI
{
    public partial class DialogViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool? dialogResult;

        [ObservableProperty]
        private bool isActive;
    }
}
