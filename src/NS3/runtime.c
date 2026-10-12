// NS3 runtime. Values cross the ABI as int64_t "slots" (ints as-is, doubles
// bitcast, bools 0/1, pointers cast). Type-dependent operations are driven by
// descriptor strings: I F S B, A<t>, M<k><v>, H<t>, Q<t>, P<t>, U<n><t1>..<tn> (tuple, n one digit).
// Heap objects (strings, arrays, maps, sets, Q, PQ) carry a 16-byte header before the
// pointer: {rc, full descriptor}. rc < 0 = immortal. Arguments are borrowed, returned
// pointers are owned (+1). Containers retain what they store and release what they drop.
#include <stdint.h>
#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

typedef int64_t slot;
typedef struct { int64_t len, cap; slot *data; } Arr;
typedef struct Map { int64_t len, cap, used; const char *kd, *vd; slot *keys, *vals; char *st; } Map; // st: 0 empty, 1 full, 2 tomb; vd NULL = set
typedef struct Node { slot v; struct Node *next; } Node;
typedef struct { int64_t len; Node *head, *tail; const char *d; } Queue;
typedef struct { int64_t len, cap; slot *data; const char *d; } PQ; // prefix-compatible with Arr

void ns_panic(const char *msg) { fprintf(stderr, "error: %s\n", msg); exit(1); }
static void *alloc(size_t n) { void *p = calloc(1, n ? n : 1); if (!p) ns_panic("out of memory"); return p; }
static void *grow(void *p, size_t n) { p = realloc(p, n ? n : 1); if (!p) ns_panic("out of memory"); return p; }
static double f_of(slot v) { double d; memcpy(&d, &v, 8); return d; }
static slot s_of(double d) { slot v; memcpy(&v, &d, 8); return v; }
static char *S(slot v) { return (char *)(intptr_t)v; }
static slot P(const void *p) { return (slot)(intptr_t)p; }

typedef struct { int64_t rc; const char *d; } Hdr;
#define HD(p) ((Hdr *)(p) - 1)
static void *obj(const char *d, size_t n) { Hdr *h = alloc(sizeof(Hdr) + n); h->rc = 1; h->d = d; return h + 1; }
static struct { Hdr h; char s[1]; } empty = {{-1, "S"}, ""}; // immortal ""
static inline int ptrk(const char *d) { // pointer kinds: S A M H Q P U (I F B are plain slots)
  switch (*d) { case 'S': case 'A': case 'M': case 'H': case 'Q': case 'P': case 'U': return 1; default: return 0; }
}
void ns_retain(void *p) { if (p && HD(p)->rc >= 0) HD(p)->rc++; }
void ns_release(void *p);
static slot ret(const char *d, slot v) { if (ptrk(d)) ns_retain(S(v)); return v; } // retain slot if pointer kind
static void rel(const char *d, slot v) { if (ptrk(d)) ns_release(S(v)); }

// Returns a pointer just past the descriptor starting at d.
static const char *skip(const char *d) {
  switch (*d) {
  case 'A': case 'H': case 'Q': case 'P': return skip(d + 1);
  case 'M': return skip(skip(d + 1));
  case 'U': { int n = d[1] - '0'; for (d += 2; n--;) d = skip(d); return d; }
  default: return d + 1;
  }
}
static const char *tel(const char *d, int64_t i) { for (d += 2; i--;) d = skip(d); return d; } // tuple element i's descriptor
// Interned descriptor c + <descriptor at d>; never freed.
static const char *intern(char c, const char *d) {
  static struct It { struct It *next; char s[]; } *list;
  size_t n = skip(d) - d;
  for (struct It *it = list; it; it = it->next)
    if (it->s[0] == c && !strncmp(it->s + 1, d, n) && !it->s[n + 1]) return it->s;
  struct It *it = alloc(sizeof *it + n + 2);
  it->s[0] = c; memcpy(it->s + 1, d, n); it->next = list; list = it;
  return it->s;
}

