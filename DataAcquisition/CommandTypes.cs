namespace DAQSystem.DataAcquisition
{
    public enum CommandTypes : short
    {
        StopAndReset = 0x0000,
        StartToCollect = 0x0001,
        SetCollectDuration = 0x0002,
        SetInitialThreshold = 0x1110,
        SetSignalSign = 0x1112,
        SetSignalBaseline = 0x1113,
        SetTimeInterval = 0x1115,
        SetGain = 0x1116
    }
}