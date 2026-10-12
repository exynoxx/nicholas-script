// NS runtime. Type-dependent operations are driven by descriptor strings:
// I F S B, A<t>, M<k><v>, H<t>, Q<t>, P<t>, U<n><t1>..<tn> (boxed tuple), V<n><t1>..<tn> (value tuple,
// members I F B only); n is one digit. Element storage size esz(d): B=1, I F=8, S A M H Q P U=8 (pointer), V=8n
// (a bool member of V is stored as int64 0/1). Containers store elements inline at stride esz; boxed tuples
// store members consecutively, each at its own esz. A value given by a descriptor ("generic value") is passed
// as a const void * to its storage bytes; element results go to a caller-provided void *out.
// Heap objects (strings, arrays, maps, sets, Q, PQ, boxed tuples) carry a 16-byte header before the pointer:
// {rc, full descriptor}. rc < 0 = immortal. Arguments are borrowed; returned pointers and *out are owned
// (pointer kinds retained, except pop, which moves). Containers retain what they store and release what they drop.
#include <stdint.h>
#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

typedef struct { int64_t len, cap; char *data; int64_t esz; } Arr;
typedef struct Map { int64_t len, cap, used, ksz, vsz; const char *kd, *vd; char *keys, *vals, *st; } Map; // st: 0 empty, 1 full, 2 tomb; vd NULL = set
typedef struct Node { struct Node *next; char v[]; } Node;
typedef struct { int64_t len; Node *head, *tail; const char *d; int64_t esz; } Queue;
typedef struct { int64_t len, cap; char *data; int64_t esz; const char *d; } PQ; // prefix-compatible with Arr

void ns_panic(const char *msg) { fprintf(stderr, "error: %s\n", msg); exit(1); }
static void *alloc(size_t n) { void *p = calloc(1, n ? n : 1); if (!p) ns_panic("out of memory"); return p; }
static void *grow(void *p, size_t n) { p = realloc(p, n ? n : 1); if (!p) ns_panic("out of memory"); return p; }
static inline void *ldp(const void *p) { void *r; memcpy(&r, p, sizeof r); return r; } // loads (storage may be unaligned)
static inline int64_t ldi(const void *p) { int64_t r; memcpy(&r, p, 8); return r; }
static inline double ldf(const void *p) { double r; memcpy(&r, p, 8); return r; }
static inline int ldb(const void *p) { return *(const unsigned char *)p; } // also reads a V member's int64 0/1 (little endian)

typedef struct { int64_t rc; const char *d; } Hdr;
#define HD(p) ((Hdr *)(p) - 1)
static void *obj(const char *d, size_t n) { Hdr *h = alloc(sizeof(Hdr) + n); h->rc = 1; h->d = d; return h + 1; }
static struct { Hdr h; char s[1]; } empty = {{-1, "S"}, ""}; // immortal ""
static inline int ptrk(const char *d) { // pointer kinds: S A M H Q P U (I F B V are plain bytes)
  switch (*d) { case 'S': case 'A': case 'M': case 'H': case 'Q': case 'P': case 'U': return 1; default: return 0; }
}
static inline int64_t esz(const char *d) { return *d == 'B' ? 1 : *d == 'V' ? 8 * (d[1] - '0') : 8; }
void ns_retain(void *p) { if (p && HD(p)->rc >= 0) HD(p)->rc++; }
void ns_release(void *p);
static inline void retv(const char *d, const void *p) { if (ptrk(d)) ns_retain(ldp(p)); }
static inline void relv(const char *d, const void *p) { if (ptrk(d)) ns_release(ldp(p)); }
static inline void cpy(const char *d, void *dst, const void *src) { memmove(dst, src, esz(d)); retv(d, src); } // copy + retain