// ---------- arrays ----------
void *ns_arr_new(const char *d, int64_t n) { // empty array with capacity n (len 0)
  int64_t cap = n > 4 ? n : 4;
  Arr *a = obj(d, sizeof(Arr) + cap * sizeof(slot)); // data inline: one allocation per array
  a->cap = cap;
  a->data = (slot *)(a + 1);
  return a;
}
static void apush(Arr *a, slot x) { // no retain: x's reference moves into a
  if (a->len == a->cap) { // move out of the inline area on first growth
    slot *d = a->data == (slot *)(a + 1) ? memcpy(alloc(a->cap * 2 * sizeof(slot)), a->data, a->cap * sizeof(slot))
                                          : grow(a->data, a->cap * 2 * sizeof(slot));
    a->data = d; a->cap *= 2;
  }
  a->data[a->len++] = x;
}
void ns_arr_push(void *p, slot x) { apush(p, ret(HD(p)->d + 1, x)); }
static int64_t idx(int64_t i, int64_t n) {
  if (i < 0) i += n;
  if (i < 0 || i >= n) ns_panic("index out of range");
  return i;
}
slot ns_arr_get(void *p, int64_t i) { Arr *a = p; return ret(HD(p)->d + 1, a->data[idx(i, a->len)]); }
void ns_arr_set(void *p, int64_t i, slot x) {
  Arr *a = p; const char *ed = HD(p)->d + 1; slot *e = &a->data[idx(i, a->len)];
  ret(ed, x); rel(ed, *e); *e = x;
}
static void clamp(int64_t *a, int64_t *b, int64_t n) {
  if (*a < 0) *a += n;
  if (*b < 0) *b += n;
  *a = *a < 0 ? 0 : *a > n ? n : *a;
  *b = *b < *a ? *a : *b > n ? n : *b;
}
void *ns_arr_slice(void *p, int64_t i, int64_t j) {
  Arr *a = p;
  clamp(&i, &j, a->len);
  Arr *r = ns_arr_new(HD(p)->d, j - i);
  for (int64_t k = i; k < j; k++) ns_arr_push(r, a->data[k]);
  return r;
}
void *ns_arr_copy(void *p) { // deep for nested arrays (value semantics), retains everything else
  Arr *a = p;
  if (HD(p)->d[1] != 'A') return ns_arr_slice(p, 0, a->len);
  Arr *r = ns_arr_new(HD(p)->d, a->len);
  for (int64_t k = 0; k < a->len; k++) apush(r, P(ns_arr_copy(S(a->data[k]))));
  return r;
}
void *ns_arr_repeat(const char *d, slot x, int64_t n) { // d: array descriptor; nested arrays are deep-copied per row
  Arr *r = ns_arr_new(d, n);
  for (int64_t k = 0; k < n; k++) if (d[1] == 'A') apush(r, P(ns_arr_copy(S(x)))); else ns_arr_push(r, x);
  return r;
}

// ---------- tuples (Arr layout, len = n, data inline, header desc "U<n>...") ----------
void *ns_tup_new(const char *d) {
  int64_t n = d[1] - '0';
  Arr *t = obj(d, sizeof(Arr) + n * sizeof(slot));
  t->len = t->cap = n; t->data = (slot *)(t + 1);
  return t;
}
void ns_tup_set(void *t, int64_t i, slot x) { ((Arr *)t)->data[i] = ret(tel(HD(t)->d, i), x); } // construction only
slot ns_tup_get(void *t, int64_t i) { return ret(tel(HD(t)->d, i), ((Arr *)t)->data[i]); }
void *ns_arr_concat(void *p, void *q) {
  Arr *r = ns_arr_copy(p), *b = q;
  for (int64_t k = 0; k < b->len; k++) ns_arr_push(r, b->data[k]);
  return r;
}
void *ns_range(int64_t a, int64_t b) {
  Arr *r = ns_arr_new("AI", a < b ? b - a : a - b);
  if (a <= b) for (int64_t i = a; i < b; i++) ns_arr_push(r, i);
  else for (int64_t i = a; i > b; i--) ns_arr_push(r, i);
  return r;
}
//@ rev ([T]) [T]
void *ns_rev(const char *dT, void *p) {
  Arr *a = p, *r = ns_arr_new(HD(p)->d, a->len);
  for (int64_t k = a->len - 1; k >= 0; k--) ns_arr_push(r, a->data[k]);
  return r;
}

