namespace DAQSystem.Application.Model
{
    public class DAQConfiguration
    {
        public int CollectDuration { get; set; }

        public int InitialThreshold { get; set; }

        public int SignalSign { get; set; }

        public int SignalBaseline { get; set; }

        public int TimeInterval { get; set; }

        public int Gain { get; set; }
    }
}
