namespace DAQSystem.Common.Utility
{
    public class SyncContextAwareObject
    {
        public SynchronizationContext SyncContext => syncContextProxy_.SyncContext;

        public SyncContextAwareObject() : this(SynchronizationContext.Current) { }

        public SyncContextAwareObject(SynchronizationContext syncContext) =>
            syncContextProxy_ = new SyncContextProxy(syncContext);

        public void ExecuteInSyncContext(Action action) =>
            syncContextProxy_.ExecuteInSyncContext(action);

        public void PostToSyncContext(Action action) =>
            syncContextProxy_.PostToSyncContext(action);

        private readonly SyncContextProxy syncContextProxy_;
    }
}
