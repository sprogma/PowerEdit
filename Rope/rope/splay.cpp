#include "bits/stdc++.h"


using namespace std;


struct node
{
    int value;
    struct node *l, *r;
    struct node *p;
    size_t size;
};

void print_tree(size_t indent, node *x);

size_t size(node *n)
{
    return (n ? n->size : 0);
}

void upd_after_hang(node *n)
{
    if (n == NULL)
    {
        return;
    }
    if (n->l)
    {
        n->l->p = n;
    }
    if (n->r)
    {
        n->r->p = n;
    }
    n->size = 1 + size(n->l) + size(n->r);
    return;
}

node *new_node(int x)
{
    node *n = (node *)calloc(1, sizeof(*n));
    n->p = n->l = n->r = NULL;
    n->value = x;
    n->size = 1;
    return n;
}


node *zig(node *x, node *p, node *g)
{
    if (x->l == x->r && x->l != 0)
    {
        printf("ZAA %p\n", x);
        printf("%p %p\n", x->l, x->r);
        exit(1);
    }
    if (p->l == p->r && p->l != 0)
    {
        printf("ZBB %p\n", p);
        printf("%p %p\n", p->l, p->r);
        exit(2);
    }
    if (x == p->l)
    {
        node *a = x->l;
        node *b = x->r;
        node *c = p->r;
        x->p = g;
        p->p = x;

        x->l = a;
        x->r = p;
        p->l = b;
        p->r = c;
        
        /* do not change order of updates */
        upd_after_hang(p);
        upd_after_hang(x);
        upd_after_hang(g);
    }
    else
    {
        node *a = p->l;
        node *b = x->l;
        node *c = x->r;
        x->p = g;
        p->p = x;

        x->l = p;
        x->r = c;
        p->l = a;
        p->r = b;

        
        /* do not change order of updates */
        upd_after_hang(p);
        upd_after_hang(x);
        upd_after_hang(g);
    }
    
    if (g && g->r == p)
    {
        g->r = x;
    }
    
    if (g && g->l == p)
    {
        g->l = x;
    }
    
    if (x->l == x->r && x->l != 0)
    {
        printf("AA %p\n", x);
        printf("%p %p\n", x->l, x->r);
        exit(1);
    }
    if (p->l == p->r && p->l != 0)
    {
        printf("BB %p\n", p);
        printf("%p %p\n", p->l, p->r);
        exit(2);
    }
    return x;
}


node *zigzig(node *x, node *p, node *g)
{
    p = zig(p, g, g->p);
    x = zig(x, p, p->p);
    return x;
}

node *zigzag(node *x, node *p, node *g)
{
    x = zig(x, p, p->p);
    x = zig(x, g, g->p);
    return x;
}


node *splay(node *x)
{
    node *p = (x ? x->p : NULL);
    node *g = (p ? p->p : NULL);
    if (!g && !p && !x)
    {
        return NULL;
    }
    if (!g && !p && x)
    {
        return x;
    }
    // return zig(x, p, g);
    if (!g && p && x)
    {
        /* ZIG */
        return zig(x, p, g);
    }
    if ((p == g->l && x == p->l) ||
        (p == g->r && x == p->r))
    {
        x = zigzig(x, p, g);
    }
    else
    {
        x = zigzag(x, p, g);
    }
    return x;
}


node *find(node *p, size_t k)
{
    if (k == size(p->l))
    {
        while (p->p != NULL)
        {
            p = splay(p);
        }
        return p;
    }
    else if (k < size(p->l))
    {
        return find(p->l, k);
    }
    else
    {
        return find(p->r, k - (size(p->l) + 1));
    }
}


pair<node*, node*> split(node *p, size_t k)
{
    if (p == NULL)
    {
        return {NULL, NULL};
    }
    if (k > p->size)
    {
        return {p, NULL};
    }
    node *x = find(p, k);
    if (size(x->l) < k)
    {
        node *a = x;
        node *b = x->r;
        if (x->r)
        {
            x->r->p = NULL;
        }
        x->r = NULL;
        upd_after_hang(a);
        upd_after_hang(b);
        return {a, b};
    }
    else
    {
        node *a = x->l;
        node *b = x;
        if (x->l)
        {
            x->l->p = NULL;
        }
        x->l = NULL;
        upd_after_hang(a);
        upd_after_hang(b);
        return {a, b};
    }
}



node *merge(node *a, node *b)
{
    if (a == NULL)
    {
        return b;
    }
    if (b == NULL)
    {
        return a;
    }
    node *r = a;
    while (r->r)
    {
        r = r->r;
    }
    while (r->p != NULL)
    {
        r = splay(r);
    }
    r->r = b;
    b->p = r;
    upd_after_hang(r);
    return r;
}



node *insert(node *root, int id, int v)
{
    node *nn = new_node(v);
    auto res = split(root, id);
    // printf("||%p %p %p||\n", res.find, nn, res.second);
    node *a = merge(res.first, nn);
    node *b = merge(a, res.second);
    return b;
}


size_t print_treeq(node *x, size_t indent = 0, size_t depth = 0)
{
    if (x == NULL)
        return 0;
    // printf("\x1b[%zu;%zuH", depth, indent * 18);
    // printf("\x1b[%zu;%zuH", depth, (indent + size(x->l)) * 4);
    printf("\x1b[%zu;%zuH", depth, (indent + size(x->l)));
    // printf("%04llx<%04llx>%04llx[%2d]", (size_t)x->l & 0xFFFF, (size_t)x & 0xFFFF, (size_t)x->r & 0xFFFF, (int)x->size);
    // printf("[%2d]", (int)x->size);
    printf("X");
    printf("\n");
    size_t a = print_treeq(x->l, indent, depth + 1);
    size_t b = print_treeq(x->r, indent + size(x->l) + 1, depth + 1);
    return max(max(depth, a), b);
}


size_t dddddd = 0;


void print_tree(node *x)
{
    if (x == NULL)
    {
        printf("  null  \n");
        dddddd++;
        return;
    }
    dddddd = print_treeq(x, 0, dddddd) + 1;
    printf("\x1b[%zu;%zuH", dddddd, 0ull);
    printf("_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_");
    dddddd++;
}


#define N 1800

int main()
{
    system("cls");
    node *root = NULL;
    for (int i = 0; i < N; ++i)
    {
        root = insert(root, 0, i);
    }
    for (int i = 0; i < 6; ++i)
    {
        for (int ii = 0; ii < 30; ++ii)
        {
            int xx = rand() % N;
            root = find(root, xx);
        }
        print_tree(root);
    }
}
