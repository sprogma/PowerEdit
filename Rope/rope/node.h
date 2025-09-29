#ifndef NODE
#define NODE


#include "stddef.h"


struct NodeStatistics
{
    size_t nodeSize;
};


struct Leaf
{
    char *value;
};


struct Fork
{
    struct Node *l, *r;
};


union NodeType
{
    struct Leaf l;
    struct Fork f;
};


struct Node
{
    union NodeType;
};

#endif
