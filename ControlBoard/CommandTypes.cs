namespace DAQSystem.DataAcquisition
{
    public enum CommandTypes
    {
        ResetAndStop,
        StartToCollect,
        SetCollectDuration,
        SetInitialThreshold,
        SetSignalSign,
        SetSignalBaseline,
        SetTimeInterval,
        SetGain
    }
}