#include "inttypes.h"
#include "assert.h"

#include "text_api.h"
#include "threading.h"
#include "structure.h"


#define logError(...) fprintf(stderr, __VA_ARGS__);


void _reserve_previous_versions(struct state *state, int64_t total_size)
{
    if (state->previous_versions_alloc < total_size)
    {
        while (state->previous_versions_alloc < total_size)
        {
            state->previous_versions_alloc *= 2;
            state->previous_versions_alloc |= 1;
        }
        void *oldPtr = state->previous_versions;
        state->previous_versions = realloc(oldPtr, sizeof(*state->previous_versions) * state->previous_versions_alloc);
        if (state->previous_versions == NULL)
        {
            exit(1);
        }
    }
}


void _reserve_next_versions(struct state *state, int64_t total_size)
{
    if (state->next_versions_alloc < total_size)
    {
        while (state->next_versions_alloc < total_size)
        {
            state->next_versions_alloc *= 2;
            state->next_versions_alloc |= 1;
        }
        void *oldPtr = state->next_versions;
        state->next_versions = realloc(oldPtr, sizeof(*state->next_versions) * state->next_versions_alloc);
        if (state->next_versions == NULL)
        {
            exit(1);
        }
    }
}


struct state *state_create_empty(struct project *project)
{

    struct state *res = calloc(1, sizeof(*res));
    res->timestamp = get_time_us();

    lockExclusive(&project->lock);
    res->version_id = project->last_version_id++;

    _reserve_state(project, project->states_len + 1);
    project->states[project->states_len++] = res;
    freeExclusive(&project->lock);

    return res;
}


struct state *state_create_dup(struct project *project, struct state *state)
{
    struct state *res = calloc(1, sizeof(*res));
    res->hash = state->hash;
    res->timestamp = get_time_us();

    _reserve_previous_versions(res, res->previous_versions_len + 1);
    res->previous_versions[res->previous_versions_len++] = state;

    _reserve_next_versions(state, state->next_versions_len + 1);
    state->next_versions[state->next_versions_len++] = res;

    res->value = state->value;
    SegmentIncRef(state->value);

    lockExclusive(&project->lock);

    res->version_id = project->last_version_id++;

    _reserve_state(project, project->states_len + 1);
    project->states[project->states_len++] = res;

    freeExclusive(&project->lock);
    return res;
}

void state_release(struct state *state)
{
    SegmentDecRef(state->value);
    free(state->previous_versions);
    free(state->next_versions);
    free(state->cursors);
    free(state->tags);
}

void _state_insert(struct project *project, struct state *state, int64_t position, int64_t length, char *source)
{
    /* create buffer for this moditification */
    struct mapped_buffer *buffer;
    int64_t offset;

    lockExclusive(&project->lock);
    if (project->current_buffer->length + length > project->current_buffer->allocated)
    {
        /* if 4/5 is filled - create new buffer */
        if (project->current_buffer->length * 4 > project->current_buffer->allocated * 5)
        {
            buffer = allocate_buffer(length + 8 * 1024 * 1024);
            buffer->length = length;
            offset = 0;
        }
        else /* create buffer for only this modification */
        {
            buffer = allocate_buffer(length);
            buffer->length = length;
            offset = 0;
        }
    }
    else
    {
        offset = project->current_buffer->length;
        buffer = project->current_buffer;
        buffer->length += length;
    }
    freeExclusive(&project->lock);

    memcpy(buffer->buffer + offset, source, length);

    /* if position is out of range - add text to end */
    if (position == SegmentLength(state->value))
    {
        printf("A: Insert %lld length at %lld\n", length, position);
        state->value = InsertSegment(state->value, (struct segment_info) { buffer, offset, length }, position, state->version_id);
        return;
    }
    if (position < 0 || position > SegmentLength(state->value))
    {
        logError("Position is out of bounds\n");
        return;
    }
    /* split current buffer by this position */
    int64_t segment_position;
    struct segment *segment = GetSegment(state->value, position, &segment_position);
    struct segment_info info;
    memcpy(&info, segment, sizeof(info));

    state->value = RemoveSegment(state->value, position, state->version_id);
    /* insert back this segment's prefix */
    if (segment_position < position)
    {
        printf("B: Insert %lld length at %lld\n", length, position);
        state->value = InsertSegment(state->value, (struct segment_info) { info.buffer, info.offset, position - segment_position }, segment_position, state->version_id);
    }
    /* insert new segment */
    state->value = InsertSegment(state->value, (struct segment_info) { buffer, offset, length }, position, state->version_id);
    /* insert back this segment's suffix */
    if (position - segment_position < info.length)
    {
        state->value = InsertSegment(state->value, (struct segment_info) { info.buffer, info.offset + (position - segment_position), info.length - (position - segment_position) }, position + length, state->version_id);
    }
}


