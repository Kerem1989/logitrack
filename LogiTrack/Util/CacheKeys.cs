namespace LogiTrack.Util ;

    // Shared so every endpoint that changes inventory can evict the same entry
    public static class CacheKeys
    {
        public const string AllInventory = "inventory:all";
    }
