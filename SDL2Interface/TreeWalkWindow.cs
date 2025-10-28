using EditorCore.Buffer;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class TreeWalkWindow: BaseWindow
    {
        private class Node
        {
            /* navigation fields */
            public Node? up;
            public Node? right;
            public Node? left;
            public Node? down;
            public List<Node> childs;
            /* inner valus */
            public Node? parent;
            public int depth;
            public int id;
            public string? name;
            public bool hidden;
            public string Label => name ?? id.ToString();
            public Vector2 position;

            public Node(int id, Node? parent, string? name)
            {
                this.hidden = false;
                this.childs = new();
                this.id = id;
                this.parent = parent;
                this.depth = parent?.depth + 1 ?? 0;
                this.name = name;
            }
        }

        List<Node> tree;
        Node current;
        TextBuffer.TextBuffer buffer;
        public bool showNumbers = true;

        const float NodeHeight = 1.0f;
        const float NodeWidth = 2.0f;
        const float LineHeight = 1.3f;
        const float NodeStepWidth = 3.0f;
        const float MovingSmooth = 10.0f;

        float Scale = 30.0f;
        float DestinationScale = 30.0f;
        Vector2 Camera = new(){ X=0.0f, Y=0.0f };

        public TreeWalkWindow(TextBuffer.TextBuffer buffer, Rect position) : base(position)
        {
            this.buffer = buffer;
            this.tree = new();
            long[] parents = this.buffer.GetVersionTree();
            for (int i = 0; i < parents.Length; i++)
            {
                int p = (int)parents[i];
                this.tree.Add(new Node(i, (this.tree.Count > p ? this.tree[p] : null), null));
            }
            current = this.tree[(int)this.buffer.GetCurrentVersion()];

            CalculateGraphPositions();
        }

        void WalkTree(Node root, Action<Node> callback)
        {
            callback(root);
            foreach (Node node in root.childs)
            {
                WalkTree(node, callback);
            }
        }

        void UpdateVisibleParent(Node node)
        {
            if (node.parent == null)
            {
                node.depth = 0;
                return;
            }
            if (node.up != null)
            {
                return;
            }
            UpdateVisibleParent(node.parent);
            node.up = (node.parent.hidden == true ? node.parent.up : node.parent);
            node.depth = node.parent.depth + (node.parent.hidden ? 0 : 1);
        }

        /// <summary>
        /// Calculates nodes position and relatives, handling "hidden" field.
        /// </summary>
        public void CalculateGraphPositions()
        {
            /* clear current graph */
            foreach (Node node in tree)
            {
                node.childs.Clear();
                node.depth = 0;
                node.up = node.down = node.left = node.right = null;
            }
            /* update parents [up] and depth */
            foreach (Node node in tree.AsEnumerable())
            {
                UpdateVisibleParent(node);
            }
            /* calculate childs */
            foreach (Node node in tree.Where(x => !x.hidden).Reverse())
            {
                node.up?.childs.Add(node);
            }
            /* calculate node's positions */
            float currentX = 0.0f;
            WalkTree(tree[0], (Node x) => { 
                x.position.Y = x.depth * LineHeight;  
                if (x.childs.Count == 0)
                {
                    x.position.X = currentX;
                    currentX += NodeStepWidth;
                }
                else
                {
                    x.position.X = x.childs.Average(x => x.position.X);
                }
            });

            /* calculate right, left and down: */
            foreach (Node node in tree.Where(x => !x.hidden))
            {
                node.down = node.childs.MinBy(x => Math.Abs(x.position.X - node.position.X));
                if (node.up != null)
                {
                    int nodeId = node.up.childs.IndexOf(node);
                    node.right = (nodeId + 1 < node.up.childs.Count ? node.up.childs[nodeId + 1] : null);
                    node.left = (nodeId > 0 ? node.up.childs[nodeId - 1] : null);
                }
            }
            /* update camera */
            if (this.tree.Count != 0)
            {
                Camera = this.tree.Where(x => !x.hidden).Select(x => x.position).Aggregate((x, y) => x + y)/this.tree.Count;
            }
        }

        public override void DrawElements()
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderFillRect(renderer, ref position);

            /* draw tree, relative to current version */
            foreach (Node node in tree.Where(x => !x.hidden))
            {
                SDL.SetRenderDrawColor(renderer, 255, 0, 0, 0);
                Vector2 pos = (node.position - Camera) * Scale + new Vector2(W * 0.5f, H * 0.5f);
                float w, h;
                w = NodeWidth * Scale;
                h = NodeHeight * Scale;
                pos -= new Vector2(w, h);
                Rect rect = new() { X = (int)pos.X, Y = (int)pos.Y, Width = (int)w, Height = (int)h};
                if (node == current)
                {
                    SDL.RenderFillRect(renderer, ref rect);
                }
                else
                {
                    SDL.RenderDrawRect(renderer, ref rect);
                }
            }

            {
                float t = 1.0f / (MovingSmooth + 1.0f);
                Camera = Camera * (1.0f - t) + current.position * t;
                Scale = Scale * (1.0f - t) + DestinationScale * t;
            }
        }

        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.TextInput:
                    return false;
                case EventType.KeyDown:
                    if (e.Keyboard.Keysym.Scancode == Scancode.A)
                    {
                        if (DestinationScale < 10.0f)
                        {
                            DestinationScale = 30.0f;
                        }
                        else
                        {
                            DestinationScale = 5.0f;
                        }
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.C)
                    {
                        if (tree.Any(x => x.hidden))
                        {
                            foreach (Node node in tree)
                            {
                                node.hidden = false;
                            }
                        }
                        else
                        {
                            foreach (Node node in tree)
                            {
                                node.hidden = (node.childs.Count == 1 && node.parent != null);
                            }
                        }
                        current.hidden = false;
                        CalculateGraphPositions();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                    {
                        current = current.up ?? current;
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                    {
                        current = current.down ?? current;
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Right)
                    {
                        current = current.right ?? current;
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                    {
                        current = current.left ?? current;
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Return)
                    {
                        buffer.SetVersion(current.id);
                        DeleteSelf();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Escape)
                    {
                        DeleteSelf();
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