// ---------- generic eq / cmp / hash ----------
void *ns_toarr(const char *d, slot coll);
typedef struct Map Map;
static int64_t find(Map *m, slot k);

int64_t ns_cmp(const char *d, slot a, slot b) {
  switch (*d) {
  case 'F': { double x = f_of(a), y = f_of(b); return (x > y) - (x < y); }
  case 'S': { int c = strcmp(S(a), S(b)); return (c > 0) - (c < 0); }
  case 'A': {
    Arr *x = (Arr *)S(a), *y = (Arr *)S(b);
    for (int64_t i = 0; i < x->len && i < y->len; i++) {
      int64_t c = d[1] == 'I' ? (x->data[i] > y->data[i]) - (x->data[i] < y->data[i]) : ns_cmp(d + 1, x->data[i], y->data[i]);
      if (c) return c;
    }
    return (x->len > y->len) - (x->len < y->len);
  }
  case 'U': {
    Arr *x = (Arr *)S(a), *y = (Arr *)S(b); const char *e = d + 2;
    for (int64_t i = 0; i < x->len; i++, e = skip(e)) { int64_t c = ns_cmp(e, x->data[i], y->data[i]); if (c) return c; }
    return 0;
  }
  default: return (a > b) - (a < b); // I, B
  }
}

int64_t ns_eq(const char *d, slot a, slot b) {
  switch (*d) {
  case 'F': return f_of(a) == f_of(b);
  case 'S': return strcmp(S(a), S(b)) == 0;
  case 'A': {
    Arr *x = (Arr *)S(a), *y = (Arr *)S(b);
    if (x->len != y->len) return 0;
    for (int64_t i = 0; i < x->len; i++) if (!ns_eq(d + 1, x->data[i], y->data[i])) return 0;
    return 1;
  }
  case 'U': {
    Arr *x = (Arr *)S(a), *y = (Arr *)S(b); const char *e = d + 2;
    for (int64_t i = 0; i < x->len; i++, e = skip(e)) if (!ns_eq(e, x->data[i], y->data[i])) return 0;
    return 1;
  }
  case 'M': case 'H': {
    Map *x = (Map *)S(a), *y = (Map *)S(b);
    if (x->len != y->len) return 0;
    for (int64_t i = 0; i < x->cap; i++) {
      int64_t j = x->st[i] == 1 ? find(y, x->keys[i]) : 0;
      if (j < 0 || (x->st[i] == 1 && x->vd && !ns_eq(x->vd, x->vals[i], y->vals[j]))) return 0;
    }
    return 1;
  }
  case 'Q': case 'P': { // compare contents in iteration order
    void *x = ns_toarr(d, a), *y = ns_toarr(d, b);
    int64_t r = ns_eq(HD(x)->d, P(x), P(y));
    ns_release(x); ns_release(y);
    return r;
  }
  default: return a == b;
  }
}

static uint64_t mix(uint64_t h) { h ^= h >> 33; h *= 0xff51afd7ed558ccdULL; h ^= h >> 33; return h; }
static uint64_t hash(const char *d, slot v) {
  switch (*d) {
  case 'F': { double x = f_of(v); return mix(x == 0 ? 0 : (uint64_t)s_of(x)); } // -0.0 == 0.0
  case 'S': { uint64_t h = 1469598103934665603ULL; for (char *s = S(v); *s; s++) h = (h ^ (unsigned char)*s) * 1099511628211ULL; return h; }
  case 'A': { Arr *a = (Arr *)S(v); uint64_t h = 7; for (int64_t i = 0; i < a->len; i++) h = h * 31 + hash(d + 1, a->data[i]); return mix(h); }
  case 'U': { Arr *a = (Arr *)S(v); uint64_t h = 11; const char *e = d + 2; for (int64_t i = 0; i < a->len; i++, e = skip(e)) h = h * 31 + hash(e, a->data[i]); return mix(h); }
  default: return mix((uint64_t)v);
  }
}

