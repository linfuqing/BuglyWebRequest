using System;
using System.Collections.Generic;

namespace ZeroGame.Networking
{
    internal sealed class UnityExceptionRecord
    {
        internal string Type, Message, Stack, Thread;
    }

    // No Unity/JNI calls: safe for Application.logMessageReceivedThreaded.
    internal sealed class UnityExceptionBuffer
    {
        private readonly object gate = new object();
        private readonly Queue<UnityExceptionRecord> pending = new Queue<UnityExceptionRecord>();
        private readonly Dictionary<Key, long> recent = new Dictionary<Key, long>();
        private int dropped;
        internal int DroppedCount { get { lock (gate) return dropped; } }
        private readonly int capacity;
        private readonly long duplicateWindowMs;
        private readonly List<Key> expired = new List<Key>();
        internal UnityExceptionBuffer(int capacity = 64, long duplicateWindowMs = 10000)
        {
            this.capacity = capacity; this.duplicateWindowMs = duplicateWindowMs;
        }
        internal bool Enqueue(string type, string message, string stack, string thread, long nowMs)
        {
            type = Limit(type, 256); message = Limit(message, 8192); stack = Limit(stack, 32768); thread = Limit(thread, 128);
            var key = new Key(type, message, stack);
            lock (gate)
            {
                long previous;
                if (recent.TryGetValue(key, out previous) && nowMs - previous < duplicateWindowMs) return false;
                if (pending.Count >= capacity) { dropped++; return false; }
                if (recent.Count >= capacity * 2)
                {
                    expired.Clear();
                    foreach (var entry in recent) if (nowMs - entry.Value >= duplicateWindowMs) expired.Add(entry.Key);
                    foreach (var entry in expired) recent.Remove(entry);
                    if (recent.Count >= capacity * 2) { dropped++; return false; }
                }
                recent[key] = nowMs;
                pending.Enqueue(new UnityExceptionRecord { Type = type, Message = message, Stack = stack, Thread = thread });
                return true;
            }
        }
        internal bool TryDequeue(out UnityExceptionRecord record)
        {
            lock (gate)
            {
                if (pending.Count == 0) { record = null; return false; }
                record = pending.Dequeue(); return true;
            }
        }
        internal void Clear() { lock (gate) { pending.Clear(); recent.Clear(); expired.Clear(); dropped = 0; } }
        private static string Limit(string text, int max) { return string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text.Substring(0, max); }
        private struct Key : IEquatable<Key>
        {
            private readonly string type, message, stack;
            internal Key(string type, string message, string stack) { this.type = type; this.message = message; this.stack = stack; }
            public bool Equals(Key other) { return type == other.type && message == other.message && stack == other.stack; }
            public override bool Equals(object value) { return value is Key && Equals((Key)value); }
            public override int GetHashCode() { unchecked { return ((type.GetHashCode() * 397) ^ message.GetHashCode()) * 397 ^ stack.GetHashCode(); } }
        }
    }
}
