// NS runtime. Type-dependent operations are driven by descriptor strings:
// I F S B, A<t>, M<k><v>, H<t>, Q<t>, P<t>, T<t> (sorted tree), K<k><t> (sorted tree keyed by k), U<n><t1>..<tn> (boxed tuple), V<n><t1>..<tn> (value tuple,
// members I F B only); n is one digit. Element storage size esz(d): B=1, I F=8, S A M H Q P U=8 (pointer), V=8n
// (a bool member of V is stored as int64 0/1). S A M H Q P U are pointers. Containers store elements inline at stride esz; boxed tuples
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
#include <sys/wait.h>
#include <setjmp.h>

typedef struct { int64_t len, cap; char *data; int64_t esz; } Arr;
typedef struct Map { int64_t len, cap, used, ksz, vsz; const char *kd, *vd; char *keys, *vals, *st, *def; } Map; // st: 0 empty, 1 full, 2 tomb; vd NULL = set; def: value for a missing key (`m ?? d`), NULL = none
typedef struct Node { struct Node *next; char v[]; } Node;
typedef struct { int64_t len; Node *head, *tail; const char *d; int64_t esz; } Queue;
typedef struct { int64_t len, cap; char *data; int64_t esz; const char *d; } PQ; // prefix-compatible with Arr

// `?? stmt` (Codegen): ns_try pushes a handler in the caller's 512-byte frame buffer, the caller _setjmps into it,
// ns_untry pops it. A panic under a handler pops it and jumps back, so the statement is skipped.
// ponytail: objects the statement had allocated or retained before the panic leak.
typedef struct Catch { jmp_buf jb; struct Catch *prev; } Catch;
_Static_assert(sizeof(Catch) <= 512, "Catch must fit the buffer Codegen allocates");
static Catch *catcher;
void ns_try(void *buf) { Catch *c = buf; c->prev = catcher; catcher = c; }
void ns_untry(void) { catcher = catcher->prev; }
void ns_panic(const char *msg) {
  if (catcher) { Catch *c = catcher; catcher = c->prev; longjmp(c->jb, 1); }
  fprintf(stderr, "error: %s\n", msg); exit(1);
}
static void *alloc(size_t n) { void *p = calloc(1, n ? n : 1); if (!p) ns_panic("out of memory"); return p; }
static void *grow(void *p, size_t n) { p = realloc(p, n ? n : 1); if (!p) ns_panic("out of memory"); return p; }
static size_t mul(size_t a, size_t b) { size_t r; if (__builtin_mul_overflow(a, b, &r) || r > PTRDIFF_MAX) ns_panic("out of memory"); return r; } // allocation size a*b
static inline void *ldp(const void *p) { void *r; memcpy(&r, p, sizeof r); return r; } // loads (storage may be unaligned)
static inline int64_t ldi(const void *p) { int64_t r; memcpy(&r, p, 8); return r; }
static inline double ldf(const void *p) { double r; memcpy(&r, p, 8); return r; }
static inline int ldb(const void *p) { return *(const unsigned char *)p; } // also reads a V member's int64 0/1 (little endian)

