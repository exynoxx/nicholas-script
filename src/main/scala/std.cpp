#include <stdio>
#include <string>
#include <vector>

typedef struct NSvar _NSvar;
enum NStype {VOID, INT,STRING,BOOL,ARRAY,FUNCTION0,FUNCTION1,FUNCTION2};

typedef union NSvalue{
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

template<_NSvar, _NSvar>
auto _NSmap(_NSvar f, _NSvar array)
{
    std::vector<NSvar> tmp;
    tmp.resize(array.value.array.size());
    transform(array.value.array.begin(), array.value.array.end(), tmp.begin(), f.value.f1);
    return tmp;
}

_NSvar stdadder (_NSvar x, _NSvar y){
    return _NSvar(x.value.i+y.value.i);
}
_NSvar intlistadder(_NSvar x, _NSvar y){
    return _NSmap(_NSvar(&_NSadd), y);
}

typedef _NSvar (*_NSbinfunc)(_NSvar x, _NSvar y);
auto adders = new _NSbinfunc[8*8+8];

_NSvar _NSadd(_NSvar x, _NSvar y){
    //8*i+j
    //_NSvar (* adder)(_NSvar x, _NSvar y) = nullptr;
    auto adder = adders[x.type*8+y.type];
    return adder(x,y);
}

int main(){

    adders[8*1+1] = &stdadder;
    adders[8*1+4] = &intlistadder;

    auto ten = _NSadd(_NSvar(5),_NSvar(4));
    std::cout << ten.value.i << std::endl;
} 

