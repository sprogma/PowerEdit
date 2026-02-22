#include "structure.h"
#include "threading.h"
#include "text_api.h"

#include <stdio.h>
#include <string.h>
#include <assert.h>
#include <stdlib.h>

char* get_all_text(struct state *s) {
    int64_t size = state_get_size(s);
    char *buf = malloc(size + 1);
    if (size > 0) {
        state_read(s, 0, size, buf);
    }
    buf[size] = '\0';
    return buf;
}

void test_insert_read() {
    printf("Test 1: Insert & Read... ");
    struct project proj = {0}; 
    proj.lock = (SRWLOCK)SRWLOCK_INIT;
    proj.current_buffer = allocate_buffer(1024);

    struct state *s = state_create_empty(&proj);
    state_moditify(&proj, s, 0, MODIFICATION_INSERT, 5, "Hello");
    state_moditify(&proj, s, 5, MODIFICATION_INSERT, 6, " World");

    char *res = get_all_text(s);
    assert(strcmp(res, "Hello World") == 0);
    assert(state_get_size(s) == 11);

    free(res);
    printf("PASSED\n");
}

void test_boundary_delete() {
    printf("Test 2: Multi-segment Delete... ");
    struct project proj = {0};
    proj.lock = (SRWLOCK)SRWLOCK_INIT;
    proj.current_buffer = allocate_buffer(1024);

    struct state *s = state_create_empty(&proj);
    
    {
        char *res = get_all_text(s);
        printf("get <%s>\n", res);
    }
    state_moditify(&proj, s, 0, MODIFICATION_INSERT, 3, "AAA"); 
    {
        char *res = get_all_text(s);
        printf("get <%s>\n", res);
    }
    state_moditify(&proj, s, 3, MODIFICATION_INSERT, 3, "BBB"); 
    {
        char *res = get_all_text(s);
        printf("get <%s>\n", res);
    }
    state_moditify(&proj, s, 6, MODIFICATION_INSERT, 3, "CCC"); 
    {
        char *res = get_all_text(s);
        printf("get <%s>\n", res);
    }

        
    state_moditify(&proj, s, 1, MODIFICATION_DELETE, 7, NULL);

    char *res = get_all_text(s);
    printf("get <%s>\n", res);
    assert(strcmp(res, "AC") == 0); 
    
    free(res);
    printf("PASSED\n");
}

void test_persistence() {
    printf("Test 3: Version Persistence... ");
    struct project proj = {0};
    proj.lock = (SRWLOCK)SRWLOCK_INIT;
    proj.current_buffer = allocate_buffer(1024);

    struct state *v1 = state_create_empty(&proj);
    state_moditify(&proj, v1, 0, MODIFICATION_INSERT, 4, "Base");
    state_commit(&proj, v1);
    
    struct state *v2 = state_create_dup(&proj, v1);
    state_moditify(&proj, v2, 4, MODIFICATION_INSERT, 6, "+Extra");

    char *t1 = get_all_text(v1);
    char *t2 = get_all_text(v2);
    printf("get <%s>\n", t1);
    printf("get <%s>\n", t2);

    assert(strcmp(t1, "Base") == 0);
    assert(strcmp(t2, "Base+Extra") == 0);
    
    
    assert(v2->previous_versions[0] == v1);
    assert(v1->next_versions[0] == v2);

    free(t1); free(t2);
    printf("PASSED\n");
}

void test_version_growth() {
    printf("Test 4: Version Growth (Realloc)... ");
    struct project proj = {0};
    proj.lock = (SRWLOCK)SRWLOCK_INIT;
    proj.current_buffer = allocate_buffer(1024);

    struct state *root = state_create_empty(&proj);
    struct state *current = root;
    
    for(int i = 0; i < 100; i++) {
        state_create_dup(&proj, root);
    }    
    assert(root->next_versions_len == 100);
    assert(root->next_versions_alloc >= 100);
    printf("PASSED\n");
}

int main() {
    test_insert_read();
    test_boundary_delete();
    test_persistence();
    test_version_growth();

    printf("\n--- ALL TESTS PASSED ---\n");
    return 0;
}