// ---------- map / set (open addressing, linear probing) ----------
void *ns_map_new(const char *d) { // d = "M<k><v>" or "H<k>" (set: vd NULL)
  Map *m = obj(d, sizeof(Map));
  m->kd = d + 1; m->vd = *d == 'M' ? skip(d + 1) : NULL; m->cap = 8;
  m->keys = alloc(8 * sizeof(slot)); m->vals = alloc(8 * sizeof(slot)); m->st = alloc(8);
  return m;
}
void *ns_set_new(const char *d) { return ns_map_new(d); }
static int64_t find(Map *m, slot k) {
  for (uint64_t i = hash(m->kd, k) & (m->cap - 1);; i = (i + 1) & (m->cap - 1)) {
    if (m->st[i] == 0) return -1;
    if (m->st[i] == 1 && ns_eq(m->kd, m->keys[i], k)) return (int64_t)i;
  }
}
static void insert(Map *m, slot k, slot v) { // k not present; no retain
  if ((m->used + 1) * 2 > m->cap) { // rehash (also drops tombstones)
    Map old = *m;
    for (m->cap = 8; m->cap < m->len * 4; m->cap *= 2) {}
    m->keys = alloc(m->cap * sizeof(slot)); m->vals = alloc(m->cap * sizeof(slot)); m->st = alloc(m->cap);
    m->len = m->used = 0;
    for (int64_t j = 0; j < old.cap; j++) if (old.st[j] == 1) insert(m, old.keys[j], old.vals[j]);
    free(old.keys); free(old.vals); free(old.st);
  }
  uint64_t j = hash(m->kd, k) & (m->cap - 1);
  while (m->st[j] == 1) j = (j + 1) & (m->cap - 1);
  if (m->st[j] == 0) m->used++;
  m->st[j] = 1; m->keys[j] = k; m->vals[j] = v; m->len++;
}
void ns_map_set(void *p, slot k, slot v) {
  Map *m = p;
  int64_t i = find(m, k);
  if (m->vd) ret(m->vd, v);
  if (i >= 0) { if (m->vd) rel(m->vd, m->vals[i]); m->vals[i] = v; return; } // keep existing key
  insert(m, ret(m->kd, k), v);
}
void *ns_q_new(const char *d);
void *ns_pq_new(const char *d);
static slot zero(const char *d) {
  switch (*d) {
  case 'S': return P(empty.s);
  case 'A': return P(ns_arr_new(d, 0));
  case 'M': case 'H': return P(ns_map_new(d));
  case 'Q': return P(ns_q_new(d));
  case 'P': return P(ns_pq_new(d));
  case 'U': { void *t = ns_tup_new(d); const char *e = d + 2; for (int64_t i = 0; i < d[1] - '0'; i++, e = skip(e)) ((Arr *)t)->data[i] = zero(e); return P(t); }
  default: return 0;
  }
}
slot ns_map_get(void *p, slot k) {
  Map *m = p;
  int64_t i = find(m, k);
  return !m->vd ? 0 : i >= 0 ? ret(m->vd, m->vals[i]) : zero(m->vd);
}
void *ns_map_vals(void *p) {
  Map *m = p; Arr *r = ns_arr_new(intern('A', m->vd), m->len);
  for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) ns_arr_push(r, m->vals[i]);
  return r;
}
void ns_map_merge(void *p, void *q) {
  Map *n = q;
  for (int64_t i = 0; i < n->cap; i++) if (n->st[i] == 1) ns_map_set(p, n->keys[i], n->vals[i]);
}

