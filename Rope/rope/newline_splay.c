#define __USE_MINGW_ANSI_STDIO 1
#include "stdio.h"

#include "malloc.h"
#include "string.h"
#include "assert.h"


#include "buffer.h"
#include "export.h"
#include "textblock.h"
#include "texttree.h"
#include "node_allocator.h"
#include "modification.h"


typedef struct node
{
    int value;
    int add;
    struct node *l, *r, *p;
    int64_t size;
} node;


struct newline_tree
{
    node *root;
    int size;
};


struct node_pair
{
    node *a, *b;
};


struct newline_tree *nltree_create()
{
    struct newline_tree *x = calloc(1, sizeof(*x));
    x->root = NULL;
    x->size = 0;
}

void free_tree(node *v)
{
    if (v == NULL)
    {
        return;
    }
    free_tree(v->l);
    free_tree(v->r);
    free(v);
}

void nltree_free(struct newline_tree *tree)
{
    free_tree(tree->root);
    free(tree);
}

inline __attribute__((always_inline))
int64_t size(const node *n)
{
    return (n ? n->size : 0);
}

void upd(node *n)
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

node *new_node(int64_t value)
{
    node *n = calloc(1, sizeof(*n));
    n->p = n->l = n->r = NULL;
    n->value = value;
    upd(n);
    return n;
}


void push(node *x)
{
    if (x && x->add != 0)
    {
        if (x->l)
        {
            x->l->add += x->add;
            x->l->value += x->add;
        }
        if (x->r)
        {
            x->r->add += x->add;
            x->r->value += x->add;
        }
        x->add = 0;
    }
}


node *zig(node *x, node *p, node *g)
{
    push(g);
    push(p);
    push(x);
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
        upd(p);
        upd(x);
        upd(g);
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
        upd(p);
        upd(x);
        upd(g);
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
    push(g);
    push(p);
    push(x);
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


node *find(node *p, int64_t k)
{
    push(p);
    if (k == size(p->l))
    {
        while (p->p != NULL)
        {
            push(p);
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


struct node_pair split(node *p, int64_t k)
{
    if (p == NULL)
    {
        return (struct node_pair){NULL, NULL};
    }
    if (k > p->size)
    {
        return (struct node_pair){p, NULL};
    }
    push(p);
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
        upd(a);
        upd(b);
        return (struct node_pair){a, b};
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
        upd(a);
        upd(b);
        return (struct node_pair){a, b};
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
    push(a);
    push(b);
    node *r = a;
    while (r->r)
    {
        r = r->r;
        push(r);
    }
    while (r->p != NULL)
    {
        r = splay(r);
    }
    r->r = b;
    b->p = r;
    upd(r);
    return r;
}



node *node_insert(node *root, int id, int v)
{
    node *nn = new_node(v);
    struct node_pair res = split(root, id);
    node *b = merge(merge(res.a, nn), res.b);
    return b;
}


node *node_remove(node *root, int id)
{
    struct node_pair res = split(root, id);
    struct node_pair res2 = split(res.b, 1);
    return merge(res.a, res2.b);
}

void nltree_insert(struct newline_tree *tree, int64_t pos, int64_t value)
{
    tree->root = node_insert(tree->root, pos, value);
    tree->size++;
}

void nltree_remove(struct newline_tree *tree, int64_t pos)
{
    tree->root = node_remove(tree->root, pos);
    tree->size--;
}

int64_t nltree_at(struct newline_tree *tree, int64_t pos)
{
    tree->root = find(tree->root, pos);
    return (tree->root ? tree->root->value : -1);
}

void nltree_update(struct newline_tree *tree, int64_t l, int64_t r, int64_t value)
{
    struct node_pair res = split(tree->root, l);
    struct node_pair res2 = split(res.b, r - l);   
    if (res2.a != NULL)
    {
        res2.a->add += value;
    }
    tree->root = merge(res.a, merge(res2.a, res2.b));
}


int64_t nltree_lowerbound(struct newline_tree *tree, int64_t value)
{
    tree->root = find(tree->root, value);
    return (tree->root ? size(tree->root->l) : 0);
}