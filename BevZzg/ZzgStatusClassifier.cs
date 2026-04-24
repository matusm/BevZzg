namespace Bev.Zzg
{
    public class ZzgStatusClassifier
    {
        private int nSync;
        private int nAsync;
        private int nSysTimeChanged;
        private int nNoResponse;
        private int nLoop;

        public BevZzgStatus Status { get; }

        public ZzgStatusClassifier(int n1, int n2, int n3, int n4, int max)
        {
            nSync = n1;
            nAsync = n2;
            nSysTimeChanged = n3;
            nNoResponse = n4;
            nLoop = max;
            Status = Classifier();
        }

        private BevZzgStatus Classifier()
        {
            // if only no response -> NoResponse
            if (nNoResponse == nLoop) return BevZzgStatus.NoResponse; 

            // no Syncs and no Asyncs -> we don't know anything
            if (nSync == 0 && nAsync == 0) return BevZzgStatus.Unspecified;

            // If no sync but at least one Async -> TimeAsync
            if (nSync == 0 && nAsync > 0) return BevZzgStatus.TimeAsync;

            // check if we have more Syncs than Asyncs
            if (nSync >= nAsync) return BevZzgStatus.Synchron;

            // check if we have more ASyncs than Syncs ???
            if (nSync < nAsync) return BevZzgStatus.Unspecified;

            return BevZzgStatus.Unspecified;
        }

        public override string ToString() => $"[ZzgStatusClassifier: Status={Status}]";
    }
}

