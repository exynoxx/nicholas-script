#include <iostream>
#include <string>
#include <algorithm>
#include <vector>
#include <cstring>
#include <memory>


/*   FUNDAMENTAL ENTITIES   */

struct _NS_var_struct;
enum _NS_enum {VOID, INT,STRING,BOOL,ARRAY,FUNCTION0,FUNCTION1,FUNCTION2};

typedef std::shared_ptr<_NS_var_struct> _NS_var;
typedef _NS_var (*_NSfunc2)(_NS_var x, _NS_var y);
typedef _NS_var (*_NSfunc1)(_NS_var x);
typedef _NS_var (*_NSfunc0)();

struct _NS_var_struct{
    _NS_enum type;

    union {
        int i;
        std::string *s;
        bool b;
        std::vector<_NS_var> *array;
        _NSfunc0 f0;
        _NSfunc1 f1;
        _NSfunc2 f2;
    };

    _NS_var_struct(){
        type=VOID;
    }

    _NS_var_struct(_NS_enum t, int i){
        type=t;
        this->i = i;
    }

    _NS_var_struct(_NS_enum t, bool b){
        type=t;
        this->b = b;
    }
    _NS_var_struct(_NS_enum t, std::string *s){
        type=t;
        this->s = s;
    }
    _NS_var_struct(_NS_enum t, std::vector<_NS_var> *a){
        type=t;
        this->array = a;
    }
    _NS_var_struct(_NS_enum t, _NSfunc0 f){
        type=t;
        this->f0 = f;
    }
    _NS_var_struct(_NS_enum t, _NSfunc1 f){
        type=t;
        this->f1 = f;
    }
    _NS_var_struct(_NS_enum t, _NSfunc2 f){
        type=t;
        this->f2 = f;
    }
    
    ~_NS_var_struct(){
        //std::cout<<"destructing:"<<i<<std::endl;
        //if (type == ARRAY) delete array;
    }
};


//TODO: add empty, partial, full
struct slab{
    int free_stack_head;
    int *free_stack;
    _NS_var_struct *memory_base;
    int num_allocs = 0;

    slab(int size){
        free_stack_head = 0;
        free_stack = new int[size];
        memory_base = new _NS_var_struct[size];
        for (int i = 0; i < size; i++)
        {
            free_stack[i] = i;
        }
        std::cout<<"Memory manager initialized"<<std::endl;
        
    }
    //x=base+idx*size
    //idx=x-base/sizes
    _NS_var_struct *alloc(){
        //TODO: if full alloc new slab. if partial slab exist. take from that.
        return memory_base + free_stack[free_stack_head++];
    }   
    void free(_NS_var_struct *block) {
        free_stack[--free_stack_head] = (block - memory_base);
    }
    
};
slab memory_manager(10000);
auto deleter = [](_NS_var_struct *p) { memory_manager.free(p); };

//std::shared_ptr<MyType> sp(new int[10], [](int *p) { delete[] p; });
_NS_var _NS_create_var(){
    return std::shared_ptr<_NS_var_struct>();
}
_NS_var _NS_create_var(int i){
    return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(INT,i),deleter);
}
_NS_var _NS_create_var(bool b)
{
    return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(BOOL,b),deleter);
}
_NS_var _NS_create_var(std::string *s)
{
    return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(STRING, s),deleter);
}
_NS_var _NS_create_var(std::vector<_NS_var> *a){
    return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(ARRAY,a),deleter);
}
_NS_var _NS_create_var(std::initializer_list<_NS_var> init){
   return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(ARRAY,new std::vector<_NS_var>(init.begin(),init.end())),deleter);
}
_NS_var _NS_create_var(_NSfunc0 f){
   return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(FUNCTION0,f),deleter);
}
_NS_var _NS_create_var(_NSfunc1 f){
    return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(FUNCTION1,f),deleter);
}
_NS_var _NS_create_var(_NSfunc2 f){
   return std::shared_ptr<_NS_var_struct>(new(memory_manager.alloc()) _NS_var_struct(FUNCTION2,f),deleter);
}


