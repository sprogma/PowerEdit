using Common;
using EditorCore.Buffer;
using EditorCore.Selection;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using Humanizer;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Text;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class SimpleGameWindow : BaseWindow
    {
        public class ChunkedGrid<T> where T : struct
        {
            private const int ChunkSize = 16; // must be power of 2
            private const int ChunkShift = 4; // log2(ChunkSize)
            private const int ChunkMask = ChunkSize - 1;
            private const int CellsPerChunk = ChunkSize * ChunkSize;

            private readonly Dictionary<(long, long), T?[]> Chunks = [];

            public void Clear()
            {
                Chunks.Clear();
            }

            public T? this[long worldX, long worldY]
            {
                get
                {
                    long cx = worldX >> ChunkShift;
                    long cy = worldY >> ChunkShift;

                    if (Chunks.TryGetValue((cx, cy), out T?[]? chunk))
                    {
                        int lx = (int)(worldX & ChunkMask);
                        int ly = (int)(worldY & ChunkMask);
                        return chunk[ly * ChunkSize + lx];
                    }
                    return default;
                }
                set
                {
                    long cx = worldX >> ChunkShift;
                    long cy = worldY >> ChunkShift;

                    int lx = (int)(worldX & ChunkMask);
                    int ly = (int)(worldY & ChunkMask);
                    int arrayIndex = ly * ChunkSize + lx;

                    bool hasChunk = Chunks.TryGetValue((cx, cy), out T?[]? chunk);

                    if (value is null)
                    {
                        if (hasChunk)
                        {
                            chunk![arrayIndex] = default;
                        }
                    }
                    else
                    {
                        if (!hasChunk)
                        {
                            chunk = new T?[CellsPerChunk];
                            Chunks[(cx, cy)] = chunk;
                        }
                        chunk![arrayIndex] = value;
                    }
                }
            }
            public ref T? GetRef(long worldX, long worldY)
            {
                long cx = worldX >> ChunkShift;
                long cy = worldY >> ChunkShift;

                if (!Chunks.TryGetValue((cx, cy), out var chunk))
                {
                    throw new Exception("Invalid chunk");
                }

                int idx = (int)(worldY & ChunkMask) * ChunkSize + (int)(worldX & ChunkMask);
                if (chunk[idx] is null)
                {
                    throw new NullReferenceException();
                }
                return ref chunk[idx];
            }


            public IEnumerable<(long X, long Y, T Value)> GetAllCells()
            {
                foreach ((var position, T?[] chunk) in Chunks)
                {
                    long x = 0, y = 0;
                    for (int i = 0; i < CellsPerChunk; i++)
                    {
                        T? cell = chunk[i];
                        if (cell is not null)
                        {
                            yield return (position.Item1 * ChunkSize + x, position.Item2 * ChunkSize + y, cell.Value);
                        }
                        if (++x == ChunkSize) { x = 0; y++; }
                    }
                }
            }

            public IEnumerable<(long X, long Y, T? Value)> GetChunkedCells(long centerX, long centerY, int radius, bool isManhattan = false)
            {
                long minX = centerX - radius;
                long maxX = centerX + radius;
                long minY = centerY - radius;
                long maxY = centerY + radius;

                long minChunkX = minX >> ChunkShift;
                long maxChunkX = maxX >> ChunkShift;
                long minChunkY = minY >> ChunkShift;
                long maxChunkY = maxY >> ChunkShift;

                for (long cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    for (long cy = minChunkY; cy <= maxChunkY; cy++)
                    {
                        if (isManhattan)
                        {
                            if (Math.Abs(cx - (centerX >> ChunkShift)) + Math.Abs(cy - (centerY >> ChunkShift)) > (radius >> ChunkShift) + 2)
                            {
                                continue;
                            }
                        }
                        if (Chunks.TryGetValue((cx, cy), out T?[]? chunk))
                        {
                            long x = 0, y = 0;
                            for (int i = 0; i < CellsPerChunk; i++)
                            {
                                T? cell = chunk[i];
                                if (cell is not null)
                                {
                                    yield return (cx + x, cy + y, cell);
                                }
                                if (++x == ChunkSize) { x = 0; y++; }
                            }
                        }
                    }
                }
            }
        }

        public struct Vector2Int
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Vector2Int(int x, int y)
            {
                X = x;
                Y = y;
            }

            public static bool operator ==(Vector2Int a, Vector2Int b) => a.X == b.X && a.Y == b.Y;
            public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
            public override bool Equals(object? obj) => obj is Vector2Int other && Equals(other);
            public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;
            public override int GetHashCode() => HashCode.Combine(X, Y);
            public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.X + b.X, a.Y + b.Y);
            public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.X - b.X, a.Y - b.Y);
        }



        public ChunkedGrid<bool> Grid = new();
        public ChunkedGrid<bool> NewGrid = new();
        public Vector2Int Position;
        public Vector2Int nextPosition;
        private Task? directionKeyPressTask = null;
        public Lock GameLock = new();
        private long GridGeneration = 0;
        private long GridSeed = Random.Shared.NextInt64();

        public long Score = 0;

        public enum GameResultType
        {
            Playing,
            Loose,
            Win,
        }

        public enum GameHardnessType
        {
            Effortless,
            Beginner,
            Medium,
            Challenging,
            Hard,
            Nightmare,
            LifeMaster,
        }

        public enum MovingType
        {
            King,
            Diagonal,
            Neibours,
            Jump2,
        }

        public MovingType Moving = MovingType.King;

        public GameResultType GameResult = GameResultType.Playing;

        public Dictionary<(long, long), long> Towers = [];

        public GameHardnessType GameHardness = GameHardnessType.Nightmare;

        public long? MaxMovingTimeout => GameHardness switch
        {
            GameHardnessType.Effortless => null,
            GameHardnessType.Beginner => 10,
            GameHardnessType.Medium => 5,
            GameHardnessType.Challenging => 5,
            GameHardnessType.Hard => 5,
            GameHardnessType.Nightmare => 3,
            GameHardnessType.LifeMaster => 3,
            _ => 0,
        };

        public long? MovingTimeout = null;

        public int? ViewRadius => GameHardness switch
        {
            GameHardnessType.Effortless => null,
            GameHardnessType.Beginner => null,
            GameHardnessType.Medium => null,
            GameHardnessType.Challenging => 10,
            GameHardnessType.Hard => 7,
            GameHardnessType.Nightmare => 5,
            GameHardnessType.LifeMaster => 5,
            _ => null,
        };
        
        public int ShadowClearRadius => GameHardness switch
        {
            GameHardnessType.Effortless => 5,
            GameHardnessType.Beginner => 4,
            GameHardnessType.Medium => 3,
            GameHardnessType.Challenging => 3,
            GameHardnessType.Hard => 2,
            GameHardnessType.Nightmare => 0,
            GameHardnessType.LifeMaster => 0,
            _ => 0,
        };

        public bool ShowPredictions => GameHardness <= GameHardnessType.Nightmare;

        public SimpleGameWindow(IApplication app, ILayoutManager layout) : base(app, layout)
        {
            Position = new(0, 0);
            nextPosition = Position;

            // clear starting circle
            ClearShadow(Position, ShadowClearRadius + 5, true);
            MovingTimeout = MaxMovingTimeout;
        }


        async Task KeyMovePressAsync()
        {
            await Task.Delay(100); // 100ms to second key press
            lock (GameLock)
            {
                if (nextPosition != Position)
                {
                    GameStep(nextPosition);
                }
                directionKeyPressTask = null;
            }
        }

        async Task KeyMoveClearAsync()
        {
            await Task.Delay(100); // 100ms to second key press
            lock (GameLock)
            {
                nextPosition = Position;
                directionKeyPressTask = null;
            }
        }

        public override bool HandleEvent(EventBase e)
        {

            if (GameResult == GameResultType.Playing)
            {
                switch (e)
                {
                    case QuitEvent:
                        Environment.Exit(1);
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.Space):
                        lock (GameLock)
                        {
                            GameShoot();
                        }
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.D1):
                        lock (GameLock)
                        {
                            if (MaxMovingTimeout == null)
                            {
                                Moving = (MovingType)0;
                            }
                        }
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.D2):
                        lock (GameLock)
                        {
                            if (MaxMovingTimeout == null)
                            {
                                Moving = (MovingType)1;
                            }
                        }
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.D3):
                        lock (GameLock)
                        {
                            if (MaxMovingTimeout == null)
                            {
                                Moving = (MovingType)2;
                            }
                        }
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.D4):
                        lock (GameLock)
                        {
                            if (MaxMovingTimeout == null)
                            {
                                Moving = (MovingType)3;
                            }
                        }
                        return false;

                }
                if (Moving == MovingType.King)
                {
                    switch (e)
                    {
                        case KeyChordEvent key when key.Is(KeyCode.Left):
                            lock (GameLock)
                            {
                                if (nextPosition.X != Position.X)
                                {
                                    GameStep(nextPosition);
                                }
                                nextPosition.X--;
                                directionKeyPressTask ??= Task.Run(KeyMovePressAsync);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Right):
                            lock (GameLock)
                            {
                                if (nextPosition.X != Position.X)
                                {
                                    GameStep(nextPosition);
                                }
                                nextPosition.X++;
                                directionKeyPressTask ??= Task.Run(KeyMovePressAsync);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Up):
                            lock (GameLock)
                            {
                                if (nextPosition.Y != Position.Y)
                                {
                                    GameStep(nextPosition);
                                }
                                nextPosition.Y--;
                                directionKeyPressTask ??= Task.Run(KeyMovePressAsync);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Down):
                            lock (GameLock)
                            {
                                if (nextPosition.Y != Position.Y)
                                {
                                    GameStep(nextPosition);
                                }
                                nextPosition.Y++;
                                directionKeyPressTask ??= Task.Run(KeyMovePressAsync);
                            }
                            return false;
                    }
                }
                else if (Moving == MovingType.Neibours)
                {
                    switch (e)
                    {
                        case KeyChordEvent key when key.Is(KeyCode.Left):
                            lock (GameLock)
                            {
                                nextPosition.X--;
                                GameStep(nextPosition);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Right):
                            lock (GameLock)
                            {
                                nextPosition.X++;
                                GameStep(nextPosition);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Up):
                            lock (GameLock)
                            {
                                nextPosition.Y--;
                                GameStep(nextPosition);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Down):
                            lock (GameLock)
                            {
                                nextPosition.Y++;
                                GameStep(nextPosition);
                            }
                            return false;
                    }
                }
                else if (Moving == MovingType.Diagonal)
                {
                    lock (GameLock)
                    {
                        switch (e)
                        {
                            case KeyChordEvent key when key.Is(KeyCode.Left) && nextPosition.X == Position.X:
                                nextPosition.X--;
                                break;
                            case KeyChordEvent key when key.Is(KeyCode.Right) && nextPosition.X == Position.X:
                                nextPosition.X++;
                                break;
                            case KeyChordEvent key when key.Is(KeyCode.Up) && nextPosition.Y == Position.Y:
                                nextPosition.Y--;
                                break;
                            case KeyChordEvent key when key.Is(KeyCode.Down) && nextPosition.Y == Position.Y:
                                nextPosition.Y++;
                                break;
                        }
                        if (nextPosition.X != Position.X && nextPosition.Y != Position.Y)
                        {
                            GameStep(nextPosition);
                        }
                        else
                        {
                            directionKeyPressTask ??= Task.Run(KeyMoveClearAsync);
                        }
                        return false;
                    }
                }
                else if (Moving == MovingType.Jump2)
                {
                    switch (e)
                    {
                        case KeyChordEvent key when key.Is(KeyCode.Left):
                            lock (GameLock)
                            {
                                nextPosition.X -= 2;
                                GameStep(nextPosition);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Right):
                            lock (GameLock)
                            {
                                nextPosition.X += 2;
                                GameStep(nextPosition);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Up):
                            lock (GameLock)
                            {
                                nextPosition.Y -= 2;
                                GameStep(nextPosition);
                            }
                            return false;
                        case KeyChordEvent key when key.Is(KeyCode.Down):
                            lock (GameLock)
                            {
                                nextPosition.Y += 2;
                                GameStep(nextPosition);
                            }
                            return false;
                    }
                }
            }
            else if (GameResult == GameResultType.Win)
            {
                switch (e)
                {
                    case QuitEvent:
                        Environment.Exit(1);
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.Space):
                        MusicCts.Cancel();
                        DeleteSelf();
                        return false;
                }
            }
            else if (GameResult == GameResultType.Loose)
            {
                switch (e)
                {
                    case QuitEvent:
                        Environment.Exit(1);
                        return false;
                    case KeyChordEvent key when key.Is(KeyCode.Space):
                        MusicCts.Cancel();
                        DeleteSelf();
                        return false;
                }
            }
            return true;
        }

        public bool CanMoveTo(long x, long y)
        {
            switch (Moving)
            {
                case MovingType.King:
                    return Math.Max(Math.Abs(x - Position.X), Math.Abs(y - Position.Y)) == 1;
                case MovingType.Neibours:
                    return Math.Abs(x - Position.X) + Math.Abs(y - Position.Y) == 1;
                case MovingType.Diagonal:
                    return Math.Max(Math.Abs(x - Position.X), Math.Abs(y - Position.Y)) == 1 && Math.Abs(x - Position.X) + Math.Abs(y - Position.Y) != 1;
                case MovingType.Jump2:
                    return (Math.Abs(x - Position.X) == 2 && Math.Abs(y - Position.Y) == 0) ||
                           (Math.Abs(x - Position.X) == 0 && Math.Abs(y - Position.Y) == 2);
            }
            return false;
        }

        public void MoveTo(Vector2Int newPosition)
        {
            if (newPosition == Position) return;
            Position = newPosition;
            nextPosition = Position;

            // check, if we need change move type
            if (MaxMovingTimeout != null)
            {
                MovingTimeout--;
                if (MovingTimeout < 0)
                {
                    MovingTimeout = MaxMovingTimeout;
                    Moving = (MovingType)Random.Shared.Next(4);
                }
            }

            CheckLive();
            if (GameResult == GameResultType.Playing)
            {
                Score += ClearShadow(Position, ShadowClearRadius);
                for (long dx = Position.X - 3; dx <= Position.X + 3; ++dx)
                {
                    for (long dy = Position.Y - 3; dy <= Position.Y + 3; ++dy)
                    {
                        if (CanMoveTo(dx, dy))
                        {
                            if (Grid[dx, dy] == null)
                            {
                                Grid[dx, dy] = ShadowValue(dx, dy);
                                Score++;
                            }
                        }
                    }
                }
            }
        }

        public void CheckLive()
        {
            if (Grid[Position.X, Position.Y] == true)
            {
                // loose
                GameResult = GameResultType.Loose;
            }
        }

        public void GameStep(Vector2Int newPosition)
        {
            // move to new position
            MoveTo(newPosition);
            // move grid
            ProcessGridStep();
            CheckLive();
        }

        public void GameShoot()
        {
            // swap data
            for (long dx = Position.X - 3; dx <= Position.X + 3; ++dx)
            {
                for (long dy = Position.Y - 3; dy <= Position.Y + 3; ++dy)
                {
                    if (CanMoveTo(dx, dy))
                    {
                        Grid[dx, dy] = Grid[dx, dy] switch
                        {
                            true => false,
                            false => true,
                            null => null,
                        };
                    }
                }
            }
            GameStep(Position);
        }

        public long ClearShadow(Vector2Int position, int radius, bool fillZero = false)
        {
            long res = 0;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        if (Grid[position.X + x, position.Y + y] == null)
                        {
                            Grid[position.X + x, position.Y + y] = !fillZero && ShadowValue(position.X + x, position.Y + y);
                            res++;
                        }
                    }
                }
            }
            return res;
        }

        public bool ShadowValue(long x, long y)
        {
            unchecked
            {
                ulong h = (ulong)(x * 73856093L ^ y * 19349663L ^ GridGeneration * 1249182941L ^ GridSeed * 74127419L);
                h ^= h >> 33;
                h *= 0xff51afd7ed558ccdL;
                h ^= h >> 33;
                return (int)(h & 1024) < 384; // ~37.5%
            }
        }

        public void ProcessGridStep()
        {
            if (GameResult != GameResultType.Playing) return;

            GridGeneration++;

            NewGrid.Clear();

            foreach (var x in Grid.GetAllCells())
            {
                /* check all neibours */
                long count = 0;
                for (int dx = -1; dx <= 1; ++dx)
                {
                    for (int dy = -1; dy <= 1; ++dy)
                    {
                        if (dx != 0 || dy != 0)
                        {
                            var cell = Grid[x.X + dx, x.Y + dy];
                            if (cell is not null)
                            {
                                count += cell.Value ? 1 : 0; 
                            }
                            else
                            {
                                count += ShadowValue(x.X + dx, x.Y + dy) ? 1 : 0;
                            }
                        }
                    }
                }

                /* update cell */
                if (x.Value == true)
                {
                    NewGrid[x.X, x.Y] = count <= 3 && count >= 2;
                }
                else
                {
                    NewGrid[x.X, x.Y] = count == 3;
                }
            }

            (Grid, NewGrid) = (NewGrid, Grid);
        }
    }
}
