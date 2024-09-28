using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DAQSystem.Common.Utility
{
    public class ObservableCollectionWithRangeSupport<T> : ObservableCollection<T>
    {
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            CheckReentrancy();

            var itemList = items.ToList();

            if (itemList.Count == 0)
                return;

            foreach (var item in itemList)
                Items.Add(item);

            OnCollectionChanged(itemList.Count > 1
                ? new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
                : new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemList[0]));
        }

        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            CheckReentrancy();

            var itemList = items.ToList();

            if (itemList.Count == 0)
                return;

            foreach (var item in itemList)
                Items.Remove(item);

            OnCollectionChanged(itemList.Count > 1
                ? new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
                : new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, itemList[0]));
        }

        public void UpdateCollection(IEnumerable<T> updatedContent)
        {
            var updateList = updatedContent.ToList();
            var deletedItems = this.Except(updateList).ToList();
            var newItems = updateList.Except(this).ToList();

            var changedItemCount = deletedItems.Count + newItems.Count;
            if (changedItemCount == 0)
                return;

            foreach (var item in deletedItems)
                Items.Remove(item);

            if (changedItemCount == 1 && deletedItems.Count == 1)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, deletedItems[0]));
                return;
            }

            foreach (var item in newItems)
                Items.Add(item);

            if (changedItemCount == 1 && newItems.Count == 1)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems[0]));
                return;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
