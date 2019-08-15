#include <stdlib.h>
#include <stdio.h>
#include <string.h>
int f1(int x);
int f2(char * in);
int f1(int x){
char *ran6= (char *) malloc (0);
int ran5 = x+x+2*x;
free(ran6);
return ran5;
}
int f2(char * in){
char *ran7= (char *) malloc (8);
char *t = ran7+0;
strcpy(t,"hell");
char *ran12 = ran7+4;
strcpy(ran12,in);
strcat(ran12,in);
strcat(ran12,in);
char * triple = ran12;
char *gg = ran7+4;
strcpy(gg,"gwrg");
free(ran7);
return 0;
}
int main (int arc, char **argv) {
char *ran8= (char *) malloc (56);
int a = 1+1+2+5+3+1;
char *b = ran8+0;
strcpy(b,"hek");
char *ran4 = ran8+3;
strcpy(ran4,"hello");
char *ran3 = ran8+8;
strcpy(ran3,"hello");
char *ran2 = ran8+13;
strcpy(ran2,"hello");
char *ran1 = ran8+18;
strcpy(ran1,"other");
char *ran0 = ran8+23;
strcpy(ran0,"world");
char *ran10 = ran8+28;
strcpy(ran10,ran0);
strcat(ran10,ran1);
strcat(ran10,b);
strcat(ran10,ran2);
strcat(ran10,ran3);
strcat(ran10,ran4);
char * y = ran10;
int c = f1(1);
free(ran8);
}
