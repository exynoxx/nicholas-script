#include <iostream>
#include <string>
#include <algorithm>
#include <vector>
#include <cstring>


/*   FUNDAMENTAL ENTITIES   */

struct _NS_var_struct;
enum _NS_enum {VOID, INT,STRING,BOOL,ARRAY,FUNCTION0,FUNCTION1,FUNCTION2};

typedef _NS_var_struct* _NS_var;
typedef _NS_var (*_NSfunc2)(_NS_var x, _NS_var y);
typedef _NS_var (*_NSfunc1)(_NS_var x);
typedef _NS_var (*_NSfunc0)();


union _NS_value{
    int i;
    std::string *s;
    bool b;
    std::vector<_NS_var> *array;
    _NSfunc0 f0;
    _NSfunc1 f1;
    _NSfunc2 f2;

    ~_NS_value(){
        std::cout<<"onion destructing:"<<i<<std::endl;
    }   

};

struct _NS_var_struct{
    _NS_enum type;
    _NS_value* value;

    _NS_var_struct(_NS_enum t,_NS_value* v){
        type=t;
        value=v;
    }
    ~_NS_var_struct(){
        std::cout<<"destructing:"<<value->i<<std::endl;
        if (type == ARRAY) delete value->array;
        delete value;
    }
};

_NS_var _NS_create_var(int i){
    return new _NS_var_struct(INT,new _NS_value{.i=i});
}
_NS_var _NS_create_var(std::vector<_NS_var> *a){
    auto value = new _NS_value; 
    value->array = a;
    return new _NS_var_struct(ARRAY,value);
}
_NS_var _NS_create_var(std::initializer_list<_NS_var> init){
    auto value = new _NS_value; 
    value->array = new std::vector<_NS_var>(init.begin(),init.end());
    return new _NS_var_struct(ARRAY,value);
}
_NS_var _NS_create_var(_NSfunc0 f){
    auto value = new _NS_value; 
    value->f0 = f;
    return new _NS_var_struct(FUNCTION0,value);
}
_NS_var _NS_create_var(_NSfunc1 f){
    auto value = new _NS_value; 
    value->f1 = f;
    return new _NS_var_struct(FUNCTION1,value);
}
_NS_var _NS_create_var(_NSfunc2 f){
    auto value = new _NS_value; 
    value->f2 = f;
    return new _NS_var_struct(FUNCTION2,value);
}


/*   MISC   */

_NS_var _NSmap(_NS_var f,_NS_var array,_NS_var x)
{
    std::vector<_NS_var> *tmp = new std::vector<_NS_var>(array->value->array->size());
    for (size_t i = 0; i < array->value->array->size(); i++)
        tmp->at(i) = f->value->f2(x,array->value->array->at(i));
    return _NS_create_var(tmp);
}





/*   EVERY OPERATOR   */
auto _NS_addition_ops = new _NSfunc2[8*8+8];
auto _NS_minus_ops = new _NSfunc2[8*8+8];
auto _NS_mult_ops = new _NSfunc2[8*8+8];

_NS_var _NSadd(_NS_var x, _NS_var y){
    auto adder = _NS_addition_ops[x->type*8+y->type];
    return adder(x,y);
}
_NS_var _NSminus(_NS_var x, _NS_var y){
    auto minus = _NS_minus_ops[x->type*8+y->type];
    return minus(x,y);
}
_NS_var _NSmult(_NS_var x, _NS_var y){
    auto mult = _NS_mult_ops[x->type*8+y->type];
    return mult(x,y);
}






/*  BINARY OPERATIONS (EACH COMBINATION)  */
//+
_NS_var _NS_int_list_adder(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSadd;
    return _NSmap(_NS_create_var(f), y, x);
}

_NS_var _NS_list_int_adder(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSadd;
    return _NSmap(_NS_create_var(f), x, y);
}

_NS_var _NS_std_adder (_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i+y->value->i);
}


//-
_NS_var _NS_int_list_minus(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSminus;
    return _NSmap(_NS_create_var(f), y, x);
}

_NS_var _NS_list_int_minus(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSminus;
    return _NSmap(_NS_create_var(f), x, y);
}

_NS_var _NS_std_minus (_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i-y->value->i);
}



//*
_NS_var _NS_std_mult (_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i*y->value->i);
}





    
int main(){
    //adder
    _NS_addition_ops[8*1+1] = &_NS_std_adder;
    _NS_addition_ops[8*1+4] = &_NS_int_list_adder;
    _NS_addition_ops[8*4+1] = &_NS_list_int_adder;

    //minus


    //mult
    
    std::cout<<"x"<<"\n";

    auto one = _NS_create_var(1);
    std::cout << one->value->i << "\n";
    auto two = _NS_create_var(2);

    auto x = _NSadd(one,two);
    std::cout << x->value->i << "\n";
    
    auto four = _NS_create_var(4);

    x = four;
    std::cout << x->value->i << "\n";

    x = _NS_create_var({four,four,four});
    std::cout << x->value->array->at(1)->value->i << "\n";

    x = _NSadd(one,x);
    std::cout << x->value->array->at(1)->value->i << "\n";

    x = _NSadd(two,x);
    std::cout << x->value->array->at(1)->value->i << "\n";


    auto f = [=](_NS_var x) {
        return [=](_NS_var y) {return _NS_std_mult(x,y);};
    };

    auto newf = f(_NS_create_var(10));
    auto hundred = newf(_NS_create_var(10));
    std::cout << hundred->value->i << "\n";
} 











// NSvar(int i){
//         type = INT;
//         value = new _NS_value{};
//         value->i=i;
//     }
//     NSvar(std::string *s){
//         auto val = new _NS_value{};
//         val->s=s;
//         type = STRING;
//         value = val;
//     }
//     NSvar(bool b){
//         auto val = new _NS_value{};
//         val->b=b;
//         type = BOOL;
//         value = val;
//     }
//     NSvar(std::vector<NSvar> *array){
//         auto val = new _NS_value{};
//         val->array=array;
//         type = ARRAY;
//         value = val;
//     }
//     NSvar(std::initializer_list<NSvar> init){
//         auto val = new _NS_value{};
//         val->array=new std::vector<NSvar>(init.begin(),init.end());
//         type = ARRAY;
//         value = val;
//     }

//     NSvar(_NSfunc f){
//         auto val = new _NS_value{};
//         val->f0 = f;
//         type = FUNCTION0;
//         value = val;
//     }
//     NSvar(_NSunifunc f){
//         auto val = new _NS_value{};
//         val->f1 = f;
//         type = FUNCTION1;
//         value = val;
//     }
//     NSvar(_NSbinfunc f){
//         auto val = new _NS_value{};
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