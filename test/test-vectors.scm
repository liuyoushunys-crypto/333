;; test-vectors.scm — Vectors: SRFI-133 ops, vector-map, -fold, -sort, bytevector, bitvector
;; Generated from merged test suites

;; =============================================================================
(display ";; === 11. Vector/bytevector ===\n")

(check "vector"        (vector 1 2 3) '#(1 2 3))
(check "vector-ref"    (vector-ref '#(a b c) 1) 'b)
(check "vector-length" (vector-length '#(1 2 3 4)) 4)
(check "vector-set!"   (let ((v (vector 1 2 3))) (vector-set! v 1 99) v) '#(1 99 3))
(check "vector->list"  (vector->list '#(x y z)) '(x y z))
(check "list->vector"  (list->vector '(a b c)) '#(a b c))
(check "make-vector"   (vector-length (make-vector 5)) 5)
(check "make-vector fill" (vector-ref (make-vector 3 'x) 1) 'x)
(check "vector-append" (vector-append '#(1 2) '#(3 4)) '#(1 2 3 4))


;; =============================================================================
;; 12. 布尔逻辑边缘场景
;; =============================================================================
(display ";; === 16. Mixed operations ===\n")

(define (vector-sum v)
  (let ((n (vector-length v)))
    (let loop ((i 0) (sum 0))
      (if (= i n) sum (loop (+ i 1) (+ sum (vector-ref v i)))))))
(check "vector-sum" (vector-sum '#(10 20 30 40)) 100)

;; 16.1 map + vector
(define (vec-map f v)
  (let* ((n (vector-length v))
         (r (make-vector n)))
    (let loop ((i 0))
      (if (= i n) r (begin (vector-set! r i (f (vector-ref v i))) (loop (+ i 1)))))))
(check "vector map"
       (vector->list (vec-map (lambda (x) (* x x)) '#(1 2 3 4)))
       '(1 4 9 16))


;; =============================================================================
;; 17. 复杂宏 — 模式匹配性能 & 深度测试
(check "(make-vector 5 'x)" (make-vector 5 'x) '#(x x x x x))
(check "(vector-ref '#(10 20 30) 1)" (vector-ref '#(10 20 30) 1) 20)
(let ((v (vector 1 2 3)))
  (vector-set! v 1 99)
  (check "vector-set!" v '#(1 99 3)))
(check "(vector->list '#(a b c))" (vector->list '#(a b c)) '(a b c))
(check "(list->vector '(1 2 3))" (list->vector '(1 2 3)) '#(1 2 3))
(check "(vector-fill! v 'z) (make-vector 3)"
       (let ((v (make-vector 3))) (vector-fill! v 'z) v) '#(z z z))
(check "(vector-length '#(1 2 3 4 5))" (vector-length '#(1 2 3 4 5)) 5)

(display "") (newline)

(display "===== 23. define-syntax / syntax-rules =====") (newline)
(define-syntax my-when
(check "vector-map" (vector-map - '#(1 2 3)) #(-1 -2 -3))
(check "vector-append" (vector-append '#(1 2) '#(3 4)) #(1 2 3 4))
(check "vector-count" (vector-count odd? '#(1 2 3 4 5)) 3)
(check "vector-any" (vector-any odd? '#(1 2 3)) #t)
(check "vector-every" (vector-every odd? '#(1 3 5)) #t)
(check "vector-empty?" (vector-empty? '#()) #t)
(check "vector-empty? not" (vector-empty? '#(1)) #f)
(check "vector-index" (vector-index odd? '#(2 4 5 6)) 2)
(check "vector-skip" (vector-skip even? '#(2 4 5 6)) 2)

;;──────────────────── SRFI-158 Generators ────────────────────
(define bv (list->bitvector '(#t #f #t)))
(check "bitvector?" (bitvector? bv) #t)
(check "bitvector->list" (bitvector->list bv) '(#t #f #t))
(check "bitvector-ref" (bitvector-ref bv 0) #t)
(check "bitvector-ref false" (bitvector-ref bv 1) #f)
(check "bitvector-length" (bitvector-length bv) 3)

;;──────────────────── Flonum predicates ────────────────────
(define bv2 (list->bitvector '(#t #f #t #t)))
(check "bytevector" (bytevector 65 66 67) #u8(65 66 67))
(check "bitvector-set!" (let ((v (bitvector-copy bv2))) (bitvector-set! v 1 #t) (bitvector->list v)) '(#t #t #t #t))
(check "bitvector-copy" (bitvector->list (bitvector-copy bv2)) '(#t #f #t #t))
(check "bitvector-append" (bitvector->list (bitvector-append (list->bitvector '(#t #f)) bv2)) '(#t #f #t #f #t #t))

;;──────────────────── Vector extras ────────────────────
  (check "vector-concatenate" (not (not (procedure? vector-concatenate))) #t)
(check "vector-copy" (vector-copy #(1 2 3)) #(1 2 3))
(define vc (vector 1 2 3))
(vector-copy! vc 0 #(4 5))
(check "vector-copy!" vc #(4 5 3))
(check "vector-fold" (vector-fold (lambda (i s v) (+ i s v)) 0 #(1 2 3 4)) 16)
(check "vector-fold-right" (vector-fold-right (lambda (i s v) (- i s v)) 0 #(1 2 3)) -1)
(check "vector-for-each" (procedure? vector-for-each) #t)
(check "vector-map!" (procedure? vector-map!) #t)
(check "vector-reverse" (vector-reverse #(1 2 3)) #(3 2 1))
(check "vector-reverse!" (let ((v #(1 2 3))) (vector-reverse! v) v) #(3 2 1))
(check "vector-sort" (vector-sort > #(1 4 2 3)) #(4 3 2 1))
(check "vector-swap!" (let ((v #(1 2 3))) (vector-swap! v 0 2) v) #(3 2 1))
(check "vector-unfold" (procedure? vector-unfold) #t)
(check "vector= int" (vector= = #(1 2) #(1 2)) #t)
(check "vector= diff" (vector= = #(1 2) #(1 3)) #f)

;;──────────────────── Generator ops (SRFI-158) ────────────────────
;; ============================================================
(test-begin "scheme_builtins_base — 向量")

(test-equal "vector?"      (vector? #(1 2 3)) #t)
(test-equal "make-vector"  (make-vector 3 0) #(0 0 0))
(test-equal "vector"       (vector 10 20 30) #(10 20 30))
(test-equal "vector-length" (vector-length #(a b c)) 3)
(test-equal "vector-ref"   (vector-ref #(10 20) 1) 20)
(test-equal "vector-set!"  (let ((v (vector 0 0))) (vector-set! v 1 99) v) #(0 99))
(test-equal "vector->list" (vector->list #(1 2)) '(1 2))
(test-equal "list->vector" (list->vector '(3 4)) #(3 4))
(test-equal "vector-fill!" (let ((v (vector 1 2 3))) (vector-fill! v 0) v) #(0 0 0))

(test-end "scheme_builtins_base — 向量")

;; ============================================================
(test-begin "scheme_builtins_adv — bytevector")

(define _bv (bytevector 1 2 3 4))
(test-equal "bytevector?" (bytevector? _bv) #t)
(test-equal "bytevector-length" (bytevector-length _bv) 4)
(test-equal "bytevector-u8-ref" (bytevector-u8-ref _bv 2) 3)
(test-equal "make-bytevector" (bytevector-length (make-bytevector 5 255)) 5)
(define _bv2 (bytevector 10 20 30))
(bytevector-u8-set! _bv2 1 99)
(test-equal "bytevector-u8-set!" (bytevector-u8-ref _bv2 1) 99)
(test-equal "bytevector-append" (bytevector-append (bytevector 1 2) (bytevector 3 4)) (bytevector 1 2 3 4))
(test-equal "utf8->string" (utf8->string (string->utf8 "中文")) "中文")
(test-equal "string->utf8" (string->utf8 "a") (bytevector 97))

;; port-position / set-port-position!
(define _bv-port (open-input-bytevector (string->utf8 "hello")))
(test-equal "port-position" (port-position _bv-port) 0)
(read-u8 _bv-port)
(test-equal "port-position after read" (port-position _bv-port) 1)
(set-port-position! _bv-port 0)
(test-equal "read after reset" (read-u8 _bv-port) 104)  ;; ord('h')
(close-port _bv-port)

(test-end "scheme_builtins_adv — bytevector")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 向量扩展")

(test-equal "vector-append" (vector-append #(1 2) #(3 4)) #(1 2 3 4))
(test-equal "vector-map"    (vector-map (lambda (x) (* x 2)) #(1 2 3)) #(2 4 6))
(test-equal "vector-for-each" (let ((acc '())) (vector-for-each (lambda (x) (set! acc (cons x acc))) #(1 2)) (reverse acc)) '(1 2))
(test-equal "vector-count"   (vector-count even? #(1 2 3 4)) 2)
(test-equal "vector-any"     (vector-any even? #(1 2 3)) #t)
(test-equal "vector-every"   (vector-every positive? #(1 2 3)) #t)
(test-equal "vector-empty?"  (vector-empty? #()) #t)
(test-equal "vector-reverse" (vector-reverse #(1 2 3)) #(3 2 1))
(test-equal "vector-fold" (vector-fold (lambda (i elt acc) (+ acc elt)) 0 #(1 2 3)) 6)
(test-equal "vector-index" (vector-index even? #(1 3 4 5)) 2)
(test-equal "vector-skip"  (vector-skip odd? #(1 3 4 5)) 2)
(test-equal "vector-swap!" (let ((v #(1 2 3))) (vector-swap! v 0 2) v) #(3 2 1))
(test-equal "vector-sort"  (vector-sort < #(3 1 4 2)) #(1 2 3 4))
(test-equal "vector-unfold" (vector-unfold (lambda (i x) (values x (+ x 1))) 5 0) #(0 1 2 3 4))

(test-end "scheme_builtins_base_ext — 向量扩展")

