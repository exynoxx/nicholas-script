// stdlib.c
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

void _ns_print_string(const char* s) {
    printf("%s\n", s);
}
void _ns_print_int(int x) {
    printf("%d\n", x);
}

char* _ns_int_to_string(int x) {
    char* buffer = (char*)malloc(12); // Enough for 32-bit int
    snprintf(buffer, 12, "%d", x);
    return buffer;
}

char* _ns_string_concat(const char* s, int times) {
    if (times <= 0) return strdup(""); // Empty string
    size_t len = strlen(s);
    char* result = (char*)malloc(len * times + 1);
    result[0] = '\0';
    for (int i = 0; i < times; ++i) {
        strcat(result, s);
    }
    return result;
}

int _ns_pow_int(int base, int exp) {
    return (int)pow((double)base, (double)exp);
}

char *_ns_readline()
{
    char* buffer = (char*)malloc(256);
    return fgets(buffer, 256, stdin);
}