// ---------- Q (linked list) and PQ (binary min-heap) ----------
void *ns_q_new(const char *d) { Queue *q = obj(d, sizeof(Queue)); q->d = d + 1; return q; } // q->d: element descriptor
void *ns_pq_new(const char *d) { PQ *q = obj(d, sizeof(PQ)); q->d = d + 1; return q; }
static void swap(slot *a, slot *b) { slot t = *a; *a = *b; *b = t; }
static void pq_push(PQ *q, slot x) { // no retain
  if (q->len == q->cap) q->data = grow(q->data, (q->cap = q->cap ? q->cap * 2 : 8) * sizeof(slot));
  int64_t i = q->len++;
  q->data[i] = x;
  for (; i && ns_cmp(q->d, q->data[i], q->data[(i - 1) / 2]) < 0; i = (i - 1) / 2) swap(&q->data[i], &q->data[(i - 1) / 2]);
}
static slot pq_pop(PQ *q) {
  slot top = q->data[0];
  q->data[0] = q->data[--q->len];
  for (int64_t i = 0;;) {
    int64_t l = 2 * i + 1, r = l + 1, m = i;
    if (l < q->len && ns_cmp(q->d, q->data[l], q->data[m]) < 0) m = l;
    if (r < q->len && ns_cmp(q->d, q->data[r], q->data[m]) < 0) m = r;
    if (m == i) break;
    swap(&q->data[i], &q->data[m]); i = m;
  }
  return top;
}
static slot top(const char *d, void *coll) {
  if (!*(int64_t *)coll) ns_panic("empty queue");
  return *d == 'Q' ? ((Queue *)coll)->head->v : ((PQ *)coll)->data[0];
}
slot ns_peek(const char *d, void *coll) { return ret(d + 1, top(d, coll)); }
slot ns_pop(const char *d, void *coll) { // transfers the container's reference
  slot v = top(d, coll);
  if (*d == 'P') return pq_pop(coll);
  Queue *q = coll; Node *n = q->head;
  q->head = n->next; free(n);
  if (!--q->len) q->tail = NULL;
  return v;
}

// ---------- strings ----------
static char *str_n(const char *s, size_t n) { char *r = obj("S", n + 1); memcpy(r, s, n); return r; }
char *ns_concat(char *a, char *b) {
  size_t n = strlen(a), m = strlen(b);
  char *r = obj("S", n + m + 1); memcpy(r, a, n); memcpy(r + n, b, m);
  return r;
}
char *ns_repeat(char *s, int64_t k) {
  size_t n = strlen(s);
  if (k < 0) k = 0;
  char *r = obj("S", n * k + 1);
  for (int64_t i = 0; i < k; i++) memcpy(r + i * n, s, n);
  return r;
}
char *ns_str_at(char *s, int64_t i) { return str_n(s + idx(i, strlen(s)), 1); }
char *ns_slice_s(char *s, int64_t i, int64_t j) { clamp(&i, &j, strlen(s)); return str_n(s + i, j - i); }
//@ chars (str) [str]
void *ns_chars(char *s) {
  Arr *r = ns_arr_new("AS", strlen(s));
  for (; *s; s++) apush(r, P(str_n(s, 1)));
  return r;
}
//@ split (str,str) [str]
void *ns_split(char *s, char *sep) {
  size_t m = strlen(sep);
  if (!m) return ns_chars(s);
  Arr *r = ns_arr_new("AS", 4);
  for (char *e; (e = strstr(s, sep)); s = e + m) apush(r, P(str_n(s, e - s)));
  apush(r, P(str_n(s, strlen(s))));
  return r;
}
//@ lower (str) str
char *ns_lower(char *s) { char *r = str_n(s, strlen(s)); for (char *c = r; *c; c++) if (*c >= 'A' && *c <= 'Z') *c += 32; return r; }
//@ upper (str) str
char *ns_upper(char *s) { char *r = str_n(s, strlen(s)); for (char *c = r; *c; c++) if (*c >= 'a' && *c <= 'z') *c -= 32; return r; }
//@ ord (str) int
int64_t ns_ord(char *s) { return (unsigned char)s[0]; }
//@ chr (int) str
char *ns_chr(int64_t c) { char ch = (char)c; return str_n(&ch, 1); }
int64_t ns_s2i(char *s) { return strtoll(s, NULL, 10); } // skips space, longest prefix, 0 if none
double ns_s2f(char *s) { return strtod(s, NULL); }
//@ read () str
char *ns_read(void) {
  char *line = NULL; size_t cap = 0;
  ssize_t n = getline(&line, &cap, stdin);
  if (n > 0 && line[n - 1] == '\n') n--;
  char *r = n > 0 ? str_n(line, n) : empty.s;
  free(line);
  return r;
}