typedef struct { int64_t rc; const char *d; } Hdr;
#define HD(p) ((Hdr *)(p) - 1)
static void *obj(const char *d, size_t n) { Hdr *h = alloc(sizeof(Hdr) + n); h->rc = 1; h->d = d; return h + 1; }
static struct { Hdr h; char s[1]; } empty = {{-1, "S"}, ""}; // immortal ""
static inline int ptrk(const char *d) { // pointer kinds: S A M H Q P U (I F B V are plain bytes)
  switch (*d) { case 'S': case 'A': case 'M': case 'H': case 'Q': case 'P': case 'U': case 'T': case 'K': return 1; default: return 0; }
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
  case 'A': case 'H': case 'Q': case 'P': case 'T': return skip(d + 1);
  case 'M': case 'K': return skip(skip(d + 1));
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
  Arr *a = obj(d, sizeof(Arr) + mul(cap, sz)); // data inline: one allocation per array
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
  case 'Q': case 'P': case 'T': case 'K': { // compare contents in iteration order
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
static Map *map_init(Map *m, const char *kd, const char *vd) {
  m->kd = kd; m->vd = vd; m->cap = 8;
  m->ksz = esz(m->kd); m->vsz = m->vd ? esz(m->vd) : 0;
  m->keys = alloc(8 * m->ksz); m->vals = alloc(8 * m->vsz); m->st = alloc(8);
  return m;
}
void *ns_map_new(const char *d) { return map_init(obj(d, sizeof(Map)), d + 1, *d == 'M' ? skip(d + 1) : NULL); } // d = "M<k><v>" or "H<k>" (set: vd NULL)
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
void ns_map_get(void *p, const void *k, void *out) { // a missing key reads the map's default, an error without one
  Map *m = p;
  int64_t i = find(m, k);
  if (i < 0 && !m->def) ns_panic("missing map key (give the map a default: m ?? d)");
  cpy(m->vd, out, i < 0 ? m->def : m->vals + i * m->vsz);
}
//@ mapdef ({K:V},V) {K:V}
void *ns_mapdef(const char *dK, const char *dV, void *p, const void *v) { // `m ?? d`: sets m's default in place, returns m
  Map *m = p;
  if (m->def) relv(m->vd, m->def); else m->def = alloc(m->vsz);
  cpy(m->vd, m->def, v);
  ns_retain(p); return p;
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

// ---------- BT: sorted set as a treap; keyed (K) nodes store the key, then the element, and order by key then element ----------
typedef struct TN { struct TN *l, *r; uint64_t pr; char v[]; } TN;
typedef struct { int64_t len; TN *root; const char *kd, *ed; int64_t ksz, esz; } Tree; // kd NULL: ordered by the element
void *ns_bt_new(const char *d) {
  Tree *t = obj(d, sizeof(Tree));
  if (*d == 'K') { t->kd = d + 1; t->ksz = esz(t->kd); t->ed = skip(d + 1); } else t->ed = d + 1;
  t->esz = esz(t->ed);
  return t;
}
static int64_t bt_cmp(Tree *t, const char *a, const char *b) { // a, b: node payloads
  int64_t c = t->kd ? ns_cmp(t->kd, a, b) : 0;
  return c ? c : ns_cmp(t->ed, a + t->ksz, b + t->ksz);
}
static uint64_t bt_rand(void) { static uint64_t x = 88172645463325252ULL; x ^= x << 13; x ^= x >> 7; x ^= x << 17; return x; }
static TN *bt_ins(Tree *t, TN *n, TN *x) { // x replaces nothing: an equal payload leaves the tree as is
  if (!n) { t->len++; return x; }
  int64_t c = bt_cmp(t, x->v, n->v);
  if (!c) return n;
  if (c < 0) { n->l = bt_ins(t, n->l, x); if (n->l->pr > n->pr) { TN *l = n->l; n->l = l->r; l->r = n; return l; } }
  else { n->r = bt_ins(t, n->r, x); if (n->r->pr > n->pr) { TN *r = n->r; n->r = r->l; r->l = n; return r; } }
  return n;
}
static TN *bt_merge(TN *a, TN *b) {
  if (!a || !b) return a ? a : b;
  if (a->pr > b->pr) { a->r = bt_merge(a->r, b); return a; }
  b->l = bt_merge(a, b->l); return b;
}
static TN *bt_drop(Tree *t, TN *n, const char *v, TN **found) {
  if (!n) return NULL;
  int64_t c = bt_cmp(t, v, n->v);
  if (c < 0) n->l = bt_drop(t, n->l, v, found);
  else if (c > 0) n->r = bt_drop(t, n->r, v, found);
  else { *found = n; return bt_merge(n->l, n->r); }
  return n;
}
static void bt_kill(Tree *t, TN *n) { if (t->kd) relv(t->kd, n->v); relv(t->ed, n->v + t->ksz); free(n); }
static void bt_free(Tree *t, TN *n) { if (n) { bt_free(t, n->l); bt_free(t, n->r); bt_kill(t, n); } }
static void bt_walk(Tree *t, TN *n, Arr *r) { if (n) { bt_walk(t, n->l, r); ns_arr_push(r, n->v + t->ksz); bt_walk(t, n->r, r); } }
void *bt_arr(Tree *t) { Arr *r = ns_arr_new(intern('A', t->ed), t->len); bt_walk(t, t->root, r); return r; }
static char *bt_payload(Tree *t, char *buf, const void *k, const void *x) { // k is ignored unless keyed
  if (t->kd) memcpy(buf, k, t->ksz);
  memcpy(buf + t->ksz, x, t->esz); return buf;
}
//@ btadd (BT[T],K,T) int
int64_t ns_btadd(const char *dT, const char *dK, void *p, const void *k, const void *x) {
  Tree *t = p; TN *n = alloc(sizeof(TN) + t->ksz + t->esz); int64_t len = t->len;
  bt_payload(t, n->v, k, x); n->pr = bt_rand();
  t->root = bt_ins(t, t->root, n);
  if (t->len == len) free(n);
  else { if (t->kd) retv(t->kd, k); retv(t->ed, x); }
  return 0;
}
//@ btdel (BT[T],K,T) int
int64_t ns_btdel(const char *dT, const char *dK, void *p, const void *k, const void *x) {
  Tree *t = p; char buf[144]; TN *f = NULL;
  t->root = bt_drop(t, t->root, bt_payload(t, buf, k, x), &f);
  if (f) { t->len--; bt_kill(t, f); }
  return 0;
}
//@ bthas (BT[T],K,T) bool
int64_t ns_bthas(const char *dT, const char *dK, void *p, const void *k, const void *x) {
  Tree *t = p; char buf[144]; bt_payload(t, buf, k, x);
  for (TN *n = t->root; n;) { int64_t c = bt_cmp(t, buf, n->v); if (!c) return 1; n = c < 0 ? n->l : n->r; }
  return 0;
}
static TN *bt_end(Tree *t, int right) {
  if (!t->root) ns_panic("empty tree");
  TN *n = t->root; while (right ? n->r : n->l) n = right ? n->r : n->l;
  return n;
}
//@ min (BT[T]) T = ns_btmin
void ns_btmin(const char *dT, void *p, void *out) { Tree *t = p; cpy(t->ed, out, bt_end(t, 0)->v + t->ksz); }
//@ max (BT[T]) T = ns_btmax
void ns_btmax(const char *dT, void *p, void *out) { Tree *t = p; cpy(t->ed, out, bt_end(t, 1)->v + t->ksz); }
//@ pop (BT[T]) T = ns_btpop
void ns_btpop(const char *dT, void *p, void *out) { // removes the smallest element, moving it into out
  Tree *t = p; TN *n = bt_end(t, 0), **at = &t->root;
  while (*at != n) at = &(*at)->l;
  *at = n->r; t->len--;
  memcpy(out, n->v + t->ksz, t->esz);
  if (t->kd) relv(t->kd, n->v);
  free(n);
}
//@ arr (BT[T]) [T] = ns_btarr
void *ns_btarr(const char *dT, void *p) { return bt_arr(p); }

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
  char *r = obj("S", mul(n, k) + 1);
  for (int64_t i = 0; i < k; i++) memcpy(r + i * n, s, n);
  return r;
}
// Immortal one-byte strings, shared: `s@i` and string iteration (`chars`) allocate nothing per character.
static struct { Hdr h; char s[2]; } chrs[256];
static char *chr(unsigned char c) {
  if (!chrs[c].h.d) { chrs[c].h.rc = -1; chrs[c].h.d = "S"; chrs[c].s[0] = c; }
  return chrs[c].s;
}
// a non-negative index only needs the string checked up to it, not its whole length
char *ns_str_at(char *s, int64_t i) { return chr(s[idx(i, i >= 0 ? (int64_t)strnlen(s, i + 1) : (int64_t)strlen(s))]); }
int64_t ns_str_eq(char *a, char *b) { return a == b || !strcmp(a, b); }
// Slices used on the spot (Opt rewrites them): a@i..j = b@k..l and find in s@i..j, without building the slices.
int64_t ns_subeq(const char *d, const void *pa, int64_t i, int64_t j, const void *pb, int64_t k, int64_t l) {
  if (*d == 'S') {
    char *a = ldp(pa), *b = ldp(pb);
    clamp(&i, &j, strlen(a)); clamp(&k, &l, strlen(b));
    return j - i == l - k && !memcmp(a + i, b + k, j - i);
  }
  Arr *a = ldp(pa), *b = ldp(pb);
  clamp(&i, &j, a->len); clamp(&k, &l, b->len);
  if (j - i != l - k) return 0;
  for (; i < j; i++, k++) if (!ns_eq(d + 1, a->data + i * a->esz, b->data + k * b->esz)) return 0;
  return 1;
}
int64_t ns_subfind(char *s, int64_t i, int64_t j, char *x) { // byte index in the slice, -1 if absent
  clamp(&i, &j, strlen(s));
  int64_t m = strlen(x);
  for (int64_t p = i; p + m <= j; p++) if (!memcmp(s + p, x, m)) return p - i;
  return -1;
}
char *ns_slice_s(char *s, int64_t i, int64_t j) { clamp(&i, &j, strlen(s)); return str_n(s + i, j - i); }
static void spush(Arr *r, const char *s, size_t n) { char *c = n == 1 ? chr(*s) : str_n(s, n); apush(r, &c); }
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
// character classes: every char of a non-empty string matches (so they work on one char or a whole word)
static int64_t all_of(const char *s, int (*f)(int)) { if (!*s) return 0; for (; *s; s++) if (!f((unsigned char)*s)) return 0; return 1; }
//@ ws (str) bool
int64_t ns_ws(char *s) { return all_of(s, isspace); }
//@ isupper (str) bool
int64_t ns_isupper(char *s) { return all_of(s, isupper); }
//@ islower (str) bool
int64_t ns_islower(char *s) { return all_of(s, islower); }
//@ isdigit (str) bool
int64_t ns_isdigit(char *s) { return all_of(s, isdigit); }
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

// All remaining lines of f (without '\n').
static void *flines(FILE *f) {
  Arr *r = ns_arr_new("AS", 16);
  char *line = NULL; size_t cap = 0; ssize_t n;
  while ((n = getline(&line, &cap, f)) > 0) {
    if (line[n - 1] == '\n') n--;
    spush(r, line, n);
  }
  free(line);
  return r;
}
//@ lines () [str]
void *ns_lines(void) { return flines(stdin); }

// ---------- tostr / print / join ----------
typedef struct { char *p; size_t n, cap; } Buf;
static void put(Buf *b, const char *s) {
  size_t m = strlen(s);
  if (b->n + m + 1 > b->cap) b->p = grow(b->p, b->cap = (b->n + m + 1) * 2);
  memcpy(b->p + b->n, s, m + 1); b->n += m;
}
static char *done(Buf *b) { char *r = str_n(b->p ? b->p : "", b->n); free(b->p); return r; }

// ---------- files ----------
static FILE *fopen_or_die(const char *path) {
  FILE *f = fopen(path, "rb");
  if (!f) { fprintf(stderr, "error: cannot open %s\n", path); exit(1); }
  return f;
}
// Whole file / its lines.
//@ read (str) str = ns_read_file
char *ns_read_file(char *path) {
  FILE *f = fopen_or_die(path);
  Buf b = {0};
  char chunk[65536]; size_t n;
  while ((n = fread(chunk, 1, sizeof chunk, f)) > 0) {
    if (b.n + n + 1 > b.cap) b.p = grow(b.p, b.cap = (b.n + n + 1) * 2);
    memcpy(b.p + b.n, chunk, n); b.n += n;
  }
  fclose(f);
  return done(&b);
}
//@ lines (str) [str] = ns_lines_file
void *ns_lines_file(char *path) { FILE *f = fopen_or_die(path); void *r = flines(f); fclose(f); return r; }
// Runs cmd through /bin/sh (stdout/stderr pass through); returns its exit code, 128+signal if killed.
//@ spawn (str) int
int64_t ns_spawn(char *cmd) {
  fflush(stdout); // keep earlier prints before the child's output
  int st = system(cmd);
  if (st == -1) { fprintf(stderr, "error: cannot spawn %s\n", cmd); exit(1); }
  return WIFEXITED(st) ? WEXITSTATUS(st) : 128 + WTERMSIG(st);
}
// Replaces the file with s; returns the number of bytes written.
//@ write (str,str) int
int64_t ns_write(char *path, char *s) {
  FILE *f = fopen(path, "wb");
  if (!f) { fprintf(stderr, "error: cannot write %s\n", path); exit(1); }
  size_t m = strlen(s), n = fwrite(s, 1, m, f);
  if (fclose(f) != 0 || n != m) { fprintf(stderr, "error: cannot write %s\n", path); exit(1); }
  return (int64_t)n;
}

static void fmt_float(char *out, double x) {
  for (int prec = 15; prec <= 17; prec++) {
    snprintf(out, 32, "%.*g", prec, x);
    if (strtod(out, NULL) == x) break;
  }
  if (isfinite(x) && !strpbrk(out, ".e")) strcat(out, ".0");
}
static void tostr(Buf *b, const char *d, const void *v, int q);
static void items(Buf *b, const char *open, const char *d, Arr *a) {
  put(b, open);
  for (int64_t i = 0; i < a->len; i++) { if (i) put(b, ","); tostr(b, d, a->data + i * a->esz, 1); }
  put(b, "]");
}
static void citems(Buf *b, const char *open, const char *d, const void *v) { // via a temp array
  Arr *a = ns_toarr(d, ldp(v)); items(b, open, d + 1, a); ns_release(a);
}
// q: quote strings (set for elements nested in containers)
static void tostr(Buf *b, const char *d, const void *v, int q) {
  char tmp[40];
  switch (*d) {
  case 'I': { // by hand: snprintf dominates building strings from ints
    int64_t x = ldi(v); uint64_t u = x < 0 ? -(uint64_t)x : (uint64_t)x;
    char *p = tmp + sizeof tmp - 1; *p = 0;
    do *--p = '0' + u % 10; while (u /= 10);
    if (x < 0) *--p = '-';
    put(b, p); break;
  }
  case 'F': fmt_float(tmp, ldf(v)); put(b, tmp); break;
  case 'B': put(b, ldb(v) ? "T" : "F"); break;
  case 'S': if (q) put(b, "\""); put(b, ldp(v)); if (q) put(b, "\""); break;
  case 'A': items(b, "[", d + 1, ldp(v)); break;
  case 'U': case 'V': {
    const char *x = tup(d, v); int64_t o = 0;
    put(b, "(");
    EACH_MEMBER(d, e, o) { if (o) put(b, ","); tostr(b, e, x + o, 1); }
    put(b, ")");
    break;
  }
  case 'H': citems(b, "%[", d, v); break;
  case 'Q': citems(b, "Q[", d, v); break;
  case 'P': citems(b, "PQ[", d, v); break;
  case 'T': case 'K': { Arr *a = ns_toarr(d, ldp(v)); items(b, "BT[", HD(a)->d + 1, a); ns_release(a); break; }
  case 'M': {
    Map *m = ldp(v); int first = 1;
    put(b, "[");
    for (int64_t i = 0; i < m->cap; i++) if (m->st[i] == 1) {
      if (!first) put(b, ",");
      first = 0;
      tostr(b, m->kd, m->keys + i * m->ksz, 1); put(b, "->"); tostr(b, m->vd, m->vals + i * m->vsz, 1);
    }
    put(b, m->len ? "]" : "->]");
    break;
  }
  default: ns_panic("bad type descriptor");
  }
}
char *ns_tostr(const char *d, const void *v) { Buf b = {0}; tostr(&b, d, v, 0); return done(&b); }
void ns_print(const char *d, const void *v) { Buf b = {0}; tostr(&b, d, v, 0); fputs(b.p ? b.p : "", stdout); free(b.p); }
void ns_println(const char *d, const void *v) { Buf b = {0}; tostr(&b, d, v, 0); puts(b.p ? b.p : ""); free(b.p); }
//@ join ([T],str) str
char *ns_join(const char *elemd, void *arr, char *sep) {
  Arr *a = arr; Buf b = {0};
  for (int64_t i = 0; i < a->len; i++) { if (i) put(&b, sep); tostr(&b, elemd, a->data + i * a->esz, 0); }
  return done(&b);
}
//@ join ([T]) str = ns_join0
char *ns_join0(const char *elemd, void *arr) { return ns_join(elemd, arr, ""); }

// String builder for Opt (no //@: not user-callable): joins of map loops, `a + b + c`, `s = s + x` loops.
// An A<B> whose buffer holds {Hdr, chars}, so done turns the buffer itself into the string object.
void *ns_sb_new(void) {
  Arr *a = obj("AB", sizeof(Arr));
  a->esz = 1; a->cap = 64; a->data = alloc(a->cap); a->len = sizeof(Hdr);
  return a;
}
int64_t ns_sb_put(const char *d, void *w, const void *x) { // appends x as print shows it
  Arr *a = w; Buf b = {a->data, a->len, a->cap};
  tostr(&b, d, x, 0);
  a->data = b.p; a->len = b.n; a->cap = b.cap;
  return 0;
}
char *ns_sb_done(void *w, char *sep) { // sep: put after every element by a join; the last one is dropped
  Arr *a = w;
  if (a->len > (int64_t)sizeof(Hdr)) a->len -= strlen(sep);
  Hdr *h = grow(a->data, a->len + 1);
  ((char *)h)[a->len] = 0;
  h->rc = 1; h->d = "S";
  a->data = NULL; a->len = a->cap = 0;
  return (char *)(h + 1);
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
  case 'T': case 'K': return bt_arr(c);
  default: ns_panic("not a collection"); return NULL;
  }
}

//@ keys ({K:V}) [K]
//@ arr ({K:V}) [K] = ns_keys
void *ns_keys(const char *dK, const char *dV, void *m) { return ns_toarr("M", m); }
//@ vals ({K:V}) [V]
void *ns_vals(const char *dK, const char *dV, void *m) { return ns_map_vals(m); }
//@ arr ([T]) [T]
//@ arr (%[T]) [T]
//@ arr (Q[T]) [T]
//@ arr (PQ[T]) [T]
void *ns_arr(const char *dT, void *c) { return ns_toarr(HD(c)->d, c); }
//@ arr (str) [str] = ns_chars
//@ pop (Q[T]) T = ns_qpop
//@ pop (PQ[T]) T = ns_qpop
void ns_qpop(const char *dT, void *q, void *out) { ns_pop(HD(q)->d, q, out); }

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
  if (e < 0) { // 1/x^-e truncated toward zero
    if (!base) ns_panic("division by zero");
    return base == 1 ? 1 : base == -1 ? (e & 1 ? -1 : 1) : 0;
  }
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
    if (m->def) { relv(m->vd, m->def); free(m->def); }
    free(m->keys); free(m->vals); free(m->st);
    break;
  }
  case 'U': { int64_t o = 0; EACH_MEMBER(d, e, o) relv(e, (char *)p + o); break; }
  case 'Q': for (Node *n = ((Queue *)p)->head, *nx; n; n = nx) { nx = n->next; relv(d + 1, n->v); free(n); } break;
  case 'T': case 'K': bt_free(p, ((Tree *)p)->root); break;
  }
  free(HD(p));
}

