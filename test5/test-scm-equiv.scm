;; ============================================================
;; 测试哪些 Python builtins 可以用纯 Scheme 等价实现
;; 不修改任何 Python 代码，只验证 Scheme 版本的正确性
;; ============================================================

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; ============================================================
;; 1. 数值谓词 — 全部可用 Scheme 表达
;; ============================================================

(display "\n=== 数值谓词 ===\n")

(define (my-zero? x) (= x 0))
(define (my-positive? x) (> x 0))
(define (my-negative? x) (< x 0))
(define (my-odd? x) (= (modulo x 2) 1))
(define (my-even? x) (= (modulo x 2) 0))
(define (my-finite? x) (not (or (infinite? x) (nan? x))))
(define (my-square x) (* x x))
(define (my-abs x) (if (< x 0) (- x) x))

(check "zero? 0"          (my-zero? 0) #t)
(check "zero? 1"          (my-zero? 1) #f)
(check "positive? 5"      (my-positive? 5) #t)
(check "positive? -1"     (my-positive? -1) #f)
(check "negative? -3"     (my-negative? -3) #t)
(check "negative? 0"      (my-negative? 0) #f)
(check "odd? 3"           (my-odd? 3) #t)
(check "odd? 4"           (my-odd? 4) #f)
(check "even? 2"          (my-even? 2) #t)
(check "even? 3"          (my-even? 3) #f)
(check "square 5"         (my-square 5) 25)
(check "abs 5"            (my-abs 5) 5)
(check "abs -5"           (my-abs -5) 5)

;; 与 Python 原生版对比
(check "zero? vs py"      (eqv? (my-zero? 0) (zero? 0)) #t)
(check "positive? vs py"  (eqv? (my-positive? 5) (positive? 5)) #t)

;; ============================================================
;; 2. 简单数学运算
;; ============================================================

(display "\n=== 简单数学运算 ===\n")

(define (my-min a b) (if (< a b) a b))
(define (my-max a b) (if (> a b) a b))
(define (my-clamp x lo hi) (my-max lo (my-min x hi)))

(check "min 3 7"          (my-min 3 7) 3)
(check "max 3 7"          (my-max 3 7) 7)
(check "clamp 5 1 10"    (my-clamp 5 1 10) 5)
(check "clamp 0 1 10"    (my-clamp 0 1 10) 1)
(check "clamp 20 1 10"   (my-clamp 20 1 10) 10)

;; ============================================================
;; 3. 列表操作
;; ============================================================

(display "\n=== 列表操作 ===\n")

(define (my-last-pair lst)
  (if (null? (cdr lst)) lst (my-last-pair (cdr lst))))

(define (my-list-ref lst k)
  (if (= k 0) (car lst) (my-list-ref (cdr lst) (- k 1))))

(define (my-list-tail lst k)
  (if (= k 0) lst (my-list-tail (cdr lst) (- k 1))))

(define (my-length lst)
  (let loop ((n 0) (xs lst))
    (if (null? xs) n (loop (+ n 1) (cdr xs)))))

(define (my-append . lists)
  (if (null? lists) '()
      (let ((first (car lists)))
        (if (null? first) (apply my-append (cdr lists))
            (cons (car first)
                  (apply my-append (cons (cdr first) (cdr lists))))))))

(check "last-pair (1 2 3)" (my-last-pair '(1 2 3)) '(3))
(check "list-ref 0"        (my-list-ref '(a b c) 0) 'a)
(check "list-ref 2"        (my-list-ref '(a b c) 2) 'c)
(check "list-tail 1"       (my-list-tail '(a b c) 1) '(b c))
(check "length"            (my-length '(1 2 3 4 5)) 5)
(check "append"            (my-append '(1 2) '(3 4)) '(1 2 3 4))

;; ============================================================
;; 4. 函数组合器
;; ============================================================

(display "\n=== 函数组合器 ===\n")

(define (my-flip f) (lambda (a b) (f b a)))
(define (my-complement f) (lambda (x) (not (f x))))
(define (my-const x) (lambda _ x))
(define (my-compose . fns)
  (lambda (x)
    (let loop ((fns (reverse fns)) (result x))
      (if (null? fns) result
          (loop (cdr fns) ((car fns) result))))))

(check "flip"              ((my-flip -) 5 3) -2)
(check "complement"        ((my-complement even?) 3) #t)
(check "const"             ((my-const 42) 1 2 3) 42)
(check "compose"           ((my-compose (lambda (x) (* x 2))
                                         (lambda (x) (+ x 1))) 5) 12)

;; ============================================================
;; 5. 列表遍历高阶函数
;; ============================================================

(display "\n=== 列表遍历 ===\n")

(define (my-for-each fn lst)
  (if (not (null? lst))
    (begin (fn (car lst)) (my-for-each fn (cdr lst)))))

(define (my-map fn lst)
  (if (null? lst) '()
      (cons (fn (car lst)) (my-map fn (cdr lst)))))

(define (my-filter pred lst)
  (cond ((null? lst) '())
        ((pred (car lst)) (cons (car lst) (my-filter pred (cdr lst))))
        (else (my-filter pred (cdr lst)))))

(define (my-find pred lst)
  (cond ((null? lst) #f)
        ((pred (car lst)) (car lst))
        (else (my-find pred (cdr lst)))))

(check "map"              (my-map - '(1 2 3)) '(-1 -2 -3))
(check "filter"           (my-filter even? '(1 2 3 4 5 6)) '(2 4 6))
(check "find found"       (my-find even? '(1 2 3 4)) 2)
(check "find missing"     (my-find (lambda (x) (= x 10)) '(1 2 3)) #f)

;; for-each 副作用测试
(let ((acc '()))
  (my-for-each (lambda (x) (set! acc (cons x acc))) '(1 2 3))
  (check "for-each" (reverse acc) '(1 2 3)))

;; ============================================================
;; 6. 列表折叠
;; ============================================================

(display "\n=== 列表折叠 ===\n")

(define (my-fold-left f init lst)
  (let loop ((acc init) (xs lst))
    (if (null? xs) acc
        (loop (f acc (car xs)) (cdr xs)))))

(define (my-fold-right f init lst)
  (let loop ((xs lst) (acc init))
    (if (null? xs) acc
        (loop (cdr xs) (f (car xs) acc)))))

(define (my-reverse lst)
  (my-fold-left (lambda (acc x) (cons x acc)) '() lst))

(define (my-length-v2 lst)
  (my-fold-left (lambda (n _) (+ n 1)) 0 lst))

(check "fold-left +"      (my-fold-left + 0 '(1 2 3 4)) 10)
(check "fold-right -"     (my-fold-right - 0 '(1 2 3)) 2)
(check "reverse"          (my-reverse '(1 2 3)) '(3 2 1))
(check "length v2"        (my-length-v2 '(a b c d)) 4)

;; ============================================================
;; 7. 比较器
;; ============================================================

(display "\n=== 比较器 ===\n")

(define (my<? . args)
  (or (null? args) (null? (cdr args))
      (and (< (car args) (cadr args))
           (apply my<? (cdr args)))))

(define (my<=? . args)
  (or (null? args) (null? (cdr args))
      (and (<= (car args) (cadr args))
           (apply my<=? (cdr args)))))

(check "<? 1 2 3"         (my<? 1 2 3) #t)
(check "<? 1 3 2"         (my<? 1 3 2) #f)
(check "<=? 1 2 2"        (my<=? 1 2 2) #t)
(check "<=? 1 2 1"        (my<=? 1 2 1) #f)

;; ============================================================
;; 8. 类型转换
;; ============================================================

(display "\n=== 类型转换 ===\n")

(define (my-number->string n)
  (let ((s (number->string n)))
    (if (and (string? s) (> (string-length s) 1)
             (char=? (string-ref s 0) #\#))
        (substring s 2 (string-length s))
        s)))

(check "number->string 42" (my-number->string 42) "42")

(display "\n=== 全部测试完成 ===\n")
