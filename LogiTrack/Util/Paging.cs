namespace LogiTrack.Util ;

    public static class Paging
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 100;
        // Lets list endpoints keep returning a plain JSON array while still telling clients the total
        public const string TotalCountHeader = "X-Total-Count";
    }
