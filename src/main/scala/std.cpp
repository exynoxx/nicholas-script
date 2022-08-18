#include <iostream>
#include <string>
#include <algorithm>
#include <vector>
#include <cstring>
#include <memory>

template <typename T>
int _NS_len(std::shared_ptr<std::vector<T>> list)
{
    return list->size();
}


template <typename T, typename G>
std::shared_ptr<std::vector<T>> _NS_map_filter(std::shared_ptr<std::vector<T>> list,std::function<G(T)> fmap, std::function<bool(T)> ffilter)
{
    //filter
    std::vector<T> filteredResult;
    std::copy_if(list->begin(), list->end(), std::back_inserter(filteredResult), ffilter);

    //map
    auto mappedResult = new std::vector(filteredResult); //clone
    std::transform(mappedResult->begin(), mappedResult->end(), mappedResult->begin(), fmap);
    return std::shared_ptr<std::vector<T>>(mappedResult);
}

template <typename T, typename G>
std::shared_ptr<std::vector<T>> _NS_map(std::shared_ptr<std::vector<T>> list,std::function<G(T)> fmap)
{
    //map
    auto mappedResult = new std::vector(*list); //clone
    std::transform(mappedResult->begin(), mappedResult->end(), mappedResult->begin(), fmap);
    return std::shared_ptr<std::vector<T>>(mappedResult);
}

template <typename T>
std::shared_ptr<std::vector<T>> _NS_concat(std::shared_ptr<std::vector<T>> a, std::shared_ptr<std::vector<T>> b)
{
	auto result = new std::vector<T>();
    result->insert(result->begin(), a->begin(), a->end());
    result->insert(result->end(), b->begin(), b->end());
    return std::shared_ptr<std::vector<T>>(result);
}

template <typename T>
std::shared_ptr<std::vector<T>> _NS_repeat(std::shared_ptr<std::vector<T>> x, int amount)
{
	auto result = new std::vector<T>((size_t) x->size()+amount);
	for(int i = 0; i < amount; i++){
		for (T elem : *x) {
			result->push_back(elem);
		}
	}
	 return std::shared_ptr<std::vector<T>>(result);
}

std::string _NS_to_string(int i){
    return std::to_string(i);
}
std::string _NS_to_string(bool b){
    if (b){
        return std::string("true");
    } else {
        return std::string("false");
    }
}
