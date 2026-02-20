#ifndef STRUCTURE_H
#define STRUCTURE_H

#include "stdio.h"
#include "stdlib.h"
#include "inttypes.h"
#include "stdatomic.h"

#include "mapped_buffer.h"
#include "threading.h"
#include "clocks.h"

struct segment_info
{
    struct mapped_buffer *buffer;
    int64_t offset; // offset buffer
    int64_t length; // length of segment 
};

struct segment
{
    struct segment_info;
    // tree info
    int64_t left, right;
    int64_t height;
    int64_t total_length;
    int64_t links_count;
    int64_t version_id;
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


struct state
{
    lock_t lock;
    ptime_t timestamp;
    struct state_hash hash;
    struct segment *version;
    int64_t version_id;
    
    int64_t previous_versions_len;
    struct state **previous_versions; // first version is main, others are "merged-in"
    
    int64_t tags_len;
    int64_t *tags;
};


struct buffer
{
    /* data */
    struct state *current;
    _Atomic int64_t last_version_id;
};


struct segment *GetSegment(struct segment *tree, int64_t position);
struct segment *RemoveSegment(struct segment *tree, int64_t position, int64_t this_version);
struct segment *InsertSegment(struct segment *tree, struct segment_info info, int64_t position, int64_t this_version);

#endif
