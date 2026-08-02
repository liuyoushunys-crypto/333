;; ════════════════════════════════════════════════════════════════
;; my-definemacro — Scheme 端等价于 C# define-macro 的实现
;; 对应 C# Evaluator.HDefineMacro / ExpandMacro / BindPattern
;;
;; 用 define (普通函数) 实现, 不套壳 eval/define-macro:
;; 宏展开逻辑 (模式绑定 + 宏体求值) 全在 Scheme 完成, 通过 C# 桥接
;; 原语 sx-defmacro 注册为 C# "macro" 元组。元组的模式固定为 rest
;; 符号 args, body 为调用 sx-macro-expand 的形式——C# ExpandMacro
;; 只绑定 args = 全部实参, 真正的模式解构与宏体求值由本文件的
;; sx-macro-expand / my-bind-pattern (C# BindPattern 的 Scheme 移植)
;; 完成。
;;
;; 用法:
;;   (my-definemacro '(name pat...) 'body...)
;;   例: (my-definemacro '(my-if test then else)
;;         '`(if ,test ,then ,else))
;;
;; 模式匹配语义 (与 C# BindPattern 完全一致):
;;   (m x)           平坦参数, x = 第 1 个实参
;;   (m . body)      顶层点尾, body = 全部实参
;;   (m (syms) ...)  单符号列表模式, syms = 整个对应实参列表
;;   (m (var cnt))   位置解构, var = (car arg), cnt = (cadr arg)
;;   (m (a . r))     点尾解构, a = (car arg), r = (cdr arg)
;;   (m _ x)         _ 跳过对应实参
;;
;; 注册到全局环境 (GlobalEnv), 顶层定义宏与 define-macro 等价。
;; ════════════════════════════════════════════════════════════════

;; ── 模式绑定 (对应 C# BindPattern/BindListPattern) ──
;; 返回 (var . value) 绑定列表, value 为未求值的实参代码。
(define (my-bind-pattern pattern args)
  (cond
    ((symbol? pattern)
     (if (eq? pattern '_) '() (list (cons pattern args))))
    ((not (pair? pattern)) '())
    ((null? pattern) '())
    (else (append (my-bind-elem (car pattern) (car args))
                  (my-bind-pattern (cdr pattern) (cdr args))))))

(define (my-bind-elem elem arg)
  (cond
    ((eq? elem '_) '())
    ((symbol? elem) (list (cons elem arg)))
    ((and (pair? elem) (symbol? (car elem)) (null? (cdr elem)))
     (list (cons (car elem) arg)))
    ((pair? elem) (my-bind-pattern elem arg))
    (else '())))

;; ── 宏展开 (对应 C# ExpandMacro 的宏体求值部分) ──
;; (sx-macro-expand pattern body args callenv)
;;   pattern: 宏模式 (宏名之后的模式)
;;   body:    宏体表达式列表 (模式变量绑定后逐条求值, 返回最后一个)
;;   args:    未求值的实参列表 (代码)
;;   callenv: 宏调用点环境 (C# Env)
;; 模式变量通过 let + quote 绑定为实参代码; 宏体经 eval 在调用点环境
;; 求值, free 标识符与 quasiquote 的 unquote 均按调用点解析。
(define (sx-macro-expand pattern body args callenv)
  (let* ((bindings (my-bind-pattern pattern args))
         (let-form (cons 'let
                    (cons (map (lambda (b) (list (car b) (list 'quote (cdr b))))
                               bindings)
                          body))))
    (eval let-form callenv)))

;; ── my-definemacro ──
(define (my-definemacro name-pat . body)
  (let ((name (car name-pat))
        (pat (cdr name-pat)))
    ;; 元组模式 = args (rest), body = ((sx-macro-expand ...)) 单元素列表,
    ;; C# EvalSeq 对宏体逐元素求值, 因此包一层使 (sx-macro-expand ...)
    ;; 作为整体调用求值。模式解构 + 宏体求值全在 Scheme (sx-macro-expand)。
    (sx-defmacro name 'args
      (list (list 'sx-macro-expand
                  (list 'quote pat)
                  (list 'quote body)
                  'args
                  '(sx-expand-env))))
    name))
