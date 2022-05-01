#include <stdio.h>
#include <string>
#include <vector>

typedef struct NSvar;
enum NStype {VOID, INT,STRING,BOOL,ARRAY,FUNCTION0,FUNCTION1,FUNCTION2};

typedef union NSvalue{
    int i;
    std::string *s;
    bool b;
    std::vector<NSvar> *array;
    NSvar (* f0)();
    NSvar (* f1)(NSvar);
    NSvar (* f2)(NSvar,NSvar);
};

struct NSvar{
    NStype type;
    NSvalue value;

    NSvar(int i){
        auto val = NSvalue();
        val.i=i;
        type = INT;
        value = val;
    }
    NSvar(std::string *s){
        auto val = NSvalue();
        val.s=s;
        type = STRING;
        value = val;
    }
    NSvar(bool b){
        auto val = NSvalue();
        val.b=b;
        type = BOOL;
        value = val;
    }
    NSvar(std::vector<NSvar> *array){
        auto val = NSvalue();
        val.array=array;
        type = ARRAY;
        value = val;
    }
    NSvar(std::initializer_list<NSvar> init){
        auto val = NSvalue();
        val.array=new std::vector<NSvar>();
        val.array->assign(init);
        type = ARRAY;
        value = val;
    }
    ~NSvar(){delete value.array;};
};

NSvar map(NSvar f, NSvar array){

    auto tmp = new std::vector<nsvar>(array.value.array.size());
    for (int i = 0; i < array.value.array.size(); i++)
    {
        tmp[i] = f.value.f1(array.value.array[i]);
    }
    return NSvar(tmp);
}

int main (){
    auto one = NSvar(1);
    auto five = NSvar(5);
    auto onetwothree = NSvar({one,five,one});


    auto f = [](int x) {return x*2;};
    auto fvar = NSvar(f);



    return 0;
}
