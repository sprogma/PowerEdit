#include "mapped_buffer.h"

#include <windows.h>

#include "stdlib.h"
#include "stdatomic.h"


struct mapped_buffer_real
{
    struct mapped_buffer;
    _Atomic int64_t links_count;
    HANDLE file_handle;
    HANDLE mapping_handle;
};

struct mapped_buffer *allocate_buffer_from_file(const char *filename)
{
    struct mapped_buffer_real *buf = calloc(1, sizeof(*buf));
    if (!buf)
    {
        return NULL;
    }

    HANDLE hFile = CreateFileA(filename, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        free(buf);
        return NULL;
    }

    LARGE_INTEGER size;
    if (!GetFileSizeEx(hFile, &size))
    {
        CloseHandle(hFile);
        free(buf);
        return NULL;
    }

    HANDLE hMapping = CreateFileMapping(hFile, NULL, PAGE_READONLY, 0, 0, NULL);
    if (hMapping == NULL)
    {
        CloseHandle(hFile);
        free(buf);
        return NULL;
    }

    void *pBuffer = MapViewOfFile(hMapping, FILE_MAP_READ, 0, 0, 0);
    if (pBuffer == NULL)
    {
        CloseHandle(hMapping);
        CloseHandle(hFile);
        free(buf);
        return NULL;
    }

    buf->buffer = (char *)pBuffer;
    buf->length = size.QuadPart;
    buf->allocated = size.QuadPart;
    buf->links_count = 1;
    buf->file_handle = hFile;
    buf->mapping_handle = hMapping;

    return (struct mapped_buffer *)buf;
}

struct mapped_buffer *allocate_buffer(int64_t size)
{
    struct mapped_buffer_real *buf = calloc(1, sizeof(*buf));
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
