function _NS_repeat(array, n){
	var tmp = [];
	for (var i=0;i<n;[i++].push.apply(tmp,array));
	return tmp;
}
