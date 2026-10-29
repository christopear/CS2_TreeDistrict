using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace TreeDistrict
{
    // Main-thread spans only. Scheduling cost and waits are measured where they occur;
    // asynchronous job execution is NOT charged back to its producer. Nested spans overlap.
    internal static partial class NativePlacementTimings
    {
        private sealed class Span { internal double Start, Sum, Max; internal int Count; }
        private static readonly Dictionary<string, Span> Spans = new Dictionary<string, Span>();
        private static string run;
        private static int afterBatch;
        private static double stopAt;
        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        internal static void Begin(string id)
        {
            Stop(); run = id; afterBatch = 0; stopAt = 0;
            Mod.Log.Info("NativeTiming begin run=" + id + "; inclusive main-thread spans; nested totals overlap; jobs not force-completed.");
        }
        internal static void Start(string name)
        {
            if (run == null) return;
            if (!Spans.TryGetValue(name, out var span)) Spans.Add(name, span = new Span());
            span.Start = Now;
        }
        internal static void End(string name)
        {
            if (run == null || !Spans.TryGetValue(name, out var span) || span.Start == 0) return;
            double ms = (Now - span.Start) * 1000;
            span.Sum += ms; span.Max = Math.Max(span.Max, ms); span.Count++; span.Start = 0;
        }
        internal static void BeforeBatch(int batch)
        {
            Flush("before-next-batch"); afterBatch = batch;
        }
        internal static void Finish() { if (run != null) stopAt = Now + 3; }
        internal static void Tick() { if (run != null && stopAt > 0 && Now >= stopAt) Stop(); }
        internal static void Stop() { Flush("tail-or-stop"); run = null; stopAt = 0; Spans.Clear(); }
        private static void Flush(string reason)
        {
            if (run == null) return;
            foreach (var pair in Spans.OrderByDescending(p => p.Value.Sum))
            {
                var span = pair.Value;
                if (span.Count > 0)
                    Mod.Log.Info(string.Format(CultureInfo.InvariantCulture,
                        "NativeTiming run={0} afterBatch={1} reason={2} system={3} calls={4} sumMs={5:F3} maxMs={6:F3}",
                        run, afterBatch, reason, pair.Key, span.Count, span.Sum, span.Max));
            }
            Spans.Clear();
        }
    }
}
