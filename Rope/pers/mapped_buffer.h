#ifndef MAPPED_BUFFER_H
#define MAPPED_BUFFER_H


#include "inttypes.h"


struct mapped_buffer
{
    char *buffer;
    int64_t length;
    int64_t allocated;
};


struct mapped_buffer *allocate_buffer(int64_t size);
void acquire_buffer(struct mapped_buffer *);
void release_buffer(struct mapped_buffer *);

#endif