// ---------- extra stdlib ----------
// `coll ** n` (Typer.typeBin): a falsy n (0, F) empties a set, map, queue or PQ in place; a truthy n keeps it. Returns coll.
static void *keep(void *p, int64_t n) {
  const char *d = HD(p)->d;
  if (!n) switch (*d) {
  case 'M': case 'H': {
    Map *m = p;
    for (int64_t i = 0; i < m->cap; i++) {
      if (m->st[i] == 1) { relv(m->kd, m->keys + i * m->ksz); if (m->vd) relv(m->vd, m->vals + i * m->vsz); }
      m->st[i] = 0;
    }
    m->len = m->used = 0;
    break;
  }
  case 'Q': {
    Queue *q = p;
    for (Node *x = q->head, *nx; x; x = nx) { nx = x->next; relv(d + 1, x->v); free(x); }
    q->head = q->tail = NULL; q->len = 0;
    break;
  }
  case 'P': {
    PQ *a = p;
    for (int64_t i = 0; i < a->len; i++) relv(d + 1, a->data + i * a->esz);
    a->len = 0;
    break;
  }
  }
  ns_retain(p); return p;
}
//@ keep (%[T],int) %[T] = ns_keep_c
//@ keep (Q[T],int) Q[T] = ns_keep_c
//@ keep (PQ[T],int) PQ[T] = ns_keep_c
void *ns_keep_c(const char *dT, void *p, int64_t n) { return keep(p, n); }
//@ keep ({K:V},int) {K:V} = ns_keep_m
void *ns_keep_m(const char *dK, const char *dV, void *p, int64_t n) { return keep(p, n); }
static const char B36[] = "0123456789abcdefghijklmnopqrstuvwxyz",
  B64[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"; // base 64 uses the RFC 4648 alphabet
static void checkbase(int64_t b) { if ((b < 2 || b > 36) && b != 64) ns_panic("base must be 2..36 or 64"); }
static char *digits(uint64_t u, int64_t b, char *end) { // writes u in base b ending at end; returns the start
  checkbase(b);
  const char *a = b == 64 ? B64 : B36;
  do { *--end = a[u % b]; u /= b; } while (u);
  return end;
}
//@ base (int,int) str
char *ns_base(int64_t v, int64_t b) { char d[64], *p = digits(v, b, d + 64); return str_n(p, d + 64 - p); } // unsigned, no leading zeros
//@ hex (int) str
char *ns_hex(int64_t v) { char d[64], *p = digits(v, 16, d + 64); *--p = 'x'; *--p = '0'; return str_n(p, d + 64 - p); }
//@ int (str,int) int = ns_unbase
int64_t ns_unbase(char *s, int64_t b) { // int:"ff",16 -> 255; reads up to the first non-digit
  checkbase(b);
  if (b != 64) return (int64_t)strtoull(s, NULL, b);
  uint64_t u = 0;
  for (const char *c; *s && (c = strchr(B64, *s)); s++) u = u * 64 + (c - B64);
  return (int64_t)u;
}
//@ gcd (int,int) int
int64_t ns_gcd(int64_t a, int64_t b) { // unsigned magnitudes: -INT64_MIN would overflow
  uint64_t x = a < 0 ? 0 - (uint64_t)a : (uint64_t)a, y = b < 0 ? 0 - (uint64_t)b : (uint64_t)b;
  while (y) { uint64_t t = x % y; x = y; y = t; }
  return (int64_t)x;
}
//@ zip ([A],[B]) [(A,B)]
void *ns_zip(const char *dA, const char *dB, const char *dR, void *a, void *b) { // stops at the shorter array
  int val = dR[1] == 'V';
  Arr *x = a, *y = b, *r = ns_arr_new(dR, 0);
  int64_t n = x->len < y->len ? x->len : y->len;
  for (int64_t i = 0; i < n; i++) {
    if (val) { char t[16] = {0}; memcpy(t, x->data + i * x->esz, x->esz); memcpy(t + 8, y->data + i * y->esz, y->esz); apush(r, t); continue; }
    void *t = ns_tup_new(dR + 1);
    ns_tup_set(t, 0, x->data + i * x->esz); ns_tup_set(t, 1, y->data + i * y->esz);
    apush(r, &t);
  }
  return r;
}
//@ cross ([A],[B]) [(A,B)]
void *ns_cross(const char *dA, const char *dB, const char *dR, void *a, void *b) { // every (x, y), x-major
  int val = dR[1] == 'V';
  Arr *x = a, *y = b, *r = ns_arr_new(dR, 0);
  for (int64_t i = 0; i < x->len; i++)
    for (int64_t j = 0; j < y->len; j++) {
      if (val) { char t[16] = {0}; memcpy(t, x->data + i * x->esz, x->esz); memcpy(t + 8, y->data + j * y->esz, y->esz); apush(r, t); continue; }
      void *t = ns_tup_new(dR + 1);
      ns_tup_set(t, 0, x->data + i * x->esz); ns_tup_set(t, 1, y->data + j * y->esz);
      apush(r, &t);
    }
  return r;
}
//@ items ({K:V}) [(K,V)]
void *ns_items(const char *dK, const char *dV, const char *dR, void *m) { // (key, value) pairs in hash order
  void *k = ns_toarr("M", m), *v = ns_map_vals(m), *r = ns_zip(dK, dV, dR, k, v);
  ns_release(k); ns_release(v);
  return r;
}
static const char *dintern(const char *d);
//@ map ([(K,V)]) {K:V}
void *ns_map(const char *dK, const char *dV, void *a) { // inverse of items; a later pair overwrites an earlier key
  (void)dK; (void)dV;
  Arr *x = a; const char *ed = HD(a)->d + 1; // U2<k><v> or V2<k><v>
  char md[64]; snprintf(md, sizeof md, "M%s", ed + 2);
  void *r = ns_map_new(dintern(md));
  const char *kd = ed + 2;
  int64_t vo = *ed == 'V' ? 8 : esz(kd);
  for (int64_t i = 0; i < x->len; i++) { const char *t = tup(ed, x->data + i * x->esz); ns_map_set(r, t, t + vo); }
  return r;
}
//@ map (%[(K,V)]) {K:V} = ns_map_h
void *ns_map_h(const char *dK, const char *dV, void *s) { // set of pairs, in hash order
  void *a = ns_toarr(HD(s)->d, s), *r = ns_map(dK, dV, a);
  ns_release(a); return r;
}
//@ take ([T], int) [T]
void *ns_take(const char *dT, void *a, int64_t n) { // first n, or the last -n when negative
  int64_t len = ((Arr *)a)->len;
  return n >= 0 ? ns_arr_slice(a, 0, n) : ns_arr_slice(a, len + n < 0 ? 0 : len + n, len);
}
//@ take (str, int) str = ns_take_s
char *ns_take_s(char *s, int64_t n) { int64_t len = strlen(s); return n >= 0 ? ns_slice_s(s, 0, n) : ns_slice_s(s, len + n < 0 ? 0 : len + n, len); }
static void tuplen(const char *dT, const char *dR, void *a, int n, void *out) { // first n elements as a tuple
  Arr *x = a;
  if (x->len < n) ns_panic("tuple: array too short");
  if (*dR == 'V') { // value tuple: members at stride 8, bool as int64 0/1
    for (int i = 0; i < n; i++) { int64_t v = *dT == 'B' ? ldb(x->data + i) : ldi(x->data + i * 8); memcpy((char *)out + i * 8, &v, 8); }
    return;
  }
  void *t = ns_tup_new(dR);
  for (int i = 0; i < n; i++) ns_tup_set(t, i, x->data + i * x->esz);
  memcpy(out, &t, sizeof t);
}
//@ tuple2 ([T]) (T,T)
void ns_tuple2(const char *dT, const char *dR, void *a, void *out) { tuplen(dT, dR, a, 2, out); }
//@ tuple3 ([T]) (T,T,T)
void ns_tuple3(const char *dT, const char *dR, void *a, void *out) { tuplen(dT, dR, a, 3, out); }
//@ pairwise ([T]) [(T,T)]
void *ns_pairwise(const char *dT, const char *dR, void *a) { // [1,2,3] -> [(1,2),(2,3)]
  Arr *x = a; void *t = ns_arr_slice(a, 1, x->len), *r = ns_zip(dT, dT, dR, a, t);
  ns_release(t); return r;
}
//@ pairwise (str) [(str,str)] = ns_pairwise_s
void *ns_pairwise_s(const char *dR, char *s) { // "abc" -> [("a","b"),("b","c")]
  void *c = ns_chars(s), *r = ns_pairwise("S", dR, c);
  ns_release(c); return r;
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
//@ flatten ([[T]]) [T]
//@ flatten ([(T,T)]) [T]
//@ flatten ([(T,T,T)]) [T]
void *ns_flatten(const char *dT, void *a) { // one level: [[1,2],[3]] and [(1,2),(3,4)] -> flat
  Arr *x = a, *r = ns_arr_new(intern('A', dT), x->len);
  const char *ed = HD(a)->d + 1;
  for (int64_t i = 0; i < x->len; i++) {
    const char *e = x->data + i * x->esz;
    if (*ed == 'A') { Arr *y = ldp(e); for (int64_t j = 0; j < y->len; j++) ns_arr_push(r, y->data + j * y->esz); continue; }
    const char *m = tup(ed, e); int64_t o = 0;
    EACH_MEMBER(ed, me, o) ns_arr_push(r, m + o);
  }
  return r;
}
//@ transpose ([[T]]) [[T]]
void *ns_transpose(const char *dT, void *a) { // rows -> columns; a short row is skipped in the columns it lacks
  Arr *x = a; int64_t w = 0;
  for (int64_t i = 0; i < x->len; i++) { Arr *y = ldp(x->data + i * x->esz); if (y->len > w) w = y->len; }
  const char *rd = intern('A', dT);
  Arr *r = ns_arr_new(intern('A', rd), w);
  for (int64_t j = 0; j < w; j++) {
    Arr *c = ns_arr_new(rd, x->len);
    for (int64_t i = 0; i < x->len; i++) { Arr *y = ldp(x->data + i * x->esz); if (j < y->len) ns_arr_push(c, y->data + j * y->esz); }
    apush(r, &c);
  }
  return r;
}
//@ transpose ([str]) [str] = ns_transpose_s
void *ns_transpose_s(void *a) { // ["abc","def"] -> ["ad","be","cf"]
  Arr *x = a; size_t w = 0;
  for (int64_t i = 0; i < x->len; i++) { size_t n = strlen(ldp(x->data + i * x->esz)); if (n > w) w = n; }
  Arr *r = ns_arr_new("AS", w);
  char *buf = malloc(x->len + 1);
  for (size_t j = 0; j < w; j++) {
    size_t n = 0;
    for (int64_t i = 0; i < x->len; i++) { char *s = ldp(x->data + i * x->esz); if (j < strlen(s)) buf[n++] = s[j]; }
    spush(r, buf, n);
  }
  free(buf);
  return r;
}
//@ starts (str,str) bool
int64_t ns_starts(char *s, char *p) { return strncmp(s, p, strlen(p)) == 0; }
//@ ends (str,str) bool
int64_t ns_ends(char *s, char *p) { size_t n = strlen(s), m = strlen(p); return m <= n && strcmp(s + n - m, p) == 0; }
//@ nums (str) [int]
void *ns_nums(char *s) { // every integer; '-' is a sign unless it follows a letter or digit ("1-3" is [1,3])
  Arr *r = ns_arr_new("AI", 4);
  for (char *c = s; *c;) {
    int neg = *c == '-' && isdigit((unsigned char)c[1]) && (c == s || !isalnum((unsigned char)c[-1]));
    if (!isdigit((unsigned char)*c) && !neg) { c++; continue; }
    char *e; int64_t v = strtoll(c, &e, 10); apush(r, &v); c = e;
  }
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
int64_t ns_abs_i(int64_t x) { return x < 0 ? (int64_t)(0 - (uint64_t)x) : x; } // wraps: abs:INT64_MIN is INT64_MIN
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
  Arr *a = arr; int64_t n = a->len, ksz = esz(dK), *ix = alloc(n * 8); // n: f may grow a through an alias
  char *keys = alloc(n * ksz);
  for (int64_t i = 0; i < n; i++) { ix[i] = i; f(env, a->data + i * a->esz, keys + i * ksz); }
  kv.d = dK; kv.keys = keys; kv.ksz = ksz;
  qsort(ix, n, 8, kv_cmp);
  Arr *r = ns_arr_new(HD(arr)->d, n);
  for (int64_t i = 0; i < n; i++) { ns_arr_push(r, a->data + ix[i] * a->esz); relv(dK, keys + i * ksz); }
  free(keys); free(ix);
  return r;
}
static struct { const char *dR; Arr *a; ns_fn f; void *env; } sw; // qsort context for sort with a comparer
static int sw_call(int64_t i, int64_t j) { // comparer on elements i, j: a bool block means i < j, an int block its sign
  const void *xy[2] = {sw.a->data + i * sw.a->esz, sw.a->data + j * sw.a->esz};
  char r[8] = {0};
  sw.f(sw.env, xy, r);
  return *sw.dR == 'B' ? (r[0] ? -1 : 0) : *(int64_t *)r < 0 ? -1 : *(int64_t *)r > 0;
}
static int sw_cmp(const void *x, const void *y) { // stable: ties keep the original order
  int64_t i = *(const int64_t *)x, j = *(const int64_t *)y;
  int c = sw_call(i, j);
  if (*sw.dR == 'B' && !c) c = -sw_call(j, i);
  return c ? c : CMP(i, j);
}
//@ sort ([T], fn(T,T)->R) [T] = ns_sortwith
void *ns_sortwith(const char *dT, const char *dR, void *arr, ns_fn f, void *env) {
  Arr *a = arr; int64_t *ix = alloc(a->len * 8);
  for (int64_t i = 0; i < a->len; i++) ix[i] = i;
  sw.dR = dR; sw.a = a; sw.f = f; sw.env = env;
  qsort(ix, a->len, 8, sw_cmp);
  Arr *r = ns_arr_new(HD(arr)->d, a->len);
  for (int64_t i = 0; i < a->len; i++) ns_arr_push(r, a->data + ix[i] * a->esz);
  free(ix);
  return r;
}
static void bykey(const char *dT, const char *dK, void *arr, ns_fn f, void *env, int sign, void *out) { // first best
  Arr *a = arr;
  if (!a->len) ns_panic("minkey/maxkey of empty array");
  int64_t ksz = esz(dK), bi = 0; char best[72], k[72]; // a key is at most a 9-member value tuple
  f(env, a->data, best);
  for (int64_t i = 1; i < a->len; i++) {
    f(env, a->data + i * a->esz, k);
    if (ns_cmp(dK, k, best) * sign > 0) { relv(dK, best); memcpy(best, k, ksz); bi = i; } else relv(dK, k);
  }
  relv(dK, best);
  cpy(dT, out, a->data + bi * a->esz);
}
//@ maxkey ([T], fn(T)->K) T
void ns_maxkey(const char *dT, const char *dK, void *arr, ns_fn f, void *env, void *out) { bykey(dT, dK, arr, f, env, 1, out); }
//@ minkey ([T], fn(T)->K) T
void ns_minkey(const char *dT, const char *dK, void *arr, ns_fn f, void *env, void *out) { bykey(dT, dK, arr, f, env, -1, out); }
static void mbest(void *m, int sign, void *out) { // the key with the largest/smallest value
  Map *M = m; int64_t bi = -1;
  for (int64_t i = 0; i < M->cap; i++)
    if (M->st[i] == 1 && (bi < 0 || ns_cmp(M->vd, M->vals + i * M->vsz, M->vals + bi * M->vsz) * sign > 0)) bi = i;
  if (bi < 0) ns_panic("minkey/maxkey of empty map");
  cpy(M->kd, out, M->keys + bi * M->ksz);
}
//@ maxkey ({K:V}) K = ns_maxkey_m
void ns_maxkey_m(const char *dK, const char *dV, void *m, void *out) { mbest(m, 1, out); }
//@ minkey ({K:V}) K = ns_minkey_m
void ns_minkey_m(const char *dK, const char *dV, void *m, void *out) { mbest(m, -1, out); }

static const char *dintern(const char *d) { return intern('A', d) + 1; } // interns a whole descriptor
static void *scombine(Map *s, Map *t, int mode) { // in place on s: 0 union, 1 intersection, 2 difference
  if (!mode) { for (int64_t i = 0; i < t->cap; i++) if (t->st[i] == 1) ns_map_set(s, t->keys + i * t->ksz, NULL); }
  else for (int64_t i = 0; i < s->cap; i++)
    if (s->st[i] == 1 && (find(t, s->keys + i * s->ksz) >= 0) != (mode == 1)) ns_del("H", s, s->keys + i * s->ksz);
  ns_retain(s); return s;
}
//@ gunion (%[T],%[T]) %[T] = ns_sunion
void *ns_sunion(const char *dT, void *s, void *t) { return scombine(s, t, 0); }
//@ ginter (%[T],%[T]) %[T] = ns_sinter
void *ns_sinter(const char *dT, void *s, void *t) { return scombine(s, t, 1); }
//@ gdiff (%[T],%[T]) %[T] = ns_sdiff
void *ns_sdiff(const char *dT, void *s, void *t) { return scombine(s, t, 2); }