// All remaining stdin lines (without '\n').
//@ lines () [str]
void *ns_lines(void) {
  Arr *r = ns_arr_new("AS", 16);
  char *line = NULL; size_t cap = 0; ssize_t n;
  while ((n = getline(&line, &cap, stdin)) > 0) {
    if (line[n - 1] == '\n') n--;
    apush(r, P(str_n(line, n)));
  }
  free(line);
  return r;
}

// ---------- tostr / print / join ----------
typedef struct { char *p; size_t n, cap; } Buf;
static void put(Buf *b, const char *s) {
  size_t m = strlen(s);
  if (b->n + m + 1 > b->cap) b->p = grow(b->p, b->cap = (b->n + m + 1) * 2);
  memcpy(b->p + b->n, s, m + 1); b->n += m;
}
static char *done(Buf *b) { char *r = str_n(b->p ? b->p : "", b->n); free(b->p); return r; }
static void fmt_float(char *out, double x) {
  for (int prec = 15; prec <= 17; prec++) {
    snprintf(out, 32, "%.*g", prec, x);
    if (strtod(out, NULL) == x) break;
  }
  if (isfinite(x) && !strpbrk(out, ".e")) strcat(out, ".0");
}
static void tostr(Buf *b, const char *d, slot v);
static void items(Buf *b, const char *open, const char *d, Arr *a) {
  put(b, open);
  for (int64_t i = 0; i < a->len; i++) { if (i) put(b, ","); tostr(b, d, a->data[i]); }
  put(b, "]");
}
static void citems(Buf *b, const char *open, const char *d, slot v) { // via a temp array
  Arr *a = ns_toarr(d, v); items(b, open, d + 1, a); ns_release(a);
}
static void tostr(Buf *b, const char *d, slot v) {
  char tmp[40];
  switch (*d) {
  case 'I': snprintf(tmp, sizeof tmp, "%lld", (long long)v); put(b, tmp); break;
  case 'F': fmt_float(tmp, f_of(v)); put(b, tmp); break;
  case 'B': put(b, v ? "T" : "F"); break;
  case 'S': put(b, S(v)); break;
  case 'A': items(b, "[", d + 1, (Arr *)S(v)); break;
  case 'U': {
    Arr *a = (Arr *)S(v); const char *e = d + 2;
    put(b, "(");
    for (int64_t i = 0; i < a->len; i++, e = skip(e)) { if (i) put(b, ","); tostr(b, e, a->data[i]); }
    put(b, ")");
    break;
  }
  case 'H': citems(b, "%[", d, v); break;
  case 'Q': citems(b, "Q[", d, v); break;
  case 'P': citems(b, "PQ[", d, v); break;
  case 'M': {
    Map *m = (Map *)S(v); int first = 1;
    put(b, "[");
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) {
      if (!first) put(b, ",");
      first = 0;
      tostr(b, m->kd, m->keys[i]); put(b, "->"); tostr(b, m->vd, m->vals[i]);
    }
    put(b, m->len ? "]" : "->]");
    break;
  }
  default: ns_panic("bad type descriptor");
  }
}
char *ns_tostr(const char *d, slot v) { Buf b = {0}; tostr(&b, d, v); return done(&b); }
void ns_print(const char *d, slot v) { Buf b = {0}; tostr(&b, d, v); puts(b.p ? b.p : ""); free(b.p); }
//@ join ([T],str) str
char *ns_join(const char *elemd, void *arr, char *sep) {
  Arr *a = arr; Buf b = {0};
  for (int64_t i = 0; i < a->len; i++) { if (i) put(&b, sep); tostr(&b, elemd, a->data[i]); }
  return done(&b);
}

// ---------- sort and generic collection ops ----------
static const char *sort_d; // qsort has no context argument
static int sort_cmp(const void *a, const void *b) { return (int)ns_cmp(sort_d, *(const slot *)a, *(const slot *)b); }
static Arr *sort_in(const char *elemd, Arr *r) { sort_d = elemd; qsort(r->data, r->len, sizeof(slot), sort_cmp); return r; }
//@ sort ([T]) [T]
void *ns_sort(const char *elemd, void *arr) { return sort_in(elemd, ns_arr_copy(arr)); }

int64_t ns_len(const char *d, slot v) { return *d == 'S' ? (int64_t)strlen(S(v)) : *(int64_t *)S(v); }

