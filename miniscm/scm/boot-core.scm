;; ═══════════════════════════════════════════════════════════════
;; boot-core.scm — Scheme 宏系统自举核心引导库
;; ═══════════════════════════════════════════════════════════════
;; 概述: 本文件提供 Scheme 宏系统的自举核心，分为 4 个阶段:
;;   Phase 1 — 最简基元（atom?, void? 等）
;;   Phase 2 — 核心特殊形式宏（let/let*/letrec/and/or/when/unless/
;;              cond/case/do/define-values/case-lambda）
;;   Phase 3 — 派生形式（let*-values/guard/include/cond-expand）
;;   Phase 4 — define-macro 实现的宏（define-record-type）
;;   Phase 5 — 补充宏（delay/let-values/parameterize/cut/cute）
;; 依赖: 仅使用 lambda / define / set! / if / begin / quote /
;;        car cdr cons null? pair? symbol? eq? memq assq equal?
;;        map reverse length apply error 等原语
;; 加载时机: 解释器启动时第一个加载的 .scm 文件
;; 测试文件: test/test-boot-core.scm
;; ═══════════════════════════════════════════════════════════════
;; ═══════════════════════════════════════════════════════════════
;; Phase 1 — 最简基元
;; ═══════════════════════════════════════════════════════════════

