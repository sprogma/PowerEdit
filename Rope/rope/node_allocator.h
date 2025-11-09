#ifndef NODE_ALLOCATOR
#define NODE_ALLOCATOR


#include "stddef.h"
#include "inttypes.h"


/*
    free [len, alloc]
        - stores free allocated nodes
*/
struct node_allocator
{
    struct tt_node **free;
    size_t        free_len;
    size_t        free_alloc;
};



int node_allocator_free(struct node_allocator *a, struct tt_node *ptr);

int node_allocator_new(struct node_allocator *a, struct tt_node **result);

int node_allocator_destroy(struct node_allocator *a);

int node_allocator_init(struct node_allocator *a);



#endif