// Returns a pointer just past the descriptor starting at d.
static const char *skip(const char *d) {
  switch (*d) {
  case 'A': case 'H': case 'Q': case 'P': return skip(d + 1);
  case 'M': return skip(skip(d + 1));
  case 'U': case 'V': { int n = d[1] - '0'; for (d += 2; n--;) d = skip(d); return d; }
  default: return d + 1;
  }
}
// Members of tuple t: descriptor e at byte offset o (declare int64_t o = 0 first).
#define EACH_MEMBER(t, e, o) for (const char *e = (t) + 2, *e##_end = skip(t); e < e##_end; o += *(t) == 'V' ? 8 : esz(e), e = skip(e))
static const char *tel(const char *t, int64_t i, int64_t *off) { // member i's descriptor and offset
  int64_t o = 0;
  EACH_MEMBER(t, e, o) if (!i--) { *off = o; return e; }
  ns_panic("bad tuple index"); return NULL;
}
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
  int64_t cap = n > 4 ? n : 4, sz = esz(d + 1);
  Arr *a = obj(d, sizeof(Arr) + cap * sz); // data inline: one allocation per array
  a->cap = cap; a->esz = sz;
  a->data = (char *)(a + 1);
  return a;
}
// Codegen reads and writes element storage inline (the layout is decided by the compiler's Repr stage);
// these return where an element lives.
void *ns_arr_append(void *p) { // a new last slot, uninitialised
  Arr *a = p;
  if (a->len == a->cap) { // move out of the inline area on first growth
    size_t n = a->cap * a->esz;
    a->data = a->data == (char *)(a + 1) ? memcpy(alloc(n * 2), a->data, n) : grow(a->data, n * 2);
    a->cap *= 2;
  }
  return a->data + a->len++ * a->esz;
}
static void apush(Arr *a, const void *x) { memcpy(ns_arr_append(a), x, a->esz); } // no retain: x's reference moves into a
void ns_arr_push(void *p, const void *x) { apush(p, x); retv(HD(p)->d + 1, x); }
static int64_t idx(int64_t i, int64_t n) {
  if (i < 0) i += n;
  if (i < 0 || i >= n) ns_panic("index out of range");
  return i;
}
void *ns_arr_at(void *p, int64_t i) { Arr *a = p; return a->data + idx(i, a->len) * a->esz; } // borrowed
void ns_arr_set(void *p, int64_t i, const void *x) {
  const char *ed = HD(p)->d + 1; char *e = ns_arr_at(p, i);
  retv(ed, x); relv(ed, e); memcpy(e, x, esz(ed));
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
  for (int64_t k = i; k < j; k++) ns_arr_push(r, a->data + k * a->esz);
  return r;
}
void *ns_arr_copy(void *p) { // deep for nested arrays (value semantics), retains everything else
  Arr *a = p;
  if (HD(p)->d[1] != 'A') return ns_arr_slice(p, 0, a->len);
  Arr *r = ns_arr_new(HD(p)->d, a->len);
  for (int64_t k = 0; k < a->len; k++) { void *c = ns_arr_copy(ldp(a->data + k * 8)); apush(r, &c); }
  return r;
}
void *ns_arr_repeat(const char *d, const void *x, int64_t n) { // d: array descriptor; nested arrays are deep-copied per row
  Arr *r = ns_arr_new(d, n);
  for (int64_t k = 0; k < n; k++) if (d[1] == 'A') { void *c = ns_arr_copy(ldp(x)); apush(r, &c); } else ns_arr_push(r, x);
  return r;
}
void *ns_arr_concat(void *p, void *q) {
  Arr *r = ns_arr_copy(p), *b = q;
  for (int64_t k = 0; k < b->len; k++) ns_arr_push(r, b->data + k * b->esz);
  return r;
}
void *ns_range(int64_t a, int64_t b) {
  Arr *r = ns_arr_new("AI", a < b ? b - a : a - b);
  if (a <= b) for (int64_t i = a; i < b; i++) apush(r, &i);
  else for (int64_t i = a; i > b; i--) apush(r, &i);
  return r;
}
//@ rev ([T]) [T]
void *ns_rev(const char *dT, void *p) {
  Arr *a = p, *r = ns_arr_new(HD(p)->d, a->len);
  for (int64_t k = a->len - 1; k >= 0; k--) ns_arr_push(r, a->data + k * a->esz);
  return r;
}

// ---------- boxed tuples (members consecutive, each at its own esz; header desc "U<n>...") ----------
void *ns_tup_new(const char *d) { int64_t o = 0; EACH_MEMBER(d, e, o) {} return obj(d, o); }
void ns_tup_set(void *t, int64_t i, const void *x) { int64_t o; const char *e = tel(HD(t)->d, i, &o); cpy(e, (char *)t + o, x); } // construction only
void ns_tup_get(void *t, int64_t i, void *out) { int64_t o; const char *e = tel(HD(t)->d, i, &o); cpy(e, out, (char *)t + o); }

// ---------- generic eq / cmp / hash ----------
void *ns_toarr(const char *d, void *coll);
static int64_t find(Map *m, const void *k);
#define CMP(x, y) (((x) > (y)) - ((x) < (y)))
// Value-tuple members are single-letter scalars (I F B), 8 bytes each: compare without walking descriptors.
static inline int64_t vcmp(const char *d, const void *a, const void *b) {
  for (int i = 0, n = d[1] - '0'; i < n; i++) {
    const char *p = (const char *)a + 8 * i, *q = (const char *)b + 8 * i;
    int64_t c = d[2 + i] == 'F' ? CMP(ldf(p), ldf(q)) : CMP(ldi(p), ldi(q));
    if (c) return c;
  }
  return 0;
}
static const char *tup(const char *d, const void *p) { return *d == 'U' ? ldp(p) : p; } // a tuple's member bytes

