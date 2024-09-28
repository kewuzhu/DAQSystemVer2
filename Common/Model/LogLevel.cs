namespace DAQSystem.Common.Model
{
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Fatal = 1,
        Error = 1 << 1,
        Warning = 1 << 2,
        Info = 1 << 3,
        Debug = 1 << 4,

        All = Fatal | Error | Warning | Info | Debug
    }
}
