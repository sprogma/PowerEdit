using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Text;

namespace EditorFramework.Widgets
{
    public class SimlpeGameWindow : BaseWindow
    {
        public class ChunkedGrid<T> where T : struct
        {
            private const int ChunkSize = 16; // must be power of 2
            private const int ChunkShift = 4; // log2(ChunkSize)
            private const int ChunkMask = ChunkSize - 1;
            private const int CellsPerChunk = ChunkSize * ChunkSize;

            private readonly Dictionary<(long, long), T?[]> Chunks = [];

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
                            yield return (position.Item1 + x, position.Item2 + y, cell.Value);
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

        public readonly record struct Vector2Int(int X, int Y)
        {
            public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.X + b.X, a.Y + b.Y);
            public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.X - b.X, a.Y - b.Y);
        }


        private readonly ChunkedGrid<bool> Grid = new();
        private Vector2Int Position;
        private Vector2Int CameraCenter;

        public SimlpeGameWindow(IApplication app, ILayoutManager layout) : base(app, layout)
        {
            Position = new(0, 0);
        }

        public void ClearShadow(Vector2Int position, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius)
                    {
                        Grid[x, y] = Random.Shared.Next(2) == 0;
                    }
                }
            }
        }

        public void ProcessGridStep()
        {
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
                                count += Random.Shared.Next(2);
                            }
                        }
                    }
                }

                /* update cell */
                if (x.Value == true)
                {
                    Grid[x.X, x.Y] = count <= 3 && count >= 2;
                }
                else
                {
                    Grid[x.X, x.Y] = count == 3;
                }
            }
        }
    }
}
