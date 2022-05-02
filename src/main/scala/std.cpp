#include <iostream>
#include <string>
#include <algorithm>
#include <vector>

typedef struct NSvar _NSvar;
enum NStype {VOID, INT,STRING,BOOL,ARRAY,FUNCTION0,FUNCTION1,FUNCTION2};

typedef _NSvar (*_NSbinfunc)(_NSvar x, _NSvar y);
typedef _NSvar (*_NSunifunc)(_NSvar x);
typedef _NSvar (*_NSfunc)();

union NSvalue{
    int i;
    std::string *s;
    bool b;
    std::vector<NSvar> *array;
    _NSvar (* f0)();
    _NSvar (* f1)(_NSvar);
    _NSvar (* f2)(_NSvar,_NSvar);
};

struct NSvar{
    NStype type;
    NSvalue value;

    NSvar(){
        
    }

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

    NSvar(_NSfunc f){
        auto val = NSvalue();
        val.f0 = f;
        type = FUNCTION0;
        value = val;
    }
    NSvar(_NSunifunc f){
        auto val = NSvalue();
        val.f1 = f;
        type = FUNCTION1;
        value = val;
    }
    NSvar(_NSbinfunc f){
        auto val = NSvalue();
        val.f2 = f;
        type = FUNCTION2;
        value = val;
    }

    ~NSvar(){delete value.array;};
};
auto adders = new _NSbinfunc[8*8+8];

_NSvar _NSadd(_NSvar x, _NSvar y){
    //8*i+j
    //_NSvar (* adder)(_NSvar x, _NSvar y) = nullptr;
    auto adder = adders[x.type*8+y.type];
    return adder(x,y);
}

_NSvar _NSmap(_NSvar f, _NSvar array, _NSvar x)
{
    std::vector<NSvar> *tmp = new std::vector<NSvar>(array.value.array->size());
    for (size_t i = 0; i < array.value.array->size(); i++)
    {
        tmp->at(i) = f.value.f2(x,array.value.array->at(i));
    }
    //std::transform(array.value.array->begin(), array.value.array->end(),x, tmp->begin(), f.value.f2);
    return _NSvar(tmp);
}

_NSvar stdadder (_NSvar x, _NSvar y){
    return _NSvar(x.value.i+y.value.i);
}
_NSvar intlistadder(_NSvar x, _NSvar y){
    _NSbinfunc f = &_NSadd;
    return _NSmap(_NSvar(5), y, x);
}
    
int main(){

    adders[8*1+1] = &stdadder;
    adders[8*1+4] = &intlistadder;

    auto ten = _NSadd(_NSvar(5),_NSvar(4));
    std::cout << ten.value.i << std::endl;
} 