int64_t ns_cmp(const char *d, const void *a, const void *b) {
  switch (*d) {
  case 'I': return CMP(ldi(a), ldi(b));
  case 'B': return CMP(ldb(a), ldb(b));
  case 'F': return CMP(ldf(a), ldf(b));
  case 'S': { int c = strcmp(ldp(a), ldp(b)); return CMP(c, 0); }
  case 'A': {
    Arr *x = ldp(a), *y = ldp(b);
    for (int64_t i = 0; i < x->len && i < y->len; i++) {
      char *p = x->data + i * x->esz, *q = y->data + i * x->esz;
      int64_t c = d[1] == 'I' ? CMP(ldi(p), ldi(q)) : ns_cmp(d + 1, p, q);
      if (c) return c;
    }
    return CMP(x->len, y->len);
  }
  case 'V': return vcmp(d, a, b);
  case 'U': {
    const char *x = tup(d, a), *y = tup(d, b); int64_t o = 0;
    EACH_MEMBER(d, e, o) { int64_t c = ns_cmp(e, x + o, y + o); if (c) return c; }
    return 0;
  }
  default: return CMP((uintptr_t)ldp(a), (uintptr_t)ldp(b));
  }
}

int64_t ns_eq(const char *d, const void *a, const void *b) {
  switch (*d) {
  case 'I': return ldi(a) == ldi(b);
  case 'B': return ldb(a) == ldb(b);
  case 'F': return ldf(a) == ldf(b);
  case 'S': return strcmp(ldp(a), ldp(b)) == 0;
  case 'A': {
    Arr *x = ldp(a), *y = ldp(b);
    if (x->len != y->len) return 0;
    for (int64_t i = 0; i < x->len; i++) if (!ns_eq(d + 1, x->data + i * x->esz, y->data + i * x->esz)) return 0;
    return 1;
  }
  case 'U': case 'V': {
    const char *x = tup(d, a), *y = tup(d, b); int64_t o = 0;
    EACH_MEMBER(d, e, o) if (!ns_eq(e, x + o, y + o)) return 0;
    return 1;
  }
  case 'M': case 'H': {
    Map *x = ldp(a), *y = ldp(b);
    if (x->len != y->len) return 0;
    for (int64_t i = 0; i < x->cap; i++) if (x->st[i] == 1) {
      int64_t j = find(y, x->keys + i * x->ksz);
      if (j < 0 || (x->vd && !ns_eq(x->vd, x->vals + i * x->vsz, y->vals + j * y->vsz))) return 0;
    }
    return 1;
  }
  case 'Q': case 'P': { // compare contents in iteration order
    void *x = ns_toarr(d, ldp(a)), *y = ns_toarr(d, ldp(b));
    int64_t r = ns_eq(HD(x)->d, &x, &y);
    ns_release(x); ns_release(y);
    return r;
  }
  default: ns_panic("bad type descriptor"); return 0;
  }
}

static uint64_t mix(uint64_t h) { h ^= h >> 33; h *= 0xff51afd7ed558ccdULL; h ^= h >> 33; return h; }
static uint64_t hash(const char *d, const void *v) {
  switch (*d) {
  case 'B': return mix(ldb(v));
  case 'F': { double x = ldf(v); if (x == 0) x = 0; uint64_t u; memcpy(&u, &x, 8); return mix(u); } // -0.0 == 0.0
  case 'S': { uint64_t h = 1469598103934665603ULL; for (const char *s = ldp(v); *s; s++) h = (h ^ (unsigned char)*s) * 1099511628211ULL; return h; }
  case 'A': { Arr *a = ldp(v); uint64_t h = 7; for (int64_t i = 0; i < a->len; i++) h = h * 31 + hash(d + 1, a->data + i * a->esz); return mix(h); }
  case 'U': case 'V': { const char *x = tup(d, v); uint64_t h = 11; int64_t o = 0; EACH_MEMBER(d, e, o) h = h * 31 + hash(e, x + o); return mix(h); }
  default: return mix((uint64_t)ldi(v));
  }
}

