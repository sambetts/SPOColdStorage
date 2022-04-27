using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    /// <summary>
    /// Process a big list of objects in parallel, via configurable chunk sizes.
    /// </summary>
    /// <typeparam name="T">Type of list object</typeparam>
    public class ParallelListProcessor<T>
    {
        private readonly int _maxItemsPerChunk;

        public ParallelListProcessor(int maxItemsPerChunk)
        {
            if (maxItemsPerChunk < 1)
            {
                throw new ArgumentException(nameof(maxItemsPerChunk));
            }
            this._maxItemsPerChunk = maxItemsPerChunk;
        }

        /// <summary>
        /// From a complete list, load in parallel chunks. Blocks until all tasks are complete.
        /// </summary>
        /// <param name="processListChunkDelegate">Function delegate for processing a chunk of all items + thread index. Must return Task</param>
        public async Task ProcessListInParallel(IEnumerable<T> allItems, Func<List<T>, int, Task> processListChunkDelegate)
        {
            await ProcessListInParallel(allItems, processListChunkDelegate, null);
        }

        /// <summary>
        /// From a complete list, load in parallel chunks. Blocks until all tasks are complete.
        /// </summary>
        /// <param name="processListChunkDelegate">Function delegate for processing a chunk of all items + thread index. Must return Task</param>
        public async Task ProcessListInParallel(IEnumerable<T> allItems, Func<List<T>, int, Task> processListChunkDelegate, Action<int>? startingDelegate)
        {
            if (allItems is null)
            {
                throw new ArgumentNullException(nameof(allItems));
            }

            if (processListChunkDelegate is null)
            {
                throw new ArgumentNullException(nameof(processListChunkDelegate));
            }


            // Figure out how many threads we'll need
            int rem = 0;
            var threadsNeeded = Math.DivRem(allItems.Count(), _maxItemsPerChunk, out rem);
            threadsNeeded = (threadsNeeded) == 0 ? 1 : threadsNeeded;
            if (rem > 0)
            {
                threadsNeeded++;        // Make sure the last thread doesn't include diving remainder
            }

            var tasks = new List<Task>();
            var recordsInsertedAlready = 0;
            if (startingDelegate != null)
            {
                startingDelegate(threadsNeeded);
            }

            for (int threadIndex = 0; threadIndex < threadsNeeded; threadIndex++)
            {
                // Figure out next threaded chunk
                var recordsToTake = _maxItemsPerChunk;
                if (threadIndex == threadsNeeded - 1)
                {
                    recordsToTake = allItems.Count() - recordsInsertedAlready;
                }

                // Split unique work for new thread
                var threadListChunk = allItems.Skip(recordsInsertedAlready).Take(recordsToTake).ToList();
                recordsInsertedAlready += recordsToTake;

                // Load chunk via delegate
                tasks.Add(processListChunkDelegate(threadListChunk, threadIndex));
            }

            // Block for all threads
            await Task.WhenAll(tasks);
        }
    }
}
