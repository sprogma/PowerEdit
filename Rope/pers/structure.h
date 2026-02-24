#ifndef STRUCTURE_H
#define STRUCTURE_H

#include "stdio.h"
#include "stdlib.h"
#include "inttypes.h"
#include "stdatomic.h"

#include "mapped_buffer.h"
#include "threading.h"
#include "clocks.h"

#define MODIFICATION_INSERT 1
#define MODIFICATION_DELETE 2

struct segment_info
{
    struct mapped_buffer *buffer;
    int64_t offset; // offset buffer
    int64_t length; // length of segment 
    int64_t newlines; // count of newlines in buffer
};

struct segment
{
    struct segment_info;
    // tree info
    int64_t left;
    int64_t right;
    int64_t total_length;
    int64_t version_id;
    int64_t height;
    int64_t total_newlines;
};


struct hash_segment
{
    int64_t offset;
    int64_t hash;
};


struct state_hash
{
    lock_t lock;
    int64_t calculated;
    int64_t segments_len;
    struct hash_segment *segments;
    int64_t total_hash[2];
};


struct cursor
{
    int64_t begin, end;
};


struct state
{
    lock_t lock;
    ptime_t timestamp;
    struct state_hash hash;
    struct segment *value;
    int64_t version_id;
    int64_t depth;

    int64_t committed;

    struct cursor *cursors;
    int64_t cursors_len;
    
    int64_t previous_versions_len;
    int64_t previous_versions_alloc;
    struct state **previous_versions; // first version is main, others are "merged-in"
    
    int64_t next_versions_len;
    int64_t next_versions_alloc;
    struct state **next_versions;

    struct state *merged_to;
    
    int64_t tags_len;
    int64_t *tags;
};


struct project
{
    lock_t lock;
    struct state **states;
    int64_t states_len;
    int64_t states_alloc;
    struct mapped_buffer *current_buffer;
    _Atomic int64_t last_version_id;
    HANDLE StopEvent;

    thread_t HashWorker;
    thread_t StatesMerger;
};


struct segment *GetSegment(struct segment *tree, int64_t position, int64_t *segment_offset);
struct segment *RemoveSegment(struct segment *tree, int64_t position, int64_t this_version);
struct segment *InsertSegment(struct segment *tree, struct segment_info info, int64_t position, int64_t this_version);
int64_t SegmentLength(struct segment *tree);
int64_t FindNearestLeft(int64_t node_id, int64_t position);
int64_t FindNearestRight(int64_t node_id, int64_t position);
int64_t SegmentNthNewline(int64_t node, int64_t n);


struct state *state_create_empty(struct project *project);
void state_release(struct state *state);
void _reserve_state(struct project *project, int64_t total_size);
void merge_state(struct state *base, struct state *child);
int64_t SegmentGetLineNumber(int64_t root_idx, int64_t position);

extern struct segment glb_nodes[];

int HashEvaluationWorker(void *param);
int StatesMergeWorker(void *param);

#endif
