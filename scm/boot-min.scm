;; ════════════════════════════════════════════════════════════════
;; boot-min.scm — 最小引导层
;; ════════════════════════════════════════════════════════════════
;; 目标: 仅提供实现 define-syntax 所需的最小基础设施。
;;   Phase 0 — 基础工具 (quasiquote 需要)
;;   Phase 1 — quasiquote (define-macro)
;;   Phase 3 — define-syntax 基础设施 (syntax-rules 模式匹配 + 模板展开)
;;   Phase 4 — define-syntax (define-macro, 生成 define-macro)
;; 依赖: C# 特殊形式 (quote/if/lambda/begin/define/set!/define-macro
;;       cond/let/let*/letrec/and/or) + C# 原语
;; 加载时机: 解释器启动时第一个加载的 .scm 文件 (在 boot-core.scm 之前)
;; ════════════════════════════════════════════════════════════════

;; ── Phase 0: 基础工具

(define (atom? x) (not (pair? x)))

(define void-sentinel (void))
(define (void? x) (eq? x void-sentinel))

(define nil '())

(define (qq-reverse-helper src dst)
  (if (null? src) dst (qq-reverse-helper (cdr src) (cons (car src) dst))))
(define (qq-reverse l) (qq-reverse-helper l '()))

(define (qq-append-lists a b)
  (if (null? a) b (cons (car a) (qq-append-lists (cdr a) b))))

(define (qq-build-list items tail)
  (if (null? items) tail (qq-build-list (cdr items) (cons (car items) tail))))

(define (qq-unquote? x) (and (pair? x) (eq? (car x) 'unquote)))
(define (qq-unsplice? x) (and (pair? x) (eq? (car x) 'unquote-splicing)))
(define (qq-tail-unquote? tail) (and (pair? tail) (eq? (car tail) 'unquote)))
(define (qq-tail-unsplice? tail) (and (pair? tail) (eq? (car tail) 'unquote-splicing)))

;; ── quasiquote 展开器 (镜像 C# Evaluator.QQ, 接受调用方词法环境 env) ──

(define (qq-process-el el items env)
  (cond
    ((qq-unquote? el) (cons (eval (cadr el) env) items))
    ((qq-unsplice? el)
     (let ((v (eval (cadr el) env)))
       (cond
         ((pair? v) (qq-append-lists (qq-reverse v) items))
         ((null? v) items)
         (else (cons v items)))))
    ((pair? el)
     (if (eq? (car el) 'quasiquote)
         (cons el items)
         (cons (qq-walk el env) items)))
    (else (cons (qq-walk el env) items))))

(define (qq-walk-list-helper cur items env)
  (cond
    ((null? cur) (qq-reverse items))
    ((not (pair? cur)) (qq-build-list (qq-reverse items) cur))
    (else
     (let ((new-items (qq-process-el (car cur) items env))
           (tail (cdr cur)))
       (cond
         ((qq-tail-unquote? tail)
          (let ((v (eval (cadr tail) env)))
            (qq-build-list (qq-reverse new-items) v)))
         ((qq-tail-unsplice? tail)
          (let ((v (eval (cadr tail) env)))
            (qq-walk-list-helper tail
              (cond
                ((pair? v) (qq-append-lists (qq-reverse v) new-items))
                ((null? v) new-items)
                (else (cons v new-items)))
              env)))
         (else (qq-walk-list-helper tail new-items env)))))))

(define (qq-walk-list e env) (qq-walk-list-helper e '() env))

(define (qq-walk-vector-helper cur items env)
  (if (null? cur) (list->vector (qq-reverse items))
  (let ((el (car cur)))
    (cond
      ((qq-unquote? el)
      (qq-walk-vector-helper (cdr cur) (cons (eval (cadr el) env) items) env))
      ((qq-unsplice? el)
      (let ((v (eval (cadr el) env)))
        (cond
          ((pair? v)
            (qq-walk-vector-helper (cdr cur) (qq-append-lists (qq-reverse v) items) env))
          ((null? v) (qq-walk-vector-helper (cdr cur) items env))
          (else (qq-walk-vector-helper (cdr cur) (cons v items) env)))))
      (else (qq-walk-vector-helper (cdr cur) (cons (qq-walk el env) items) env))))))

(define (qq-walk-vector v env)
  (qq-walk-vector-helper (vector->list v) '() env))

(define (qq-walk e env)
  (cond
    ((pair? e) (qq-walk-list e env))
    ((vector? e) (qq-walk-vector e env))
    (else e)))

;; quasiquote 宏: 用 the-environment 在调用方词法环境求值 unquote,
;; 展开结果用 quote 包装防止再次求值。
(define-macro (quasiquote e)
  (list 'quote (qq-walk e (the-environment))))

;; ════════════════════════════════════════════════════════════════
;; Phase 3: define-syntax 基础设施
;;   在 Scheme 中镜像 C# PatternMatcher.Match 与 TemplateExpander.ExpandTmpl。
;; ════════════════════════════════════════════════════════════════

;; ── 绑定 alist 辅助 ──

(define (sx-lookup var bindings)
  (let ((b (assq var bindings)))
    (if b (cdr b) #f)))

(define (sx-merge-bindings b1 b2) (append b2 b1))

(define (sx-rev-append src acc)
  (if (null? src) acc (sx-rev-append (cdr src) (cons (car src) acc))))
(define (sx-reverse l) (sx-rev-append l '()))

;; 收集模式中的变量 (排除 _ 和 ...), 镜像 C# GetPatternVars
(define (sx-pattern-vars pat)
  (sx-reverse (sx-pattern-vars-loop (list pat) '())))

(define (sx-pattern-vars-loop stack acc)
  (if (null? stack)
      acc
      (let ((curr (car stack)))
        (cond
          ((symbol? curr)
           (cond
             ((eqv? curr '_)   (sx-pattern-vars-loop (cdr stack) acc))
             ((eqv? curr '...) (sx-pattern-vars-loop (cdr stack) acc))
             (else             (sx-pattern-vars-loop (cdr stack) (cons curr acc)))))
          ((pair? curr)
           (sx-pattern-vars-loop (cons (car curr) (cons (cdr curr) (cdr stack))) acc))
          (else (sx-pattern-vars-loop (cdr stack) acc))))))

(define (sx-merge-vars a b)
  (cond
    ((null? b) a)
    ((memq (car b) a) (sx-merge-vars a (cdr b)))
    (else (sx-merge-vars (cons (car b) a) (cdr b)))))

;; 将 ellipsis 变量累积为列表, 合并进 base 绑定
(define (sx-accum-ellipsis vars groups base)
  (if (null? vars) base
  (let ((v (car vars)) (vals (map (lambda (g) (sx-lookup (car vars) g)) groups)))
    (sx-merge-bindings
      (cons (cons v (sx-rev-append vals '())) '())
      (sx-accum-ellipsis (cdr vars) groups base)))))

;; ── 模式匹配: 镜像 C# PatternMatcher.Match ──

(define (sx-match pat inp lits)
  (cond
    ((null? pat) (if (null? inp) '() #f))
    ((symbol? pat) (sx-match-sym pat inp lits))
    ((not (pair? pat)) (if (equal? pat inp) '() #f))
    ((pair? (cdr pat))
     (if (eq? (cadr pat) '...)
         (sx-match-ellipsis (car pat) (cddr pat) inp lits)
         (sx-match-pair pat inp lits)))
    (else (sx-match-pair pat inp lits))))

;; 符号模式
(define (sx-match-sym pat inp lits)
  (cond
    ((eq? pat '_) '())
    ((memq pat lits) (if (and (symbol? inp) (eq? pat inp)) '() #f))
    (else (list (cons pat inp)))))

;; 普通 pair: b2 优先合并
(define (sx-match-pair pat inp lits)
  (if (pair? inp)
      (let ((b1 (sx-match (car pat) (car inp) lits)))
        (if (not b1)
            #f
            (let ((b2 (sx-match (cdr pat) (cdr inp) lits)))
              (if (not b2) #f (sx-merge-bindings b1 b2)))))
      #f))

;; ellipsis 匹配: 镜像 C# PatternMatcher.Match 的 ellipsis 分支。
(define (sx-match-ellipsis prefix rest-pat inp lits)
  (let ((res (sx-match-ellipsis-loop prefix rest-pat inp lits '())))
    (sx-match-ellipsis-finish prefix rest-pat res lits)))

;; 处理 while 循环后的剩余部分
(define (sx-match-ellipsis-finish prefix rest-pat res lits)
  (let ((in (car res))
        (groups (cdr res))
        (evars (sx-pattern-vars prefix)))
  (if (null? rest-pat) (if (null? in) (sx-accum-ellipsis evars groups '()) #f)
  (let ((rb (sx-match rest-pat in lits)))
  (if rb (sx-accum-ellipsis evars groups rb) #f)))))

;; while 循环
(define (sx-match-ellipsis-loop prefix rest-pat in lits groups)
  (if (not (pair? in)) (cons in groups)
  (let ((b (sx-match prefix (car in) lits)))
    (if b
        (cond
          ((null? rest-pat)
          (sx-match-ellipsis-loop prefix rest-pat (cdr in) lits (cons b groups)))
          ((sx-match rest-pat in lits)
          (cons in groups))
          (else
          (sx-match-ellipsis-loop prefix rest-pat (cdr in) lits
            (cons b groups))))
        (cons in groups)))))

;; ── 模板展开: 镜像 C# TemplateExpander.ExpandTmpl ──

(define (sx-expand tmpl bindings)
  (cond
    ((symbol? tmpl) (let ((p (assq tmpl bindings))) (if p (cdr p) tmpl)))
    ((not (pair? tmpl)) tmpl)
    ((pair? (cdr tmpl))
     (if (eq? (cadr tmpl) '...)
         (sx-expand-ellipsis (car tmpl) (cddr tmpl) bindings)
         (sx-expand-pair tmpl bindings)))
    (else (sx-expand-pair tmpl bindings))))

(define (sx-expand-pair tmpl bindings)
  (cons (sx-expand (car tmpl) bindings)
        (sx-expand (cdr tmpl) bindings)))

;; 展开 (sub ... rest)
(define (sx-expand-ellipsis sub rest bindings)
  (let ((evars (sx-ellipsis-vars sub bindings)))
    (if (null? evars)
        (sx-expand-ellipsis-novar sub rest bindings)
        (sx-expand-ellipsis-var sub rest bindings evars))))

;; 无 ellipsis 变量: 用第一个列表绑定确定次数
(define (sx-expand-ellipsis-novar sub rest bindings)
  (sx-repeat sub rest bindings #f (sx-find-list-count bindings)))

;; 有 ellipsis 变量: 按第一个变量的长度重复
(define (sx-expand-ellipsis-var sub rest bindings evars)
  (sx-repeat sub rest bindings evars
             (length (cdr (assq (car evars) bindings)))))

;; 重复展开 sub 与 rest
(define (sx-repeat sub rest bindings evars cnt)
  (sx-repeat-helper sub rest bindings evars (- cnt 1)
                    (sx-expand rest bindings)))

(define (sx-repeat-helper sub rest bindings evars i res)
  (if (< i 0) res
  (sx-repeat-helper sub rest bindings evars (- i 1)
                    (cons (if evars
                              (sx-expand sub (sx-sub-bindings evars bindings i))
                              (sx-expand sub bindings))
                          res))))

;; 找到子模板中绑定为列表的变量
(define (sx-ellipsis-vars sub bindings)
  (sx-reverse (sx-ellipsis-vars-helper (sx-pattern-vars sub) bindings '())))

(define (sx-ellipsis-vars-helper vs bindings acc)
  (if (null? vs) acc
  (let ((p (assq (car vs) bindings)))
    (if p
        (cond
          ((pair? (cdr p))
          (sx-ellipsis-vars-helper (cdr vs) bindings (cons (car vs) acc)))
          ((null? (cdr p))
          (sx-ellipsis-vars-helper (cdr vs) bindings (cons (car vs) acc)))
          (else (sx-ellipsis-vars-helper (cdr vs) bindings acc)))
        (sx-ellipsis-vars-helper (cdr vs) bindings acc)))))

;; 找到任一列表绑定的长度
(define (sx-find-list-count bindings)
  (if (null? bindings) 0
     (let ((v (cdar bindings)))
       (if (pair? v) (length v) (sx-find-list-count (cdr bindings))))))

;; 构造第 i 个子绑定
(define (sx-sub-bindings evars bindings i)
  (if (null? evars) '()
     (cons (sx-sub-bindings-cons (car evars) bindings i)
           (sx-sub-bindings (cdr evars) bindings i))))

(define (sx-sub-bindings-cons v bindings i)
  (let ((lst (cdr (assq v bindings))))
    (cons v (if (< i (length lst)) (list-ref lst i) '()))))

;; ── 顶层展开: 逐规则匹配, 成功则展开模板 ──

(define (sx-dispatch args lits rules)
  (if (null? rules) (error "syntax-rules: no match")
  (let* ((rule (car rules))
        (pat (if (pair? rule) (car rule) '()))
        (tmpl (if (pair? rule) (sx-rule-tmpl rule) '()))
        (pat-args (if (pair? pat) (cdr pat) '()))
        (b (sx-match pat-args args lits)))
    (if b (sx-expand tmpl b) (sx-dispatch args lits (cdr rules))))))

(define (sx-rule-tmpl rule)
  (if (pair? (cdr rule)) (cadr rule) '()))

;; ════════════════════════════════════════════════════════════════
;; Phase 3b: R6RS 宏系统形式
;; ════════════════════════════════════════════════════════════════

(define *sx-bindings* '())

(define (sx-get-bindings) *sx-bindings*)
(define (sx-set-bindings! b) (set! *sx-bindings* b))

;; 在 b 下求值 thunk, 结束后恢复原绑定
(define (sx-with-bindings b thunk)
  (let ((old *sx-bindings*))
    (sx-set-bindings! b)
    (let ((r (thunk)))
      (sx-set-bindings! old)
      r)))

;; gensym
(define *sx-gensym-counter* 0)
(define (sx-gensym)
  (set! *sx-gensym-counter* (+ *sx-gensym-counter* 1))
  (string->symbol (string-append "__t" (number->string *sx-gensym-counter*))))

;; ── syntax ──
(define-macro (syntax tmpl)
  (sx-expand tmpl (sx-get-bindings)))

;; ── generate-temporaries ──
(define-macro (generate-temporaries lst)
  (list 'sx-gen-temps lst))

(define (sx-gen-temps lst)
  (letrec ((loop (lambda (n acc)
                   (if (= n 0) acc (loop (- n 1) (cons (sx-gensym) acc))))))
    (loop (length lst) '())))

;; ── syntax-case ──
(define-macro (syntax-case . args)
  (let ((expr (car args))
        (lits (cadr args))
        (clauses (cddr args)))
    (list 'sx-syntax-case expr
          (list 'quote lits)
          (list 'quote clauses))))

(define (sx-syntax-case expr lits clauses)
  (let* ((datum expr)
         (cl (car clauses))
         (rest-cl (cdr cl))
         (pat (car cl))
         (has-fender (and (pair? rest-cl) (pair? (cdr rest-cl))))
         (fender (if has-fender (car rest-cl) #f))
         (tmpl (if has-fender (cadr rest-cl) (car rest-cl)))
         (b (sx-match pat datum lits)))
    (cond
      ((null? clauses) (error "syntax-case: no match"))
      (b
       (if (or (not has-fender) (sx-check-fender fender b))
           (sx-eval-tmpl tmpl b)
           (sx-syntax-case datum lits (cdr clauses))))
      (else (sx-syntax-case datum lits (cdr clauses))))))

;; 求值 fender, 为假则换下一子句
(define (sx-check-fender fender b)
  (sx-with-bindings b (lambda () (not (eq? (eval fender) #f)))))

;; 在绑定 b 下求值模板
(define (sx-eval-tmpl tmpl b)
  (sx-with-bindings b (lambda () (eval tmpl))))

;; ── with-syntax ──
(define-macro (with-syntax . args)
  (let ((bindings (car args))
        (body (cdr args)))
    (list 'sx-with-syntax
          (cons 'list
                (map (lambda (b) (list 'list (list 'quote (car b)) (cadr b)))
                     bindings))
          (list 'quote body))))

(define (sx-with-syntax pairs body)
  (letrec ((loop (lambda (ps acc)
                   (if (null? ps)
                       (sx-with-bindings acc (lambda () (sx-eval-body body)))
                       (let* ((p (car ps))
                              (pat (caar ps))
                              (val (cadar ps))
                              (b (sx-match pat val '())))
                         (if b
                             (loop (cdr ps) (sx-merge-bindings acc b))
                             (error "with-syntax: no match")))))))
    (loop pairs '())))

(define (sx-eval-body body)
  (if (null? body) (void)
     (letrec ((loop (lambda (bs last)
                       (cond
                         ((null? bs) last)
                         (else (loop (cdr bs) (eval (car bs)))))))
       (loop body #f)))))


;; ── let-syntax / letrec-syntax ──
(define-macro (let-syntax . args)
  (sx-let-syntax (car args) (cdr args)))

(define-macro (letrec-syntax . args)
  (sx-let-syntax (car args) (cdr args)))

(define (sx-let-syntax bindings body)
  (list (cons 'lambda
              (cons '()
                    (append (map sx-make-macro-binding bindings) body)))))

;; 将 (name transformer) 转为局部 define-macro
(define (sx-make-macro-binding binding)
  (let ((name (car binding))
        (trans (cadr binding)))
    (if (and (pair? trans) (eq? (car trans) 'syntax-rules))
       (let ((lits (if (pair? (cdr trans)) (cadr trans) '()))
             (rules (cddr trans)))
         (list 'define-macro
               (cons name 'args)
               (list 'sx-dispatch 'args (list 'quote lits) (list 'quote rules))))
       (list 'define-macro
             (cons name 'args)
             (list (cons 'lambda (cdr trans))
                   (list 'cons (list 'quote name) 'args))))))

;; ════════════════════════════════════════════════════════════════
;; Phase 4: define-syntax (define-macro)
;; ════════════════════════════════════════════════════════════════

(define-macro (define-syntax name trans)
  (sx-make-macro-binding (list name trans)))

(display "=== boot-min.scm 加载完成 ===\n")(newline)
