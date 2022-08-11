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
    return std::shared_ptr(mappedResult)
}

template <typename T>
std::shared_ptr<std::vector<T>> _NS_concat(std::shared_ptr<std::vector<T>> a, std::shared_ptr<std::vector<T>> b)
{
	auto result = new std::vector<T>(a->size()+b->size());
    result.insert(result->begin(), a->begin(), a->end());
    result.insert(result->end(), b->begin(), b->end());
    return std::shared_ptr(result);
}