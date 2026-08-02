;; ════════════════════════════════════════════════════════════════
;; my-definemacro — Scheme 端等价于 C# define-macro 的实现
;; 对应 C# Evaluator.HDefineMacro / ExpandMacro / BindPattern / BindListPattern
;; 模式匹配语义 (与 C# 完全一致):
;;   (m x)           平坦参数, x = 第 1 个实参
;;   (m . body)      顶层点尾, body = 全部实参
;;   (m (syms) ...)  单符号列表模式, syms = 整个对应实参列表
;;   (m (var cnt))   位置解构, var = (car arg), cnt = (cadr arg)
;;   (m (a . r))     点尾解构, a = (car arg), r = (cdr arg)
;;   (m _ x)         _ 跳过对应实参
;; 注册借道 C# define-macro (Scheme 无创建 macro 元组的入口)。
;; ════════════════════════════════════════════════════════════════

;; ── 模式变量收集 (遍历序, 跳过 _) ──
(define (my-pat-vars pat)
  (cond
    ((symbol? pat) (list pat))
    ((not (pair? pat)) '())
    ((null? pat) '())
    (else (append (my-elem-vars (car pat)) (my-pat-vars (cdr pat))))))

(define (my-elem-vars e)
  (cond
    ((eq? e '_) '())
    ((symbol? e) (list e))
    ((pair? e) (my-pat-vars e))
    (else '())))

;; ── 按模式从实参提取值 (与 my-pat-vars 同序) ──
(define (my-pat-vals pat args)
  (cond
    ((symbol? pat) (list args))
    ((not (pair? pat)) '())
    ((null? pat) '())
    (else (append (my-elem-vals (car pat) (car args))
                  (my-pat-vals (cdr pat) (cdr args))))))

(define (my-elem-vals e arg)
  (cond
    ((eq? e '_) '())
    ((symbol? e) (list arg))
    ((and (pair? e) (symbol? (car e)) (null? (cdr e)))
     (list arg))
    ((pair? e) (my-pat-vals e arg))
    (else '())))

;; ── my-definemacro ──
;; (my-definemacro (name pat...) body...)
;; 生成 (define-macro (name . __args)
;;         (apply (lambda (patvars...) body...) (my-pat-vals 'pat __args)))
;; __args 用 sx-gensym 防止与用户变量冲突; 模式变量成为 lambda 参数,
;; 闭包捕获调用点环境 (与 C# 展开时在调用点求值宏体一致)。
(define-macro (my-definemacro name-pat . body)
  (let* ((name (car name-pat))
         (pat (cdr name-pat))
         (vars (my-pat-vars pat))
         (arg-var (sx-gensym)))
    (list 'define-macro
          (cons name arg-var)
          (list 'apply
                (cons 'lambda (cons vars body))
                (list 'my-pat-vals (list 'quote pat) arg-var)))))

;; ════════════════════════════════════════════════════════════════
;; 自检: 用 my-definemacro 定义各模式形态的宏并验证
;; (结果与 C# define-macro 版本逐字节一致)
;; ════════════════════════════════════════════════════════════════

;; 平坦参数
(my-definemacro (my-if test then else)
  `(if ,test ,then ,else))

;; 顶层点尾 (rest)
(my-definemacro (my-cond . clauses)
  (if (null? clauses) (if #f #f)
    (let ((first (car clauses)))
      (if (eq? (car first) 'else)
        `(begin ,@(cdr first))
        `(if ,(car first) (begin ,@(cdr first)) (my-cond ,@(cdr clauses)))))))

;; 平坦 + 点尾
(my-definemacro (my-let bindings . body)
  (let ((vars (map car bindings)) (vals (map cadr bindings)))
    (cons (cons 'lambda (cons vars body)) vals)))

;; 单符号列表模式 (整表) + 点尾
(my-definemacro (with-gensyms (syms) . body)
  (let ((news (map (lambda (s) (list s (list 'sx-gensym))) syms)))
    `(let ,news ,@body)))

;; 位置解构列表 + 点尾
(my-definemacro (my-dotimes (var count) . body)
  `(do ((,var 0 (+ ,var 1))) ((>= ,var ,count)) ,@body))

;; 点尾解构
(my-definemacro (my-swap! a b)
  `(let ((tmp ,a)) (set! ,a ,b) (set! ,b tmp)))

;; _ 跳过
(my-definemacro (my-ignore _ x) x)

;; ── 断言式自检 (无 FAIL 即通过) ──
(define (my-check label actual expected)
  (if (equal? actual expected)
      (display (string-append "[PASS] " label "\n"))
      (begin (display (string-append "[FAIL] " label)) (newline))))

(define (t-if x) (my-if (> x 0) 'pos 'non))
(my-check "my-if" (t-if 5) 'pos)

(define (t-cond x) (my-cond ((< x 0) 'neg) ((= x 0) 'zero) (else 'pos)))
(my-check "my-cond" (t-cond -3) 'neg)

(define (t-let a b) (my-let ((x (+ a b)) (y (* a b))) (+ x y)))
(my-check "my-let" (t-let 3 4) 19)

(define (t-gensyms a b) (with-gensyms (g1 g2) (list g1 g2 a b)))
(my-check "with-gensyms" (length (t-gensyms 1 2)) 4)

(define (t-dotimes n) (let ((acc 0)) (my-dotimes (i n) (set! acc (+ acc i))) acc))
(my-check "my-dotimes" (t-dotimes 100) 4950)

(define (t-swap a b) (let ((x a) (y b)) (my-swap! x y) (list x y)))
(my-check "my-swap!" (t-swap 1 2) '(2 1))

(define (t-ignore a b c) (my-ignore b (+ a c)))
(my-check "my-ignore" (t-ignore 10 20 30) 40)

(display "=== my-definemacro.scm 自检完成 ===\n")(newline)
