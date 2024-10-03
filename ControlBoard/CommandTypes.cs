namespace DAQSystem.DataAcquisition
{
    public enum CommandTypes
    {
        StopAndReset,
        StartToCollect,
        SetCollectDuration,
        SetInitialThreshold,
        SetSignalSign,
        SetSignalBaseline,
        SetTimeInterval,
        SetGain
    }
}