// ---------- map / set (open addressing, linear probing; keys and values inline at ksz / vsz) ----------
void *ns_map_new(const char *d) { // d = "M<k><v>" or "H<k>" (set: vd NULL)
  Map *m = obj(d, sizeof(Map));
  m->kd = d + 1; m->vd = *d == 'M' ? skip(d + 1) : NULL; m->cap = 8;
  m->ksz = esz(m->kd); m->vsz = m->vd ? esz(m->vd) : 0;
  m->keys = alloc(8 * m->ksz); m->vals = alloc(8 * m->vsz); m->st = alloc(8);
  return m;
}
void *ns_set_new(const char *d) { return ns_map_new(d); }
static inline uint64_t khash(Map *m, const void *k) { return *m->kd == 'I' ? mix(ldi(k)) : hash(m->kd, k); } // int keys inline
static inline int keq(Map *m, const void *a, const void *b) { return *m->kd == 'I' ? ldi(a) == ldi(b) : ns_eq(m->kd, a, b); }
static int64_t find(Map *m, const void *k) {
  for (uint64_t i = khash(m, k) & (m->cap - 1);; i = (i + 1) & (m->cap - 1)) {
    if (m->st[i] == 0) return -1;
    if (m->st[i] == 1 && keq(m, m->keys + i * m->ksz, k)) return (int64_t)i;
  }
}
static int64_t insert(Map *m, const void *k, const void *v) { // k not present; no retain; returns its slot
  if ((m->used + 1) * 2 > m->cap) { // rehash (also drops tombstones)
    Map old = *m;
    for (m->cap = 8; m->cap < m->len * 4; m->cap *= 2) {}
    m->keys = alloc(m->cap * m->ksz); m->vals = alloc(m->cap * m->vsz); m->st = alloc(m->cap);
    m->len = m->used = 0;
    for (int64_t j = 0; j < old.cap; j++) if (old.st[j] == 1) insert(m, old.keys + j * m->ksz, old.vals + j * m->vsz);
    free(old.keys); free(old.vals); free(old.st);
  }
  uint64_t j = khash(m, k) & (m->cap - 1);
  while (m->st[j] == 1) j = (j + 1) & (m->cap - 1);
  if (m->st[j] == 0) m->used++;
  m->st[j] = 1; m->len++;
  memcpy(m->keys + j * m->ksz, k, m->ksz);
  if (m->vd) memcpy(m->vals + j * m->vsz, v, m->vsz);
  return (int64_t)j;
}
void ns_map_set(void *p, const void *k, const void *v) {
  Map *m = p;
  int64_t i = find(m, k);
  if (m->vd) retv(m->vd, v);
  if (i < 0) { retv(m->kd, k); insert(m, k, v); return; }
  if (m->vd) { char *e = m->vals + i * m->vsz; relv(m->vd, e); memcpy(e, v, m->vsz); } // keep existing key
}
void *ns_q_new(const char *d);
void *ns_pq_new(const char *d);
static void zero(const char *d, void *out) { // writes d's zero value (owned) to out
  void *p;
  switch (*d) {
  case 'S': p = empty.s; break;
  case 'A': p = ns_arr_new(d, 0); break;
  case 'M': case 'H': p = ns_map_new(d); break;
  case 'Q': p = ns_q_new(d); break;
  case 'P': p = ns_pq_new(d); break;
  case 'U': { p = ns_tup_new(d); int64_t o = 0; EACH_MEMBER(d, e, o) zero(e, (char *)p + o); break; }
  default: memset(out, 0, esz(d)); return;
  }
  memcpy(out, &p, sizeof p);
}
void *ns_map_ref(void *p, const void *k) { // the value's storage, NULL if k is missing (borrowed)
  Map *m = p; int64_t i = find(m, k);
  return i < 0 ? NULL : m->vals + i * m->vsz;
}
void *ns_map_put(void *p, const void *k) { // the value's storage for k, inserting a zeroed value if missing
  Map *m = p; int64_t i = find(m, k);
  if (i < 0) { static const char z[72]; retv(m->kd, k); i = insert(m, k, z); }
  return m->vals + i * m->vsz;
}
void ns_map_get(void *p, const void *k, void *out) { // zero value if missing
  Map *m = p;
  int64_t i = find(m, k);
  if (i >= 0) cpy(m->vd, out, m->vals + i * m->vsz); else zero(m->vd, out);
}
void *ns_map_vals(void *p) {
  Map *m = p; Arr *r = ns_arr_new(intern('A', m->vd), m->len);
  for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) ns_arr_push(r, m->vals + i * m->vsz);
  return r;
}
void ns_map_merge(void *p, void *q) {
  Map *n = q;
  for (int64_t i = 0; i < n->cap; i++) if (n->st[i] == 1) ns_map_set(p, n->keys + i * n->ksz, n->vals + i * n->vsz);
}

