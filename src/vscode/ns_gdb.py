# gdb pretty-printer for NS programs built with `ns build -g` (loaded by the VS Code extension: `source ns_gdb.py`).
# Heap objects (arrays, maps, sets, queues, trees, boxed tuples) are typed `ns_obj *` in the debug info; they print
# the way the program would print them (runtime ns_debug_str renders by the object's descriptor header).
import gdb


class NsObj:
    def __init__(self, val):
        self.val = val

    def to_string(self):
        p = int(self.val)
        if p == 0:
            return "null"
        return gdb.parse_and_eval(f"(const char *)ns_debug_str((void *){p})").string()


def lookup(val):
    t = val.type.strip_typedefs()
    if t.code == gdb.TYPE_CODE_PTR and t.target().strip_typedefs().tag == "ns_obj":
        return NsObj(val)
    return None


gdb.pretty_printers.append(lookup)
