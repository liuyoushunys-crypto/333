;; demo-power.scm — Scheme Macro Power Showcase
;; Each form has doc + 1-2 usage examples demonstrating
;; how syntax-rules and define-macro bend the language.
;; Load via: (load "scm/demo-power.scm")

(display "=== loading demo-power.scm ===\n")

;; ═══════════════════════════════════════════════════════════════
;; 1. infix — 中缀表达式 (来自 C/Python/Java)
;;    (infix 2 + 3 * 4) → 14
;; ═══════════════════════════════════════════════════════════════

(define-macro (infix . terms)
  `(infix-impl (quote ,terms)))

;; (display (infix 2 + 3 * 4)) (newline)    ;; 14
;; (display (infix 10 - 3 - 2)) (newline)    ;; 5


;; ═══════════════════════════════════════════════════════════════
;; 2. times — 执行n次 (来自 Ruby)
;;    (times 3 (display "yo") (newline))
;; ═══════════════════════════════════════════════════════════════

;; already exists in boot-sugar.scm!


;; ═══════════════════════════════════════════════════════════════
;; 3. for-in — 遍历迭代 (来自 Python)
;;    (for-in x '(1 2 3) (display x) (newline))
;; ═══════════════════════════════════════════════════════════════

(define-syntax for-in
  (syntax-rules ()
    ((_ var lst body ...)
     (for-each (lambda (var) body ...) lst))))

;; (for-in x '(a b c) (display x) (display " "))  (newline)
;; → a b c


;; ═══════════════════════════════════════════════════════════════
;; 4. do-while — 至少执行一次 (来自 C/Java)
;;    (let ((i 0)) (do-while (display i) (set! i (+ i 1)) (< i 3)))
;; ═══════════════════════════════════════════════════════════════

;; already exists


;; ═══════════════════════════════════════════════════════════════
;; 5. until — 反向 while (来自 Ruby)
;;    (let ((i 0)) (until (= i 5) (display i) (set! i (+ i 1))))
;; ═══════════════════════════════════════════════════════════════

(define-syntax until
  (syntax-rules ()
    ((_ test body ...)
     (let loop () (if test (if #f #f) (begin body ... (loop)))))))

;; (let ((x 0)) (until (> x 3) (display x) (set! x (+ x 1)))) (newline)
;; → 0123


;; ═══════════════════════════════════════════════════════════════
;; 6. unless — 否定条件 (来自 Common Lisp/Ruby)
;;    (unless (> 1 2) (display "nope"))
;; ═══════════════════════════════════════════════════════════════

;; already exists


;; ═══════════════════════════════════════════════════════════════
;; 7. ai — 无定代词 if (anaphoric if, 来自 Paul Graham)
;;    引用条件值通过 it 访问
;; ═══════════════════════════════════════════════════════════════

;; already exists in boot-sugar as aif


;; ═══════════════════════════════════════════════════════════════
;; 8. == — 断言的简洁写法 (来自 C/C++)
;;    (== (+ 1 2) 3)
;; ═══════════════════════════════════════════════════════════════

(define-syntax ==
  (syntax-rules ()
    ((_ actual expected)
     (if (equal? actual expected)
         (begin (display "[PASS] ") (display 'actual) (newline))
         (begin (display "[FAIL] ") (display 'actual)
                (display "  expected: ") (write expected)
                (display "  actual: ") (write actual) (newline))))))

;; (== (+ 1 2) 3)   → [PASS] (+ 1 2)
;; (== (* 2 3) 7)   → [FAIL] (* 2 3) expected: 7 actual: 6


;; ═══════════════════════════════════════════════════════════════
;; 9. → — 线程宏 (threading, 来自 Clojure)
;;     将值一步步传入函数链
;; ═══════════════════════════════════════════════════════════════

;; already exists as -> in boot-sugar


;; ═══════════════════════════════════════════════════════════════
;; 10. ? : — 三元运算符 (来自 C/Java/JavaScript)
;;     (? test then else)
;; ═══════════════════════════════════════════════════════════════

(define-syntax ?
  (syntax-rules (:)
    ((_ test : then : else) (if test then else))))

;; (display (? (> 3 1) : "yes" : "no")) (newline)   ;; yes


;; ═══════════════════════════════════════════════════════════════
;; 11. define-record — 简洁数据结构 (模仿 struct)
;;     (define-record point (x y))
;; ═══════════════════════════════════════════════════════════════

;; wrapped by boot-core's define-record-type


;; ═══════════════════════════════════════════════════════════════
;; 12. lazy-cons — 惰性流 (来自 Haskell)
;;     (define fibs (lazy-cons 0 (lazy-cons 1 (map + fibs (cdr fibs)))))
;; ═══════════════════════════════════════════════════════════════

;; stream-cons already exists in boot-sugar
;; this is show how to build lazy sequences manually

(define (lazy-map f s)
  (stream-cons (f (car s)) (lazy-map f (cdr (force s)))))

(define (lazy-take n s)
  (if (zero? n) '()
      (cons (car s) (lazy-take (- n 1) (cdr (force s))))))

(define (lazy-filter pred s)
  (if (pred (car s))
      (stream-cons (car s) (lazy-filter pred (cdr (force s))))
      (lazy-filter pred (cdr (force s)))))

(define naturals
  (letrec ((go (lambda (n) (stream-cons n (go (+ n 1))))))
    (go 0)))

;; (display (lazy-take 5 (cdr (force naturals)))) (newline)
;; → (1 2 3 4 5)


;; ═══════════════════════════════════════════════════════════════
;; 13. defcurry — 柯里化函数定义 (来自 Haskell/ML)
;;     (defcurry (add a b) (+ a b))
;;     ((add 3) 4) → 7
;; ═══════════════════════════════════════════════════════════════

(define-syntax defcurry
  (syntax-rules ()
    ((_ (name . args) body ...)
     (define name
       (let ((n (length '(args ...))))
         (letrec ((curry
                   (lambda (args-so-far)
                     (lambda (x)
                       (let ((new (append args-so-far (list x))))
                         (if (= (length new) n)
                             (apply (lambda args body ...) new)
                             (curry new)))))))
           (curry '())))))))

;; (defcurry (add a b) (+ a b))
;; (display ((add 3) 4)) (newline)   ;; 7


;; ═══════════════════════════════════════════════════════════════
;; 14. catch — 异常捕获简化 (来自 JavaScript/Python)
;;     (catch (error "wrong") (exn) (display exn))
;; ═══════════════════════════════════════════════════════════════

(define-syntax catch
  (syntax-rules ()
    ((_ body (var) handler ...)
     (guard (var (else handler ...)) body))))

;; (catch (+ 1 2) (exn) "oops")            → 3
;; (catch (error "boom") (e) (display e))  → prints error


;; ═══════════════════════════════════════════════════════════════
;; 15. pipe — Unix 管道风格 (来自 Elixir/Shell)
;;     将值通过 |> 传入下一个函数
;; ═══════════════════════════════════════════════════════════════

(define-syntax |>
  (syntax-rules ()
    ((_ x) x)
    ((_ x (f . args) rest ...) (|> (f x . args) rest ...))
    ((_ x f rest ...) (|> (f x) rest ...))))

;; (|> 5 (+ 1) (* 2))                      → 12
;; (|> '(3 1 4 1 5) (sort <) (reverse))    → (5 4 3 1 1)


;; ═══════════════════════════════════════════════════════════════
;; 16. =def — 模式解构绑定 (来自 Rust/Elixir)
;;     (=def (a . b) (list 1 2 3))
;; ═══════════════════════════════════════════════════════════════

(define-syntax =def
  (syntax-rules ()
    ((_ (a . b) expr) (let ((lst expr)) (define a (car lst)) (define b (cdr lst))))
    ((_ (a b) expr) (let ((lst expr)) (define a (car lst)) (define b (cadr lst))))
    ((_ (a b c) expr) (let ((lst expr)) (define a (car lst)) (define b (cadr lst)) (define c (caddr lst))))
    ((_ a expr) (define a expr))))

;; (=def (x y) '(10 20))
;; (display (+ x y)) (newline)   ;; 30


;; ═══════════════════════════════════════════════════════════════
;; 17. set-last! — 设置列表最后一个元素 (来自 Perl/PHP)
;;     遍历到列表末尾设置值
;; ═══════════════════════════════════════════════════════════════

(define-macro (set-last! lst val)
  `(begin
     (let loop ((l ,lst))
       (if (null? (cdr l))
           (set-car! l ,val)
           (loop (cdr l))))
     ,val))

