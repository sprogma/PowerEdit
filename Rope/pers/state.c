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
    res->depth = 0;

    lockExclusive(&project->lock);
    res->version_id = project->last_version_id++;

    _reserve_state(project, project->states_len + 1);
    project->states[project->states_len++] = res;
    freeExclusive(&project->lock);

    return res;
}


struct state *state_create_dup(struct project *project, struct state *state)
{
    while (state->merged_to) state = state->merged_to;
    struct state *res = calloc(1, sizeof(*res));
    res->depth = state->depth + 1;
    res->hash = state->hash;
    res->timestamp = get_time_us();

    _reserve_previous_versions(res, res->previous_versions_len + 1);
    res->previous_versions[res->previous_versions_len++] = state;

    _reserve_next_versions(state, state->next_versions_len + 1);
    state->next_versions[state->next_versions_len++] = res;

    res->value = state->value;
    res->committed = 0;
    res->hash.calculated = 0;

    lockExclusive(&project->lock);

    res->version_id = project->last_version_id++;

    _reserve_state(project, project->states_len + 1);
    project->states[project->states_len++] = res;

    freeExclusive(&project->lock);
    return res;
}

void merge_state(struct state *base, struct state *child)
{
    assert(base != child);
    if (base->depth > child->depth)
    {
        void *tmp = child;
        child = base;
        base = tmp;
    }

    /* set all child childs to base childs */
    _reserve_next_versions(base, base->next_versions_len + child->next_versions_len);
    for (int64_t i = 0; i < child->next_versions_len; ++i)
    {
        printf("New parent\n");
        base->next_versions[base->next_versions_len++] = child->next_versions[i];
    }

    /* set all parents of child to out parents */
    _reserve_previous_versions(base, base->previous_versions_len + child->previous_versions_len);
    for (int64_t i = 0; i < child->previous_versions_len; ++i)
    {
        printf("New child\n");
        base->previous_versions[base->previous_versions_len++] = child->previous_versions[i];
    }

    /* remove all links on this child */
    for (int64_t i = 0; i < child->next_versions_len; ++i)
    {
        struct state *node = child->next_versions[i];
        if (node != base)
        {
            for (int64_t j = 0; j < node->previous_versions_len; ++j)
            {
                if (node->previous_versions[j] == child)
                {
                    printf("Change link A\n");
                    node->previous_versions[j] = base;
                }
                else if (node->previous_versions[j] == base)
                {
                    node->previous_versions[j] = node->previous_versions[--node->previous_versions_len];
                }
            }
        }
    }
    for (int64_t i = 0; i < child->previous_versions_len; ++i)
    {
        struct state *node = child->previous_versions[i];
        if (node != base)
        {
            for (int64_t j = 0; j < node->next_versions_len; ++j)
            {
                if (node->next_versions[j] == child)
                {
                    printf("Change link B\n");
                    node->next_versions[j] = base;
                }
                else if (node->next_versions[j] == base)
                {
                    node->next_versions[j] = node->next_versions[--node->next_versions_len];
                }
            }
        }
    }
    /* clear all child links */
    child->previous_versions_len = 0;
    child->next_versions_len = 0;
    child->merged_to = base;
}

void state_release(struct state *state)
{
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
        position = SegmentLength(state->value);
        //logError("Position is out of bounds\n");
        //return;
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
    int64_t insert_position = position;
    int64_t insert_length = length;
    while (insert_length > 0)
    {
        int64_t to_insert = length;
        if (to_insert > SEGMENT_SIZE) { to_insert = SEGMENT_SIZE; }
        state->value = InsertSegment(state->value, (struct segment_info) { buffer, offset, to_insert }, insert_position, state->version_id);
        insert_length -= to_insert;
        offset += to_insert;
        insert_position += to_insert;
    }
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
    while (state->merged_to) state = state->merged_to;
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
    while (state->merged_to) state = state->merged_to;

    (void)project;

    lockExclusive(&state->lock);
    state->committed = 1;
    freeExclusive(&state->lock);
}


int64_t state_get_size(struct state *state)
{
    while (state->merged_to) state = state->merged_to;
    return SegmentLength(state->value);
}

void state_read(struct state *state, int64_t position, int64_t length, char *buffer)
{
    while (state->merged_to) state = state->merged_to;
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
    while (state->merged_to) state = state->merged_to;
    lockExclusive(&state->lock);
    free(state->cursors);
    state->cursors_len = count;
    state->cursors = calloc(1, sizeof(*state->cursors) * state->cursors_len);
    memcpy(state->cursors, cursors, sizeof(*state->cursors) * state->cursors_len);
    freeExclusive(&state->lock);
}

int64_t state_get_cursors_count(struct state *state)
{
    while (state->merged_to) state = state->merged_to;
    return state->cursors_len;
}

void state_get_cursors(struct state *state, int64_t count, struct cursor *result)
{
    while (state->merged_to) state = state->merged_to;
    lockShared(&state->lock);
    memcpy(result, state->cursors, sizeof(*state->cursors) * count);
    freeShared(&state->lock);
}

void state_get_offsets(struct state *state, int64_t position, int64_t *result_line, int64_t *result_column)
{
    while (state->merged_to) state = state->merged_to;
    int64_t node_id = state->value - glb_nodes;
    if (!node_id || position <= 0)
    {
        *result_line = 0;
        *result_column = 0;
        return;
    }
    *result_line = SegmentGetLineNumber(node_id, position);
    int64_t last_nl_pos = FindNearestLeft(node_id, position - 1);
    if (last_nl_pos == -1)
    {
        *result_column = position;
    }
    else
    {
        *result_column = position - (last_nl_pos + 1);
    }
}


int64_t state_nearest_left(struct state *state, int64_t position)
{
    while (state->merged_to) state = state->merged_to;
    int64_t id = (state->value ? state->value - glb_nodes : 0);
    return FindNearestLeft(id, position);
}

int64_t state_nearest_right(struct state *state, int64_t position)
{
    while (state->merged_to) state = state->merged_to;
    int64_t id = (state->value ? state->value - glb_nodes : 0);
    return FindNearestRight(id, position);
}

int64_t state_line_number(struct state *state, int64_t position)
{
    while (state->merged_to) state = state->merged_to;
    int64_t id = (state->value ? state->value - glb_nodes : 0);
    return SegmentGetLineNumber(id, position);
}

int64_t state_nth_newline(struct state *state, int64_t n)
{
    while (state->merged_to) state = state->merged_to;
    int64_t id = (state->value ? state->value - glb_nodes : 0);
    return SegmentNthNewline(id, n);
}