void _state_delete(struct project *project, struct state *state, int64_t position, int64_t length)
{
    (void)project;

    int64_t segment_position, delta, prefix;
    struct segment *segment;
    struct segment_info info;
    while (length > 0)
    {
        segment = GetSegment(state->value, position, &segment_position);
        memcpy(&info, segment, sizeof(info));
        state->value = RemoveSegment(state->value, position, state->version_id);
        printf("Requested %lld -> get %lld of len %lld\n", position, segment_position, info.length);
        /* insert back prefix */
        prefix = position - segment_position;
        if (prefix > 0)
        {
            state->value = InsertSegment(state->value, (struct segment_info) { info.buffer, info.offset, prefix }, segment_position, state->version_id);
        }
        delta = info.length - prefix;
        if (delta > length) { delta = length; }
        length -= delta;
    }
    /* insert back suffix */
    if (delta < info.length - prefix)
    {
        state->value = InsertSegment(state->value, (struct segment_info) { info.buffer, info.offset + prefix + delta, info.length - prefix - delta }, position, state->version_id);
    }
}


void state_moditify(struct project *project, struct state *state, int64_t position, int64_t type, int64_t length, char *buffer)
{
    lockExclusive(&state->lock);
    if (state->committed)
    {
        logError("Moditifying of commited state\n");
        freeExclusive(&state->lock);
        return;
    }
    if (type == MODIFICATION_INSERT)
    {
        _state_insert(project, state, position, length, buffer);
    }
    else if (type == MODIFICATION_DELETE)
    {
        _state_delete(project, state, position, length);
    }
    freeExclusive(&state->lock);
    return;
}


void state_commit(struct project *project, struct state *state)
{
    (void)project;

    lockExclusive(&state->lock);
    state->committed = 1;
    freeExclusive(&state->lock);
}


int64_t state_get_size(struct state *state)
{
    return SegmentLength(state->value);
}

void state_read(struct state *state, int64_t position, int64_t length, char *buffer)
{
    while (length > 0)
    {
        int64_t segment_position;
        struct segment *segment = GetSegment(state->value, position, &segment_position);
        int64_t delta = position - segment_position;
        int64_t to_copy = segment->length - delta;
        if (to_copy > length) { to_copy = length; }
        memcpy(buffer, segment->buffer->buffer + segment->offset + delta, to_copy);
        position += to_copy;
        length -= to_copy;
        buffer += to_copy;
    }
}


void state_set_cursors(struct state *state, int64_t count, struct cursor *cursors)
{
    lockExclusive(&state->lock);
    free(state->cursors);
    state->cursors_len = count;
    state->cursors = calloc(1, sizeof(*state->cursors) * state->cursors_len);
    memcpy(state->cursors, cursors, sizeof(*state->cursors) * state->cursors_len);
    freeExclusive(&state->lock);
}

int64_t state_get_cursors_count(struct state *state)
{
    return state->cursors_len;
}

void state_get_cursors(struct state *state, int64_t count, struct cursor *result)
{
    lockShared(&state->lock);
    memcpy(result, state->cursors, sizeof(*state->cursors) * count);
    freeShared(&state->lock);
}

// TODO: rewrite this
char const_buffer[1000000];
int64_t line[1000000];
int64_t offset[1000000];
struct state *state_version = NULL;
int64_t buffer_size = -1;
void state_get_offsets(struct state *state, int64_t position, int64_t *result_line, int64_t *result_column)
{
    if (state_version != state)
    {
        int64_t size = 0;
        size = state_get_size(state);
        state_read(state, 0, size, const_buffer);
        state_version = state;
        buffer_size = size;
        line[0] = 0;
        for (int64_t i = 1; i <= size; ++i)
        {
            line[i] = line[i - 1] + (int64_t)(const_buffer[i - 1] == '\n');
            offset[i] = (const_buffer[i - 1] == '\n' ? 0 : offset[i - 1] + 1);
        }
    }
    *result_line = line[position];
    *result_column = offset[position];
}
