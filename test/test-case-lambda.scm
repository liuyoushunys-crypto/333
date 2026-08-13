;; test-case-lambda.scm — case-lambda 宏全面测试
;; Run: python3 miniscm.py test/test-case-lambda.scm
;;
;; 测试策略:
;;   1. 基本元数分派 — 0/1/2/3+ 参数匹配
;;   2. rest 参数子句 — 兜底匹配任意参数
;;   3. 点对形式参数 — (x . rest) 至少匹配 N 个
;;   4. 无匹配报错 — case-lambda: no matching clause
;;   5. 单子句 / 纯 rest 子句边界情况
;;   6. 高阶函数 — map / apply 结合使用
;;   7. JIT 编译路径 — 命名函数 + 循环体
;;   8. 嵌套 case-lambda — 返回 case-lambda 的 case-lambda

(display "\n=== case-lambda 全面测试 ===\n\n")

;; ════════════════════════════════════════════════════════════════
;; 1. 基本元数分派
;; ════════════════════════════════════════════════════════════════

(display "-- 基本元数分派\n")

(define cl-basic
  (case-lambda
    (()      0)
    ((x)     x)
    ((x y)   (+ x y))
    ((x y z) (+ x y z))
    (args    (apply + args))))

(test-equal "cl-basic 0 args"    0 (cl-basic))
(test-equal "cl-basic 1 arg"     42 (cl-basic 42))
(test-equal "cl-basic 2 args"    7 (cl-basic 3 4))
(test-equal "cl-basic 3 args"    9 (cl-basic 2 3 4))
(test-equal "cl-basic 4 args"    15 (cl-basic 1 2 3 4 5))

(display "-- 只匹配 rest\n")

(define cl-rest-only
  (case-lambda
    (args (apply list args))))