// ---------- Q (linked list) and PQ (binary min-heap) ----------
void *ns_q_new(const char *d) { Queue *q = obj(d, sizeof(Queue)); q->d = d + 1; q->esz = esz(d + 1); return q; } // q->d: element descriptor
void *ns_pq_new(const char *d) { PQ *q = obj(d, sizeof(PQ)); q->d = d + 1; q->esz = esz(d + 1); return q; }
static inline int64_t qcmp(const char *d, const void *a, const void *b) { // fast paths for int and [int] elements, else generic
  if (d[0] == 'A' && d[1] == 'I') {
    Arr *x = ldp(a), *y = ldp(b); int64_t *p = (int64_t *)x->data, *q = (int64_t *)y->data;
    for (int64_t i = 0; i < x->len && i < y->len; i++) if (p[i] != q[i]) return p[i] < q[i] ? -1 : 1;
    return CMP(x->len, y->len);
  }
  if (d[0] == 'V') return vcmp(d, a, b);
  return d[0] == 'I' ? CMP(ldi(a), ldi(b)) : ns_cmp(d, a, b);
}
#define AT(q, i) ((q)->data + (i) * (q)->esz)
// element copies/swaps: word-wise when the size is a multiple of 8 (inlines; no libc memcpy call)
static inline void cpyn(char *d, const char *s, int64_t n) { if (n & 7) memcpy(d, s, n); else for (int64_t k = 0; k < n; k += 8) memcpy(d + k, s + k, 8); }
static inline void swap(char *a, char *b, int64_t n) {
  if (n & 7) { char t[72]; memcpy(t, a, n); memcpy(a, b, n); memcpy(b, t, n); return; } // n <= esz of V9
  for (int64_t k = 0; k < n; k += 8) { int64_t t; memcpy(&t, a + k, 8); memcpy(a + k, b + k, 8); memcpy(b + k, &t, 8); }
}
static void pq_push(PQ *q, const void *x) { // no retain
  if (q->len == q->cap) q->data = grow(q->data, (q->cap = q->cap ? q->cap * 2 : 8) * q->esz);
  int64_t i = q->len++;
  cpyn(AT(q, i), x, q->esz);
  for (; i && qcmp(q->d, AT(q, i), AT(q, (i - 1) / 2)) < 0; i = (i - 1) / 2) swap(AT(q, i), AT(q, (i - 1) / 2), q->esz);
}
static void pq_pop(PQ *q, void *out) { // moves the top into out
  cpyn(out, q->data, q->esz);
  cpyn(q->data, AT(q, --q->len), q->esz);
  for (int64_t i = 0;;) {
    int64_t l = 2 * i + 1, r = l + 1, m = i;
    if (l < q->len && qcmp(q->d, AT(q, l), AT(q, m)) < 0) m = l;
    if (r < q->len && qcmp(q->d, AT(q, r), AT(q, m)) < 0) m = r;
    if (m == i) break;
    swap(AT(q, i), AT(q, m), q->esz); i = m;
  }
}
static const void *top(const char *d, void *coll) {
  if (!*(int64_t *)coll) ns_panic("empty queue");
  return *d == 'Q' ? (void *)((Queue *)coll)->head->v : (void *)((PQ *)coll)->data;
}
void ns_peek(const char *d, void *coll, void *out) { cpy(d + 1, out, top(d, coll)); }
void ns_pop(const char *d, void *coll, void *out) { // moves the container's reference into out
  const void *v = top(d, coll);
  if (*d == 'P') { pq_pop(coll, out); return; }
  Queue *q = coll; Node *n = q->head;
  memcpy(out, v, q->esz);
  q->head = n->next; free(n);
  if (!--q->len) q->tail = NULL;
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
static void spush(Arr *r, const char *s, size_t n) { char *c = str_n(s, n); apush(r, &c); }
//@ chars (str) [str]
void *ns_chars(char *s) {
  Arr *r = ns_arr_new("AS", strlen(s));
  for (; *s; s++) spush(r, s, 1);
  return r;
}
//@ split (str,str) [str]
void *ns_split(char *s, char *sep) {
  size_t m = strlen(sep);
  if (!m) return ns_chars(s);
  Arr *r = ns_arr_new("AS", 4);
  for (char *e; (e = strstr(s, sep)); s = e + m) spush(r, s, e - s);
  spush(r, s, strlen(s));
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
    spush(r, line, n);
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
static void tostr(Buf *b, const char *d, const void *v);
static void items(Buf *b, const char *open, const char *d, Arr *a) {
  put(b, open);
  for (int64_t i = 0; i < a->len; i++) { if (i) put(b, ","); tostr(b, d, a->data + i * a->esz); }
  put(b, "]");
}
static void citems(Buf *b, const char *open, const char *d, const void *v) { // via a temp array
  Arr *a = ns_toarr(d, ldp(v)); items(b, open, d + 1, a); ns_release(a);
}
static void tostr(Buf *b, const char *d, const void *v) {
  char tmp[40];
  switch (*d) {
  case 'I': snprintf(tmp, sizeof tmp, "%lld", (long long)ldi(v)); put(b, tmp); break;
  case 'F': fmt_float(tmp, ldf(v)); put(b, tmp); break;
  case 'B': put(b, ldb(v) ? "T" : "F"); break;
  case 'S': put(b, ldp(v)); break;
  case 'A': items(b, "[", d + 1, ldp(v)); break;
  case 'U': case 'V': {
    const char *x = tup(d, v); int64_t o = 0;
    put(b, "(");
    EACH_MEMBER(d, e, o) { if (o) put(b, ","); tostr(b, e, x + o); }
    put(b, ")");
    break;
  }
  case 'H': citems(b, "%[", d, v); break;
  case 'Q': citems(b, "Q[", d, v); break;
  case 'P': citems(b, "PQ[", d, v); break;
  case 'M': {
    Map *m = ldp(v); int first = 1;
    put(b, "[");
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) {
      if (!first) put(b, ",");
      first = 0;
      tostr(b, m->kd, m->keys + i * m->ksz); put(b, "->"); tostr(b, m->vd, m->vals + i * m->vsz);
    }
    put(b, m->len ? "]" : "->]");
    break;
  }
  default: ns_panic("bad type descriptor");
  }
}
char *ns_tostr(const char *d, const void *v) { Buf b = {0}; tostr(&b, d, v); return done(&b); }
void ns_print(const char *d, const void *v) { Buf b = {0}; tostr(&b, d, v); puts(b.p ? b.p : ""); free(b.p); }
//@ join ([T],str) str
char *ns_join(const char *elemd, void *arr, char *sep) {
  Arr *a = arr; Buf b = {0};
  for (int64_t i = 0; i < a->len; i++) { if (i) put(&b, sep); tostr(&b, elemd, a->data + i * a->esz); }
  return done(&b);
}

// ---------- sort and generic collection ops ----------
static const char *sort_d; // qsort has no context argument
static int sort_cmp(const void *a, const void *b) { return (int)ns_cmp(sort_d, a, b); }
static Arr *sort_in(const char *elemd, Arr *r) { sort_d = elemd; qsort(r->data, r->len, r->esz, sort_cmp); return r; }
//@ sort ([T]) [T]
void *ns_sort(const char *elemd, void *arr) { return sort_in(elemd, ns_arr_copy(arr)); }

int64_t ns_len(const char *d, const void *v) { // tuples: member count
  return *d == 'U' || *d == 'V' ? d[1] - '0' : *d == 'S' ? (int64_t)strlen(ldp(v)) : *(int64_t *)ldp(v);
}

void *ns_toarr(const char *d, void *c) {
  switch (*d) {
  case 'A': return ns_arr_copy(c);
  case 'S': return ns_chars(c);
  case 'M': case 'H': {
    Map *m = c; Arr *r = ns_arr_new(intern('A', m->kd), m->len);
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) ns_arr_push(r, m->keys + i * m->ksz);
    return r;
  }
  case 'Q': {
    Arr *r = ns_arr_new(intern('A', d + 1), ((Queue *)c)->len);
    for (Node *n = ((Queue *)c)->head; n; n = n->next) ns_arr_push(r, n->v);
    return r;
  }
  case 'P': {
    PQ *q = c; Arr *r = ns_arr_new(intern('A', d + 1), q->len);
    for (int64_t i = 0; i < q->len; i++) ns_arr_push(r, AT(q, i));
    return sort_in(d + 1, r);
  }
  default: ns_panic("not a collection"); return NULL;
  }
}

