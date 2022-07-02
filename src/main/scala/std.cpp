#include <iostream>
#include <string>
#include <algorithm>
#include <vector>
#include <cstring>
#include <memory>
#include <variant>



int _NS_print(int x){
	std::cout << x << std::endl;
	/*switch(x.index) {
        case 0:
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
    }*/
	return 0;
}
int _NS_println(int x){
    _NS_print(x);
    std::cout << std::endl;
    return 0;
}

std::vector<int>* map (int (*f)(int),std::vector<int>* array){
	auto nums = new std::vector<int>();
    std::transform(array->begin(), array->end(), std::back_inserter(nums), f);
	return nums;
}
std::vector<int>* map (int (*func)(int),bool (*filter)(int),std::vector<int>* array){
	auto nums = new std::vector<int>();

	for (const &e : array){
		if(filter(e)){
			nums.push_back(func(e));
		}
	}
	
    std::transform(array->begin(), array->end(), std::back_inserter(nums), f);
	return nums;
}
