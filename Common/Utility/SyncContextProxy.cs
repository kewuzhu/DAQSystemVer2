namespace DAQSystem.Common.Utility
{
    public class SyncContextProxy
    {
        public SynchronizationContext SyncContext { get; }

        public SyncContextProxy() : this(SynchronizationContext.Current) { }

        public SyncContextProxy(SynchronizationContext syncContext) => SyncContext = syncContext;

        public void ExecuteInSyncContext(Action action)
        {
            if (SyncContext != null)
                SyncContext.Send(state => { action(); }, null);
            else
                action();
        }

        public void ExecuteInSyncContext<T>(Action<T> action, T actionParam)
        {
            if (SyncContext != null)
                SyncContext.Send(state => { action((T)state); }, actionParam);
            else
                action(actionParam);
        }

        public void PostToSyncContext(Action action)
        {
            if (SyncContext != null)
                SyncContext.Post(state => { action(); }, null);
            else
                action();
        }

        public void PostToSyncContext<T>(Action<T> action, T actionParam)
        {
            if (SyncContext != null)
                SyncContext.Post(state => { action((T)state); }, actionParam);
            else
                action(actionParam);
        }
    }
}
