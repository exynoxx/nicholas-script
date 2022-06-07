Array.prototype._NS_repeat = function(n){
	var tmp = [];
	for (var i=0;i<n;[i++].push.apply(tmp,this));
	return tmp;
}