void *ns_toarr(const char *d, slot coll) {
  void *c = (void *)S(coll);
  switch (*d) {
  case 'A': return ns_arr_copy(c);
  case 'S': return ns_chars(c);
  case 'M': case 'H': {
    Map *m = c; Arr *r = ns_arr_new(intern('A', m->kd), m->len);
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) ns_arr_push(r, m->keys[i]);
    return r;
  }
  case 'Q': {
    Arr *r = ns_arr_new(intern('A', d + 1), ((Queue *)c)->len);
    for (Node *n = ((Queue *)c)->head; n; n = n->next) ns_arr_push(r, n->v);
    return r;
  }
  case 'P': {
    PQ *q = c; Arr *r = ns_arr_new(intern('A', d + 1), q->len);
    for (int64_t i = 0; i < q->len; i++) ns_arr_push(r, q->data[i]);
    return sort_in(d + 1, r);
  }
  default: ns_panic("not a collection"); return NULL;
  }
}

int64_t ns_find(const char *d, slot coll, slot x) { // A: index of first equal element, S: byte index of substring; -1 if absent
  if (*d == 'S') { char *e = strstr(S(coll), S(x)); return e ? e - S(coll) : -1; }
  Arr *a = (Arr *)S(coll);
  for (int64_t i = 0; i < a->len; i++) if (ns_eq(d + 1, a->data[i], x)) return i;
  return -1;
}

int64_t ns_has(const char *d, slot coll, slot x) {
  switch (*d) {
  case 'S': return strstr(S(coll), S(x)) != NULL;
  case 'M': case 'H': return find((Map *)S(coll), x) >= 0;
  case 'A': return ns_find(d, coll, x) >= 0; // no (deep) copy
  default: { // Q, P: linear scan
    Arr *a = ns_toarr(d, coll); int64_t r = 0;
    for (int64_t i = 0; i < a->len && !r; i++) r = ns_eq(d + 1, a->data[i], x);
    ns_release(a);
    return r;
  }
  }
}

void ns_add(const char *d, void *coll, slot x) {
  switch (*d) {
  case 'H': ns_map_set(coll, x, 0); break;
  case 'P': pq_push(coll, ret(d + 1, x)); break;
  case 'A': ns_arr_push(coll, x); break;
  case 'Q': {
    Queue *q = coll; Node *n = alloc(sizeof(Node));
    n->v = ret(d + 1, x);
    if (q->tail) q->tail->next = n; else q->head = n;
    q->tail = n; q->len++;
    break;
  }
  default: ns_panic("cannot add to this type");
  }
}

void ns_del(const char *d, void *coll, slot x) {
  (void)d; // set and map share the layout
  Map *m = coll;
  int64_t i = find(m, x);
  if (i < 0) return;
  m->st[i] = 2; m->len--;
  rel(m->kd, m->keys[i]);
  if (m->vd) rel(m->vd, m->vals[i]);
}

//@ pow (int,int) int = ns_ipow
int64_t ns_ipow(int64_t base, int64_t e) {
  if (e < 0) return 0;
  uint64_t r = 1, b = (uint64_t)base; // unsigned: wraparound instead of UB
  for (; e; e >>= 1, b *= b) if (e & 1) r *= b;
  return r;
}

// Floor division/modulo. b == -1 is special-cased: INT64_MIN / -1 traps in C.
int64_t ns_idiv(int64_t a, int64_t b) {
  if (!b) ns_panic("division by zero");
  if (b == -1) return (int64_t)(0 - (uint64_t)a);
  int64_t q = a / b;
  return (a % b && (a < 0) != (b < 0)) ? q - 1 : q;
}
int64_t ns_imod(int64_t a, int64_t b) {
  if (!b) ns_panic("division by zero");
  if (b == -1) return 0;
  int64_t r = a % b;
  return (r && (r < 0) != (b < 0)) ? r + b : r;
}

// Adds every element of an array to a set/Q/PQ (used by literals and set:/Q:/PQ: conversions).
void ns_add_all(const char *d, void *coll, void *arr) {
    Arr *a = arr;
    for (int64_t i = 0; i < a->len; i++) ns_add(d, coll, a->data[i]);
}

