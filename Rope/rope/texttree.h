#ifndef TEXTTREE
#define TEXTTREE

#include "stddef.h"
#include "inttypes.h"

#include "export.h"
#include "buffer.h"
#include "textblock.h"



enum
{
    /*
        if set, this node contains allocated data, 
        and it must be freed after any manipulation 
        with node
    */
    NodeValueAllocated = 0x1,
};



/*
    Node can use data from texttree buffer

    flags
        - flags of node
    global_time
        - global action time, used to order 
          actions between each other.
    node *l, *r, *p
        - pointers on childs and parent
    size
        - size of node with all subnodes, [in characters]
    value [len]
        - content of node, len - it's length.
*/
struct node
{
    uint64_t flags;
    uint64_t global_time;
    struct node *l, *r, *p;
    size_t size;
    char *value;
    size_t value_len;
};




enum
{
    /*
        if set, this texttree is replaced with another, 
        and now is readonly
    */
    TexttreeFreezed = 0x1,
};


/*
    flags
        - this tree flags
    actions [len, alloc] 
        - stored actions, from begin of rebuild, 
    root [len, alloc] 
        - saves all tree stages, after last 
          rebuild.
    root_saved_size 
        - count of modifications which will 
          be stored only in this texttree, or
          index of first modification, stored 
          into actions buffer, so, all other
          modifications will be stored insize
          next texttree.
    buffer [len] 
        - result of last rebuild - buffer, to which nodes
          can reference. 
    prev 
        - pointer on previous texttree, from whitch this
          tree fonds i't buffer.
*/
struct texttree
{
    uint64_t flags;
    
    struct modification *actions;
    size_t               actions_len;
    size_t               actions_alloc;
    
    struct node **root;
    size_t        root_len;
    size_t        root_alloc;
    size_t        root_saved_size;
    
    char  *buffer;
    size_t buffer_len;
    
    struct texttree *prev;
};



#endif
