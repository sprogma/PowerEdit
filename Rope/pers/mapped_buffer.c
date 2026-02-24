#include "mapped_buffer.h"

#include "stdlib.h"
#include "stdatomic.h"


struct mapped_buffer_real
{
    struct mapped_buffer;
    _Atomic int64_t links_count;
};

struct mapped_buffer *allocate_buffer(int64_t size)
{
    struct mapped_buffer_real *buf = malloc(sizeof(*buf));
    buf->buffer = malloc(size);
    buf->length = 0;
    buf->allocated = size;
    buf->links_count = 1;
    return (struct mapped_buffer *)buf;
}

void acquire_buffer(struct mapped_buffer *_buf)
{
    struct mapped_buffer_real *buf = (struct mapped_buffer_real *)_buf;
    buf->links_count++;
}
void release_buffer(struct mapped_buffer *_buf)
{
    struct mapped_buffer_real *buf = (struct mapped_buffer_real *)_buf;
    buf->links_count--;
    if (buf->links_count == 0)
    {
        free(buf->buffer);
        free(buf);
    }
}
