using EditorCore.Buffer;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using Logging;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Numerics;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class TreeWalkWindow: BaseWindow
    {
        public class Node
        {
            /* navigation fields */
            public Node? up;
            public Node? right;
            public Node? left;
            public Node? down;
            /* inner valus */
            public List<Node> childs;
            public List<Node> parents;

            public int depth;
            public IntPtr id;
            public string? name;
            public bool hidden;
            public string Label => name ?? id.ToString();
            public Vector2 position;
            public DateTime? Date;

            public Node(IntPtr id)
            {
                this.id = id;
                this.hidden = false;
                this.childs = [];
                this.parents = [];
            }

            public void AddChild(Node child)
            {
                childs.Add(child);
            }

            public void AddParent(Node parent)
            {
                parents.Add(parent);
            }
        }

        HashSet<Node> used = [];
        public Dictionary<IntPtr, Node> tree;
        public Node current;
        public List<Node> roots;
        public List<Node> initials;
        public IUndoTextBuffer cBuffer;
        public EditorBuffer buffer;
        public bool showNumbers = true;

        public float Scale = 30.0f;
        public float DestinationScale = 30.0f;
        public Vector2 Camera = new(){ X=0.0f, Y=0.0f };

        public TreeWalkWindow(IApplication app, ILayoutManager layout, EditorBuffer editBuffer, IUndoTextBuffer textBuffer) : base(app, layout)
        {
            this.buffer = editBuffer;
            this.cBuffer = textBuffer;
            (var states, var links) = this.cBuffer.GetVersionTree();
            this.tree = states.Select(x => new Node(x)).ToDictionary(x => x.id);
            foreach ((var parent, var child) in links)
            {
                this.tree[parent].AddChild(this.tree[child]);
                this.tree[child].AddParent(this.tree[parent]);
            }
            current = this.tree[this.cBuffer.GetCurrentVersion()];
            initials = this.cBuffer.GetInitialVersions().Select(x => this.tree[x]).ToList();
            roots = this.tree.Values.Where(x => x.parents.Count == 0).ToList();


            used.Clear();
            initials.ForEach(root => WalkTree(root, (Node x) => {
                foreach (Node child in x.childs)
                {
                    child.parents = child.parents.OrderBy(x => x != root).ToList();
                }
            }));

            CalculateGraphPositions();
        }

        void WalkTree(Node root, Action<Node> callback)
        {
            if (!used.Add(root))
            {
                return;
            }
            foreach (Node node in root.childs)
            {
                WalkTree(node, callback);
            }
            callback(root);
        }

        void SpreadDepth(Node root, int depth = 0)
        {
            if (!used.Add(root))
            {
                return;
            }
            root.depth = depth;
            foreach (Node node in root.childs)
            {
                SpreadDepth(node, depth + 1);
            }
        }

        void UpdateVisibleParent(Node node)
        {
            if (!used.Add(node))
            {
                return;
            }
            if (node.parents.Count == 0)
            {
                return;
            }
            if (node.up != null)
            {
                return;
            }
            UpdateVisibleParent(node.parents[0]);
            node.up = (node.parents[0].hidden ? node.parents[0].up : node.parents[0]);
        }

        /// <summary>
        /// Calculates nodes position and relatives, handling "hidden" field.
        /// </summary>
        public void CalculateGraphPositions()
        {
            /* clear current graph */
            foreach ((IntPtr id, Node node) in tree)
            {
                node.childs.Clear();
                node.up = node.down = node.left = node.right = null;
            }
            used.Clear();
            foreach ((IntPtr id, Node node) in tree.AsEnumerable())
            {
                UpdateVisibleParent(node);
            }
            /* calculate childs */
            foreach (Node node in tree.Values.Where(x => !x.hidden))
            {
                node.up?.childs.Add(node);
            }
            foreach (Node node in tree.Values.Where(x => !x.hidden))
            {
                node.childs.Sort((x, y) => x.id.CompareTo(y.id));
            }
            /* update parents [up] and depth */
            used.Clear();
            roots.ForEach(root => SpreadDepth(root));

            /* calculate node's positions */
            float currentX = 0.0f;
            for (int i = 0; i < roots.Count; i++)
            {
                roots[i].right = i + 1 < roots.Count ? roots[i + 1] : null;
                roots[i].left = i - 1 >= 0 ? roots[i - 1] : null;
            }
            used.Clear();
            roots.ForEach(root => WalkTree(root, (Node x) => {
                if (!x.hidden)
                {
                    x.position.Y = x.depth;  
                    if (x.childs.Count == 0)
                    {
                        x.position.X = currentX;
                        currentX++;
                    }
                    else
                    {
                        x.position.X = x.childs.Average(x => x.position.X);
                    }
                }
            }));

            /* calculate right, left and down: */
            foreach (Node node in tree.Values.Where(x => !x.hidden))
            {
                // add 0.01f to select rightmost from all
                node.down = node.childs.MinBy(x => Math.Abs(x.position.X - node.position.X - 0.01f));
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
                Camera = this.tree.Values.Where(x => !x.hidden).Select(x => x.position).Aggregate((x, y) => x + y)/this.tree.Count;
            }
        }

        public string? CurrentPreview()
        {
            return cBuffer.SubstringEx(current.id, 0, Math.Min(32*1024, cBuffer.LengthEx(current.id)));
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case TextInputEvent:
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.A):
                    if (DestinationScale < 10.0f)
                    {
                        DestinationScale = 30.0f;
                    }
                    else
                    {
                        DestinationScale = 5.0f;
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.C):
                    if (tree.Values.Any(x => x.hidden))
                    {
                        foreach (Node node in tree.Values)
                        {
                            node.hidden = false;
                        }
                    }
                    else
                    {
                        foreach (Node node in tree.Values)
                        {
                            node.hidden = (node.childs.Count == 1 && node.parents.Count != 0);
                        }
                    }
                    current.hidden = false;
                    CalculateGraphPositions();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Up):
                    current = current.up ?? current;
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Down):
                    current = current.down ?? current;
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Right):
                    current = current.right ?? current;
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Left):
                    current = current.left ?? current;
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Enter):
                    buffer.SetVersion(current.id);
                    DeleteSelf();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Escape):
                    DeleteSelf();
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}