;; ── atom? ──
;; 判断对象是否为原子（非 pair）。
;;   用法: (atom? obj)
;;   示例: (atom? 42)      => #t
;;         (atom? '(1 2))  => #f
;;   展开: (not (pair? obj))
(define (atom? x) (not (pair? x)))

;; ── void-sentinel / void? ──
;; void 哨兵及其检测谓词。
;;   void-sentinel: 由 (void) 创建的不可见返回值
;;   void?: 判断对象是否为 void 哨兵
;;   用法: (void? obj)
;;   示例: (void? (if #f 42))       => #t
;;         (void? (if #t 42))       => #f
(define void-sentinel (void))
(define (void? x) (eq? x void-sentinel))

;; ═══════════════════════════════════════════════════════════════
;; Phase 2 — 核心特殊形式宏
;; ═══════════════════════════════════════════════════════════════

;; ── let ──
;; 局部绑定（含命名 let 递归，SRFI-2/R7RS 标准）。
;;   用法: (let ((var val) ...) body ...)           — 匿名 let
;;         (let name ((var val) ...) body ...)        — 命名 let（递归）
;;   示例: (let ((x 1) (y 2)) (+ x y))              => 3
;;         (let loop ((i 5) (acc 1))
;;           (if (= i 0) acc (loop (- i 1) (* acc i))))  => 120
;;   展开: (let ((x 1) (y 2)) (+ x y))
;;      => ((lambda (x y) (+ x y)) 1 2)
;;   展开（命名）: (let fact ((n 5)) (if (= n 0) 1 (* n (fact (- n 1)))))
;;      => ((letrec ((fact (lambda (n) (if (= n 0) 1 (* n (fact (- n 1))))))) fact) 5)
;;   注意:
;;     - 变量并行绑定，不可相互引用
;;     - 命名 let 是 named-let / 内循环的惯用写法
(define-syntax let
  (syntax-rules ()
    ((let (((values var ...) expr)) body1 body2 ...)
     (let-values (((var ...) expr)) body1 body2 ...))
    ((let ((var val) ...))
     ((lambda (var ...) (if #f #f)) val ...))
    ((let ((var val) ...) body1 body2 ...)
     ((lambda (var ...) body1 body2 ...) val ...))
    ((let name ((var val) ...) body1 body2 ...)
     ((letrec ((name (lambda (var ...) body1 body2 ...))) name) val ...))))

;; ── let* ──
;; 顺序局部绑定：每个变量可引用前面已绑定的变量。
;;   用法: (let* ((var val) ...) body ...)
;;   示例: (let* ((x 3) (y (* x 2))) y)            => 6
;;         (let* ((a 1) (b (+ a 1)) (c (+ b 1))) c)  => 3
;;   展开: (let* ((x 3) (y (* x 2))) y)
;;      => (let ((x 3)) (let ((y (* x 2))) y))
;;   注意: 等价于嵌套 let，不支持命名形式
(define-syntax let*
  (syntax-rules ()
    ((let* () body1 body2 ...)
     (let () body1 body2 ...))
    ((let* ((var val) rest ...) body1 body2 ...)
     (let ((var val))
       (let* (rest ...) body1 body2 ...)))))

;; ── letrec ──
;; 递归局部绑定：变量可相互引用（用于定义互递归函数）。
;;   用法: (letrec ((var val) ...) body ...)
;;   示例: (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
;;                   (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
;;           (even? 4))                              => #t
;;   展开: (letrec ((f (lambda (n) (if (< n 2) n (* n (f (- n 1)))))))
;;           (f 5))
;;      => (let ((f #f)) (set! f (lambda (n) ...)) (let () (f 5)))
;;   注意: 内部实现为先赋 #f，再 set! 为实际值
(define-syntax letrec
  (syntax-rules ()
    ((letrec ((var val) ...) body1 body2 ...)
     (let ((var #f) ...)
       (set! var val) ...
       (let () body1 body2 ...)))))

;; ── and ──
;; 逻辑与：从左到右求值，遇到假值立即短路返回 #f。
;;   用法: (and expr ...)
;;   示例: (and)                 => #t
;;         (and 1)               => 1
;;         (and 1 2 3)           => 3
;;         (and 1 #f 3)          => #f
;;         (and (> 3 2) (< 1 5)) => #t
;;   展开: (and 1 #f 3)
;;      => (if 1 (and #f 3) #f)
;;      => (if 1 (if #f (and 3) #f) #f)
;;   注意: 最后一个表达式的值即为 and 返回值（不仅仅是 #t/#f）
(define-syntax and
  (syntax-rules ()
    ((and) #t)
    ((and test) test)
    ((and test1 test2 ...)
     (if test1 (and test2 ...) #f))))

;; ── or ──
;; 逻辑或：从左到右求值，遇到真值立即返回该值。
;;   用法: (or expr ...)
;;   示例: (or)                  => #f
;;         (or #f)               => #f
;;         (or #f 42)            => 42
;;         (or #f #f 3)          => 3
;;         (or #f (< 1 0) 'yes)  => 'yes
;;   展开: (or #f 42)
;;      => ((lambda (temp)
;;           (if temp temp 42))
;;         #f)
;;   注意:
;;     - 使用 lambda 确保 test1 仅求值一次（存取 temp 而非重复求值）
;;     - 不依赖外部 let，纯 lambda 实现
(define-syntax or
  (syntax-rules ()
    ((or) #f)
    ((or test) test)
    ((or test1 test2 ...)
     ((lambda (temp)
        (if temp temp (or test2 ...)))
     test1))))

;; ── when ──
;; 条件执行：条件为真时执行表达式序列（无 else 分支）。
;;   用法: (when test body ...)
;;   示例: (when (> 3 2) (display "yes") (newline))  => "yes" + 换行
;;         (when #f 42)                                => #f
;;   展开: (when (positive? -5) (display "pos"))
;;      => (if (positive? -5) (begin (display "pos")) #f)
;;   注意: 无 else 分支，条件假时返回 #f
(define-syntax when
  (syntax-rules ()
    ((when test expr1 expr2 ...)
     (if test (begin expr1 expr2 ...) #f))))

;; ── unless ──
;; 条件执行取反：条件为假时执行表达式序列。
;;   用法: (unless test body ...)
;;   示例: (unless (< 1 2) (display "no"))  => 不输出
;;         (unless #f 42)                    => 42
;;   展开: (unless (negative? -5) (display "not neg"))
;;      => (if (not (negative? -5)) (begin (display "not neg")) #f)
;;   注意: when 的对偶形式
(define-syntax unless
  (syntax-rules ()
    ((unless test expr1 expr2 ...)
     (if (not test) (begin expr1 expr2 ...) #f))))

;; ── cond ──
;; 多路条件分支（R7RS 标准，支持所有分支模式）。
;;   用法: (cond (test result ...) ...)
;;         (cond (test => proc) ...)    — 箭头分派
;;         (cond (test))                — 隐式 return
;;         (cond (else result ...))     — 默认分支
;;   分支模式:
;;     (test result1 result2 ...)
;;       — 标准分支：test 真时执行 result 序列
;;     (test => expression)
;;       — 箭头分支：test 真时将 test 值传给 expression
;;     (test)
;;       — 隐式分支：test 本身作为结果（test 须为真）
;;     (else result1 result2 ...)
;;       — 默认分支：必须最后一个
;;   示例:
;;     (cond ((> 3 2) 'big) ((< 3 2) 'small))       => 'big
;;     (cond ((> 3 2) => number?))                    => #t
;;     (cond ((member 2 '(1 2 3)) => car) (else #f))  => 2
;;     (cond ((even? 3) 'even) (else 'odd))           => 'odd
;;   展开过程:
;;     (cond ((> 3 2) 'big) ((< 3 2) 'small))
;;      => (if (> 3 2) (begin 'big) (cond ((< 3 2) 'small)))
;;      => (if (> 3 2) (begin 'big) (if (< 3 2) (begin 'small)))
;;     (cond ((member 2 '(1 2 3)) => car))
;;      => ((lambda (temp) (if temp (car temp))) (member 2 '(1 2 3)))
;;   注意:
;;     - (=> expression) 箭头中 expression 必须为单参数过程
;;     - (else) 子句必须放在最后，识别为关键字（syntax-rules else）
(define-syntax cond
  (syntax-rules (else =>)
    ((cond (else result1 result2 ...))
     (begin result1 result2 ...))
    ((cond (test => expression))
     ((lambda (temp)
        (if temp (expression temp)))
      test))
    ((cond (test => expression) clause1 clause2 ...)
     ((lambda (temp)
        (if temp
            (expression temp)
            (cond clause1 clause2 ...)))
      test))
    ((cond (test)) test)
    ((cond (test) clause1 clause2 ...)
     ((lambda (temp)
        (if temp temp (cond clause1 clause2 ...)))
      test))
    ((cond (test result1 result2 ...))
     (if test (begin result1 result2 ...)))
    ((cond (test result1 result2 ...) clause1 clause2 ...)
     (if test
         (begin result1 result2 ...)
         (cond clause1 clause2 ...)))))

;; ── case ──
;; 键值分派：根据 key 的值选择匹配分支（R7RS 标准）。
;;   用法: (case key ((datum ...) result ...) ...)
;;         (case key (else result ...))
;;   示例:
;;     (case 2 ((1) 'one) ((2) 'two) ((3) 'three))    => 'two
;;     (case 'b ((a) 1) ((b c) 2) (else 0))            => 2
;;     (case 5 ((1 2 3) 'small) ((4 5 6) 'big))        => 'big
;;   展开:
;;     (case key clause1 clause2 ...)
;;      => ((lambda (val) (case-helper val clause1 clause2 ...)) key)
;;   注意:
;;     - 使用 memv（eqv? 比较）而非 equal?
;;     - (else) 必须放最后
;;     - 无可匹配分支且无 else 时返回 unspecified
(define-syntax case
  (syntax-rules (else)
    ((case key clause1 clause2 ...)
     ((lambda (val)
        (case-helper val clause1 clause2 ...))
      key))))

;; ── case-helper ──
;; case 的递归分派辅助宏（不直接使用）。
;;   匹配过程: 遍历各分支，用 memv 检查 val 是否在键列表中。
(define-syntax case-helper
  (syntax-rules (else)
    ((case-helper val (else result1 result2 ...))
     (begin result1 result2 ...))
    ((case-helper val ((key ...) => proc) clause1 clause2 ...)
     (if (memv val '(key ...))
         (proc val)
         (case-helper val clause1 clause2 ...)))
    ((case-helper val ((key ...) result1 result2 ...))
     (if (memv val '(key ...))
         (begin result1 result2 ...)
         #f))
    ((case-helper val ((key ...) result1 result2 ...) clause1 clause2 ...)
     (if (memv val '(key ...))
         (begin result1 result2 ...)
          (case-helper val clause1 clause2 ...)))))

;; ── do ──
;; 迭代循环（R7RS 标准）。
;;   用法: (do ((var init step ...) ...) (test expr ...) command ...)
;;   参数:
;;     (var init step) — 变量初始化 + 可选的步进表达式
;;     (test expr ...) — 终止条件及终止时返回值
;;     command ...     — 每次迭代执行（副作用）
;;   示例:
;;     (do ((i 0 (+ i 1))) ((= i 5) 'done) (display i) (newline))
;;       => 输出 0 1 2 3 4，返回 'done
;;     (do ((x 1 (* x 2)) (n 0 (+ n 1))) ((> x 100) n))
;;       => 7 (2^7 = 128 > 100)
;;   展开:
;;     (do ((i 0 (+ i 1))) ((= i 5) 'done) (display i))
;;      => (letrec ((loop (lambda (i)
;;                          (if (= i 5)
;;                              (begin (void) 'done)
;;                              (begin (display i) (loop (+ i 1)))))))
;;           (loop 0))
;;   注意:
;;     - step 可选，省略时变量保持不变（同 init）
;;     - 支持多重赋值（并行步进）
(define-syntax do
  (syntax-rules ()
    ((do ((var init step ...) ...)
         (test expr ...)
       command ...)
     (letrec ((loop (lambda (var ...)
                      (if test
                          (begin (void) expr ...)
                          (begin
                            command ...
                            (loop (do-step var step ...) ...))))))
       (loop init ...)))))

;; ── do-step ──
;; do 循环的步进表达式选择辅助宏（不直接使用）。
;;   有 step 时用 step，无 step 时保持原值。
(define-syntax do-step
  (syntax-rules ()
    ((do-step var) var)
    ((do-step var step) step)))

;; ── define-values ──
;; 多值定义：将表达式产生的多个值分别绑定到多个变量（R7RS）。
;;   用法: (define-values (var ...) expr)
;;   示例: (define-values (x y) (values 1 2))
;;         (+ x y)  => 3
;;   实现: define-macro, 用 gensym 生成唯一临时名, 避免共享 temp 污染。
(define-macro (define-values . args)
  (let ((var-pat (car args))
        (expr (cadr args)))
    (let ((temp (gensym)))
      (let build ((vars var-pat) (i 0) (acc '()))
        (if (null? vars)
            `(begin
               (define ,temp (call-with-values (lambda () ,expr) vector))
               ,@(reverse acc))
            (build (cdr vars) (+ i 1)
                   (cons `(define ,(car vars) (vector-ref ,temp ,i)) acc)))))))

;; ── define-values-helper (保留: 兼容旧引用) ──
(define-syntax define-values-helper
  (syntax-rules ()
    ((define-values-helper temp idx ())
     (begin))
    ((define-values-helper temp idx (var rest ...))
     (begin
       (define var (vector-ref temp idx))
       (define-values-helper temp (+ idx 1) (rest ...))))))

;; ── case-lambda ──
;; 多态参数函数：根据实参个数（元数）分派到不同函数体（SRFI-16）。
;;   用法: (case-lambda (formals body ...) ...)
;;   每个子句 (formals body ...) 中:
;;     formals 可以是:
;;       ()         — 匹配 0 个参数
;;       (x)        — 匹配恰好 1 个参数
;;       (x y)      — 匹配恰好 2 个参数
;;       (x y z)    — 匹配恰好 3 个参数
;;       args       — 匹配任意个参数（rest 参数，放在最后）
;;       (x . rest) — 匹配至少 1 个参数（点对形式）
;;   分派规则:
;;     1. 从第一个子句开始依次尝试匹配
;;     2. 匹配成功则执行对应 body，失败则继续检查下一子句
;;     3. 所有子句均不匹配则报错 "case-lambda: no matching clause"
;;   示例:
;;     (define greet
;;       (case-lambda
;;         (()       "hello world")
;;         ((name)   (string-append "hello " name))
;;         ((a b)    (string-append a " and " b))
;;         (rest     (apply string-append (intersperse ", " rest)))))
;;     (greet)               => "hello world"
;;     (greet "Alice")       => "hello Alice"
;;     (greet "A" "B")       => "A and B"
;;     (greet "x" "y" "z")   => "x, y, z"
;;   展开:
;;     (case-lambda (() 0) ((x) x) (args (apply + args)))
;;     => (lambda args
;;          (let ((n (length args)))
;;            (case-lambda-helper args n
;;              (() 0) ((x) x) (args (apply + args)))))
;;     => (lambda args
;;          (let ((n (length args)))
;;            (if (case-lambda-arity n ())
;;                (apply (lambda () 0) args)
;;                (if (case-lambda-arity n (x))
;;                    (apply (lambda (x) x) args)
;;                    (if (case-lambda-arity n args)
;;                        (apply (lambda args (apply + args)) args)
;;                        (error "case-lambda: no matching clause"))))))
;;   注意:
;;     - 子句按定义顺序匹配，第一个匹配的生效
;;     - rest 参数（如 args / (x . rest)）必须放最后
;;     - 若无匹配子句且无 rest 兜底，触发运行时错误
;;     - case-lambda-arity 在编译期常量化（define-macro），
;;       运行时开销仅为 (= n k) 或 (>= n k) 的比较
(define-macro (case-lambda . clauses)
  (letrec ((arity
            (lambda (fmls)
              (cond ((null? fmls) '(= n 0))
                    ((pair? fmls)
                     (let loop ((f fmls) (count 0))
                       (if (null? f) (list '= 'n count)
                           (if (pair? f) (loop (cdr f) (+ count 1))
                               (list '>= 'n count)))))
                    (else #t))))
           (dispatch
            (lambda (cs)
              (if (null? cs)
                  '(error "case-lambda: no matching clause")
                  (let* ((clause (car cs))
                         (fmls (car clause))
                         (body (cdr clause)))
                    `(if ,(arity fmls)
                         (apply (lambda ,fmls ,@body) args)
                         ,(dispatch (cdr cs))))))))
    `(lambda args
       (let ((n (length args)))
         ,(dispatch clauses)))))

;; ── case-lambda-helper ──
;; case-lambda 的递归分派辅助宏（不直接使用）。
;;   遍历子句列表，用 case-lambda-arity 测试各子句的元数匹配。
;;   无匹配子句时调用 (error "case-lambda: no matching clause")。
(define-syntax case-lambda-helper
  (syntax-rules ()
    ((_ args n)
     (error "case-lambda: no matching clause"))
    ((_ args n (fmls b1 b2 ...) rest ...)
     (if (case-lambda-arity n fmls)
         (apply (lambda fmls b1 b2 ...) args)
         (case-lambda-helper args n rest ...)))))

;; ── case-lambda-arity ──
;; 编译期元数检测宏（define-macro），生成运行时条件表达式。
;;   输入: n — 实参个数的变量名（编译期引用）
;;         fmls — 形式参数列表
;;   输出: 根据 fmls 的形态生成不同的比较表达式:
;;     ()         => (= n 0)      空参数表 → 匹配 0 个参数
;;     (x y)      => (= n 2)      列表 → 匹配恰好 count 个
;;     (x . rest) => (>= n 1)     点对 → 匹配至少 count 个
;;     sym        => #t           纯 symbol → 匹配任意个
(define-macro (case-lambda-arity n fmls)
  (cond ((null? fmls) `(= ,n 0))
        ((pair? fmls)
         (let loop ((f fmls) (count 0))
           (cond ((null? f) `(= ,n ,count))
                 ((pair? f) (loop (cdr f) (+ count 1)))
                 (else `(>= ,n ,count)))))
        (else #t)))


;; ═══════════════════════════════════════════════════════════════
;; Phase 3 — 派生形式
;; ═══════════════════════════════════════════════════════════════
;; 这些宏由 Phase 2 中的核心宏组合派生而成。

;; ── let*-values ──
;; 顺序多值绑定：每个表达式可产生多个值，绑定到对应变量列表。
;;   用法: (let*-values ((vars expr) ...) body ...)
;;   示例: (let*-values (((a b) (values 1 2)) ((c d) (values 3 4)))
;;           (+ a b c d))                               => 10
;;   展开: 嵌套 call-with-values
;;   注意: 类似 let*，后续 binding 可引用前面绑定的变量
(define-syntax let*-values
  (syntax-rules ()
    ((_ () body ...) (begin body ...))
    ((_ ((vars expr) rest ...) body ...)
     (call-with-values (lambda () expr)
       (lambda vars (let*-values (rest ...) body ...))))))

;; ── guard ──
;; 异常保护：执行 body，若抛出异常则由 cond 子句处理（R7RS）。
;;   用法: (guard (var cond-clause ...) body ...)
;;   示例:
;;     (guard (e ((error-object? e) 'caught))
;;       (error "test error"))                           => 'caught
;;     (guard (e (else 'caught))
;;       (/ 1 0))                                         => 'caught
;;   展开:
;;     (guard (e (else 'caught)) (error "boom"))
;;      => (with-exception-handler
;;           (lambda (e) (cond (else 'caught)))
;;           (lambda () (error "boom")))
;;   注意:
;;     - with-exception-handler 是原语
;;     - guard 异常处理器只能捕获 body 中抛出的异常
(define-syntax guard
  (syntax-rules ()
    ((_ (var cond-clause ...) body ...)
     (with-exception-handler (lambda (var) (cond cond-clause ...))
       (lambda () body ...)))))

;; ── include ──
;; 文件包含：加载指定文件（类似 C 的 #include）。
;;   用法: (include filename)
;;   示例: (include "lib/utils.scm")
;;   展开: (load filename)
(define-syntax include
  (syntax-rules () ((_ filename) (load filename))))

(define-syntax import
  (syntax-rules () ((_ library ...) (if #f #f))))

(define NIL '())

;; ── cond-expand ──
;; 条件编译：根据库是否存在选择分支（R7RS）。
;;   用法: (cond-expand (library body ...) ...)
;;   示例: (cond-expand (srfi-1 (display "have srfi-1")) (else (display "no srfi-1")))
;;   注意: 当前实现仅检查第一个分支，不支持特性检测
(define-syntax cond-expand
  (syntax-rules () ((_ (library body ...) rest ...) (begin body ...))))

;; ═══════════════════════════════════════════════════════════════
;; Phase 4 — define-macro 实现的宏
;; ═══════════════════════════════════════════════════════════════

;; ── define-record-type ──
;; 记录类型定义（SRFI-9 风格，简化版）。
;;   用法: (define-record-type name constructor pred . fields)
;;   参数:
;;     name       — 类型名
;;     constructor — (ctor-name arg ...) 构造器名称与参数
;;     pred       — 类型判断谓词名称
;;     fields     — (field-name accessor [mutator]) 
;;                  字段名、访问器名、可选的修改器名
;;   示例:
;;     (define-record-type pare
;;       (kons x y) pare?
;;       (kar kar)
;;       (kdr kdr))
;;     (define p (kons 1 2))
;;     (pare? p)     => #t
;;     (kar p)       => 1
;;     (kdr p)       => 2
;;     ;; 含修改器的记录:
;;     (define-record-type box
;;       (make-box val) box?
;;       (val get-val set-val!))
;;   展开:
;;     (define-record-type pare (kons x y) pare? (kar kar) (kdr kdr))
;;      => (begin
;;           (define (kons x y) (list 'pare x y))
;;           (define (pare? obj) (and (pair? obj) (eq? (car obj) 'pare)))
;;           (define (kar obj) (list-ref obj 1))
;;           (define (kdr obj) (list-ref obj 2)))
;;   注意:
;;     - 记录表示为 list（car 为类型标记）
;;     - 修改器位于字段描述第三位：(accessor mutator)
;;       两个元素时表示无修改器
;;     - 使用 list-ref 访问字段（非向量）
(define-macro (define-record-type name ctor pred . fields)
  (let* ((ctor-name (car ctor)) (ctor-args (cdr ctor)) (n -1))
    `(begin
       (define (,ctor-name ,@ctor-args) (list (quote ,name) ,@ctor-args))
       (define (,pred obj) (and (pair? obj) (eq? (car obj) (quote ,name))))
       ,@(map (lambda (f)
                (set! n (+ n 1))
                 (let ((accessor (cadr f))
                      (mutator (if (pair? (cddr f)) (caddr f) #f)))
                  `(begin
                     (define (,accessor obj) (list-ref obj ,(+ n 1)))
                     ,@(if mutator
                         `((define (,mutator obj val) (set-car! (list-tail obj ,(+ n 1)) val)))
                         '()))))
             fields)
       (quote ,name))))

;; ═══════════════════════════════════════════════════════════════
;; Phase 5 — 补充宏
;; ═══════════════════════════════════════════════════════════════

(display "=== boot-core.scm 加载完成 ===\n")(newline)

;; ── delay ──
;; 惰性求值：创建 promise，值在首次 force 时计算（R7RS）。
;;   用法: (delay expr)
;;   示例: (define p (delay (+ 1 2)))
;;         (promise? p)  => #t
;;         (force p)     => 3
;;         (force p)     => 3  （缓存，不重复计算）
;;   展开: (make-promise (lambda () expr))
(define-syntax delay
  (syntax-rules ()
    ((_ expr) (make-promise (lambda () expr)))))

;; ── let-values ──
;; 多值局部绑定（R7RS）。
;;   用法: (let-values ((vars expr) ...) body ...)
;;   示例: (let-values (((a b) (values 1 2)) ((c) (values 3)))
;;           (+ a b c))                               => 6
;;   注意: 与 let 类似，但每个绑定可接收多值
(define-syntax let-values
  (syntax-rules ()
    ((_ () body ...) (begin body ...))
    ((_ (((vars ...) expr)) body ...)
     (call-with-values (lambda () expr)
       (lambda (vars ...) body ...)))
    ((_ (((values vars ...) expr) rest ...) body ...)
     (call-with-values (lambda () expr)
       (lambda vars (let-values (rest ...) body ...))))
    ((_ ((vars expr) rest ...) body ...)
     (call-with-values (lambda () expr)
       (lambda vars (let-values (rest ...) body ...))))))

;; ── parameterize ──
;; 动态参数绑定（R7RS）。
;;   用法: (parameterize ((param value) ...) body ...)
;;   示例:
;;     (define p (make-parameter 0))
;;     (parameterize ((p 5)) (p))    => 5
;;     (p)                            => 0
;;   展开:
;;     (parameterize ((p 5)) (body))
;;      => (let ((saved (p)))
;;           (dynamic-wind
;;             (lambda () (p 5))
;;             (lambda () (body))
;;             (lambda () (p saved))))
;;   注意:
;;     - 使用 dynamic-wind 确保异常和 continuation 穿越时正确恢复
;;     - 嵌套 parameterize 会正确保存/恢复
(define-syntax parameterize
  (syntax-rules ()
    ((_ () body ...) (begin body ...))
    ((_ ((param value) rest ...) body ...)
     (let ((saved (param)))
       (dynamic-wind
         (lambda () (param value))
         (lambda () (parameterize (rest ...) body ...))
         (lambda () (param saved)))))))

;; ── cut ──
;; 柯里化参数占位宏（SRFI-26 标准）。
;;   用法: (cut proc slot ...)
;;   槽位类型:
;;     <>       — 占位符，调用时用实参替换
;;     <...>    — 多参数占位符，展开为 apply + rest args
;;     其他     — 固定值，展开时保留
;;   示例:
;;     (define add5 (cut + 5 <>))
;;     (add5 3)                            => 8
;;     (define add (cut + <> <>))
;;     (add 3 4)                           => 7
;;     (define max-lst (cut max <...>))
;;     (max-lst 1 8 3 5)                   => 8
;;     (define mul3 (cut * 3 4 5))
;;     (mul3)                              => 60
;;   展开:
;;     (cut + 5 <>)
;;      => (lambda (__cut_1) (apply + 5 __cut_1))
;;     (cut max <...>)
;;      => (lambda args (apply max args))
;;   注意:
;;     - 使用 define-macro 实现，支持槽位编号
;;     - <> 占位符按出现顺序编号（__cut_1, __cut_2, ...）
(define-macro (cut . slots)
  (let ((counter 0) (args '()) (vars '()) (rest? #f))
    (let process ((s slots) (proc #f))
      (cond
        ((not proc)
         (process (cdr s) (car s)))
        ((null? s)
         (let ((lambdavars vars))
           (if rest?
               `(lambda (,@lambdavars . __rest)
                  (apply ,proc ,@args __rest))
               `(lambda (,@lambdavars)
                  (,proc ,@args)))))
        ((equal? (car s) '<>)
         (set! counter (+ counter 1))
         (let ((v (string->symbol (string-append "__cut_" (number->string counter)))))
           (set! args (append args (list v)))
           (set! vars (append vars (list v)))
           (process (cdr s) proc)))
        ((equal? (car s) '<...>)
         (set! rest? #t)
         (process (cdr s) proc))
        (else
         (set! args (append args (list (car s))))
         (process (cdr s) proc))))))

;; ── cute ──
;; 柯里化单次求值宏（SRFI-26 标准，≈ cut 但固定槽在展开时求值）。
;;   用法: (cute proc slot ...)
;;   语义: cute 应在展开时求值固定槽，cut 在调用时求值。
;;         但 syntax-rules 无法生成唯一变量名以避免 temp 遮蔽，
;;         因此当前 cute ≡ cut，对绝大多数实际使用无影响。
;;   示例: (cute (* 2 3) <>)  ≡ (cut (* 2 3) <>)
(define-syntax cute
  (syntax-rules (<> <...>)
    ((cute . args) (cut . args))))