static int64_t ns_find(const char *d, void *coll, const void *x) { // A: index of first equal element, S: byte index of substring; -1 if absent
  if (*d == 'S') { char *e = strstr(coll, ldp(x)); return e ? e - (char *)coll : -1; }
  Arr *a = coll;
  for (int64_t i = 0; i < a->len; i++) if (ns_eq(d + 1, a->data + i * a->esz, x)) return i;
  return -1;
}

int64_t ns_has(const char *d, void *coll, const void *x) {
  switch (*d) {
  case 'M': case 'H': return find(coll, x) >= 0;
  case 'S': case 'A': return ns_find(d, coll, x) >= 0; // no (deep) copy
  default: { // Q, P: linear scan
    Arr *a = ns_toarr(d, coll); int64_t r = ns_find(HD(a)->d, a, x) >= 0;
    ns_release(a);
    return r;
  }
  }
}

void ns_add(const char *d, void *coll, const void *x) {
  switch (*d) {
  case 'H': ns_map_set(coll, x, NULL); break;
  case 'P': pq_push(coll, x); retv(d + 1, x); break;
  case 'A': ns_arr_push(coll, x); break;
  case 'Q': {
    Queue *q = coll; Node *n = alloc(sizeof(Node) + q->esz);
    cpy(d + 1, n->v, x);
    if (q->tail) q->tail->next = n; else q->head = n;
    q->tail = n; q->len++;
    break;
  }
  default: ns_panic("cannot add to this type");
  }
}

void ns_del(const char *d, void *coll, const void *x) {
  (void)d; // set and map share the layout
  Map *m = coll;
  int64_t i = find(m, x);
  if (i < 0) return;
  m->st[i] = 2; m->len--;
  relv(m->kd, m->keys + i * m->ksz);
  if (m->vd) relv(m->vd, m->vals + i * m->vsz);
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
  for (int64_t i = 0; i < a->len; i++) ns_add(d, coll, a->data + i * a->esz);
}

