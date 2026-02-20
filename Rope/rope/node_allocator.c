#include "node_allocator.h"
#include "texttree.h"


#include "malloc.h"


int node_allocator_init(struct node_allocator *a)
{
    a->free = NULL;
    a->free_len = 0;
    a->free_alloc = 0;
    return 0;
}


int node_allocator_destroy(struct node_allocator *a)
{
    free(a->free);
    a->free = NULL;
    a->free_len = 0;
    a->free_alloc = 0;
    return 0;
}


int node_allocator_new(struct node_allocator *a, struct tt_node **result)
{
    struct tt_node *node = NULL;
    if (a->free_len)
    {
        node = a->free[--a->free_len];
    }
    else
    {
        node = malloc(sizeof(*node));
    }

    node->flags = 0;
    node->value = NULL;
    node->value_len = 1;
    node->size = 0;
    node->l = node->r = node->p = NULL;

    *result = node;
    return 0;
}

int node_allocator_free(struct node_allocator *a, struct tt_node *ptr)
{
    if (a->free_len == a->free_alloc)
    {
        a->free_alloc = 2 * a->free_alloc + !(a->free_alloc);
        void *res = realloc(a->free, sizeof(*a->free) * a->free_alloc);
        if (res != 0)
        {
            return 1;
        }
        a->free = res;
    }

    a->free[a->free_len++] = ptr;
    return 0;
}


