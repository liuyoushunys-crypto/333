;; ═══════════════════════════════════════════════════════════════════════════
;; boot-sugar.scm — 语法糖宏库
;; ═══════════════════════════════════════════════════════════════════════════
;; 概述: 本文件提供 5 个阶段约 70 个 syntax-rules 宏，分为用户宏、语法糖、
;;       多语言风格（C#/D/Haskell/Clojure/Kotlin/Rust/Elixir/Python/CL/Scheme）精华。
;; 依赖: boot-core.scm（提供基础的 Scheme 过程）
;; 加载时机: 解释器启动时在 boot-core.scm 之后自动加载
;; 测试文件: test/test-boot-sugar-usage.scm
;; ═══════════════════════════════════════════════════════════════════════════

;; ═══════════════════════════════════════════════════════════════════════════
;; Phase 5 — 用户宏（来自 scheme-macros.scm）
;; ═══════════════════════════════════════════════════════════════════════════
;; 从经典 Scheme 宏集合中精选的实用宏，涵盖位置访问、条件反转、惰性列、
;; 动态作用域、多值绑定、断言、推导式、测试框架等部门。

;; ── nth ──
;; 从剩余参数中取第 n 个元素（0-based）。
;;   用法: (nth n x ...)
;;   示例: (nth 0 'a 'b 'c)  => 'a
;;         (nth 2 'a 'b 'c)  => 'c
;;   展开: (list-ref (list x ...) n)
;;   注意: 不是通用列表索引宏，仅适用于已知数量的参数。
(define-syntax nth
  (syntax-rules ()
    ((_ n x ...)
     (list-ref (list x ...) n))))

;; ── if-not ──
;; 条件取反的 if。
;;   用法: (if-not cond then else)
;;   示例: (if-not #t 'yes 'no)  => 'no
;;         (if-not (< 1 2) 'a 'b) => 'b
;;   展开: (if cond else then)  — 交换 then/else
(define-syntax if-not
  (syntax-rules ()
    ((_ cond then else)
     (if cond else then))))

;; ── stream-cons ──
;; 惰性 cons 单元：cdr 被 delay 包装，通过 force 访问。
;;   用法: (stream-cons head tail)
;;   示例: (define s (stream-cons 1 (list 2 3)))
;;         (car s)           => 1
;;         (force (cdr s))   => '(2 3)
;;   展开: (cons head (delay tail))
;;   注意: 与 SRFI-41 的 stream-cons 用法一致。
(define-syntax stream-cons
  (syntax-rules ()
    ((_ head tail)
     (cons head (delay tail)))))

;; ── fluid-let ──
;; 动态作用域绑定：临时修改变量值，离开作用域后自动恢复。
;;   用法: (fluid-let ((var val) ...) body ...)
;;   示例: (define x 10)
;;         (fluid-let ((x 99)) x)  => 99
;;         x                        => 10
;;   展开: 递归地保存 → set! → 执行 body → 恢复
;;   注意: 不支持多变量并行绑定（嵌套递归实现，每个变量单独绑定）。
(define-syntax fluid-let
  (syntax-rules ()
    ((_ () body ...)
     (begin body ...))
    ((_ ((var val) . rest) body ...)
     (let ((saved var))
       (set! var val)
       (let ((result (fluid-let rest body ...)))
         (set! var saved)
         result)))))

;; ── receive ──
;; 绑定多返回值（SRFI-8）。
;;   用法: (receive formals expression body ...)
;;   示例: (receive (a b) (values 1 2) (+ a b))  => 3
;;   展开: (call-with-values (lambda () expression)
;;                           (lambda formals body ...))
(define-syntax receive
  (syntax-rules ()
    ((_ formals expression body ...)
     (call-with-values (lambda () expression) (lambda formals body ...)))))

;; ── with-values ──
;; 将生产者（无参函数）的多值传给消费者函数。
;;   用法: (with-values producer consumer)
;;   示例: (with-values (values 3 4) (lambda (a b) (* a b)))  => 12
;;   注意: producer 是表达式（宏负责包装为 lambda），
;;         不是无参函数。
(define-syntax with-values
  (syntax-rules ()
    ((_ producer consumer)
     (call-with-values (lambda () producer) consumer))))

;; ── assume ──
;; 断言表达式为真，失败时报错。
;;   用法: (assume expr)
;;   示例: (assume (= 1 1))  => #t
;;         (assume (= 1 2))  => error "assume failed: (= 1 2)"
;;   展开: (or expr (error "assume failed:" 'expr))
;;   注意: 利用 or 短路：expr 真时返回，否则报错。
(define-syntax assume
  (syntax-rules ()
    ((_ expr)
     (or expr (error "assume failed:" 'expr)))))

;; ── and-let* ──
;; 顺序绑定 + 短路求值（SRFI-2）。
;;   用法: (and-let* ((var val) ...) body ...)
;;   示例: (and-let* ((a 1) (b 2)) (+ a b))    => 3
;;         (and-let* ((a #f) (b 2)) (+ a b))   => #f
;;         (and-let*)                           => #t
;;   支持四种子句形式：
;;     (var val)  — 绑定 var 为 val，若假则短路
;;     (var)      — 仅测试 var（需已绑定）若假则短路
;;     (test)     — 仅测试 test 表达式
;;     ()         — 无绑定，直接执行 body
(define-syntax and-let*
  (syntax-rules ()
    ((_) #t)
    ((_ () body ...)
     (if (null? (quote (body ...))) #t (begin body ...)))
    ((_ ((test) . rest) body ...)
     (if test (and-let* rest body ...) #f))
    ((_ ((var val) . rest) body ...)
     (let ((var val))
       (if var (and-let* rest body ...) #f)))
    ((_ (var . rest) body ...)
     (let ((var var))
       (if var (and-let* rest body ...) #f)))))

;; ── rec ──
;; 递归 lambda：无需 letrec 即可定义自引用函数。
;;   用法: (rec (name args ...) body ...)
;;   示例: (define fact (rec (fact n)
;;                   (if (= n 0) 1 (* n (fact (- n 1))))))
;;         (fact 5)  => 120
;;   展开: (letrec ((name (lambda args body ...))) name)
(define-syntax rec
  (syntax-rules ()
    ((_ (name . args) body ...)
     (letrec ((name (lambda args body ...))) name))))

;; ── do-ec ──
;; 命令式推导：带副作用的循环构造。
;;   用法: (do-ec expr clause ...)
;;   支持子句:
;;     (for var lst)  — 遍历列表
;;     (if test)      — 条件过滤
;;   示例: (define sum 0)
;;         (do-ec (set! sum (+ sum x)) (for x '(1 2 3 4 5)))
;;         sum  => 15
(define-syntax do-ec
  (syntax-rules (if for)
    ((_ expr (if test) rest ...)
     (if test (do-ec expr rest ...)))
    ((_ expr (for var lst) rest ...)
     (for-each (lambda (var) (do-ec expr rest ...)) lst))
    ((_ expr (for var lst))
     (for-each (lambda (var) expr) lst))
    ((_ expr)
     expr)))

;; ── list-ec ──
;; 列表推导式：类似 Haskell 的 list comprehension。
;;   用法: (list-ec expr clause ...)
;;   支持子句:
;;     (for var lst)   — 遍历
;;     (if test)       — 过滤
;;   示例: (list-ec (* x 2) (for x '(1 2 3 4)))          => '(2 4 6 8)
;;         (list-ec x (for x '(1 2 3 4 5)) (if (> x 2)))  => '(3 4 5)
;;         (list-ec (+ x y) (for x '(1 2)) (for y '(10 20)))
;;           => '(11 21 12 22)  笛卡尔积
(define-syntax list-ec
  (syntax-rules (for if)
    ((_ expr)
     (list expr))
    ((_ expr (for var lst))
     (map (lambda (var) expr) lst))
    ((_ expr (if test))
     (if test (list expr) '()))
    ((_ expr (for var lst) (if test) more ...)
     (apply append (map (lambda (var) (if test (list-ec expr more ...) '())) lst)))
    ((_ expr (for var lst) more ...)
     (apply append (map (lambda (var) (list-ec expr more ...)) lst)))))

;; ── sum-ec ──
;; 求和推导式。
;;   用法: (sum-ec expr clause ...)
;;   示例: (sum-ec x (for x '(1 2 3 4 5)))                 => 15
;;         (sum-ec x (for x '(1 2 3 4 5)) (if (> x 2)))    => 12
(define-syntax sum-ec
  (syntax-rules (for if)
    ((_ expr (if test) more ...)
     (if test (sum-ec expr more ...) 0))
    ((_ expr (for var lst) more ...)
     (apply + 0 (list-ec expr (for var lst) more ...)))
    ((_ expr)
     expr)))

;; ── any?-ec / every?-ec ──
;; 量化推导式：存在/全部满足条件。
;;   用法: (any?-ec? predicate clause ...)
;;         (every?-ec predicate clause ...)
;;   示例: (any?-ec (even? x) (for x '(1 3 5 7)))      => #f
;;         (every?-ec (odd? x) (for x '(1 3 5 7)))      => #t
(define-syntax any?-ec
  (syntax-rules (for if)
    ((_ expr (for var lst) more ...)
     (any (lambda (var) (any?-ec expr more ...)) lst))
    ((_ expr (if test) more ...)
     (if test (any?-ec expr more ...) #f))
    ((_ expr)
     expr)))

(define-syntax every?-ec
  (syntax-rules (for if)
    ((_ expr (for var lst) more ...)
     (every (lambda (var) (every?-ec expr more ...)) lst))
    ((_ expr (if test) more ...)
     (if test (every?-ec expr more ...) #f))
    ((_ expr)
     expr)))

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (display expected)
             (display "  actual: ") (display actual) (newline))))

;; ── check-ec ──
;; 推导式测试断言：检查推导中所有元素均符合预期。
;;   用法: (check-ec expected (for var lst) expr)
;;   示例: (check-ec 1 (for x '(1 2 3)) x)
(define-syntax check-ec
  (syntax-rules (for if)
    ((_ expected (for var lst) expr)
     (every?-ec (equal? expr expected) (for var lst)))
    ((_ expected (for var lst) (if test) expr)
     (every?-ec (equal? expr expected) (for var lst) (if test)))))

;; ── aif (anaphoric if) ──
;; 指代 if：成功分支中 it 自动绑定测试值。
;;   用法: (aif test then else)
;;   示例: (aif (+ 1 2) (* it 2) 'nope)   => 6
;;         (aif (memq 2 '(1 2 3)) (car it) 'nope)  => 2
;;   展开: (let ((it test)) (if it then else))
(define-syntax aif
  (syntax-rules ()
    ((_ test then else)
     (let ((it test))
       (if it then else)))))

;; ── aand (anaphoric and) ──
;; 指代 and：每个表达式的结果绑定到 it，短路求值。
;;   用法: (aand expr ...)
;;   示例: (aand 1 2 3)          => 3
;;         (aand 1 #f 3)         => #f
;;         (aand 1 2 (+ it 3))   => 5
(define-syntax aand
  (syntax-rules ()
    ((_) #t)
    ((_ expr) expr)
    ((_ expr . rest)
     (let ((it expr))
       (if it (aand . rest) it)))))

;; ── alet (anaphoric let) ──
;; 指代 let：标准 let 别名，无特殊 it 绑定。
;;   用法: (alet ((var val) ...) body ...)
;;   示例: (alet ((x 1) (y 2)) (+ x y))  => 3
(define-syntax alet
  (syntax-rules ()
    ((_ ((var val) ...) body ...)
     (let ((var val) ...) body ...))))

;; ── test-assert / test-equal ──
;; 带命名的测试宏，输出 [PASS]/[FAIL] 格式。
;;   用法: (test-assert name expr)        — 断言 expr 为真
;;         (test-equal name expected actual) — 断言期望=实际
;;   示例: (test-assert "positive" (positive? 5))
;;         (test-equal "sum" (+ 1 2) 3)
(define-syntax test-assert
  (syntax-rules ()
    ((_ name expr)
     (let ((result expr))
       (if result
         (begin (display (string-append "[PASS] " name)) (newline))
         (begin (display (string-append "[FAIL] " name)) (newline)))
       result))))

(define-syntax test-equal
  (syntax-rules ()
    ((_ name expected actual)
     (let ((e expected) (a actual))
       (if (equal? a e)
         (begin (display (string-append "[PASS] " name)) (newline))
         (begin (display (string-append "[FAIL] " name)) (newline)
                (display (string-append "  expected: " (with-output-to-string (lambda () (write e))))) (newline)
                (display (string-append "  actual:   " (with-output-to-string (lambda () (write a))))) (newline)))))))

;; ── define-immutable ──
;; 定义不可变函数（展开为 lambda，不创建可 set! 的变量）。
;;   用法: (define-immutable (name args ...) body ...)
;;   展开: (define name (lambda args body ...))
;;   注意: 与标准 define 的区别在于 define-immutable 保证函数体不变，
;;         不能通过 set! 替换。
(define-syntax define-immutable
  (syntax-rules ()
    ((_ (name . args) body ...)
     (define name (lambda args body ...)))))

;; ── dbind (destructuring bind) ──
;; 解构绑定：将列表按结构拆分为变量。
;;   用法: (dbind pattern expr body ...)
;;   支持的模式:
;;     ()         — 忽略值
;;     (a)        — 单值
;;     (a b)      — 双值 (car/cadr)
;;     (a b c)    — 三值 (car/cadr/caddr)
;;     (a . b)    — 点对 (car/cdr)
;;   示例: (dbind (a b) '(10 20) (+ a b))  => 30
(define-syntax dbind
  (syntax-rules ()
    ((_ () expr body ...)
     (begin body ...))
    ((_ (a) expr body ...)
     (let ((a expr)) body ...))
    ((_ (a b) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cadr tmp))) body ...)))
    ((_ (a b c) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cadr tmp)) (c (caddr tmp))) body ...)))
    ((_ (a . b) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cdr tmp))) body ...)))))

;; ═══════════════════════════════════════════════════════════════════════════
;; Phase 6 — 语法糖（易用性扩展）
;; ═══════════════════════════════════════════════════════════════════════════
;; 提供现代编程语言的常见语法元素：lambda 简写、自增自减、循环、
;; 线程宏、断言、条件绑定、列表推导、计时等。

;; ── λ ──
;; lambda 的 Unicode 简写。
;;   用法: (λ args body ...)
;;   示例: (define add (λ (a b) (+ a b)))
;;   展开: (lambda . args)
(define-syntax λ (syntax-rules () ((_ . args) (lambda . args))))

;; ── inc / dec ──
;; 自增/自减（可指定步长）。
;;   用法: (inc var) 或 (inc var n)   — var += 1 或 var += n
;;         (dec var) 或 (dec var n)   — var -= 1 或 var -= n
;;   示例: (define x 5) (inc x)       => x=6
;;         (inc x 3)                   => x=9
(define-syntax inc (syntax-rules () ((_ x) (set! x (+ x 1))) ((_ x n) (set! x (+ x n)))))
(define-syntax dec (syntax-rules () ((_ x) (set! x (- x 1))) ((_ x n) (set! x (- x n)))))

;; ── while ──
;; while 循环：条件为真时重复执行 body。
;;   用法: (while test body ...)
;;   示例: (define i 0) (while (< i 3) (display i) (set! i (+ i 1)))
;;   实现: 命名 let 递归
;;   注意: 返回 (if #f #f) 即未指定值。
(define-syntax while
  (syntax-rules ()
    ((_ test body ...)
     (let loop () (if test (begin body ... (loop)) (if #f #f))))))

;; ── for ──
;; for-each 的宏包装：遍历列表。
;;   用法: (for var in lst body ...)
;;   示例: (for x in '(a b c) (display x))
;;   展开: (for-each (lambda (var) body ...) lst)
(define-syntax for
  (syntax-rules (in)
    ((_ var in lst body ...)
     (for-each (lambda (var) body ...) lst))))

;; ── some-> ──
;; 条件线程宏：值为真时才传入下一个函数，遇 #f 短路。
;;   用法: (some-> x (f args ...) ...)
;;   示例: (some-> 3 (lambda (x) (+ x 1)) (lambda (x) (* x 2)))  => 8
;;         (some-> #f (lambda (x) (* x 2)))                      => #f
;;   对比: -> 宏在 boot-py.scm 中（支持 :method 语法）
;;   注意: 仅适用于真值检查（Scheme 中仅 #f 为假）。
(define-syntax some->
  (syntax-rules ()
    ((_ x) x)
    ((_ x (f . args)) (if x (f x . args) #f))
    ((_ x (f . args) . rest) (if x (some-> (f x . args) . rest) #f))))

;; ── doto ──
;; 对对象执行多个操作后返回对象本身（Clojure doto）。
;;   用法: (doto val (f args ...) ...)
;;   示例: (doto (list 1 2 3) (set-car! 10) (set-cdr! (list 20 30)))
;;   展开: (begin (f val args) ... val)
(define-syntax doto
  (syntax-rules ()
    ((_ val) val)
    ((_ val (f . args) rest ...)
     (begin (f val . args) (doto val rest ...)))))

;; ── and=> ──
;; 条件应用：值为真时应用函数，否则返回 #f。
;;   用法: (and=> val proc ...)
;;   示例: (and=> 5 (lambda (x) (* x 2)))           => 10
;;         (and=> (memq 3 '(1 2 3)) cdr car)         => 4
;;   展开: 单 proc: (if val (proc val) #f)
;;   注意: 支持链式调用。
(define-syntax and=>
  (syntax-rules ()
    ((_ val proc) (if val (proc val) #f))
    ((_ val proc . more) (if val (and=> (proc val) . more) #f))))

;; ── swap! ──
;; 交换两个变量的值。
;;   用法: (swap! a b)
;;   示例: (define x 1) (define y 2) (swap! x y)  => x=2, y=1
(define-syntax swap!
  (syntax-rules ()
    ((_ a b) (let ((tmp a)) (set! a b) (set! b tmp)))))

;; ── assert ──
;; 断言：条件为假时报错，可选择自定义消息。
;;   用法: (assert expr) 或 (assert expr msg)
;;   示例: (assert (= 1 1))
;;         (assert (> x 0) "x should be positive")
;;   注意: 使用 if #f #f 作为无值返回（避免某些实现的副作用）。
(define-syntax assert
  (syntax-rules ()
    ((_ expr) (if expr (if #f #f) (error "assertion failed:" 'expr)))
    ((_ expr msg) (if expr (if #f #f) (error "assertion failed:" msg 'expr)))))

;; ── if-let / when-let ──
;; 条件绑定：绑定值后检查真假，再决定分支。
;;   用法: (if-let (var val) then)
;;         (if-let (var val) then else)
;;         (when-let (var val) body ...)
;;   示例: (if-let (x 42) (* x 2))       => 84
;;         (if-let (x #f) 'then 'else)   => 'else
(define-syntax if-let
  (syntax-rules ()
    ((_ (var val) then) (let ((var val)) (if var then (if #f #f))))
    ((_ (var val) then else) (let ((var val)) (if var then else)))))

(define-syntax when-let
  (syntax-rules ()
    ((_ (var val) body ...) (let ((var val)) (when var body ...)))))

;; ── list-of ──
;; 列表推导（类 Python 语法）。
;;   用法: (list-of expr for var in lst)
;;         (list-of expr for var in lst if test)
;;   示例: (list-of (* x 2) for x in '(1 2 3 4))           => '(2 4 6 8)
;;         (list-of x for x in '(1 2 3 4 5) if (odd? x))   => '(1 3 5)
;;   注意: 与 list-ec 功能重叠，语法更接近 Python。
(define-syntax list-of
  (syntax-rules (for if)
    ((_ expr) (list expr))
    ((_ expr for var in lst) (map (lambda (var) expr) lst))
    ((_ expr for var in lst if test) (filter (lambda (var) test) (map (lambda (var) expr) lst)))
    ((_ expr for var in lst body ...) (map (lambda (var) expr) lst))))

;; ── ensure ──
;; 后置条件：计算结果必须满足谓词，否则报错。
;;   用法: (ensure expr pred) 或 (ensure expr pred msg)
;;   示例: (ensure (+ 2 3) (lambda (x) (= x 5)))  => 5
;;         (ensure (/ 1 0) number? "not a number") => error
(define-syntax ensure
  (syntax-rules ()
    ((_ expr pred) (let ((result expr)) (if (pred result) result (error "ensure failed:" 'expr result))))
    ((_ expr pred msg) (let ((result expr)) (if (pred result) result (error msg 'expr result))))))

;; ── timeit ──
;; 计时执行：打印耗时后返回表达式的值。
;;   用法: (timeit expr)
;;   示例: (timeit (fib 30))  => 打印 "elapsed: 0.xxx sec" 后返回结果
(define-syntax timeit
  (syntax-rules ()
    ((_ expr)
      (let* ((start (current-second)) (result expr) (end (current-second)))
        (display "elapsed: ") (display (- end start)) (display " sec") (newline)
        result))))

;; ═══════════════════════════════════════════════════════════════════════════
;; Phase 7 — C# 风格语法糖
;; ═══════════════════════════════════════════════════════════════════════════
;; 借鉴 C# 的实用语法元素：null 合并、模式匹配、资源管理、循环区间等。

;; ── ?? (null coalescing) ──
;; 空值合并：x 为真时返回 x，否则返回 default。
;;   用法: (?? x default)
;;   示例: (?? 42 0)        => 42
;;         (?? #f 'default) => 'default
;;   注意: Scheme 中仅 #f 为假，所以此宏等价于 (if x x default)。
(define-syntax ??
  (syntax-rules ()
    ((_ x default) (if x x default))))

;; ── ??= (null coalescing assign) ──
;; 空值赋值：var 为假时才赋值为 val 并返回 val。
;;   用法: (??= var val)
;;   示例: (??= x 99)  — 若 x 为假则设为 99
(define-syntax ??=
  (syntax-rules ()
    ((_ var val) (if var var (begin (set! var val) val)))))

;; ── match ──
;; 表达式匹配（C# switch 表达式语义）。
;;   用法: (match expr (pattern body ...) ...)
;;         (match expr (else body ...))
;;   示例: (match 2 (1 'one) (2 'two) (else 'other))  => 'two
;;   注意: pattern 是字面符号（被 quote 化），非通配符。
;;         若要匹配变量值，需使用 equal? 比较。
;;         关键字 else 作为默认分支。
(define-syntax match
  (syntax-rules (else)
    ((_ expr) #f)
    ((_ expr (else body ...)) (begin body ...))
    ((_ expr (pattern body ...) rest ...)
     (let ((v expr))
       (if (equal? v 'pattern) (begin body ...)
           (match v rest ...))))))

;; ── using ──
;; 资源管理：初始化资源，body 执行后自动关闭端口。
;;   用法: (using (var init) body ...)
;;   示例: (using (f (open-input-file "x.txt")) (read-char f))
;;   实现: dynamic-wind 确保无论正常或异常退出都关闭资源
(define-syntax using
  (syntax-rules ()
    ((_ (var init) body ...)
     (let ((var init))
       (dynamic-wind (lambda () (if #f #f))
                     (lambda () body ...)
                     (lambda () (if (input-port? var) (close-port var)
                                    (if (output-port? var) (close-port var) (if #f #f)))))))))

;; ── repeat ──
;; 重复执行 n 次循环（i 从 0 到 n-1）。
;;   用法: (repeat n body ...)
;;   示例: (repeat 5 (display "hi"))
(define-syntax repeat
  (syntax-rules ()
    ((_ n body ...)
     (do ((__idx__ 0 (+ __idx__ 1))) ((>= __idx__ n)) body ...))))

;; ── do-while ──
;; 后测试循环：至少执行一次 body。
;;   用法: (do-while body ... test)
;;   示例: (do-while (set! i (+ i 1)) (< i 5))
(define-syntax do-while
  (syntax-rules ()
    ((_ body ... test)
     (let loop () (begin body ...) (if test (loop) (if #f #f))))))

;; ── range ──
;; 生成数值区间列表。
;;   用法: (range start end)        — [start, end)
;;         (range start end step)   — 按步长
;;   示例: (range 0 5)    => '(0 1 2 3 4)
;;         (range 0 10 3) => '(0 3 6 9)
;;   实现: iota 来自 boot-core.scm
(define-syntax range
  (syntax-rules ()
    ((_ start end) (iota (- end start) start))
    ((_ start end step) (iota (ceiling (/ (- end start) step)) start step))))

;; ── nameof ──
;; 获取变量名的符号表示（C# nameof）。
;;   用法: (nameof var)
;;   示例: (nameof x) => 'x
(define-syntax nameof
  (syntax-rules ()
    ((_ var) 'var)))

;; ── cond? ──
;; 三元运算符（C# cond ? then : else）。
;;   用法: (cond? test then else)
;;   展开: 等价于 if
(define-syntax cond?
  (syntax-rules ()
    ((_ test then else) (if test then else))))

;; ── try-finally ──
;; 无论是否异常都执行清理代码。
;;   用法: (try-finally body ... cleanup ...)
;;   实现: dynamic-wind 确保 cleanup 必然执行
(define-syntax try-finally
  (syntax-rules ()
    ((_ body ... cleanup ...)
     (dynamic-wind (lambda () (if #f #f))
                   (lambda () body ...)
                   (lambda () cleanup ...)))))

;; ── try-catch ──
;; 异常捕获（C# try-catch）。
;;   用法: (try-catch body ... (exn handler ...))
;;   示例: (try-catch (error "boom") (exn (display "caught")))
;;   展开: 基于 guard 实现
(define-syntax try-catch
  (syntax-rules ()
    ((_ body ... (exn handler ...))
     (guard (exn (else handler ...)) body ...))))

;; ═══════════════════════════════════════════════════════════════════════════
;; Phase 8 — D 语言风格语法糖
;; ═══════════════════════════════════════════════════════════════════════════
;; 借鉴 D 语言的实用特性：作用域守卫、迭代辅助、记忆化、字符串模板等。

;; ── scope-exit ──
;; 作用域退出守卫：无论正常/异常退出都执行 cleanup。
;;   用法: (scope-exit cleanup body ...)
;;   示例: (scope-exit (display "bye") (display "hello"))
;;   输出: hello → bye（顺序执行）
(define-syntax scope-exit
  (syntax-rules ()
    ((_ cleanup body ...)
     (dynamic-wind (lambda () (if #f #f))
                   (lambda () body ...)
                   (lambda () cleanup)))))

;; ── scope-success ──
;; 成功退出守卫：body 正常执行完后执行 cleanup。
;;   用法: (scope-success cleanup body ...)
;;   注意: 不同于 scope-exit，异常退出时不执行 cleanup
(define-syntax scope-success
  (syntax-rules ()
    ((_ cleanup body ...)
     (let ((result (begin body ...)))
       cleanup
       result))))

;; ── countdown ──
;; 倒序迭代：从 end-1 递减到 start。
;;   用法: (countdown var start end body ...)
;;   示例: (countdown i 0 5 (display i)) => 4 3 2 1 0
(define-syntax countdown
  (syntax-rules ()
    ((_ var start end body ...)
     (do ((__idx__ (- end 1) (- __idx__ 1))) ((< __idx__ start))
       (let ((var __idx__)) body ...)))))

;; ── times ──
;; 重复执行 n 次，i 从 0 到 n-1。
;;   用法: (times n body ...)
;;   示例: (times 3 (display i)) => 0 1 2
(define-syntax times
  (syntax-rules ()
    ((_ n body ...)
     (do ((__idx__ 0 (+ __idx__ 1))) ((>= __idx__ n))
       (let ((i __idx__)) body ...)))))

;; ── with ──
;; 对同一对象连续调用方法（D with 语句）。
;;   用法: (with obj (method . args) ...)
;;   示例: (with (cons 1 2) (set-car! 10) (set-cdr! 20))
(define-syntax with
  (syntax-rules ()
    ((_ obj (method . args))
     (method obj . args))
    ((_ obj (method . args) rest ...)
     (begin
       (method obj . args)
       (with obj rest ...)))))

;; ── static-if ──
;; 编译期 if（在当前实现中直接展开为 if）。
;;   用法: (static-if test then else)
;;   未来: 可在宏展开时计算 test 常量，消除死代码
(define-syntax static-if
  (syntax-rules ()
    ((_ test then else)
     (if test then else))))

;; ── tap ──
;; 值传递的同时执行副作用（Ruby tap）。
;;   用法: (tap x (proc . args) ...)
;;   示例: (tap 42 (lambda (x) (display x)))  => 显示 42，返回 42
;;   用途: 调试管道中的中间值，不改变原值
(define-syntax tap
  (syntax-rules ()
    ((_ x) x)
    ((_ x (proc . args) rest ...)
     (begin
       (proc x . args)
       (tap x rest ...)))))

;; ── lazy ──
;; 延迟求值。
;;   用法: (lazy expr)
;;   示例: (define x (lazy (+ 1 2)))  — 不立即求值
;;         (force x)                   => 3
;;   展开: (delay expr)
(define-syntax lazy
  (syntax-rules ()
    ((_ expr) (delay expr))))

;; ── memo ──
;; 记忆化：第一次调用时计算并缓存结果，后续直接返回缓存值。
;;   用法: (memo (name args ...) body ...)
;;   示例: (memo (fib n) (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))
;;   注意: 使用 'unset 哨兵值区分"未缓存"和"缓存值为 #f"
(define-syntax memo
  (syntax-rules ()
    ((_ (name . args) body ...)
     (begin
       (define cache 'unset)
       (define (name . args)
         (if (eq? cache 'unset)
             (begin (set! cache (begin body ...)) cache)
             cache))))))

;; ── once ──
;; 创建只执行一次的函数（D once!）。
;;   用法: (once body ...)
;;   示例: (define f (once (display "once")))  — 首次调用打印，后续静默
(define-syntax once
  (syntax-rules ()
    ((_ body ...)
     (let ((done #f))
       (lambda ()
         (if done (if #f #f)
             (begin (set! done #t) body ...)))))))

;; ── either ──
;; 二选一（if 别名）。
;;   用法: (either test then else)
(define-syntax either
  (syntax-rules ()
    ((_ test then else) (if test then else))))

;; ── tuple ──
;; 多值构造简写。
;;   用法: (tuple val ...)
;;   示例: (receive (a b) (tuple 1 2) (+ a b))  => 3
;;   展开: (values . args)
(define-syntax tuple
  (syntax-rules ()
    ((_ . args) (values . args))))

;; ── str-join ──
;; 字符串模板：string-append 简写。
;;   用法: (str-join part ...)
;;   示例: (str-join "x=" 42)  => "x=42"
(define-syntax str-join
  (syntax-rules ()
    ((_ . parts)
     (string-append . parts))))

;; ── enumerate ──
;; 带索引的迭代（D enumerate）。
;;   用法: (enumerate (i val lst) body ...)
;;   示例: (enumerate (i v '(a b c)) (display (list i v)))
;;         => (0 a) (1 b) (2 c)
(define-syntax enumerate
  (syntax-rules ()
    ((_ (i val lst) body ...)
     (let loop ((xs lst) (idx 0))
       (when (pair? xs)
         (let ((i idx) (val (car xs))) body ...)
          (loop (cdr xs) (+ idx 1)))))))

;; ═══════════════════════════════════════════════════════════════════════════
;; Phase 9 — 多语言精华语法糖
;; ═══════════════════════════════════════════════════════════════════════════
;; 汇集 Haskell、Clojure、Kotlin、Rust、Elixir、Python、Common Lisp、
;; 以及 Scheme 扩展的精华宏。

;; ── Haskell ───────────────────────────────────────────────────────────────

;; ── $ (apply) ──
;; 应用运算符：减少括号嵌套。
;;   用法: (f $ x) = (f x)
;;   示例: (display (+ 1 $ * 2 3))  => 7
(define-syntax $
  (syntax-rules ()
    ((_ f x) (f x))
    ((_ f x . rest) (f x . rest))))

;; ── o (composition) ──
;; 函数组合（从右到左）。
;;   用法: (o f g) = (lambda (x) (f (g x)))
;;   示例: (define f (o (lambda (x) (* x 2)) (lambda (x) (+ x 1))))
;;         (f 5)  => 12
(define-syntax o
  (syntax-rules ()
    ((_ f) f)
    ((_ f g) (lambda (x) (f (g x))))
    ((_ f g . rest) (o f (o g . rest)))))

;; ── const ──
;; 常函数：接受任意参数，始终返回 x。
;;   用法: (const x)
;;   示例: (define f (const 5)) (f 1 2 3)  => 5
(define-syntax const
  (syntax-rules ()
    ((_ x) (lambda args x))))

;; ── identity ──
;; 恒等函数：返回输入参数。
;;   用法: (identity x)
(define-syntax identity
  (syntax-rules ()
    ((_ x) x)))

;; ── Clojure ──────────────────────────────────────────────────────────────

;; ── cond-> ──
;; 条件线程：每个条件为真时才应用对应的函数。
;;   用法: (cond-> x (test f) ...)
;;   示例: (cond-> 5
;;          (#t (lambda (x) (+ x 1)))
;;          (#f (lambda (x) (* x 2))))  => 6
;;   对比: 与 -> 不同，cond-> 每个步骤有条件判断
(define-syntax cond->
  (syntax-rules ()
    ((_ x) x)
    ((_ x (test f) . rest)
     (let ((v x))
       (if test (cond-> (f v) . rest) (cond-> v . rest))))))

;; ── as-> ──
;; 命名线程：每次转换的结果绑定到指定变量名。
;;   用法: (as-> x name expr ...)
;;   示例: (as-> 5 n (+ n 1) (* n 2))  => 12
;;   对比: 与 -> 不同，as-> 允许在不同位置引用变量
(define-syntax as->
  (syntax-rules ()
    ((_ x name) x)
    ((_ x name (expr) . rest)
     (as-> (let ((name x)) expr) name . rest))
    ((_ x name expr . rest)
     (as-> (let ((name x)) expr) name . rest))))

;; ── juxt ──
;; 并联应用：多个函数作用于同一输入，返回结果列表。
;;   用法: (juxt f g ...)
;;   示例: (juxt (lambda (x) (* x 2)) (lambda (x) (+ x 1))) 5
;;         => '(10 6)
(define-syntax juxt
  (syntax-rules ()
    ((_ f) (lambda (x) (f x)))
    ((_ f g) (lambda (x) (list (f x) (g x))))
    ((_ f g h . rest)
     (lambda (x) (apply list (f x) (g x) (h x) (map (lambda (fn) (fn x)) rest))))))

;; ── Kotlin 作用域函数 ──────────────────────────────────────────────────

;; ── let-it ──
;; 将值绑定到 it 后执行 body（Kotlin let）。
;;   用法: (let-it x body ...)
;;   示例: (let-it "hello" (string-length it))  => 5
(define-syntax let-it
  (syntax-rules ()
    ((_ x body ...) (let ((it x)) body ...))))

;; ── also ──
;; 执行副作用后返回原值（Kotlin also）。
;;   用法: (also x body ...)
;;   示例: (also 42 (display x))  => 显示 42，返回 42
(define-syntax also
  (syntax-rules ()
    ((_ x body ...) (begin body ... x))))

;; ── run ──
;; 在上下文中执行多个表达式，返回最后结果（Kotlin run）。
;;   用法: (run x body ...)
;;   示例: (run (list 1 2 3) (car it))  => 1
(define-syntax run
  (syntax-rules ()
    ((_ x body ...) (let ((it x)) body ...))))

;; ── Rust ─────────────────────────────────────────────────────────────────

;; ── unwrap ──
;; 解包：值为真时返回，否则报错（Rust unwrap）。
;;   用法: (unwrap x) 或 (unwrap x msg)
;;   示例: (unwrap 42)        => 42
;;         (unwrap #f "boom") => error
(define-syntax unwrap
  (syntax-rules ()
    ((_ x) (if x x (error "unwrap: got #f")))
    ((_ x msg) (if x x (error msg)))))

;; ── expect ──
;; 解包带自定义消息（Rust expect）。
;;   用法: (expect x msg)
(define-syntax expect
  (syntax-rules ()
    ((_ x msg) (if x x (error msg)))))

;; ── Elixir ──────────────────────────────────────────────────────────────

;; ── with-chain ──
;; 链式模式匹配（Elixir with）。
;;   用法: (with-chain (x val) do body ...)
;;         (with-chain (x val) do body ... else else-body ...)
;;   示例: (with-chain (x 42) do (* x 2))           => 84
;;         (with-chain (x #f) do 'body else 'fallback) => 'fallback
(define-syntax with-chain
  (syntax-rules (do else)
    ((_ (x val) do body ... else else-body ...)
     (let ((v val)) (if v (begin body ...) (begin else-body ...))))
    ((_ (x val) do body ...)
     (let ((x val)) (if x (begin body ...) #f)))))

;; ── Python ──────────────────────────────────────────────────────────────

;; ── all? / any? ──
;; 列表谓词快捷（Python all/any）。
;;   用法: (all? pred lst)
;;         (any? pred lst)
;;   示例: (all? positive? '(1 2 3 4))  => #t
;;         (any? even? '(1 3 5 7))       => #f
(define-syntax all?
  (syntax-rules ()
    ((_ pred lst) (every pred lst))))

(define-syntax any?
  (syntax-rules ()
    ((_ pred lst) (any pred lst))))

;; ── Common Lisp ─────────────────────────────────────────────────────────

;; ── comment ──
;; 注释块：忽略参数，返回未指定值。
;;   用法: (comment ...)
(define-syntax comment
  (syntax-rules ()
    ((_ . ignore) (if #f #f))))

;; ── prog1 / prog2 ──
;; 按序执行表达式，分别返回第一/二个表达式的值。
;;   用法: (prog1 first rest...)
;;         (prog2 first second rest...)
;;   示例: (prog1 1 2 3)   => 1
;;         (prog2 1 2 3)   => 2
(define-syntax prog1
  (syntax-rules ()
    ((_ first . rest) (let ((v first)) (begin . rest) v))))

(define-syntax prog2
  (syntax-rules ()
    ((_ first second . rest) (let ((v second)) (begin first . rest) v))))

;; ── Scheme 扩展 ─────────────────────────────────────────────────────────

;; ── value-> ──
;; 值传递给函数（类似 Clojure ->，但插入第一个参数位置）。
;;   用法: (value-> x (f args ...) ...)
;;   示例: (value-> 5 (+ 1)) => 6
;;   注意: 与 -> 的区别：-> 在 boot-py.scm 中实现，支持 :method 语法
(define-syntax value->
  (syntax-rules ()
    ((_ x) x)
    ((_ x (f . args)) (f x . args))
    ((_ x (f . args) . rest) (value-> (f x . args) . rest))))

;; ── nlet ──
;; 命名 let：定义局部递归函数。
;;   用法: (nlet name ((var init) ...) body ...)
;;   示例: (nlet loop ((i 5) (acc 1))
;;           (if (= i 0) acc (loop (- i 1) (* acc i))))
;;         => 120
;;   展开: (let name ((var init) ...) body ...)
;;   注意: 区别于标准 named let (let name ...) 的语法
(define-syntax nlet
  (syntax-rules ()
    ((_ name ((var init) ...) body ...)
     (let name ((var init) ...) body ...))))

;; ── let1 ──
;; 单变量 let 快捷。
;;   用法: (let1 var val body ...)
;;   示例: (let1 x 5 (* x 2))  => 10
(define-syntax let1
  (syntax-rules ()
    ((_ var val body ...) (let ((var val)) body ...))))

;; ── letr ──
;; 递归 let（letrec 快捷）。
;;   用法: (letr ((var init) ...) body ...)
;;   示例: (define even?
;;           (letr ((e? (lambda (n) (if (= n 0) #t (o? (- n 1)))))
;;                  (o? (lambda (n) (if (= n 0) #f (e? (- n 1))))))
;;             e?))
(define-syntax letr
  (syntax-rules ()
    ((_ ((var init) ...) body ...)
     (letrec ((var init) ...) body ...))))

;; ── tf ──
;; if 三元简写。
;;   用法: (tf test then else)
(define-syntax tf
  (syntax-rules ()
    ((_ test then else) (if test then else))))

;; ── true? / false? ──
;; 布尔值精确判断（eq? 而非 equal?）。
;;   用法: (true? x)   — 是否精确等于 #t
;;         (false? x)  — 是否精确等于 #f
;;   示例: (true? #t)  => #t
;;         (true? 42)  => #f
(define-syntax true?
  (syntax-rules ()
    ((_ x) (eq? x #t))))

(define-syntax false?
  (syntax-rules ()
    ((_ x) (eq? x #f))))

;; ═══════════════════════════════════════════════════════════════════════════
;; Phase 10 — Python _SPECIAL 替代
;; ═══════════════════════════════════════════════════════════════════════════
;; 在纯 Scheme 模式（pyb=False）下替代 Python 特殊形式的占位。
;; 当前为空（Python 相关宏已迁移到 boot-py.scm）。

(display "=== boot-sugar.scm 加载完成 ===\n")(newline)