// Drops one reference; at zero releases pointer children per the header descriptor, then frees.
void ns_release(void *p) {
  if (!p || HD(p)->rc < 0 || --HD(p)->rc) return;
  const char *d = HD(p)->d;
  switch (*d) {
  case 'A': case 'P': {
    Arr *a = p;
    if (ptrk(d + 1)) for (int64_t i = 0; i < a->len; i++) ns_release(ldp(a->data + i * 8));
    if (a->data != (char *)(a + 1)) free(a->data);
    break;
  }
  case 'M': case 'H': {
    Map *m = p;
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) { relv(m->kd, m->keys + i * m->ksz); if (m->vd) relv(m->vd, m->vals + i * m->vsz); }
    free(m->keys); free(m->vals); free(m->st);
    break;
  }
  case 'U': { int64_t o = 0; EACH_MEMBER(d, e, o) relv(e, (char *)p + o); break; }
  case 'Q': for (Node *n = ((Queue *)p)->head, *nx; n; n = nx) { nx = n->next; relv(d + 1, n->v); free(n); } break;
  }
  free(HD(p));
}

// ---------- extra stdlib ----------
//@ hex (int) str
char *ns_hex(int64_t v) { char b[17]; snprintf(b, sizeof b, "%llx", (unsigned long long)v); return str_n(b, strlen(b)); }
//@ gcd (int,int) int
int64_t ns_gcd(int64_t a, int64_t b) {
  if (a < 0) a = -a;
  if (b < 0) b = -b;
  while (b) { int64_t t = a % b; a = b; b = t; }
  return a;
}
//@ zip ([A],[B]) [(A,B)]
void *ns_zip(const char *dA, const char *dB, void *a, void *b) { // stops at the shorter array
  int val = strchr("IFB", *dA) && strchr("IFB", *dB); // same rule as Repr: all-scalar tuples are value tuples
  char d[64]; snprintf(d, sizeof d, "A%c2%.*s%.*s", val ? 'V' : 'U', (int)(skip(dA) - dA), dA, (int)(skip(dB) - dB), dB);
  const char *rd = intern(d[0], d + 1);
  Arr *x = a, *y = b, *r = ns_arr_new(rd, 0);
  int64_t n = x->len < y->len ? x->len : y->len;
  for (int64_t i = 0; i < n; i++) {
    if (val) { char t[16] = {0}; memcpy(t, x->data + i * x->esz, x->esz); memcpy(t + 8, y->data + i * y->esz, y->esz); apush(r, t); continue; }
    void *t = ns_tup_new(rd + 1);
    ns_tup_set(t, 0, x->data + i * x->esz); ns_tup_set(t, 1, y->data + i * y->esz);
    apush(r, &t);
  }
  return r;
}
//@ uniq ([T]) [T]
void *ns_uniq(const char *dT, void *a) { // keeps first occurrences in order
  const char *d = HD(a)->d, *sd = intern('H', d + 1);
  Arr *x = a, *r = ns_arr_new(d, x->len);
  void *seen = ns_set_new(sd);
  for (int64_t i = 0; i < x->len; i++) {
    char *e = x->data + i * x->esz;
    if (!ns_has(sd, seen, e)) { ns_add(sd, seen, e); ns_arr_push(r, e); }
  }
  ns_release(seen);
  return r;
}
//@ trim (str) str
char *ns_trim(char *s) {
  while (isspace((unsigned char)*s)) s++;
  size_t n = strlen(s);
  while (n && isspace((unsigned char)s[n - 1])) n--;
  return str_n(s, n);
}
//@ replace (str,str,str) str
char *ns_replace(char *s, char *a, char *b) { // every occurrence of a in s replaced by b
  size_t m = strlen(a), k = strlen(b), cnt = 0;
  if (!m) return str_n(s, strlen(s));
  for (char *e = s; (e = strstr(e, a)); e += m) cnt++;
  char *r = obj("S", strlen(s) - cnt * m + cnt * k + 1), *o = r;
  for (char *e; (e = strstr(s, a)); s = e + m) { memcpy(o, s, e - s); o += e - s; memcpy(o, b, k); o += k; }
  strcpy(o, s);
  return r;
}

// ---------- stdlib: math, search, folds, block callbacks ----------
typedef void (*ns_fn)(void *env, const void *x, void *out); // a compiled NS block: borrowed x, owned *out

