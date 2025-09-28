#ifndef ROPE
#define ROPE


enum node_flags
{
    
};


struct node_t
{
    uint64_t flags;
    char *string;
    size_t string_size;
    struct rope_t *l, *r;
};


struct node_allocator_t
{
    
};


struct rope_t
{
    struct node_allocator_t *node_allocator;
    
    struct node_t *roots;
    size_t         roots_len;
    size_t         roots_alloc;
};


#endif