/*   MISC   */
_NS_var _NS_map1(_NS_var f,_NS_var array)
{
    std::vector<_NS_var> *tmp = new std::vector<_NS_var>(array->array->size());
    for (size_t i = 0; i < array->array->size(); i++)
        tmp->at(i) = f->f1(array->array->at(i));
    return _NS_create_var(tmp);
}

_NS_var _NS_map2(_NS_var f,_NS_var array,_NS_var x)
{
    std::vector<_NS_var> *tmp = new std::vector<_NS_var>(array->array->size());
    for (size_t i = 0; i < array->array->size(); i++)
        tmp->at(i) = f->f2(x,array->array->at(i));
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
    return _NS_create_var(factorial(x->i));
}

_NS_var _NS_boolinv(_NS_var x)
{
    return _NS_create_var(!x->b);
}

_NS_var _NS_print(_NS_var x){
	switch(x->type) {
        case INT:
            std::cout << x->i;
            break;

        case BOOL:
            x->b ? std::cout << "true": std::cout << "false";
            break;

        case STRING:
            std::cout << x->s;
            break;

        case ARRAY:
            std::cout << "[";
            for (size_t i = 0; i < x->array->size(); i++)
            {
                _NS_print(x->array->at(i));
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
    return _NS_create_var(x->i%y->i);
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
    return _NS_create_var(x->i+y->i);
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
    return _NS_create_var(x->i-y->i);
}



//
_NS_var _NS_std_mult (_NS_var x, _NS_var y){
    return _NS_create_var(x->i*y->i);
}

_NS_var _NS_list_int_mult(_NS_var x, _NS_var y)
{
    // reserve to optimize performance
    //y->i * x->array->size()
    auto tmp = new std::vector<_NS_var>();
    for (size_t k = 0; k < y->i; k++)
    {
        for (size_t i = 0; i < x->array->size(); i++)
        {
            auto elem = x->array->at(i);
            switch (elem->type)
            {
                case INT:
                    tmp->push_back(_NS_create_var( elem->i));
                    break;
                case BOOL:
                    tmp->push_back(_NS_create_var( elem->b));
                    break;
                case STRING:
                    tmp->push_back(_NS_create_var( elem->s));
                    break;
                // case ARRAY:
                //     {
                //         auto copy = *elem->array;
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
    return _NS_create_var((int)x->i/y->i);
}

//==
_NS_var _NS_eq(_NS_var x, _NS_var y){
    if (x->type!=y->type) return _NS_create_var(false);
    switch (x->type) {
        case INT: return _NS_create_var(x->i==y->i);
        case BOOL: return _NS_create_var(x->b==y->b);
        case STRING: return _NS_create_var(x->s==y->s);
        case ARRAY: return _NS_create_var(x->array==y->array);
        default: return _NS_create_var(false);
    }
    return _NS_create_var(false);
}
//!=
_NS_var _NS_neq(_NS_var x, _NS_var y){
    if (x->type!=y->type) return _NS_create_var(true);
    switch (x->type) {
        case INT: return _NS_create_var(x->i!=y->i);
        case BOOL: return _NS_create_var(x->b!=y->b);
        case STRING: return _NS_create_var(x->s!=y->s);
        case ARRAY: return _NS_create_var(x->array!=y->array);
        default: return _NS_create_var(true);
    }
    return _NS_create_var(true);
}
//<=
_NS_var _NS_le(_NS_var x, _NS_var y){
    return _NS_create_var(x->i <= y->i);
}
//>=
_NS_var _NS_ge(_NS_var x, _NS_var y){
    return _NS_create_var(x->i >= y->i);
}
//<
_NS_var _NS_lt(_NS_var x, _NS_var y){
    return _NS_create_var(x->i < y->i);
}
//>
_NS_var _NS_gt(_NS_var x, _NS_var y){
    return _NS_create_var(x->i > y->i);
}