//@ min (T,T) T = ns_min2
void ns_min2(const char *dT, const void *a, const void *b, void *out) { cpy(dT, out, ns_cmp(dT, a, b) <= 0 ? a : b); }
//@ max (T,T) T = ns_max2
void ns_max2(const char *dT, const void *a, const void *b, void *out) { cpy(dT, out, ns_cmp(dT, a, b) >= 0 ? a : b); }
static void best(const char *dT, void *arr, int sign, void *out) {
  Arr *a = arr;
  if (!a->len) ns_panic("min/max of empty array");
  char *b = a->data;
  for (int64_t i = 1; i < a->len; i++) if (ns_cmp(dT, a->data + i * a->esz, b) * sign > 0) b = a->data + i * a->esz;
  cpy(dT, out, b);
}
//@ min ([T]) T = ns_min_arr
void ns_min_arr(const char *dT, void *arr, void *out) { best(dT, arr, -1, out); }
//@ max ([T]) T = ns_max_arr
void ns_max_arr(const char *dT, void *arr, void *out) { best(dT, arr, 1, out); }
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
//@ lcm (int,int) int
int64_t ns_lcm(int64_t a, int64_t b) { int64_t g = ns_gcd(a, b); return g ? ns_abs_i(a / g * b) : 0; }
//@ rotl (int,int) int
int64_t ns_rotl(int64_t x, int64_t n) { uint64_t u = x; n &= 63; return (int64_t)(n ? u << n | u >> (64 - n) : u); }
//@ rotr (int,int) int
int64_t ns_rotr(int64_t x, int64_t n) { uint64_t u = x; n &= 63; return (int64_t)(n ? u >> n | u << (64 - n) : u); }
//@ popcount (int) int
int64_t ns_popcount(int64_t x) { return __builtin_popcountll((uint64_t)x); }
static void fold(const char *dT, void *arr, int prod, void *out) { // sum/prod of int or float; an empty array gives 0 / 1
  Arr *a = arr; int64_t *p = (int64_t *)a->data;
  if (*dT == 'F') {
    double r = prod, *f = (double *)a->data;
    for (int64_t i = 0; i < a->len; i++) r = prod ? r * f[i] : r + f[i];
    memcpy(out, &r, 8); return;
  }
  int64_t r = prod;
  for (int64_t i = 0; i < a->len; i++) r = prod ? r * p[i] : r + p[i];
  memcpy(out, &r, 8);
}
//@ sum ([T]) T
void ns_sum(const char *dT, void *arr, void *out) { fold(dT, arr, 0, out); }
//@ prod ([T]) T
void ns_prod(const char *dT, void *arr, void *out) { fold(dT, arr, 1, out); }
//@ find ([T],T) int = ns_find_arr
int64_t ns_find_arr(const char *dT, void *arr, const void *x) { return ns_find(HD(arr)->d, arr, x); }
//@ find (str,str) int = ns_find_str
int64_t ns_find_str(char *s, char *x) { return ns_find("S", s, &x); }
//@ rev (str) str = ns_rev_s
char *ns_rev_s(char *s) { size_t n = strlen(s); char *r = str_n(s, n); for (size_t i = 0; i < n; i++) r[i] = s[n - 1 - i]; return r; }
//@ sort (str) str = ns_sort_s
static int chr_cmp(const void *a, const void *b) { return *(unsigned char *)a - *(unsigned char *)b; }
char *ns_sort_s(char *s) { size_t n = strlen(s); char *r = str_n(s, n); qsort(r, n, 1, chr_cmp); return r; }
//@ join (str,str) str = ns_join_s
char *ns_join_s(char *s, char *sep) { void *c = ns_chars(s); char *r = ns_join("S", c, sep); ns_release(c); return r; }
//@ bsearch ([T], fn(T)->bool) int
int64_t ns_bsearch(const char *dT, void *arr, ns_fn f, void *env) { // first index where f holds (f false...true)
  Arr *a = arr; int64_t lo = 0, hi = a->len;
  while (lo < hi) {
    int64_t m = lo + (hi - lo) / 2; char r[8] = {0}; // bool result: 1 byte (an i64 0/1 also works)
    f(env, a->data + m * a->esz, r);
    if (r[0]) hi = m; else lo = m + 1;
  }
  return lo;
}
static struct { const char *d; char *keys; int64_t ksz; } kv; // qsort context for sortby
static int kv_cmp(const void *x, const void *y) { // by key, then original position (stable)
  int64_t i = *(const int64_t *)x, j = *(const int64_t *)y, c = ns_cmp(kv.d, kv.keys + i * kv.ksz, kv.keys + j * kv.ksz);
  return c ? (int)c : CMP(i, j);
}
//@ sortby ([T], fn(T)->K) [T]
void *ns_sortby(const char *dT, const char *dK, void *arr, ns_fn f, void *env) {
  Arr *a = arr; int64_t ksz = esz(dK), *ix = alloc(a->len * 8);
  char *keys = alloc(a->len * ksz);
  for (int64_t i = 0; i < a->len; i++) { ix[i] = i; f(env, a->data + i * a->esz, keys + i * ksz); }
  kv.d = dK; kv.keys = keys; kv.ksz = ksz;
  qsort(ix, a->len, 8, kv_cmp);
  Arr *r = ns_arr_new(HD(arr)->d, a->len);
  for (int64_t i = 0; i < a->len; i++) { ns_arr_push(r, a->data + ix[i] * a->esz); relv(dK, keys + i * ksz); }
  free(keys); free(ix);
  return r;
}
