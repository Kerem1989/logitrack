using Microsoft.Extensions.Primitives;

namespace LogiTrack.Util ;

    // Inventory is cached per page, so a write can't evict a single key. Every cached page is tied to the
    // current token instead, and Invalidate() expires them all at once.
    public sealed class InventoryCacheSignal
    {
        private CancellationTokenSource _source = new();

        public IChangeToken Token => new CancellationChangeToken(_source.Token);

        public void Invalidate() => Interlocked.Exchange(ref _source, new CancellationTokenSource()).Cancel();
    }
