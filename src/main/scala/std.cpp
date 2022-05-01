template<NSvar, NSvar>
auto _NSmap(NSvar f, NSvar array)
{
    std::vector<NSvar> tmp;
    tmp.resize(array.size());
    transform(array.begin(), array.end(), tmp.begin(), f.value.f1);
    return tmp;
}