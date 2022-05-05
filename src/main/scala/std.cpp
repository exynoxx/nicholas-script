#include <iostream>
#include <string>
#include <algorithm>
#include <vector>
#include <cstring>

typedef struct NSvar _NSvar;
enum NStype {VOID, INT,STRING,BOOL,ARRAY,FUNCTION0,FUNCTION1,FUNCTION2};

typedef _NSvar* var;
typedef var (*_NSbinfunc)(var x, var y);
typedef var (*_NSunifunc)(var x);
typedef var (*_NSfunc)();


typedef union NSvalue{
    int i;
    std::string *s;
    bool b;
    std::vector<var> *array;
    _NSfunc f0;
    _NSunifunc f1;
    _NSbinfunc f2;

    ~NSvalue(){
        std::cout<<"onion destructing:"<<i<<std::endl;
    }   

}_NSvalue;

struct NSvar{
    NStype type;
    _NSvalue* value;

    NSvar(NStype t,_NSvalue* v){
        type=t;
        value=v;
    }
    ~NSvar(){
        std::cout<<"destructing:"<<value->i<<std::endl;
        if (type == ARRAY) delete value->array;
        delete value;
    }
};

var create_var(int i){
    return new _NSvar(INT,new _NSvalue{.i=i});
}
var create_var(std::vector<var> *a){
    auto value = new _NSvalue; 
    value->array = a;
    return new _NSvar(ARRAY,value);
}
var create_var(std::initializer_list<var> init){
    auto value = new _NSvalue; 
    value->array = new std::vector<var>(init.begin(),init.end());
    return new _NSvar(ARRAY,value);
}
var create_var(_NSfunc f){
    auto value = new _NSvalue; 
    value->f0 = f;
    return new _NSvar(FUNCTION0,value);
}
var create_var(_NSunifunc f){
    auto value = new _NSvalue; 
    value->f1 = f;
    return new _NSvar(FUNCTION1,value);
}
var create_var(_NSbinfunc f){
    auto value = new _NSvalue; 
    value->f2 = f;
    return new _NSvar(FUNCTION2,value);
}


auto adders = new _NSbinfunc[8*8+8];




var _NSmap(var f,var array,var x)
{
    std::vector<var> *tmp = new std::vector<var>(array->value->array->size());
    for (size_t i = 0; i < array->value->array->size(); i++)
    {
        tmp->at(i) = f->value->f2(x,array->value->array->at(i));
    }
    //std::transform(array.value.array->begin(), array.value.array->end(),x, tmp->begin(), f.value.f2);
    return create_var(tmp);
}


var _NSadd(var x, var y){
    auto adder = adders[x->type*8+y->type];
    return adder(x,y);
}


var intlistadder(var x, var y){
    _NSbinfunc f = &_NSadd;
    return _NSmap(create_var(f), y, x);
}

var stdadder (var x, var y){
    //delete x;
    //delete y;
    return create_var(x->value->i+y->value->i);
}
    
int main(){
    adders[8*1+1] = &stdadder;
    adders[8*1+4] = &intlistadder;
    
    std::cout<<"x"<<"\n";

    auto one = create_var(1);
    auto two = create_var(2);

    auto x = _NSadd(one,two);
    std::cout << x->value->i << "\n";
    
    // std::cout<<"four"<<"\n";
    // auto four = _NSvar(4);

    std::cout<<"v"<<"\n";
    auto four = create_var(4);
    std::vector<var> *v = new std::vector<var>(4);
    v->at(0) = four;
    v->at(1) = four;
    v->at(2) = four;
    v->at(3) = four;

    std::cout<<"assign v"<<"\n";
    x = create_var(v);
    std::cout << x->value->array->at(1)->value->i << "\n";
    x = create_var({four,four,four});
    std::cout << x->value->array->at(1)->value->i << "\n";
    x = _NSadd(one,x);
    std::cout << x->value->array->at(1)->value->i << "\n";
} 











// NSvar(int i){
//         type = INT;
//         value = new _NSvalue{};
//         value->i=i;
//     }
//     NSvar(std::string *s){
//         auto val = new _NSvalue{};
//         val->s=s;
//         type = STRING;
//         value = val;
//     }
//     NSvar(bool b){
//         auto val = new _NSvalue{};
//         val->b=b;
//         type = BOOL;
//         value = val;
//     }
//     NSvar(std::vector<NSvar> *array){
//         auto val = new _NSvalue{};
//         val->array=array;
//         type = ARRAY;
//         value = val;
//     }
//     NSvar(std::initializer_list<NSvar> init){
//         auto val = new _NSvalue{};
//         val->array=new std::vector<NSvar>(init.begin(),init.end());
//         type = ARRAY;
//         value = val;
//     }

//     NSvar(_NSfunc f){
//         auto val = new _NSvalue{};
//         val->f0 = f;
//         type = FUNCTION0;
//         value = val;
//     }
//     NSvar(_NSunifunc f){
//         auto val = new _NSvalue{};
//         val->f1 = f;
//         type = FUNCTION1;
//         value = val;
//     }
//     NSvar(_NSbinfunc f){
//         auto val = new _NSvalue{};
//         val->f2 = f;
//         type = FUNCTION2;
//         value = val;
//     }

//     ~NSvar(){
//         std::cout << "destructing: ";
//         std::cout << value->i << std::endl;

//         if (type == ARRAY) delete value->array;
//         delete value;
//     };