(test-equal "cl-rest-only 0"  '() (cl-rest-only))
(test-equal "cl-rest-only 1"  '(7) (cl-rest-only 7))
(test-equal "cl-rest-only 5"  '(1 2 3 4 5) (cl-rest-only 1 2 3 4 5))

(display "-- 数字运算分派\n")

(define cl-math
  (case-lambda
    (()       -1)
    ((x)      (* x x))
    ((x y)    (* x y))
    ((x y z)  (* x y z))
    (rest     (apply * rest))))

(test-equal "cl-math 0"     -1 (cl-math))
(test-equal "cl-math 1"     25 (cl-math 5))
(test-equal "cl-math 2"     42 (cl-math 6 7))
(test-equal "cl-math 3"     60 (cl-math 3 4 5))
(test-equal "cl-math 5"     120 (cl-math 1 2 3 4 5))

(display "-- 字符串处理分派\n")

(define cl-str
  (case-lambda
    (()           "")
    ((s)          s)
    ((s n)        (substring s 0 n))
    ((s start end) (substring s start end))
    (rest         (apply string-append rest))))

(test-equal "cl-str 0"       "" (cl-str))
(test-equal "cl-str 1"       "hello" (cl-str "hello"))
(test-equal "cl-str 2"       "hel" (cl-str "hello" 3))
(test-equal "cl-str 3"       "ell" (cl-str "hello" 1 4))
(test-equal "cl-str multi"   "abcd" (cl-str "a" "b" "c" "d"))

;; ════════════════════════════════════════════════════════════════
;; 2. Rest 参数子句（点对形式）
;; ════════════════════════════════════════════════════════════════

(display "\n-- 点对形式参数 (x . rest)\n")

(define cl-dot
  (case-lambda
    ((x . rest) (cons x rest))))

(test-equal "cl-dot 1 arg"  '(a) (cl-dot 'a))
(test-equal "cl-dot 3 args" '(a b c) (cl-dot 'a 'b 'c))
(test-equal "cl-dot 5 args" '(1 2 3 4 5) (cl-dot 1 2 3 4 5))

(define cl-mixed-dot
  (case-lambda
    (()          '(none))
    ((x y)       (list x y))
    ((x . rest)  (cons x rest))))

(test-equal "cl-mixed-dot 0"     '(none) (cl-mixed-dot))
(test-equal "cl-mixed-dot 2"    '(a b) (cl-mixed-dot 'a 'b))
(test-equal "cl-mixed-dot 3"    '(x y z) (cl-mixed-dot 'x 'y 'z))
(test-equal "cl-mixed-dot 1"    '(only) (cl-mixed-dot 'only))

(display "-- 点对+精确匹配 vs rest\n")

;; 点对形式只匹配至少 N 个，rest 匹配任意个
(define cl-dot-precise
  (case-lambda
    ((x y . rest) (list '>=2 x y rest))
    ((x . rest)   (list '>=1 x rest))))

(test-equal "cl-dot-precise 1"  '(>=1 a ()) (cl-dot-precise 'a))
(test-equal "cl-dot-precise 2"  '(>=2 a b ()) (cl-dot-precise 'a 'b))
(test-equal "cl-dot-precise 4"  '(>=2 a b (c d)) (cl-dot-precise 'a 'b 'c 'd))

;; ════════════════════════════════════════════════════════════════
;; 3. 无匹配子句报错
;; ════════════════════════════════════════════════════════════════

(display "\n-- 无匹配报错\n")

(define cl-no-rest
  (case-lambda
    ((x) (* x 2))
    ((x y) (+ x y))))

(test-equal "cl-no-rest 1"  10 (cl-no-rest 5))
(test-equal "cl-no-rest 2"  7 (cl-no-rest 3 4))

;; 无 rest 子句时报错，用 guard 捕获
(test-assert "cl-no-rest 0 error"
   (guard (e (else (string-contains? (error-object-message e) "no matching arity")))
    (cl-no-rest)
    #f))

(test-assert "cl-no-rest 3 error"
   (guard (e (else (string-contains? (error-object-message e) "no matching arity")))
    (cl-no-rest 1 2 3)
    #f))

;; ════════════════════════════════════════════════════════════════
;; 4. 单子句 / 边界情况
;; ════════════════════════════════════════════════════════════════

(display "\n-- 单子句边界情况\n")

(define cl-single-0
  (case-lambda
    (() 42)))

(test-equal "cl-single-0" 42 (cl-single-0))
(test-assert "cl-single-0 error"
  (guard (e (else (error-object? e)))
    (cl-single-0 1)
    #f))

(define cl-single-rest
  (case-lambda
    (args (length args))))

(test-equal "cl-single-rest 0" 0 (cl-single-rest))
(test-equal "cl-single-rest 1" 1 (cl-single-rest 'a))
(test-equal "cl-single-rest 5" 5 (cl-single-rest 1 2 3 4 5))

(display "-- 同一元数多个子句（按顺序第一个匹配）\n")

(define cl-first-match
  (case-lambda
    ((x) (list 'first x))
    ((y) (list 'second y))))

(test-equal "cl-first-match" '(first 42) (cl-first-match 42))

;; ════════════════════════════════════════════════════════════════
;; 5. 高阶函数结合
;; ════════════════════════════════════════════════════════════════

(display "\n-- 高阶函数结合\n")

;; map 中使用 case-lambda
(define cl-double-or-sum
  (case-lambda
    ((x) (* x 2))
    ((x y) (+ x y))))

(test-equal "map case-lambda 1" '(2 4 6 8 10)
  (map cl-double-or-sum '(1 2 3 4 5)))

(test-equal "map case-lambda 2" '(3 7 11)
  (map cl-double-or-sum '(1 3 5) '(2 4 6)))

;; apply 结合 case-lambda
(test-equal "apply case-lambda" 15
  (apply cl-double-or-sum 7 8))

;; case-lambda 作为返回值
(define (make-arithmetic op identity)
  (case-lambda
    (() identity)
    ((x) x)
    ((x y) (op x y))
    (args (apply op args))))

(define cl-add (make-arithmetic + 0))
(define cl-mul (make-arithmetic * 1))

(test-equal "factory add 0"     0 (cl-add))
(test-equal "factory add 1"     42 (cl-add 42))
(test-equal "factory add 2"     7 (cl-add 3 4))
(test-equal "factory add 3"     9 (cl-add 2 3 4))
(test-equal "factory mul 0"     1 (cl-mul))
(test-equal "factory mul 1"     5 (cl-mul 5))
(test-equal "factory mul 2"     42 (cl-mul 6 7))
(test-equal "factory mul 3"     60 (cl-mul 3 4 5))

;; ════════════════════════════════════════════════════════════════
;; 6. JIT 编译路径（命名函数 + 循环）
;; ════════════════════════════════════════════════════════════════

(display "\n-- JIT 编译路径\n")

;; 命名函数触发 JIT 编译
(define (cl-fact n)
  ((case-lambda
     (() 1)
     ((n) (if (= n 0) 1 (* n (cl-fact (- n 1))))))
   n))

(test-equal "cl-fact 0" 1 (cl-fact 0))
(test-equal "cl-fact 5" 120 (cl-fact 5))
(test-equal "cl-fact 10" 3628800 (cl-fact 10))

(display "-- 循环中反复调用 case-lambda\n")

(define cl-loop-sum
  (let ((f (case-lambda
             (() 0)
             ((x) x)
             ((x y) (+ x y))
             (rest (apply + rest)))))
    (lambda (lst)
      (let loop ((l lst) (acc 0))
        (if (null? l) acc
            (loop (cdr l) (f acc (car l))))))))

(test-equal "cl-loop-sum empty" 0 (cl-loop-sum '()))
(test-equal "cl-loop-sum list" 15 (cl-loop-sum '(1 2 3 4 5)))

(display "-- 嵌套 case-lambda\n")

(define cl-dispatch
  (case-lambda
    (() (case-lambda
          (() 'null-null)
          ((x) (list 'null x))))
    ((x) (case-lambda
          ((y) (list x y))
          ((y z) (list x y z))))
    ((x y) (list x y))))

(test-equal "cl-dispatch 0 -> 0"    'null-null ((cl-dispatch)))
(test-equal "cl-dispatch 0 -> 1"    '(null 42) ((cl-dispatch) 42))
(test-equal "cl-dispatch 1 -> 1"    '(a b) ((cl-dispatch 'a) 'b))
(test-equal "cl-dispatch 1 -> 2"    '(a b c) ((cl-dispatch 'a) 'b 'c))
(test-equal "cl-dispatch 2"         '(x y) (cl-dispatch 'x 'y))

;; ════════════════════════════════════════════════════════════════
;; 7. 精确元数与 >= 元数混合
;; ════════════════════════════════════════════════════════════════

(display "\n-- 混合精确/范围匹配\n")

(define cl-mixed-arity
  (case-lambda
    ((x)              (list '=1 x))
    ((x y)            (list '=2 x y))
    ((x y z)          (list '=3 x y z))
    ((a b c . d)      (list '>=3 a b c d))))

(test-equal "cl-mixed-arity 1"    '(=1 10) (cl-mixed-arity 10))
(test-equal "cl-mixed-arity 2"    '(=2 10 20) (cl-mixed-arity 10 20))
(test-equal "cl-mixed-arity 3"    '(=3 10 20 30) (cl-mixed-arity 10 20 30))
(test-equal "cl-mixed-arity 4"    '(>=3 10 20 30 (40)) (cl-mixed-arity 10 20 30 40))
(test-equal "cl-mixed-arity 6"    '(>=3 10 20 30 (40 50 60)) (cl-mixed-arity 10 20 30 40 50 60))

;; ════════════════════════════════════════════════════════════════
;; 8. case-lambda 作为参数传递
;; ════════════════════════════════════════════════════════════════

(display "\n-- 作为参数传递\n")

(define (call-with-args f . args)
  (apply f args))

(define cl-passthru
  (case-lambda
    (() 'none)
    ((x) x)
    ((x y) (+ x y))))

(test-equal "passthru 0"  'none (call-with-args cl-passthru))
(test-equal "passthru 1"  42 (call-with-args cl-passthru 42))
(test-equal "passthru 2"  7 (call-with-args cl-passthru 3 4))

;; ════════════════════════════════════════════════════════════════
;; 9. 直接内联使用
;; ════════════════════════════════════════════════════════════════

(display "\n-- 内联 case-lambda\n")

(test-equal "inline 0"  0 ((case-lambda (() 0) ((x) x) (else (apply + else))) 0))
(test-equal "inline 1"  42 ((case-lambda (() 0) ((x) x) (else (apply + else))) 42))
(test-equal "inline 2"  7 ((case-lambda (() 0) ((x) x) (else (apply + else))) 3 4))
(test-equal "inline 4"  10 ((case-lambda (() 0) ((x) x) (else (apply + else))) 1 2 3 4))

;; inline 中 case-lambda 的 rest 参数使用 else 关键字
(test-equal "inline else" 15
  ((case-lambda
     (() 0)
     ((x) x)
     ((x y) (+ x y))
     (else (apply + else)))
   1 2 3 4 5))

;; ════════════════════════════════════════════════════════════════
;; 10. body 含多个表达式（begin 隐含）
;; ════════════════════════════════════════════════════════════════

(display "\n-- 多表达式 body\n")

(define cl-multi-body
  (case-lambda
    ((x)
     (display "  (cl-multi-body 1) body multi-expr\n")
     (* x 2))
    ((x y)
     (display "  (cl-multi-body 2) body multi-expr\n")
     (let ((s (+ x y)))
       (display "    sum: ") (display s) (newline)
       s))
    (rest
     (display "  (cl-multi-body rest) ")
     (let ((r (apply + rest)))
       (display "sum: ") (display r) (newline)
       r))))

(test-equal "cl-multi-body 1" 10 (cl-multi-body 5))
(test-equal "cl-multi-body 2" 7 (cl-multi-body 3 4))
(test-equal "cl-multi-body 3" 6 (cl-multi-body 1 2 3))

(display "\n=== case-lambda 测试完成 ===\n")
