#define __USE_MINGW_ANSI_STDIO 1
#include "stdio.h"
#include "export.h"


int main()
{
    struct buffer b;
    struct modification mod;


    buffer_init(&b);
    
    mod = (struct modification){
        ModificationInsert, 0, 3, "abc"
    };
    buffer_moditify(&b, &mod);
    
    mod = (struct modification){
        ModificationInsert, 3, 10, "0123456789"
    };
    buffer_moditify(&b, &mod); 
    
    mod = (struct modification){
        ModificationInsert, 1, 3, "xyz"
    };
    buffer_moditify(&b, &mod); 
    
    mod = (struct modification){
        ModificationDelete, 3, 2, NULL
    };
    buffer_moditify(&b, &mod); 

    char s[128];

    int64_t len;
    buffer_get_size(&b, b.version, &len);
    buffer_read(&b, b.version, 0, len, s);
    printf("Char at s[0] = %*.*s\n", (int)len, (int)len, s);

    buffer_destroy(&b);

    return 0;
}