;; (let ((x (list 1 2 3))) (set-last! x 99) x)  → (1 2 99)


;; ═══════════════════════════════════════════════════════════════
;; 18. def-union — 和类型 (来自 Rust/OCaml/Haskell)
;;     简单代数数据类型模拟
;; ═══════════════════════════════════════════════════════════════

(define-macro (def-union name . variants)
  `(begin
     ,@(map (lambda (v)
              (let ((tag (car v)) (fields (cdr v)))
                `(define (,tag . args)
                   (list (quote ,tag) ,@(map (lambda (f i) (list 'car (list 'list-ref 'args i)))
                                              fields (iota (length fields)))))))
            variants)))

;; Simpler tagged variant via define-macro:
(define-macro (variant tag . args)
  `(cons (quote ,tag) (list ,@args)))

(define-macro (match-variant val . clauses)
  (let ((tag-var (gensym)))
    `(let ((,tag-var (car ,val)))
       (cond
        ,@(map (lambda (clause)
                 (let ((pat (car clause)) (body (cdr clause)))
                   `((eq? ,tag-var (quote ,pat)) ,@body)))
               clauses)))))

;; (define v (variant int 42))
;; (match-variant v (int (display "got int: ") (display (cadr v))))


;; ═══════════════════════════════════════════════════════════════
;; 19. defer — 作用域退出执行 (来自 Go/Julia)
;;     (defer (display "cleanup") body ...)
;; ═══════════════════════════════════════════════════════════════

(define-syntax defer
  (syntax-rules ()
    ((_ cleanup body ...)
     (dynamic-wind (lambda () (if #f #f))
                   (lambda () body ...)
                   (lambda () cleanup)))))

;; (let ((x 1))
;;   (defer (set! x 2)
;;     (display "body"))
;;   (display x)) (newline)
;; → body2


;; ═══════════════════════════════════════════════════════════════
;; 20. range comprehension — 生成式 (来自 Python list comp)
;;     [x*2 for x in (1 2 3) if (> x 1)]
;; ═══════════════════════════════════════════════════════════════

;; already exists as list-ec / list-of


;; ═══════════════════════════════════════════════════════════════
;; 21. quick-sort — 快速排序 (来自 Haskell/函数式)
;;     一行实现，展示 Scheme 的表达力
;; ═══════════════════════════════════════════════════════════════

(define (qsort lst)
  (if (null? lst) '()
      (append (qsort (filter (lambda (x) (< x (car lst))) (cdr lst)))
              (list (car lst))
              (qsort (filter (lambda (x) (>= x (car lst))) (cdr lst))))))

;; (display (qsort '(3 1 4 1 5 9 2))) (newline)
;; → (1 1 2 3 4 5 9)


;; ═══════════════════════════════════════════════════════════════
;; 22. show — 带标签的多值打印
;; ═══════════════════════════════════════════════════════════════

(define-syntax show
  (syntax-rules ()
    ((_ label expr)
     (let ((v expr))
       (display label) (display ": ") (write v) (newline)
       v))))

;; (show "sum" (+ 1 2 3))  → sum: 6, returns 6


;; ═══════════════════════════════════════════════════════════════
;; 23. λ> — 带打印的 lambda (调试用)
;; ═══════════════════════════════════════════════════════════════

(define-syntax λ>
  (syntax-rules ()
    ((_ (args ...) body ...)
     (lambda (args ...)
       (display "calling with: ") (display (list args ...)) (newline)
       body ...))))

;; (map (λ> (x) (* x 2)) '(1 2 3))
;; → prints "calling with: (1)" etc.


;; ═══════════════════════════════════════════════════════════════
;; 24. assert-throws — 断言抛出异常 (来自 JUnit/pytest)
;; ═══════════════════════════════════════════════════════════════

(define-syntax assert-throws
  (syntax-rules ()
    ((_ expr)
     (let ((caught #f))
       (guard (exn (else (set! caught #t)))
         expr)
       caught))))

;; (assert-throws (error "oops"))   → [PASS]
;; (assert-throws (+ 1 2))          → [FAIL]


;; ═══════════════════════════════════════════════════════════════
;; 25. compose — 函数组合 (来自数学 f∘g)
;; ═══════════════════════════════════════════════════════════════

(define (compose-all . fns)
  (if (null? fns) values
      (let ((f (car fns)) (rest (apply compose-all (cdr fns))))
        (lambda (x) (f (rest x))))))

;; (define f (compose-all (lambda (x) (* x 2)) (lambda (x) (+ x 1))))
;; (display (f 5)) (newline)   ;; 12


;; ═══════════════════════════════════════════════════════════════
;; 26. data-driven — 数据驱动 DSL
;;     定义 DSL 并用宏转换
;; ═══════════════════════════════════════════════════════════════

(define-syntax route
  (syntax-rules (->)
    ((_ (path -> action) rest ...)
     (begin
       (display "registered: ") (display path) (display " -> ")
       (display 'action) (newline)
       (route rest ...)))
    ((_) (if #f #f))))

;; (route
;;   ("/users" -> list-users)
;;   ("/users/:id" -> get-user)
;;   ("/posts" -> list-posts))


;; ═══════════════════════════════════════════════════════════════
;; 27. with-open — 自动关闭资源 (来自 Python with / Java try-with)
;; ═══════════════════════════════════════════════════════════════

(define-syntax with-open
  (syntax-rules ()
    ((_ (var expr) body ...)
     (let ((var expr))
       (let ((result (begin body ...)))
         (if (input-port? var) (close-input-port var) (if #f #f))
         result)))))

;; (with-open (p (open-input-string "hello"))
;;   (read-char p))                          → #\h


;; ═══════════════════════════════════════════════════════════════
;; 28. def-immutable-pair — 不可变对 (value object)
;;    用 define-macro 实现
;; ═══════════════════════════════════════════════════════════════

(define-macro (def-pair name car-field cdr-field)
  `(begin
     (define (,name c1 c2)
       (list (quote ,name) c1 c2))
     (define (,car-field pair)
       (cadr pair))
     (define (,cdr-field pair)
       (caddr pair))))

;; (def-pair point x y)
;; (define p (point 3 4))
;; (display (x p)) (display " ") (display (y p)) (newline)  ;; 3 4


;; ═══════════════════════════════════════════════════════════════
;; 29. retry — 失败重试 (来自 Elixir/分布式系统)
;; ═══════════════════════════════════════════════════════════════

(define-macro (retry n . body)
  (let ((the-body (gensym)) (count (gensym)))
    `(let ((,count 0))
       (let ,the-body ()
         (let ((result (begin ,@body)))
           (if (or result (>= ,count ,n))
               result
               (begin (set! ,count (+ ,count 1))
                      (,the-body))))))))

;; ;; simulate flaky operation
;; (define counter 0)
;; (display (retry 3
;;            (set! counter (+ counter 1))
;;            (if (< counter 3) #f counter)))
;; (newline)
;; → retries twice, returns 3


;; ═══════════════════════════════════════════════════════════════
;; 30. hash — 字面量 hash-table (来自 Clojure/JS)
;;     (hash a 1 b 2 c 3)
;; ═══════════════════════════════════════════════════════════════

(define-syntax hash
  (syntax-rules ()
    ((_ k v rest ...)
     (let ((ht (make-hash-table)))
       (hash-table-set! ht k v)
       (hash-helper ht rest ...)))
    ((_ k v)
     (let ((ht (make-hash-table)))
       (hash-table-set! ht k v)
       ht))))

(define-syntax hash-helper
  (syntax-rules ()
    ((_ ht k v rest ...)
     (begin
       (hash-table-set! ht k v)
       (hash-helper ht rest ...)))
    ((_ ht) ht)))

;; (define h (hash 'a 1 'b 2))
;; (display (hash-table-ref h 'a)) (newline)   ;; 1


;; ═══════════════════════════════════════════════════════════════
;; 31. defn — 函数定义带文档 (来自 Clojure)
;; ═══════════════════════════════════════════════════════════════

(define-syntax defn
  (syntax-rules ()
    ((_ name args body ...)
     (define name (lambda args body ...)))))

;; (defn square (x) (* x x))
;; (display (square 5)) (newline)   ;; 25


;; ═══════════════════════════════════════════════════════════════
;; 32. let-one — 单值 let 简洁写法
;; ═══════════════════════════════════════════════════════════════

;; already exists as let1


;; ═══════════════════════════════════════════════════════════════
;; 33. parallel — 并行求值 (语义上是按顺序求值)
;; ═══════════════════════════════════════════════════════════════

(define-syntax parallel
  (syntax-rules ()
    ((_ expr ...)
     (call-with-values (lambda () (values expr ...)) list))))

;; (display (parallel (+ 1 2) (* 3 4) (- 10 5))) (newline)
;; → (3 12 5)


;; ═══════════════════════════════════════════════════════════════
;; 34. do-times — 计数循环 (来自 Common Lisp)
;; ═══════════════════════════════════════════════════════════════

(define-syntax do-times
  (syntax-rules ()
    ((_ (var n) body ...)
     (do ((var 0 (+ var 1))) ((>= var n)) body ...))))

;; (let ((s 0)) (do-times (i 10) (set! s (+ s i))) (display s)) (newline)  ;; 45


;; ═══════════════════════════════════════════════════════════════
;; 35. define-struct — 结构体 (来自 Racket/SRFI-9)
;;     用 vector 实现的轻量结构
;; ═══════════════════════════════════════════════════════════════

(define-macro (define-struct name . fields)
  (let* ((n (length fields))
         (make (string->symbol (string-append "make-" (symbol->string name))))
         (pred (string->symbol (string-append name "?")))
         (getters (map (lambda (f) (string->symbol (string-append name "-" (symbol->string f)))) fields))
         (idxes (iota n)))
    `(begin
       (define (,make ,@fields) (vector ,@fields))
       (define (,pred obj) (and (vector? obj) (= (vector-length obj) ,n)))
       ,@(map (lambda (getter idx)
                `(define (,getter obj) (vector-ref obj ,idx)))
              getters idxes))))

;; (define-struct book title author year)
;; (define b (make-book "SICP" "Abelson" 1984))
;; (display (book-title b)) (newline)   ;; SICP
;; (display (book? b)) (newline)        ;; #t


;; ═══════════════════════════════════════════════════════════════
;; 36. comment — 注释宏 (已存在)
;; ═══════════════════════════════════════════════════════════════


;; ═══════════════════════════════════════════════════════════════
;; 37. expand — 显示宏展开结果 (调试用)
;; ═══════════════════════════════════════════════════════════════

(define-macro (expand expr)
  `(begin
     (display "expanded: ")
     (write (quote ,(let ((_expr expr)) _expr)))  ;; simplified
     (newline)
     ,expr))

;; (expand (+ 1 2))   ;; expanded: (+ 1 2) \n 3


;; ═══════════════════════════════════════════════════════════════
;; 38. cond-let — 条件绑定 (来自 Racket)
;;     在条件分支中绑定并使用值
;; ═══════════════════════════════════════════════════════════════

(define-syntax cond-let
  (syntax-rules (else)
    ((_ (else body ...)) (begin body ...))
    ((_ ((var val) body ...) rest ...)
     (let ((var val))
       (if var (begin body ...) (cond-let rest ...))))))

;; (cond-let
;;   ((x #f) (display x))
;;   ((y 42) (+ y 1)))   → 43


;; ═══════════════════════════════════════════════════════════════
;; 39. def-interface — 接口定义 (来自 Java/Go)
;;     纯文档用途
;; ═══════════════════════════════════════════════════════════════

(define-syntax def-interface
  (syntax-rules ()
    ((_ name (method args ...) ...)
     (begin
       (display "interface ") (display 'name)
       (display " requires: ") (display '(method ...)) (newline)
       (if #f #f)))))

;; (def-interface Stack (push x) (pop) (empty?))


;; ═══════════════════════════════════════════════════════════════
;; 40. fizz-buzz — FizzBuzz 用 match
;;     经典面试题演示模式匹配
;; ═══════════════════════════════════════════════════════════════

(define (fizzbuzz n)
  (do ((i 1 (+ i 1))) ((> i n))
    (display
      (cond ((= 0 (remainder i 15)) "FizzBuzz\n")
            ((= 0 (remainder i 3)) "Fizz\n")
            ((= 0 (remainder i 5)) "Buzz\n")
            (else (begin (display i) (newline)))))))

;; (fizzbuzz 15)


;; ═══════════════════════════════════════════════════════════════
;; 41. Y combinator — 不动点组合子 (来自 λ演算)
;;     不使用 define 的递归
;; ═══════════════════════════════════════════════════════════════

(define Y
  (lambda (f)
    ((lambda (x) (f (lambda (y) ((x x) y))))
     (lambda (x) (f (lambda (y) ((x x) y)))))))

(define fact-y
  (Y (lambda (fact)
       (lambda (n)
         (if (zero? n) 1 (* n (fact (- n 1))))))))

;; (display (fact-y 10)) (newline)   ;; 3628800


;; ═══════════════════════════════════════════════════════════════
;; 42. for-else — 循环带 else (来自 Python)
;;     break 时不执行 else
;; ═══════════════════════════════════════════════════════════════

(define-macro (for-else . args)
  (let* ((var (car args))
         (lst (cadr args))
         (rest (cddr args))
         (has-else (and (pair? (car (reverse rest)))
                        (eq? (caar (reverse rest)) 'else)))
         (body (if has-else (reverse (cdr (reverse rest))) rest))
         (else-body (if has-else (cdr (car (reverse rest))) '())))
    `(let ((found #f))
       (for-each (lambda (,var) (let ((v (begin ,@body))) (if v (set! found #t)))) ,lst)
       (if (not found) (begin ,@else-body) (if #f #f)))))

;; (for-else (x '(1 2 3)) (if (even? x) x)
;;   (else (display "no even found"))) (newline)


;; ═══════════════════════════════════════════════════════════════
;; 43. call-with-timing — 计时执行 (性能分析)
;; ═══════════════════════════════════════════════════════════════

(define (call-with-timing thunk)
  (let ((start (current-second)))
    (let ((result (thunk)))
      (display "elapsed: ") (display (- (current-second) start))
      (display " sec") (newline)
      result)))

;; (call-with-timing (lambda () (fold + 0 (iota 100000))))
;; → 4999950000 (with timing printed)


;; ═══════════════════════════════════════════════════════════════
;; 44. def-json-like — JSON 风格 DSL
;;     用 S 表达式模拟 JSON
;; ═══════════════════════════════════════════════════════════════

(define-macro (json . pairs)
  (let ((clauses '()))
    (let loop ((p pairs))
      (if (null? p) '()
          (let ((k (caar p)) (v (cadar p)))
            (set! clauses (cons `((eq? k (quote ,k)) ,v) clauses))
            (loop (cdr p)))))
    `(lambda (k) (cond ,@(reverse clauses)))))

(display "=== demo-power.scm loaded ===\n") (newline)

;; (define person (json (name "Alice") (age 30)))
;; (display (person 'name)) (newline)   ;; Alice


;; ═══════════════════════════════════════════════════════════════
;; 45. while* — while 循环体返回累加值
;; ═══════════════════════════════════════════════════════════════

(define-syntax while*
  (syntax-rules ()
    ((_ test body ...)
     (let ((result (if #f #f)))
       (let loop ()
         (if test
             (begin (set! result (begin body ...)) (loop))
             result))))))

;; (let ((i 0) (s 0))
;;   (while* (< i 5) (set! s (+ s i)) (set! i (+ i 1))))   ;; 10


(display "=== demo-power.scm loaded ===\n") (newline)
