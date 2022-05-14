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

_NS_var _NS_create_var(){
    return new _NS_var_struct(VOID,NULL);
}
_NS_var _NS_create_var(int i){
    return new _NS_var_struct(INT,new _NS_value{.i=i});
}
_NS_var _NS_create_var(bool b)
{
    auto value = new _NS_value;
    value->b = b;
    return new _NS_var_struct(BOOL, value);
}
_NS_var _NS_create_var(std::string *s)
{
    auto value = new _NS_value;
    value->s = s;
    return new _NS_var_struct(STRING, value);
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
_NS_var _NS_map1(_NS_var f,_NS_var array)
{
    std::vector<_NS_var> *tmp = new std::vector<_NS_var>(array->value->array->size());
    for (size_t i = 0; i < array->value->array->size(); i++)
        tmp->at(i) = f->value->f1(array->value->array->at(i));
    return _NS_create_var(tmp);
}

_NS_var _NS_map2(_NS_var f,_NS_var array,_NS_var x)
{
    std::vector<_NS_var> *tmp = new std::vector<_NS_var>(array->value->array->size());
    for (size_t i = 0; i < array->value->array->size(); i++)
        tmp->at(i) = f->value->f2(x,array->value->array->at(i));
    return _NS_create_var(tmp);
}

int factorial(int n) {
	if(n > 1)
		return n * factorial(n - 1);
	else
		return 1;
}

_NS_var _NS_fac(_NS_var x)
{
    return _NS_create_var(factorial(x->value->i));
}

_NS_var _NS_boolinv(_NS_var x)
{
    return _NS_create_var(!x->value->b);
}

_NS_var _NS_print(_NS_var x){
	switch(x->type) {
        case INT:
            std::cout << x->value->i;
            break;

        case BOOL:
            x->value->b ? std::cout << "true": std::cout << "false";
            break;

        case STRING:
            std::cout << x->value->s;
            break;

        case ARRAY:
            std::cout << "[";
            for (size_t i = 0; i < x->value->array->size(); i++)
            {
                _NS_print(x->value->array->at(i));
                std::cout << ",";
            }   
            std::cout << "]";
            break;
            
        default:
            std::cout <<  "not printable: "<< x->type;
    }
	return _NS_create_var();
}
_NS_var _NS_println(_NS_var x){
    _NS_print(x);
    std::cout << std::endl;
    return _NS_create_var();
}




/*   EVERY OPERATOR   */
auto _NS_addition_ops = new _NSfunc2[8*8+8];
auto _NS_minus_ops = new _NSfunc2[8*8+8];
auto _NS_mult_ops = new _NSfunc2[8*8+8];
auto _NS_div_ops = new _NSfunc2[8*8+8];

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
_NS_var _NSdiv(_NS_var x, _NS_var y){
    auto div = _NS_mult_ops[x->type*8+y->type];
    return div(x,y);
}
_NS_var _NSmod(_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i%y->value->i);
}



/*  BINARY OPERATIONS (EACH COMBINATION)  */
//+
_NS_var _NS_int_list_adder(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSadd;
    return _NS_map2(_NS_create_var(f), y, x);
}

_NS_var _NS_list_int_adder(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSadd;
    return _NS_map2(_NS_create_var(f), x, y);
}

_NS_var _NS_std_adder (_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i+y->value->i);
}


//-
_NS_var _NS_int_list_minus(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSminus;
    return _NS_map2(_NS_create_var(f), y, x);
}

_NS_var _NS_list_int_minus(_NS_var x, _NS_var y){
    _NSfunc2 f = &_NSminus;
    return _NS_map2(_NS_create_var(f), x, y);
}

_NS_var _NS_std_minus (_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i-y->value->i);
}



//*
_NS_var _NS_std_mult (_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i*y->value->i);
}

_NS_var _NS_list_int_mult(_NS_var x, _NS_var y)
{
    // reserve to optimize performance
    //y->value->i * x->value->array->size()
    auto tmp = new std::vector<_NS_var>();
    for (size_t k = 0; k < y->value->i; k++)
    {
        for (size_t i = 0; i < x->value->array->size(); i++)
        {
            auto elem = x->value->array->at(i);
            switch (elem->type)
            {
                case INT:
                    tmp->push_back(_NS_create_var( elem->value->i));
                    break;
                case BOOL:
                    tmp->push_back(_NS_create_var( elem->value->b));
                    break;
                case STRING:
                    tmp->push_back(_NS_create_var( elem->value->s));
                    break;
                // case ARRAY:
                //     {
                //         auto copy = *elem->value->array;
                //         auto heap_copy = new std::vector<_NS_var>(copy);
                //         tmp->push_back(_NS_create_var(heap_copy));
                //     }
                //     break;
                default:
                    break;
                    // TODO: f0,f1,f2
            }
        }
    }
    return _NS_create_var(tmp);
}
_NS_var _NS_int_list_mult(_NS_var x, _NS_var y)
{
    return _NS_list_int_mult(y, x);
}


// (/)
_NS_var _NS_std_div (_NS_var x, _NS_var y){
    return _NS_create_var((int)x->value->i/y->value->i);
}

//==
_NS_var _NS_eq(_NS_var x, _NS_var y){
    if (x->type!=y->type) return _NS_create_var(false);
    switch (x->type) {
        case INT: return _NS_create_var(x->value->i==y->value->i);
        case BOOL: return _NS_create_var(x->value->b==y->value->b);
        case STRING: return _NS_create_var(x->value->s==y->value->s);
        case ARRAY: return _NS_create_var(x->value->array==y->value->array);
        default: return _NS_create_var(false);
    }
    return _NS_create_var(false);
}
//!=
_NS_var _NS_neq(_NS_var x, _NS_var y){
    if (x->type!=y->type) return _NS_create_var(true);
    switch (x->type) {
        case INT: return _NS_create_var(x->value->i!=y->value->i);
        case BOOL: return _NS_create_var(x->value->b!=y->value->b);
        case STRING: return _NS_create_var(x->value->s!=y->value->s);
        case ARRAY: return _NS_create_var(x->value->array!=y->value->array);
        default: return _NS_create_var(true);
    }
    return _NS_create_var(true);
}
//<=
_NS_var _NS_le(_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i <= y->value->i);
}
//>=
_NS_var _NS_ge(_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i >= y->value->i);
}
//<
_NS_var _NS_lt(_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i < y->value->i);
}
//>
_NS_var _NS_gt(_NS_var x, _NS_var y){
    return _NS_create_var(x->value->i > y->value->i);
}


struct slab{
    int free_stack_head;
    int *free_stack;
    _NS_struct *memory_base;
}

_NS_var alloc(){
    //TODO: if full alloc new slab. if partial slab exist. take from that.
    return memory_base + free_list_stack[free_stack_head--];
}
void free(_NS_var block) {
    free_list_stack[++free_stack_head] = block - memory_base;
}