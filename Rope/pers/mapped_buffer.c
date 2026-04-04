#include "structure.h"
#include "text_api.h"
#include "mapped_buffer.h"

#include <windows.h>

#include <io.h>
#include <fcntl.h>
#include <sys/types.h>
#include <sys/stat.h>
#include "assert.h"
#include "stdlib.h"
#include "stdlib.h"
#include "stdatomic.h"


struct mapped_buffer_real
{
    struct mapped_buffer;
    _Atomic int64_t links_count;
    HANDLE file_handle;
    HANDLE mapping_handle;
};

int _dump_nodes_recurse(struct segment *seg, FILE *file)
{
    if (seg->left) { _dump_nodes_recurse(&glb_nodes[seg->left], file); }
    if (fwrite(seg->buffer->buffer + seg->offset, 1, seg->length, file) != seg->length)
    {
        return 1;
    }
    if (seg->right) { _dump_nodes_recurse(&glb_nodes[seg->right], file); }
    return 0;
}

int create_buffer_from_save(struct project *project, struct state *state, const char *filename, struct state **result_state, struct mapped_buffer **result_buffer)
{
    /* create file with given rights */
    HANDLE hFile = CreateFileA(filename, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        return 1;
    }
    HANDLE hFileCRT;
    DuplicateHandle( GetCurrentProcess(), hFile, GetCurrentProcess(), &hFileCRT, 0, FALSE, DUPLICATE_SAME_ACCESS);
    if (hFileCRT == INVALID_HANDLE_VALUE)
    {
        CloseHandle(hFile);
        return 1;
    }

    int fd = _open_osfhandle((intptr_t)hFileCRT, _O_WRONLY | _O_BINARY);
    if (fd == -1) { CloseHandle(hFileCRT); CloseHandle(hFile); return 1; }

    FILE *file = _fdopen(fd, "wb");
    if (!file) { CloseHandle(hFile); _close(fd); return 1; }

    if (state->value != NULL)
    {
        if (_dump_nodes_recurse(state->value, file))
        {
            return 2;
        }
    }

    fclose(file);

    LARGE_INTEGER size;
    if (!GetFileSizeEx(hFile, &size))
    {
        fclose(file);
        CloseHandle(hFile);
        return 3;
    }

    int64_t total_length = (state->value ? state->value->total_length : 0);

    assert(total_length == size.QuadPart);

    HANDLE hMapping = CreateFileMapping(hFile, NULL, PAGE_READONLY, 0, 0, NULL);
    if (hMapping == NULL)
    {
        CloseHandle(hFile);
        fclose(file);
        return 4;
    }

    void *pBuffer = MapViewOfFile(hMapping, FILE_MAP_READ, 0, 0, 0);
    if (pBuffer == NULL)
    {
        CloseHandle(hMapping);
        fclose(file);
        return 5;
    }

    struct mapped_buffer_real *buf = calloc(1, sizeof(*buf));
    buf->buffer = (char *)pBuffer;
    buf->length = size.QuadPart;
    buf->allocated = size.QuadPart;
    buf->links_count = 1;
    buf->file_handle = hFile;
    buf->mapping_handle = hMapping;

    struct state *new_state = state_create_dup(project, state);

    if (state->value)
    {
        new_state->value = calloc(1, sizeof(*new_state->value));
        new_state->value->buffer = (struct mapped_buffer *)buf;
        new_state->value->offset = 0;
        new_state->value->length = total_length;
        new_state->value->newlines = state->value->total_newlines;
        new_state->value->left = 0;
        new_state->value->right = 0;
        new_state->value->total_length = total_length;
        new_state->value->version_id = new_state->version_id;
        new_state->value->height = 1;
        new_state->value->total_newlines = state->value->total_newlines;
    }

    *result_buffer = (struct mapped_buffer *)buf;
    *result_state = new_state;

    return 0;
}

struct mapped_buffer *allocate_buffer_from_file(const char *filename)
{
    struct mapped_buffer_real *buf = calloc(1, sizeof(*buf));
    if (!buf)
    {
        return NULL;
    }

    HANDLE hFile = CreateFileA(filename, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
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
