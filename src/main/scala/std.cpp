#include <iostream>
#include <string>
#include <algorithm>
#include <vector>
#include <cstring>
#include <memory>

template <typename T>
int _NS_len(std::shared_ptr<std::vector<T>> list){
    return list->size();
}


template <typename T, typename G>
std::shared_ptr<std::vector<T>> _NS_map_filter(std::function<G(T)> fmap, std::function<bool(T)> ffilter,  std::shared_ptr<std::vector<T>> list){
    
    //filter
    std::vector<T> filteredResult;
    std::copy_if(list->begin(), list->end(), std::back_inserter(filteredResult), ffilter);

    //map
    auto mappedResult = new std::vector(filteredResult); //clone
    //auto unary_op = [](int num) {return std::pow(num, 2);};
    std::transform(mappedResult->begin(), mappedResult->end(), mappedResult->begin(), fmap);
}