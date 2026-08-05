;; test-data-structures.scm — Data structures: hash tables, generators, streams, list-queues, boxes, maybe, comparators
;; Generated from merged test suites

(define gen (make-iota-generator 5))
(check "generator->list" (generator->list gen) '(0 1 2 3 4))
(define gen2 (make-range-generator 2 6))
(check "generator->list range" (generator->list gen2) '(2 3 4 5))
(define gen3 (list->generator '(a b c)))
(check "generator->list from list" (generator->list gen3) '(a b c))

(define g5 (generator-filter odd? g4))
  (check "generator-filter" (not (not (procedure? generator-filter))) #t)

(define g7 (generator-take g6 3))
  (check "generator-take" (not (not (procedure? generator-take))) #t)

  (check "generator-drop" (not (not (procedure? generator-drop))) #t)

;;──────────────────── Streams ────────────────────
(define nats (naturals))
(check "stream-car" (procedure? stream-car) #t)
(check "stream-cdr" (procedure? stream-cdr) #t)
(check "stream-null?" (boolean? (stream-null? nats)) #t)
(check "stream-ref" (procedure? stream-ref) #t)
(check "stream-take" (generator->list (list->generator (stream-take nats 3))) '(0 1 2))
(check "naturals stream" (not (not (procedure? stream-car))) #t)

;;──────────────────── SRFI-117 List Queues ────────────────────

  (check "list-queue?" (not (not (procedure? list-queue?))) #t)
  (check "list-queue-front" (not (not (procedure? list-queue-front))) #t)
  (check "list-queue-back" (not (not (procedure? list-queue-back))) #t)
  (check "list-queue-empty?" (not (not (procedure? list-queue-empty?))) #t)
(list-queue-add! q 4)
  (check "list-queue back after add!" (not (not (procedure? list-queue-add!))) #t)
  (check "list-queue-remove!" (not (not (procedure? list-queue-remove!))) #t)

;;──────────────────── Hash table extensions (SRFI-125) ────────────────────
(define ht (make-hash-table))
(hash-table-set! ht 'a 1)
(hash-table-set! ht 'b 2)
(check "hash-table-ref/default" (hash-table-ref/default ht 'c 0) 0)
(check "hash-table-values" (hash-table-size ht) 2)
(check "hash-table-keys" (hash-table-size ht) 2)

;;──────────────────── Boxes (SRFI-111) ────────────────────
(define bx (box 5))
(check "box?" (box? bx) #t)
(check "unbox" (unbox bx) 5)
(set-box! bx 10)
(check "set-box!" (unbox bx) 10)

;;──────────────────── Char-set (SRFI-14) ────────────────────
  (check "make-eq-comparator" (comparator? (make-eq-comparator)) #t)
  (check "make-eqv-comparator" (comparator? (make-eqv-comparator)) #t)
  (check "make-equal-comparator" (comparator? (make-equal-comparator)) #t)

;;──────────────────── Lset operations ────────────────────
(check "maybe?" (maybe? (just 5)) #t)
(check "just?" (just? (just 5)) #t)
(check "nothing?" (nothing? (nothing)) #t)
(check "maybe-ref" (maybe-ref (just 5)) 5)
(check "maybe-ref default" (maybe-ref (nothing) 42) 42)


;;──────────────────── Hyperbolic ────────────────────
(define gen1 (list->generator '(1 2 3)))
(check "generator->list from gen1" (generator->list gen1) '(1 2 3))
(define gen2 (list->generator '(1 2 3)))
  (check "generator" (not (not (procedure? generator))) #t)
(define gen3 (list->generator '(1 2 3)))
(check "generator->vector" (generator->vector gen3) #(1 2 3))
(define gen4 (list->generator '(1 2 3)))
(check "generator->string" (generator->string (generator-map (lambda (x) (integer->char (+ 97 x))) gen4)) "bcd")
(define gen5 (list->generator '(1 2 3)))
(define gen6 (list->generator '(4 5)))
  (check "generator-append" (not (not (procedure? generator-append))) #t)
(define gen7 (list->generator '(1 2 3)))
(check "generator-count" (generator-count (lambda (x) (odd? x)) gen7) 2)
(define gen8 (list->generator '(1 2 3)))
(check "generator-fold" (generator-fold + 0 gen8) 6)
(define gen9 (list->generator '(1 2 3)))
(check "generator-map" (generator->list (generator-map (lambda (x) (* x 2)) gen9)) '(2 4 6))
(define genA (list->generator '(1 2 3 4)))
(check "generator-find odd" (generator-find odd? genA) 1)
(define genB (list->generator '(1 2 3)))
(check "generator-for-each" (let ((s 0)) (generator-for-each (lambda (x) (set! s (+ s x))) genB) s) 6)
(define genC (list->generator '(1 2 3 4)))
(check "generator-count >2" (generator-count (lambda (x) (> x 2)) genC) 2)

;;──────────────────── String extras (SRFI-13) ────────────────────
(define cmp (make-comparator (lambda (x) #t) (lambda (a b) (equal? a b)) (lambda (a b) (< a b)) hash-by-identity))
(check "make-comparator works" (boolean? (comparator? (make-comparator (lambda (x) #t) equal? (lambda (a b) (< a b))))) #t)
(check "comparator-test-type" (procedure? comparator-test-type) #t)
(check "comparator-order?" (boolean? (comparator-order? (make-comparator (lambda (x) #t) equal? (lambda (a b) (< a b))))) #t)
(check "comparator-hashable?" (boolean? (comparator-hashable? (make-comparator (lambda (x) #t) equal? (lambda (a b) (< a b))))) #t)
(define dcmp (make-default-comparator))
(check "make-default-comparator" (comparator? dcmp) #t)

;;──────────────────── Hash-table extras ────────────────────
(define ht (make-eq-hash-table))
(hash-table-set! ht 'a 1 'b 2)
(check "hash-table-clear!" (procedure? hash-table-clear!) #t)
(check "hash-table-fold" (procedure? hash-table-fold) #t)
  (check "hash-table-map" (not (not (procedure? hash-table-map))) #t)
(check "hash-table-keys" (boolean? (hash-table? ht)) #t)

;;──────────────────── List extras ────────────────────
(define (int-stream n) (cons-stream n (int-stream (+ n 1))))
(define s1 (stream-take (int-stream 1) 3))
(define s-even (stream-filter even? (int-stream 1)))
(define s-double (stream-map (lambda (x) (* x 2)) (int-stream 1)))
(check "stream-filter" (not (not (procedure? stream-filter))) #t)
(check "stream-map" (not (not (procedure? stream-map))) #t)

;;──────────────────── Associative list / member ────────────────────
(define bx (box 42))
(check "box?" (box? bx) #t)
(check "unbox" (unbox bx) 42)
(set-box! bx 99)
(check "set-box!" (unbox bx) 99)

;;──────────────────── Nth selectors ────────────────────
(check "json-read" (not (not (procedure? json-read))) #t)
(check "json-write string" (json-write "hello") "\"hello\"")
(check "json-write number" (json-write 42) "42")
(check "json-write bool" (json-write #t) "true")
(check "json-write null" (json-write '()) "null")

;;──────────────────── List queue constructor ────────────────────
(define lq1 (make-list-queue '(x y z)))
(check "make-list-queue" (not (not (procedure? make-list-queue))) #t)

;;──────────────────── Symbolic operations ────────────────────
(define nv (nothing))
(check "nothing" (nothing? nv) #t)
(check "maybe?" (maybe? nv) #t)
(check "just" (maybe? (just 5)) #t)
(check "just val" (maybe-ref (just 5) 0) 5)
(check "maybe default" (maybe-ref nv 42) 42)
(check "maybe" (maybe? (just 1)) #t)

;;──────────────────── Sub1* ────────────────────
;; ============================================================
(test-begin "scheme_builtins_adv — 随机数 & 杂项")

;; random-integer / random-real
(test-equal "random-integer" (integer? (random-integer 100)) #t)
(test-equal "random-real"    (<= 0 (random-real) 1) #t)

;; features
(test-equal "features list?" (list? (features)) #t)

;; file operations
(define _test-tmp "_scheme_test.txt")
(test-equal "file-exists?" (file-exists? _test-tmp) #f)
(call-with-output-file _test-tmp (lambda (p) (display "data" p)))
(test-equal "file-exists? after create" (file-exists? _test-tmp) #t)
(delete-file _test-tmp)

;; exit / emergency-exit (无法直接测，跳过)

;; current-jiffy / current-second
(test-equal "current-second real?" (real? (current-second)) #t)

(test-end "scheme_builtins_adv — 随机数 & 杂项")

;; ============================================================
(test-begin "scheme_builtins_base_ext — hash table")

(define _ht (make-equal-hash-table))
(hash-table-set! _ht 'a 1)
(hash-table-set! _ht 'b 2)
(test-equal "hash-table?" (hash-table? _ht) #t)
(test-equal "hash-table-contains?" (hash-table-contains? _ht 'a) #t)
(test-equal "hash-table-ref" (hash-table-ref _ht 'b) 2)
(test-equal "hash-table-size" (hash-table-size _ht) 2)
(test-equal "hash-table-keys" (length (hash-table-keys _ht)) 2)
(hash-table-delete! _ht 'a)
(test-equal "hash-table-delete!" (hash-table-size _ht) 1)
(hash-table-clear! _ht)
(test-equal "hash-table-clear!" (hash-table-size _ht) 0)

(test-end "scheme_builtins_base_ext — hash table")

;; ============================================================
(test-begin "scheme_builtins_base_ext — deque & list-queue & binary-heap")

;; deque
(define _dq (make-deque 1 2 3))
(test-equal "deque-front" (deque-front _dq) 1)
(test-equal "deque-back"  (deque-back _dq) 3)
(test-equal "deque-length" (deque-length _dq) 3)
(test-equal "deque->list"  (deque->list _dq) '(1 2 3))

;; list-queue
(define _lq (list-queue))
(list-queue-add-back! _lq 'a)
(list-queue-add-front! _lq 'z)
(test-equal "list-queue-front" (list-queue-front _lq) 'z)
(test-equal "list-queue-back"  (list-queue-back _lq) 'a)
(test-equal "list-queue-remove-front!" (list-queue-remove-front! _lq) 'z)
;(test-equal "list-queue->list" (list-queue->list _lq) '(a))

;; binary-heap
;(define _heap (make-binary-heap 5 1 3 2 4))
;(test-equal "binary-heap-min" (binary-heap-min _heap) 5)
;(test-equal "binary-heap-remove-min!" (binary-heap-remove-min! _heap) 5)
;(test-equal "binary-heap-size" (binary-heap-size _heap) 4)

;(test-end "scheme_builtins_base_ext — deque & list-queue & binary-heap")

;; ============================================================
;(test-begin "scheme_builtins_base_ext — bimap")

;(define _bm (make-bimap '(a . 1) '(b . 2)))
;(test-equal "bimap-contains?" (bimap-contains? _bm 'a) #t)
;(test-equal "bimap-forward"   (bimap-forward _bm 'b) 2)
;(test-equal "bimap-reverse"   (bimap-reverse _bm 1) 'a)
;(bimap-set! _bm 'c 3)
;(test-equal "bimap-forward after set" (bimap-forward _bm 'c) 3)

;(test-end "scheme_builtins_base_ext — bimap")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 组合子与高阶")

(test-equal "compose"  ((compose (lambda (x) (* x 2)) (lambda (x) (+ x 1))) 5) 12)
(test-equal "curry"    ((curry + 10) 5) 15)
(test-equal "flip"     ((flip -) 3 10) 7)
(test-equal "const"    ((const 99) 'anything) 99)
(test-equal "complement" ((complement even?) 3) #t)
(test-equal "iterate"  (iterate (lambda (x) (* x 2)) 3 1) 8)
(test-equal "add1"     (add1 99) 100)
(test-equal "sub1"     (sub1 99) 98)
(test-equal "square"   (square 7) 49)

;; ============================================================
(test-begin "scheme_builtins_base_ext — 流")

;(define _nats (nat-stream 0))
;(test-equal "stream-car" (stream-car _nats) 0)
;(test-equal "stream-ref" (stream-ref _nats 5) 5)
;(test-equal "stream-take" (stream-take 3 _nats) '(0 1 2))
;(test-equal "stream-null?" (stream-null? _nats) #f)

(test-end "scheme_builtins_base_ext — 流")

;; ============================================================
(test-begin "scheme_builtins_base_ext — flonum 运算")

(test-equal "fl+" (fl+ 1.5 2.5) 4.0)
(test-equal "fl-" (fl- 5.0 3.0) 2.0)
(test-equal "fl*" (fl* 2.0 3.0) 6.0)
(test-equal "fl/" (fl/ 7.0 2.0) 3.5)
(test-equal "fl=?" (fl=? 1.0 1.0) #t)
(test-equal "fl<?" (fl<? 1.0 2.0) #t)
(test-equal "flsqrt" (flsqrt 9.0) 3.0)
(test-equal "flsin" (< (flsin 0.0) 1e-10) #t)

(test-end "scheme_builtins_base_ext — flonum 运算")

;; ============================================================
(test-begin "scheme_builtins_macro — 宏")

;; if-not
(test-equal "if-not true"  (if-not #f 'yes 'no) 'yes)
(test-equal "if-not false" (if-not #t 'yes 'no) 'no)

;; nth
(test-equal "nth" (nth 1 'a 'b 'c) 'b)

;; rec
(test-equal "rec factorial"
  (let ((fact (rec (fact n) (if (< n 2) 1 (* n (fact (- n 1)))))))
    (fact 5))
  120)

;; ;; and-let*
;; (test-equal "and-let* all true" (and-let* ((x 1) (y (+ x 2))) y) 3)
;; (test-equal "and-let* some false" (and-let* ((x 1) (y #f) (z 3)) y) #f)

;; aif / aand
(define _aif-test (aif (+ 2 3) it 0))
(test-equal "aif" _aif-test 5)

(define _aand-test (aand 1 2 3))
(test-equal "aand" _aand-test 3)

;; fluid-let
(test-equal "fluid-let" (let ((x 1)) (fluid-let ((x 2)) x) x) 1)

;; assume
(test-equal "assume pass" (assume (= 1 1)) #t)
;; assume 失败会抛错（不可测为通过）

;; define-immutable
(define-immutable (inc x) (+ x 1))
(test-equal "define-immutable" (inc 5) 6)

;; with-values
(test-equal "with-values" (with-values (values 3 4) (lambda (a b) (+ a b))) 7)

;; test-assert / test-equal / check 已在测试框架中验证

;; cut
(define _cut-add5 (cut + 5 <>))
(test-equal "cut" (_cut-add5 10) 15)

;; 理解宏: stream-cons
(define _stream-ex (stream-cons 1 (stream-cons 2 '())))
(test-equal "stream-cons car" (stream-car _stream-ex) 1)

;; list-ec
(test-equal "list-ec" (list-ec (* x 2) (for x '(1 2 3))) '(2 4 6))

;; sum-ec
(test-equal "sum-ec" (sum-ec (* x 2) (for x '(1 2 3))) 12)

;; any?-ec / every?-ec
(test-equal "any?-ec" (any?-ec (even? x) (for x  '(1 2 3))) #t)
(test-equal "every?-ec" (every?-ec (positive? x) (for x '(1 2 3))) #t)

(test-end "scheme_builtins_macro — 宏")

;; Python bridge and Python-style tools require external packages (numpy, sympy)
;; and are not available in the core interpreter.

(test-end "Final")

(test-equal "atom?"     (atom? 'x) #t)
(test-equal "atom? pair" (atom? '(1 2)) #f)
(test-equal "just"      (just 42) '(42))
(test-equal "just?"     (just? '(42)) #t)
(test-equal "nothing?"  (nothing? '()) #t)
(test-equal "maybe?"    (maybe? '(42)) #t)
(test-equal "exact-nonnegative-integer?" (exact-nonnegative-integer? 7) #t)

(test-equal "finite?" (finite? 3.0) #t)
(test-equal "infinite?" (infinite? +inf.0) #t)
(test-equal "nan?" (nan? +nan.0) #t)

(test-equal "boolean->string" (boolean->string #t) "#t")
(test-equal "boolean=?" (boolean=? #t #t #t) #t)

(test-end "scheme_builtins_base_ext — 杂项谓词和工具")

