#include "structure.h"
#include "stdlib.h"
#include "string.h"
#include "threading.h"
#include "text_api.h"

#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include <stdint.h>
#include <stdlib.h>
#include <stdbool.h>

#include <stdint.h>
#include <stdlib.h>
#include <stdbool.h>

#define Key128(x) (Key128){{x[0], x[1]}}

typedef struct { int64_t v[2]; } Key128;

typedef struct
{
	Key128 key;
	void *value;
	int32_t used;
	int32_t was_used;
} HashEntry;

typedef struct
{
	HashEntry *entries;
	int64_t alloc, len, reallen;
} HashTable;

int key_equal(Key128 a, Key128 b)
{
	return a.v[0] == b.v[0] && a.v[1] == b.v[1];
}

uint64_t hash_k(Key128 k)
{
	uint64_t h = 0xCBF29CE484222325;
	for (int i = 0; i < 16; i++)
	{    
		h = (h ^ ((uint8_t *)k.v)[i]) * 0x100000001B3;
	}
	return h;
}

HashEntry *find_slot(HashEntry *entries, size_t size, Key128 k)
{
	uint64_t i = hash_k(k) & (size - 1), mask = size - 1;
	while (entries[i].was_used && !key_equal(entries[i].key, k))
	{
	    i++; 
	    i &= mask;
	}
	return &entries[i];
}

void insert(HashTable *table, Key128 k, void *val)
{
	if (table->len * 10 >= 7 * table->alloc)
	{
		size_t old_s = table->alloc;
		HashEntry *old_e = table->entries;
		if (table->reallen >= table->alloc / 2)
		{
			table->alloc = table->alloc ? table->alloc * 2 : 1024;
		}
		table->entries = calloc(table->alloc, sizeof(HashEntry));
		for (size_t i = 0; i < old_s; i++)
		{
			if (old_e[i].used) 
			{
			    *find_slot(table->entries, table->alloc, old_e[i].key) = old_e[i];
			}
		}
		table->len = table->reallen;
		free(old_e);
	}

	HashEntry *entry = find_slot(table->entries, table->alloc, k);
	if (!entry->was_used)
	{
	    table->len++;
		entry->was_used = 1;
	}
	if (!entry->used) 
	{ 
	    entry->key = k; 
		entry->used = 1; 
	    table->reallen++;
    }
	entry->value = val;
}

void *get(HashTable *table, Key128 k)
{
	if (table->alloc == 0) return NULL;
	HashEntry *e = find_slot(table->entries, table->alloc, k);
	return e->used ? e->value : NULL;
}

void rem(HashTable *table, Key128 k)
{
	if (table->alloc == 0) return;
	HashEntry *e = find_slot(table->entries, table->alloc, k);
	e->used = 0;
	table->reallen--;
}


static struct state *TryMerge(HashTable *table, struct state *base, struct state *child)
{
	/* full compare of them */
	int64_t len1 = SegmentLength(base->value), len2 = SegmentLength(child->value);
	if (len1 != len2)
	{
		/* differs - remove this hash for now [ignore this states] */
		rem(table, Key128(base->hash.total_hash));
		return NULL;
	}
	{
		int64_t i = 0;
		char bufferA[1024], bufferB[1024];
		while (i + 1024 < len1)
		{
			state_read(base, i, 1024, bufferA);
			state_read(child, i, 1024, bufferB);
			if (memcmp(bufferA, bufferB, 1024) != 0)
			{
				/* differs - remove this hash for now [ignore this states] */
				rem(table, Key128(base->hash.total_hash));
				return NULL;
			}
		}
		if (i < len1)
		{
			state_read(base, i, len1 - i, bufferA);
			state_read(child, i, len1 - i, bufferB);
			if (memcmp(bufferA, bufferB, len1 - i) != 0)
			{
				/* differs - remove this hash for now [ignore this states] */
				rem(table, Key128(base->hash.total_hash));
				return NULL;
			}
		}
	}
	/* found same states: merge them */
	printf("states %p and %p are same!\n", base, child);
	merge_state(base, child);
	printf("merged into %p\n", (base->merged_to ? child : base));
	return (base->merged_to ? child : base);
}


int StatesMergeWorker(void *param)
{
	struct project *project = param;
	HashTable table = {};

	while (WaitForSingleObject(project->StopEvent, 1) == WAIT_TIMEOUT)
	{
		struct state *base = NULL, *child = NULL;
		lockShared(&project->lock);
		for (int64_t i = 0; i < project->states_len; ++i)
		{
			if (project->states[i]->hash.calculated && project->states[i]->committed && !project->states[i]->merged_to)
			{
				struct state *this = get(&table, Key128(project->states[i]->hash.total_hash));
				if (this == NULL)
				{
					insert(&table, Key128(project->states[i]->hash.total_hash), project->states[i]);
				}
				else if (this != project->states[i])
				{
					base = this;
					child = project->states[i];
					break;
				}
			}
		}
		freeShared(&project->lock);
		if (base != NULL && child != NULL)
		{
			struct state *result = TryMerge(&table, base, child);
			if (result != NULL)
			{
				insert(&table, Key128(result->hash.total_hash), result);
			}
		}
		else
		{
			msleep(50);
		}
	}
	return 0;
}
