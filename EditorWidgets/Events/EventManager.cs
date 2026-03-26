using System.Collections.Concurrent;

namespace EditorFramework.Events
{
    public class EventManager
    {
        internal readonly Func<EventBase, bool> Handler;
        private readonly int maxChordLength;
        internal int MaxChordLength;
        internal double ChordTimeout;
        internal double MouseClickTreshold;
        internal List<KeyEvent> Chord = [];
        internal List<MouseClickEvent> ButtonSequence = [];
        internal ConcurrentQueue<EventBase> Queue = [];

        public EventManager(Func<EventBase, bool> handler, int maxChordLength = 32, double chordTimeout = 1000, double mouseClickTimeout = 200)
        {
            this.maxChordLength = maxChordLength;
            Handler = handler;
            MaxChordLength = maxChordLength;
            ChordTimeout = chordTimeout;
            MouseClickTreshold = mouseClickTimeout;
        }

        public void AddEvent(EventBase item)
        {
            Queue.Enqueue(item);
        }

        public void ProcessEvents()
        {
            while (Queue.TryDequeue(out var item))
            {
                // handle raw event
                if (!Handler(item))
                {
                    continue; // don't add event to chords if it was handled
                }

                // add to chord
                if (item is KeyEvent key)
                {
                    if (Chord.Count > 0 && (key.Timestamp - Chord[^1].Timestamp).TotalMilliseconds > ChordTimeout)
                    {
                        Chord.Clear();
                    }
                    Chord.Add(key);
                    while (Chord.Count > maxChordLength)
                    {
                        Chord.RemoveAt(0);
                    }
                    if (!Handler(new KeyChordEvent(Chord.ToArray())))
                    {
                        Chord.Clear();
                    }
                }

                // add to button seq
                if (item is MouseClickEvent click)
                {
                    if (ButtonSequence.Count > 0 && 
                        (click.Timestamp - ButtonSequence[^1].Timestamp).TotalMilliseconds > MouseClickTreshold)
                    {
                        ButtonSequence.Clear();
                    }
                    ButtonSequence.Add(click);
                    while (ButtonSequence.Count > maxChordLength)
                    {
                        ButtonSequence.RemoveAt(0);
                    }
                    if (!Handler(new MouseSequenceEvent(ButtonSequence.ToArray())))
                    {
                        ButtonSequence.Clear();
                    }
                }
            }
        }
    }
}
