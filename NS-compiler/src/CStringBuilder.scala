class CStringBuilder(var pre: String, var code: String) {
    def +(that: CStringBuilder) = new CStringBuilder("",that.pre+that.code+pre+code)
}