// Drops one reference; at zero releases pointer children per the header descriptor, then frees.
void ns_release(void *p) {
  if (!p || HD(p)->rc < 0 || --HD(p)->rc) return;
  const char *d = HD(p)->d;
  switch (*d) {
  case 'A': case 'P': { Arr *a = p; if (ptrk(d + 1)) for (int64_t i = 0; i < a->len; i++) ns_release(S(a->data[i])); if (a->data != (slot *)(a + 1)) free(a->data); break; }
  case 'M': case 'H': {
    Map *m = p;
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) { rel(m->kd, m->keys[i]); if (m->vd) rel(m->vd, m->vals[i]); }
    free(m->keys); free(m->vals); free(m->st);
    break;
  }
  case 'U': { Arr *a = p; const char *e = d + 2; for (int64_t i = 0; i < a->len; i++, e = skip(e)) rel(e, a->data[i]); break; }
  case 'Q': for (Node *n = ((Queue *)p)->head, *nx; n; n = nx) { nx = n->next; rel(d + 1, n->v); free(n); } break;
  }
  free(HD(p));
}

// ---------- stdlib: math, search, folds, block callbacks ----------
typedef slot (*ns_fn)(void *env, slot x); // a compiled NS block: borrowed argument, owned result

//@ min (T,T) T = ns_min2
slot ns_min2(const char *dT, slot a, slot b) { return ret(dT, ns_cmp(dT, a, b) <= 0 ? a : b); }
//@ max (T,T) T = ns_max2
slot ns_max2(const char *dT, slot a, slot b) { return ret(dT, ns_cmp(dT, a, b) >= 0 ? a : b); }
static slot best(const char *dT, void *arr, int sign) {
  Arr *a = arr;
  if (!a->len) ns_panic("min/max of empty array");
  slot b = a->data[0];
  for (int64_t i = 1; i < a->len; i++) if (ns_cmp(dT, a->data[i], b) * sign > 0) b = a->data[i];
  return ret(dT, b);
}
//@ min ([T]) T = ns_min_arr
slot ns_min_arr(const char *dT, void *arr) { return best(dT, arr, -1); }
//@ max ([T]) T = ns_max_arr
slot ns_max_arr(const char *dT, void *arr) { return best(dT, arr, 1); }
//@ abs (int) int = ns_abs_i
int64_t ns_abs_i(int64_t x) { return x < 0 ? -x : x; }
//@ abs (float) float = ns_abs_f
double ns_abs_f(double x) { return fabs(x); }
//@ pow (float,float) float = ns_pow_f
double ns_pow_f(double a, double b) { return pow(a, b); }
//@ sqrt (float) float
double ns_sqrt(double x) { return sqrt(x); }
//@ floor (float) int
int64_t ns_floor(double x) { return (int64_t)floor(x); }
//@ ceil (float) int
int64_t ns_ceil(double x) { return (int64_t)ceil(x); }
//@ round (float) int
int64_t ns_round(double x) { return (int64_t)round(x); }
//@ find ([T],T) int = ns_find_arr
int64_t ns_find_arr(const char *dT, void *arr, slot x) { return ns_find(HD(arr)->d, P(arr), x); }
//@ find (str,str) int = ns_find_str
int64_t ns_find_str(char *s, char *x) { return ns_find("S", P(s), P(x)); }
//@ rev (str) str = ns_rev_s
char *ns_rev_s(char *s) { size_t n = strlen(s); char *r = str_n(s, n); for (size_t i = 0; i < n; i++) r[i] = s[n - 1 - i]; return r; }
//@ join (str,str) str = ns_join_s
char *ns_join_s(char *s, char *sep) { void *c = ns_chars(s); char *r = ns_join("S", c, sep); ns_release(c); return r; }
//@ bsearch ([T], fn(T)->bool) int
int64_t ns_bsearch(const char *dT, void *arr, ns_fn f, void *env) { // first index where f holds (f false...true)
  Arr *a = arr; int64_t lo = 0, hi = a->len;
  while (lo < hi) { int64_t m = lo + (hi - lo) / 2; if (f(env, a->data[m])) hi = m; else lo = m + 1; }
  return lo;
}
