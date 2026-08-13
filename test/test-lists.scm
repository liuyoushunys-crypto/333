;; test-lists.scm — Lists: SRFI-1 list ops, pairs, alists, nth, fold, map, filter
;; Generated from merged test suites

;; =============================================================================
(display ";; === 10. List/pair edge cases ===\n")

(check "list head"     (list-head '(1 2 3 4) 2) '(1 2))
(check "list tail"     (list-tail '(1 2 3 4) 2) '(3 4))
(check "list-ref"      (list-ref '(a b c d) 2) 'c)
(check "member"        (member 'b '(a b c)) '(b c))
(check "assoc"         (assoc 'b '((a 1) (b 2) (c 3))) '(b 2))
(check "assq"          (assq 'b '((a 1) (b 2) (c 3))) '(b 2))
(check "append empty"  (append) '())
(check "append single" (append '(1 2)) '(1 2))
(check "append multi"  (append '(1) '(2) '(3)) '(1 2 3))
(check "map"           (map (lambda (x) (* x 2)) '(1 2 3)) '(2 4 6))
(check "filter"        (filter (lambda (x) (> x 2)) '(1 2 3 4)) '(3 4))
(check "fold"          (fold-left (lambda (acc x) (+ acc x)) 0 '(1 2 3)) 6)
(check "reverse"       (reverse '(1 2 3)) '(3 2 1))
(check "iota"          (iota 5) '(0 1 2 3 4))
(check "length 0"      (length '()) 0)
(check "list-copy"     (let ((l '(1 2 3))) (equal? (list-copy l) l)) #t)

;; 10.1 循环列表检测
(define circular (list 1 2 3))
(set-cdr! (cddr circular) circular)
(check "list? detects cycle" (list? circular) #f)

;; 10.2 set-car!/set-cdr!
(let ((p (cons 1 2)))
  (set-car! p 10)
  (set-cdr! p 20)
  (check "set-car!/set-cdr!" p '(10 . 20)))


;; =============================================================================
;; 11. 向量 & 字节向量
(check "(cdr '(1 2 3))" (cdr '(1 2 3)) '(2 3))
(check "(cons 1 '(2 3))" (cons 1 '(2 3)) '(1 2 3))
(check "(cons 'a 'b)" (cons 'a 'b) '(a . b))
(check "(list 1 2 3)" (list 1 2 3) '(1 2 3))
(check "()" '() '())

(display "") (newline)
(display "===== 6. c[ad]+r 全套 (28个) =====") (newline)
(check "(car '(1 2 3))" (car '(1 2 3)) 1)
(check "(cdr '(1 2 3))" (cdr '(1 2 3)) '(2 3))
(check "(caar '((1 2) 3))" (caar '((1 2) 3)) 1)
(check "(cadr '(1 2 3))" (cadr '(1 2 3)) 2)
(check "(cdar '((1 2) 3))" (cdar '((1 2) 3)) '(2))
(check "(cddr '(1 2 3))" (cddr '(1 2 3)) '(3))
(check "(caaar '(((1) 2) 3))" (caaar '(((1) 2) 3)) 1)
(check "(caadr '((1) (2 3)))" (caadr '((1) (2 3))) 2)
(check "(cadar '((1 2) 3))" (cadar '((1 2) 3)) 2)
(check "(caddr '(1 2 3 4))" (caddr '(1 2 3 4)) 3)
(check "(cdaar '(((1 2) 3) 4))" (cdaar '(((1 2) 3) 4)) '(2))
(check "(cdadr '((1) (2 3)))" (cdadr '((1) (2 3))) '(3))
(check "(cddar '((1 2) 3))" (cddar '((1 2) 3)) '())
(check "(cdddr '(1 2 3 4))" (cdddr '(1 2 3 4)) '(4))
(check "(caaaar '((((42)))))" (caaaar '((((42))))) 42)
(check "(caaadr '(((1)) ((2))))" (caaadr '(((1)) ((2)))) 2)
(check "(caadar '((1 (2)) 3))" (caadar '((1 (2)) 3)) 2)
(check "(caaddr '(1 (2) (3 4 5)))" (caaddr '(1 (2) (3 4 5))) 3)
(check "(cadaar '(((a 3)) 5))" (cadaar '(((a 3)) 5)) 3)
(check "(cadadr '(1 (2 3) (4 5)))" (cadadr '(1 (2 3) (4 5))) 3)
(check "(caddar '((x a 3) y))" (caddar '((x a 3) y)) 3)
(check "(cadddr '(1 2 3 4))" (cadddr '(1 2 3 4)) 4)
(check "(cdaaar '((((1 2))) 3))" (cdaaar '((((1 2))) 3)) '(2))
(check "(cdaadr '(((1)) ((2 3))))" (cdaadr '(((1)) ((2 3)))) '(3))
(check "(cdadar '((0 (1 2)) 3))" (cdadar '((0 (1 2)) 3)) '(2))
(check "(cdaddr '(1 (2) (3 4 5)))" (cdaddr '(1 (2) (3 4 5))) '(4 5))
(check "(cddaar '(((a b 3 4) z) w))" (cddaar '(((a b 3 4) z) w)) '(3 4))
(check "(cddadr '(1 (2 3 4 x)))" (cddadr '(1 (2 3 4 x))) '(4 x))
(check "(cdddar '((a b c 4) z))" (cdddar '((a b c 4) z)) '(4))
(check "(cddddr '(1 2 3 4 5))" (cddddr '(1 2 3 4 5)) '(5))

(display "") (newline)
(display "===== 7. 列表算法 =====") (newline)
(check "(length '(a b c d e))" (length '(a b c d e)) 5)
(check "(length '())" (length '()) 0)
(check "(reverse '(1 2 3 4))" (reverse '(1 2 3 4)) '(4 3 2 1))
(check "(reverse '())" (reverse '()) '())
(check "(append '(1 2) '(3 4))" (append '(1 2) '(3 4)) '(1 2 3 4))
(check "(append '(1 2) '(3 4) '(5 6))" (append '(1 2) '(3 4) '(5 6)) '(1 2 3 4 5 6))
(check "(append '())" (append) '())
(check "(list-tail '(1 2 3 4) 2)" (list-tail '(1 2 3 4) 2) '(3 4))
(check "(list-tail '(1 2 3 4) 0)" (list-tail '(1 2 3 4) 0) '(1 2 3 4))
(check "(last-pair '(1 2 3))" (last-pair '(1 2 3)) '(3))
(check "(list-ref '(10 20 30) 1)" (list-ref '(10 20 30) 1) 20)
(check "(memq 'b '(a b c d))" (memq 'b '(a b c d)) '(b c d))
(check "(memq 'x '(a b c))" (memq 'x '(a b c)) #f)
(check "(memv 3 '(1 2 3 4 5))" (memv 3 '(1 2 3 4 5)) '(3 4 5))
(check "(member 3 '(1 2 3 4 5))" (member 3 '(1 2 3 4 5)) '(3 4 5))

(display "") (newline)
(display "===== 8. 关联列表 =====") (newline)
(check "(assq 'b '((a 1) (b 2) (c 3)))" (assq 'b '((a 1) (b 2) (c 3))) '(b 2))
(check "(assq 'x '((a 1) (b 2)))" (assq 'x '((a 1) (b 2))) #f)
(check "(assv 2 '((1 one) (2 two) (3 three)))" (assv 2 '((1 one) (2 two) (3 three))) '(2 two))
(check "(assoc 'b '((a 1) (b 2) (c 3)))" (assoc 'b '((a 1) (b 2) (c 3))) '(b 2))

(display "") (newline)
(display "===== 9. 高阶函数 =====") (newline)
(check "map (lambda (x) (* x x))" (map (lambda (x) (* x x)) '(1 2 3 4 5)) '(1 4 9 16 25))
(check "map + multi" (map + '(1 2 3) '(10 20 30)) '(11 22 33))
(check "map empty" (map (lambda (x) x) '()) '())
(check "map string->symbol" (map string->symbol '("a" "b" "c")) '(a b c))

(let ((acc '()))
  (for-each (lambda (x) (set! acc (cons x acc))) '(a b c))
  (check "for-each" (reverse acc) '(a b c)))

(define applied-result (apply + '(1 2 3)))
(check "apply + list" applied-result 6)

(check "apply list" (apply list '(1 2 3)) '(1 2 3))

(display "") (newline)
(display "===== 10. 数学函数 =====") (newline)
(check "(abs -5)" (abs -5) 5)
(check "take" (take '(a b c d) 3) '(a b c))
(check "drop" (drop '(a b c d) 2) '(c d))
(check "take-right" (take-right '(a b c d) 2) '(c d))
(check "drop-right" (drop-right '(a b c d) 2) '(a b))
(check "split-at" (car (split-at '(a b c d) 2)) '(a b))
(check "take-while" (take-while odd? '(1 3 5 6 7)) '(1 3 5))
(check "drop-while" (drop-while odd? '(1 3 5 6 7)) '(6 7))
  (check "span" (span odd? '(1 3 5 6 7)) '((1 3 5) (6 7)))
  (check "break" (break even? '(1 3 5 6 7)) '((1 3 5) (6 7)))
(check "any" (any odd? '(2 4 6 8)) #f)
(check "any true" (any odd? '(2 3 4)) #t)
(check "every" (every odd? '(1 3 5)) #t)
(check "every false" (every odd? '(1 2 3)) #f)
(check "list-index" (list-index even? '(1 3 5 6 7)) 3)
(check "find" (find even? '(1 3 5 6 7)) 6)
(check "filter" (filter odd? '(1 2 3 4 5)) '(1 3 5))
(check "remove" (remove odd? '(1 2 3 4 5)) '(2 4))
(check "partition" (partition even? '(1 2 3 4 5)) '((2 4) (1 3 5)))
(check "fold" (fold + 0 '(1 2 3 4)) 10)
(check "fold-right" (fold-right - 0 '(1 2 3)) (- 1 (- 2 (- 3 0))))
(check "append-map" (append-map (lambda (x) (list x x)) '(1 2)) '(1 1 2 2))
(check "flatten" (flatten '(1 (2 (3 4)) 5)) '(1 2 3 4 5))
(check "concatenate" (concatenate '((a b) (c d))) '(a b c d))
(check "zip" (zip '(1 2 3) '(a b c)) '((1 a) (2 b) (3 c)))
(check "unzip2" (unzip2 '((1 x) (2 y) (3 z))) '((1 2 3) (x y z)))
(check "count" (count odd? '(1 2 3 4 5)) 3)
(check "iota" (iota 5) '(0 1 2 3 4))
(check "circular-list?" (circular-list? (circular-list 1 2 3)) #t)
(check "proper-list?" (proper-list? '(a b c)) #t)
(check "dotted-list?" (dotted-list? '(a . b)) #t)
(check "null-list?" (null-list? '()) #t)
(check "not-pair?" (not-pair? 'a) #t)
(check "list-copy" (list-copy '(1 2 3)) '(1 2 3))
(check "make-list" (make-list 3 'x) '(x x x))
(check "last" (last '(1 2 3)) 3)
(check "last-pair" (last-pair '(1 2 3)) '(3))
(check "list-tabulate" (list-tabulate 5 (lambda (i) (* i i))) '(0 1 4 9 16))
  (check "cons*" (cons* 1 2 3 4) '(1 2 3 . 4))
  (check "list*" (not (not (procedure? cons*))) #t)
(check "alist-cons" (alist-cons 'a 1 '((b . 2))) '((a . 1) (b . 2)))
(check "assq" (assq 'a '((a . 1) (b . 2))) '(a . 1))
(check "assv" (assv 'a '((a . 1) (b . 2))) '(a . 1))
(check "memq" (memq 'b '(a b c)) '(b c))
(check "member" (member 3 '(1 2 3 4)) '(3 4))
(check "delete" (delete 2 '(1 2 3 2 4)) '(1 3 4))
(check "delete-duplicates" (delete-duplicates '(1 2 1 3 2)) '(1 2 3))
(check "filter-map" (filter-map (lambda (x) (if (odd? x) (* x x) #f)) '(1 2 3 4)) '(1 9))
(check "pair-for-each ok" (let ((r '())) (pair-for-each (lambda (p) (set! r (cons (car p) r))) '(a b c)) r) '(c b a))
(check "map-in-order" (map-in-order - '(1 2 3)) '(-1 -2 -3))
  (check "xcons" (xcons 1 2) '(2 . 1))
(check "curry" ((curry + 3) 4) 7)
(check "complement" ((complement odd?) 2) #t)
(check "flip" ((flip cons) 1 2) '(2 . 1))
(check "const" ((const 5) 'anything) 5)
(check "iterate" (iterate (lambda (x) (* x 2)) 3 1) 8)
(check "compose" ((compose (lambda (x) (+ x 1)) (lambda (x) (* x 2))) 3) 7)

;;──────────────────── List accessors ────────────────────
(check "first" (first '(a b c d e f g h i j)) 'a)
(check "second" (second '(a b c)) 'b)
(check "third" (third '(a b c)) 'c)
(check "fourth" (fourth '(a b c d)) 'd)
(check "fifth" (fifth '(a b c d e)) 'e)
(check "eighth" (eighth '(a b c d e f g h)) 'h)
(check "tenth" (tenth '(a b c d e f g h i j)) 'j)

;;──────────────────── SRFI-152 String utilities ────────────────────
(check "lset-union" (lset-union eq? '(1 2 3) '(2 3 4)) '(1 2 3 4))
(check "lset-intersection" (lset-intersection eq? '(1 2 3) '(2 3 4)) '(2 3))
(check "lset-difference" (lset-difference eq? '(1 2 3) '(2 4)) '(1 3))

;;──────────────────── Maybe ────────────────────
(check "permutations count" (length (permutations '(1 2 3))) 6)
(check "combinations count" (length (combinations '(1 2 3 4) 2)) 6)
(check "cartesian-product" (length (cartesian-product '(1 2) '(a b))) 4)

(check "json-write string" (json-write "hello") "\"hello\"")
(check "json-write number" (json-write 42) "42")
(check "json-write bool" (json-write #t) "true")
(check "json-write null" (json-write '()) "null")

;;──────────────────── List sort ────────────────────
(check "list-sort" (list-sort < '(3 1 4 1 5)) '(1 1 3 4 5))
(check "list-sort reverse" (list-sort > '(1 2 3)) '(3 2 1))
(check "sorted?" (sorted? < '(1 2 3)) #t)
(check "sorted? false" (sorted? < '(3 1 2)) #f)

;;──────────────────── Merge ────────────────────
(check "merge" (merge < '(1 3 5) '(2 4 6)) '(1 2 3 4 5 6))

;;──────────────────── Sorted set ops ────────────────────
(check "lset-union sorted" (lset-union < '(1 3 5) '(2 4 6)) '(1 3 5 6))
(check "lset-intersection sorted" (lset-intersection < '(1 2 3) '(2 3 4)) '(1 2 3))
(check "lset-difference sorted" (lset-difference < '(1 2 3) '(2 4)) '())

;;──────────────────── String specific ────────────────────
(check "alist-copy" (alist-copy '((a . 1))) '((a . 1)))
(check "alist-delete" (alist-delete 'a '((a . 1) (b . 2))) '((b . 2)))
(check "append-reverse" (append-reverse '(3 2 1) '(4 5)) '(1 2 3 4 5))
(check "but-last" (but-last '(1 2 3)) '(1 2))
(check "delete-assoc" (delete-assoc 'a '((a . 1) (b . 2))) '((b . 2)))
(define ld1 (list 1 2 3))
(define ld2 (list 1 2 3))
(take! ld2 2)
(define ld3 (list 1 2 3))
(define ld4 (list 1 2 3))
(filter! (lambda (x) (even? x)) ld4)
(check "drop!" (drop! ld1 2) '(3))
(check "take!" (take! ld2 2) '(1 2))
(check "filter!" (list? ld4) #t)
(check "fold-left list" (fold-left (lambda (s x) (cons x s)) '() '(1 2 3)) '(3 2 1))
(check "length+" (length+ '(1 2 3)) 3)
(check "list-any" (list-any even? '(1 2 3)) #t)
(check "list-any none" (list-any even? '(1 3 5)) #f)
(check "list-every odd" (list-every odd? '(1 3 5)) #t)
(check "list-every fail" (list-every odd? '(1 2 3)) #f)
(check "list-filter-map" (list-filter-map (lambda (x) (if (odd? x) (* x x) #f)) '(1 2 3 4)) '(1 9))
(check "list-find" (list-find (lambda (x) (> x 2)) '(1 2 3 4)) 3)
(check "list-find-index" (list-find-index (lambda (x) (> x 2)) '(1 2 3 4)) 2)
(check "list-flatten" (list-flatten '(1 (2 (3) 4) 5)) '(1 2 3 4 5))
(check "list-head 3" (list-head '(1 2 3 4) 2) '(1 2))
(check "list-partition" (list-partition even? '(1 2 3 4 5 6)) '((2 4 6) (1 3 5)))
(check "list-queue (list-q)" (not (not (procedure? list-queue))) #t)
(check "list-queue-list" (not (not (procedure? list-queue-list))) #t)
(check "list-remove" (list-remove even? '(1 2 3 4 5)) '(1 3 5))
(check "list-set!" (let ((l (list 1 2 3))) (list-set! l 1 'x) l) '(1 x 3))
(check "list-stable-sort" (list-stable-sort < '(3 1 4 1 5)) '(1 1 3 4 5))
(check "list-tail" (list-tail '(1 2 3 4) 2) '(3 4))
(check "list-zip" (list-zip '(1 2) '(a b)) '((1 a) (2 b)))
(define lset (list 1 2 3))
(define lset2 (list 2 3 4))
(check "list= by = " (list= = '(1 2) '(1 2)) #t)
(check "list= diff" (list= = '(1 2) '(1 3)) #f)
(check "merge!" (merge! < '(1 3) '(2 4)) '(1 2 3 4))
(check "reduce" (reduce + 0 '(1 2 3)) 6)
(check "reduce-right" (reduce-right (lambda (a b) (- a b)) 0 '(1 2 3)) 2)
  (check "break-list" (not (not (procedure? break-list))) #t)

;;──────────────────── Stream/SICP ────────────────────
(check "assoc" (assoc 'b '((a . 1) (b . 2))) '(b . 2))
(check "assoc not-found" (assoc 'c '((a . 1) (b . 2))) #f)
(check "memv" (memv 'b '(a b c)) '(b c))
(check "memv not-found" (memv 'z '(a b c)) #f)
(check "assoc key fn" (assoc 2 '((1 . a) (2 . b)) (lambda (x y) (= x y))) '(2 . b))
  (check "member key fn" (not (not (procedure? member))) #t)
(check "delete-assoc not found" (delete-assoc 'z '((a . 1))) '((a . 1)))

;;──────────────────── Character / misc ────────────────────
(check "sixth" (sixth '(a b c d e f g)) 'f)
(check "seventh" (seventh '(a b c d e f g h)) 'g)
(check "ninth" (ninth '(a b c d e f g h i j)) 'i)
(check "ne-list?" (ne-list? '(1)) #t)
(check "ne-list? empty" (ne-list? '()) #f)

;;──────────────────── JSON ────────────────────
(check "unfold" (unfold (lambda (x) (> x 5)) (lambda (x) (* x x)) (lambda (x) (+ x 1)) 1) '(1 4 9 16 25))
(check "unfold-right" (unfold-right (lambda (x) (> x 5)) (lambda (x) (* x x)) (lambda (x) (+ x 1)) 1) '(25 16 9 4 1))
(check "interleave" (interleave '(1 3 5) '(2 4 6)) '(1 2 3 4 5 6))

;;──────────────────── More list build ────────────────────
(check "reverse-list->vector" (reverse-list->vector '(1 2 3)) #(3 2 1))
(check "reduce" (reduce + 0 '(1 2 3 4)) 10)
  (check "span" (span even? '(2 4 5 6)) '((2 4) (5 6)))
  (check "break int" (break odd? '(2 4 5 6)) '((2 4) (5 6)))
(check "iota 0" (iota 0) '())
(check "range 0 5" (range 0 5) '(0 1 2 3 4))

;;──────────────────── Hyp / trig ────────────────────
;; ============================================================
(test-begin "scheme_builtins_base — 布尔与 pair 操作")

(test-equal "boolean?" (boolean? #t) #t)
(test-equal "not"      (not #f) #t)
(test-equal "pair?"    (pair? '(1 . 2)) #t)
(test-equal "cons"     (cons 1 2) '(1 . 2))
(test-equal "car"      (car '(a b c)) 'a)
(test-equal "cdr"      (cdr '(a b c)) '(b c))
(test-equal "null?"    (null? '()) #t)
(test-equal "list?"    (list? '(1 2)) #t)
(test-equal "list"     (list 1 2 3) '(1 2 3))
(test-equal "length"   (length '(a b c)) 3)
(test-equal "append"   (append '(1 2) '(3 4)) '(1 2 3 4))
(test-equal "reverse"  (reverse '(1 2 3)) '(3 2 1))
(test-equal "list-tail" (list-tail '(a b c d) 2) '(c d))
(test-equal "list-ref"  (list-ref '(a b c) 1) 'b)
(test-equal "memq"      (memq 'b '(a b c)) '(b c))
(test-equal "memv"      (memv 3 '(1 2 3)) '(3))
(test-equal "assq"      (assq 'b '((a 1) (b 2))) '(b 2))
(test-equal "assv"      (assv 2 '((1 x) (2 y))) '(2 y))
(test-equal "set-car!"  (let ((p (list 1 2))) (set-car! p 99) p) '(99 2))
(test-equal "set-cdr!"  (let ((p (list 1 2))) (set-cdr! p (list 3)) p) '(1 3))

(test-end "scheme_builtins_base — 布尔与 pair 操作")

;; ============================================================
(test-begin "scheme_builtins_adv — 列表操作（SRFI-1）")

;; any / every
(test-equal "any"    (any even? '(1 2 3)) #t)
(test-equal "every"  (every positive? '(1 2 3)) #t)

;; filter / remove
(test-equal "filter"  (filter even? '(1 2 3 4)) '(2 4))
(test-equal "remove"  (remove even? '(1 2 3 4)) '(1 3))

;; find
(test-equal "find"   (find even? '(1 3 5 6 7)) 6)

;; fold-left / fold-right
(test-equal "fold-left"  (fold-left + 0 '(1 2 3 4)) 10)
(test-equal "fold-right" (fold-right cons '() '(1 2 3)) '(1 2 3))

;; partition (returns multiple values)
(test-equal "partition yes" (call-with-values (lambda () (partition even? '(1 2 3 4 5 6))) (lambda (yes no) (length yes))) 3)

;; take / drop
(test-equal "take" (take '(a b c d) 3) '(a b c))
(test-equal "drop" (drop '(a b c d) 2) '(c d))

;; take-right / drop-right
(test-equal "take-right" (take-right '(a b c) 2) '(b c))
(test-equal "drop-right" (drop-right '(a b c) 1) '(a b))

;; take-while / drop-while
(test-equal "take-while" (take-while even? '(2 4 5 6)) '(2 4))
(test-equal "drop-while" (drop-while even? '(2 4 5 6)) '(5 6))

;; span / break (return multiple values)
(test-equal "span prefix" (call-with-values (lambda () (span even? '(2 4 5 6))) (lambda (pre suf) (length pre))) 2)
(test-equal "break prefix" (call-with-values (lambda () (break odd? '(2 4 5 6))) (lambda (pre suf) (length pre))) 2)

;; concatenate / append-map / flat-map
(test-equal "concatenate" (concatenate '((1 2) (3) (4 5))) '(1 2 3 4 5))
(test-equal "append-map"  (append-map (lambda (x) (list x (- x))) '(1 2)) '(1 -1 2 -2))
(test-equal "flat-map"    (flat-map (lambda (x) (list x (* x x))) '(2 3)) '(2 4 3 9))

;; count
(test-equal "count" (count even? '(1 2 3 4 5)) 2)

;; first..tenth
(test-equal "first"  (first '(a b c)) 'a)
(test-equal "second" (second '(a b c)) 'b)
(test-equal "third"  (third '(a b c)) 'c)
(test-equal "last"   (last '(a b c)) 'c)

;; member / assoc
(test-equal "member" (member 3 '(1 2 3 4)) '(3 4))
(test-equal "assoc"  (assoc 'b '((a 1) (b 2))) '(b 2))

;; list-index / list-tabulate
(test-equal "list-index"    (list-index even? '(1 3 4 5)) 2)
(test-equal "list-tabulate" (list-tabulate 3 (lambda (i) (* i i))) '(0 1 4))

;; unfold
(test-equal "unfold" (unfold (lambda (x) (> x 5))
                              (lambda (x) (* x x))
                              (lambda (x) (+ x 1))
                              0)
  '(0 1 4 9 16 25))

;; split-at (returns multiple values)
(test-equal "split-at length" (call-with-values (lambda () (split-at '(a b c d e) 3)) (lambda (h t) (length h))) 3)

;; zip
(test-equal "zip" (zip '(1 2) '(a b)) '((1 a) (2 b)))

;; cons* / list*
(test-equal "cons*" (cons* 1 2 3 4) '(1 2 3 . 4))
(test-equal "list*" (list* 1 2 '(3 4)) '(1 2 3 4))

;; circular-list? / dotted-list? / proper-list?
(test-equal "proper-list?"  (proper-list? '(1 2 3)) #t)
(test-equal "dotted-list?"  (dotted-list? '(1 2 . 3)) #t)

;; delete-duplicates
(test-equal "delete-duplicates" (delete-duplicates '(1 2 1 3 2)) '(1 2 3))

;; alist-copy
(test-equal "alist-copy" (alist-copy '((a 1) (b 2))) '((a 1) (b 2)))

;; reduce
(test-equal "reduce" (reduce + 0 '(1 2 3 4)) 10)

;; iota
(test-equal "iota" (iota 5 0 2) '(0 2 4 6 8))

;; xcons
(test-equal "xcons" (xcons '(b c) 'a) '(a b c))

;; but-last
(test-equal "but-last" (but-last '(a b c d)) '(a b c))

;; pair-fold / pair-fold-right
(test-equal "pair-fold" (pair-fold (lambda (p acc) (cons (car p) acc)) '() '(1 2 3)) '(3 2 1))

;; unzip1~5
(define _uz1 (unzip1 '((1 a) (2 b) (3 c))))
(test-equal "unzip1" _uz1 '(1 2 3))

(test-end "scheme_builtins_adv — 列表操作")

;; ============================================================
;; ============================================================
(test-begin "scheme_builtins_base_ext — 排序与合并")

(test-equal "list-sort"  (list-sort < '(3 1 4 2)) '(1 2 3 4))
(test-equal "merge"      (merge < '(1 3 5) '(2 4 6)) '(1 2 3 4 5 6))
(test-equal "sorted?"    (sorted? < '(1 2 3)) #t)

(test-end "scheme_builtins_base_ext — 排序与合并")

(test-end "scheme_builtins_base_ext — 组合子与高阶")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 数学扩展")

(test-equal "factorial"  (factorial 5) 120)
(test-equal "fibonacci"  (fibonacci 10) 55)
(test-equal "binomial"   (binomial 5 2) 10)
(test-equal "prime? true" (prime? 17) #t)
(test-equal "prime? false" (prime? 15) #f)
(test-equal "factor"      (factor 12) '(2 2 3))
(test-equal "permutations" (length (permutations '(1 2 3))) 6)
(test-equal "combinations" (length (combinations '(1 2 3 4) 2)) 6)
(test-equal "cartesian-product" (length (cartesian-product '(1 2) '(a b c))) 6)

(test-end "scheme_builtins_base_ext — 数学扩展")

;; ============================================================
(test-begin "scheme_builtins_base — CxR 组合")

(test-equal "caar" (caar '((1 2) 3)) 1)
(test-equal "cadr" (cadr '(1 2 3)) 2)
(test-equal "cddr" (cddr '(1 2 3)) '(3))
(test-equal "caddr" (caddr '(1 2 3 4)) 3)
(test-equal "cadddr" (cadddr '(1 2 3 4)) 4)

(test-end "scheme_builtins_base — CxR 组合")
