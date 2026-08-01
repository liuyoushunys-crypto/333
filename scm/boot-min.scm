;; ════════════════════════════════════════════════════════════════
;; boot-min.scm — 最小引导层
;; ════════════════════════════════════════════════════════════════
;; 目标: 仅提供实现 define-syntax 所需的最小基础设施。
;;   Phase 0 — 基础工具 (quasiquote 需要)
;;   Phase 1 — quasiquote (define-macro)
;;   Phase 2 — (removed: 基础宏全部用 if/lambda 替换)
;;   Phase 3 — define-syntax 基础设施 (syntax-rules 模式匹配 + 模板展开)
;;   Phase 4 — define-syntax (define-macro, 生成 define-macro)
;; 依赖: 仅使用 C# 特殊形式 (quote/if/lambda/begin/define/set!/define-macro)
;;       和 C# 原语 (car cdr cons null? pair? symbol? eq? not memq assq
;;       map reverse append list filter 等)
;; 加载时机: 解释器启动时第一个加载的 .scm 文件 (在 boot-core.scm 之前)
;; 之后 boot-core.scm 的 define-syntax 定义会正常加载。
;; ════════════════════════════════════════════════════════════════

;; ── Phase 0: 基础工具 ──

(define (atom? x) (not (pair? x)))

(define void-sentinel (void))
(define (void? x) (eq? x void-sentinel))

(define (qq-reverse-helper src dst)
  (if (null? src) dst (qq-reverse-helper (cdr src) (cons (car src) dst))))
(define (qq-reverse l) (qq-reverse-helper l '()))

(define (qq-append-lists a b)
  (if (null? a) b (cons (car a) (qq-append-lists (cdr a) b))))

(define (qq-build-list items tail)
  (if (null? items) tail (qq-build-list (cdr items) (cons (car items) tail))))

(define (qq-unquote? x)
  (if (pair? x) (eq? (car x) 'unquote) #f))

(define (qq-unsplice? x)
  (if (pair? x) (eq? (car x) 'unquote-splicing) #f))

(define (qq-tail-unquote? tail)
  (if (pair? tail) (eq? (car tail) 'unquote) #f))

(define (qq-tail-unsplice? tail)
  (if (pair? tail) (eq? (car tail) 'unquote-splicing) #f))

;; ── quasiquote 展开器 (镜像 C# Evaluator.QQ, 接受调用方词法环境 env) ──

;; 处理单个元素, 镜像 C# QQ 的 if/else 扁平结构 (纯 if, 无宏, JIT 友好)
(define (qq-process-el el items env)
  (if (qq-unquote? el)
      (cons (eval (cadr el) env) items)
      (if (qq-unsplice? el)
          ((lambda (v)
             (if (pair? v)
                 (qq-append-lists (qq-reverse v) items)
                 (if (null? v)
                     items
                     (cons v items))))
           (eval (cadr el) env))
          (if (pair? el)
              (if (eq? (car el) 'quasiquote)
                  (cons el items)
                  (cons (qq-walk el env) items))
              (cons (qq-walk el env) items)))))

(define (qq-walk-list-helper cur items env)
  (if (null? cur)
      (qq-reverse items)
      (if (pair? cur)
          ((lambda (new-items)
             ((lambda (tail)
                (if (qq-tail-unquote? tail)
                    ((lambda (v)
                       (qq-build-list (qq-reverse new-items) v))
                     (eval (cadr tail) env))
                    (if (qq-tail-unsplice? tail)
                        ((lambda (v)
                           (qq-walk-list-helper tail
                             (if (pair? v)
                                 (qq-append-lists (qq-reverse v) new-items)
                                 (if (null? v)
                                     new-items
                                     (cons v new-items)))
                             env))
                         (eval (cadr tail) env))
                        (qq-walk-list-helper tail new-items env))))
              (cdr cur)))
           (qq-process-el (car cur) items env)))
          (qq-build-list (qq-reverse items) cur)))

(define (qq-walk-list e env)
  (qq-walk-list-helper e '() env))

(define (qq-walk-vector-helper cur items env)
  (if (null? cur)
      (list->vector (qq-reverse items))
      ((lambda (el)
         (if (qq-unquote? el)
             (qq-walk-vector-helper (cdr cur) (cons (eval (cadr el) env) items) env)
             (if (qq-unsplice? el)
                 ((lambda (v)
                    (if (pair? v)
                        (qq-walk-vector-helper (cdr cur)
                          (qq-append-lists (qq-reverse v) items) env)
                        (if (null? v)
                            (qq-walk-vector-helper (cdr cur) items env)
                            (qq-walk-vector-helper (cdr cur) (cons v items) env))))
                  (eval (cadr el) env))
                 (qq-walk-vector-helper (cdr cur) (cons (qq-walk el env) items) env))))
       (car cur))))

(define (qq-walk-vector v env)
  (qq-walk-vector-helper (vector->list v) '() env))

(define (qq-walk e env)
  (if (pair? e) (qq-walk-list e env)
      (if (vector? e) (qq-walk-vector e env)
          e)))

;; quasiquote 宏: 用 the-environment 在调用方词法环境求值 unquote,
;; 展开结果用 quote 包装防止再次求值。
(define-macro (quasiquote e)
  (list 'quote (qq-walk e (the-environment))))

;; ── Phase 2: (removed — all macros replaced by if/lambda inline) ──
;; sx-and2, sx-or2, sx-when, sx-unless, sx-let, sx-let*, sx-letrec, sx-cond
;; 全部用 plain if/lambda/begin 替换, 避免 define-macro 互相递归展开。

;; ════════════════════════════════════════════════════════════════
;; Phase 3: define-syntax 基础设施
;;   在 Scheme 中镜像 C# PatternMatcher.Match 与 TemplateExpander.ExpandTmpl。
;; ════════════════════════════════════════════════════════════════

;; ── 绑定 alist 辅助 ──

(define (sx-lookup var bindings)
  ((lambda (b) (if b (cdr b) #f)) (assq var bindings)))

(define (sx-merge-bindings b1 b2)
  (append b2 b1))

(define (sx-rev-append src acc)
  (if (null? src) acc (sx-rev-append (cdr src) (cons (car src) acc))))

(define (sx-reverse l)
  (sx-rev-append l '()))

;; 收集模式中的变量 (排除 _ 和 ...), 镜像 C# GetPatternVars
(define (sx-pattern-vars pat)
  (sx-reverse (sx-pattern-vars-loop (list pat) '())))

(define (sx-pattern-vars-loop stack acc)
  (if (null? stack)
      acc
      ((lambda (curr)
         (if (symbol? curr)
             (if (eqv? curr '_)
                 (sx-pattern-vars-loop (cdr stack) acc)
                 (if (eqv? curr '...)
                     (sx-pattern-vars-loop (cdr stack) acc)
                     (sx-pattern-vars-loop (cdr stack) (cons curr acc))))
             (if (pair? curr)
                 (sx-pattern-vars-loop (cons (car curr) (cons (cdr curr) (cdr stack))) acc)
                 (sx-pattern-vars-loop (cdr stack) acc))))
       (car stack))))

(define (sx-merge-vars a b)
  (if (null? b)
      a
      (if (memq (car b) a)
          (sx-merge-vars a (cdr b))
          (sx-merge-vars (cons (car b) a) (cdr b)))))

;; 将 ellipsis 变量累积为列表, 合并进 base 绑定
(define (sx-accum-ellipsis vars groups base)
  (if (null? vars)
      base
      ((lambda (v vals)
         (sx-merge-bindings
           (cons (cons v (sx-rev-append vals '())) '())
           (sx-accum-ellipsis (cdr vars) groups base)))
       (car vars)
       (map (lambda (g) (sx-lookup (car vars) g)) groups))))

;; ── 模式匹配: 镜像 C# PatternMatcher.Match (纯 if, 无宏) ──

(define (sx-match pat inp lits)
  (if (null? pat)
      (if (null? inp) '() #f)
      (if (symbol? pat)
          (sx-match-sym pat inp lits)
          (if (not (pair? pat))
              (if (equal? pat inp) '() #f)
              (if (pair? (cdr pat))
                  (if (eq? (cadr pat) '...)
                      (sx-match-ellipsis (car pat) (cddr pat) inp lits)
                      (sx-match-pair pat inp lits))
                  (sx-match-pair pat inp lits))))))

;; 符号模式
(define (sx-match-sym pat inp lits)
  (if (eq? pat '_)
      '()
      (if (memq pat lits)
          (if (symbol? inp)
              (if (eq? pat inp) '() #f)
              #f)
          (list (cons pat inp)))))

;; 普通 pair: b2 优先合并
(define (sx-match-pair pat inp lits)
  (if (pair? inp)
      ((lambda (b1)
         (if (not b1)
             #f
             ((lambda (b2)
                (if (not b2)
                    #f
                    (sx-merge-bindings b1 b2)))
              (sx-match (cdr pat) (cdr inp) lits))))
       (sx-match (car pat) (car inp) lits))
      #f))

;; ellipsis 匹配: 镜像 C# PatternMatcher.Match 的 ellipsis 分支。
(define (sx-match-ellipsis prefix rest-pat inp lits)
  ((lambda (res)
     (sx-match-ellipsis-finish prefix rest-pat res lits))
   (sx-match-ellipsis-loop prefix rest-pat inp lits '())))

;; 处理 while 循环后的剩余部分
(define (sx-match-ellipsis-finish prefix rest-pat res lits)
  ((lambda (in groups)
     ((lambda (evars)
        (if (null? rest-pat)
            (if (null? in)
                (sx-accum-ellipsis evars groups '())
                #f)
            ((lambda (rb)
               (if rb
                   (sx-accum-ellipsis evars groups rb)
                   #f))
             (sx-match rest-pat in lits))))
      (sx-pattern-vars prefix)))
   (car res) (cdr res)))

;; while 循环: 对应 C# 的 `while (inp is Cell) { ... }`
(define (sx-match-ellipsis-loop prefix rest-pat in lits groups)
  (if (pair? in)
      ((lambda (b)
         (if b
             (if (null? rest-pat)
                 (sx-match-ellipsis-loop prefix rest-pat (cdr in) lits
                                        (cons b groups))
                 (if (sx-match rest-pat in lits)
                     (cons in groups)
                     (sx-match-ellipsis-loop prefix rest-pat (cdr in) lits
                                            (cons b groups))))
             (cons in groups)))
       (sx-match prefix (car in) lits))
      (cons in groups)))

;; ── 模板展开: 镜像 C# TemplateExpander.ExpandTmpl (纯 if, 无宏) ──

(define (sx-expand tmpl bindings)
  (if (symbol? tmpl)
      ((lambda (p) (if p (cdr p) tmpl)) (assq tmpl bindings))
      (if (not (pair? tmpl))
          tmpl
          (if (pair? (cdr tmpl))
              (if (eq? (cadr tmpl) '...)
                  (sx-expand-ellipsis (car tmpl) (cddr tmpl) bindings)
                  (sx-expand-pair tmpl bindings))
              (sx-expand-pair tmpl bindings)))))

(define (sx-expand-pair tmpl bindings)
  (cons (sx-expand (car tmpl) bindings)
        (sx-expand (cdr tmpl) bindings)))

;; 展开 (sub ... rest)
(define (sx-expand-ellipsis sub rest bindings)
  ((lambda (evars)
     (if (null? evars)
         (sx-expand-ellipsis-novar sub rest bindings)
         (sx-expand-ellipsis-var sub rest bindings evars)))
   (sx-ellipsis-vars sub bindings)))

;; 无 ellipsis 变量: 用第一个列表绑定确定次数
(define (sx-expand-ellipsis-novar sub rest bindings)
  (sx-repeat sub rest bindings #f (sx-find-list-count bindings)))

;; 有 ellipsis 变量: 按第一个变量的长度重复
(define (sx-expand-ellipsis-var sub rest bindings evars)
  (sx-repeat sub rest bindings evars
             (length (cdr (assq (car evars) bindings)))))

;; 重复展开 sub 与 rest; evars 非 #f 时用第 i 个元素子绑定
;; 镜像 C#: result2 = ExpandTmpl(rest); for (i = cnt-1; i >= 0; i--)
;;          result2 = Cell(ExpandTmpl(sub, subBindings), result2);
(define (sx-repeat sub rest bindings evars cnt)
  (sx-repeat-helper sub rest bindings evars (- cnt 1)
                    (sx-expand rest bindings)))

(define (sx-repeat-helper sub rest bindings evars i res)
  (if (< i 0)
      res
      (sx-repeat-helper sub rest bindings evars (- i 1)
                        (cons (if evars
                                  (sx-expand sub (sx-sub-bindings evars bindings i))
                                  (sx-expand sub bindings))
                              res))))

;; 找到子模板中绑定为列表的变量, 镜像 C# EllipsisVars2
(define (sx-ellipsis-vars sub bindings)
  (sx-reverse (sx-ellipsis-vars-helper (sx-pattern-vars sub) bindings '())))

(define (sx-ellipsis-vars-helper vs bindings acc)
  (if (null? vs)
      acc
      ((lambda (p)
         (if p
             (if (pair? (cdr p))
                 (sx-ellipsis-vars-helper (cdr vs) bindings (cons (car vs) acc))
                 (if (null? (cdr p))
                     (sx-ellipsis-vars-helper (cdr vs) bindings (cons (car vs) acc))
                     (sx-ellipsis-vars-helper (cdr vs) bindings acc)))
             (sx-ellipsis-vars-helper (cdr vs) bindings acc)))
       (assq (car vs) bindings))))

;; 找到任一列表绑定的长度, 镜像 C#: 第一个值为 Cell 的绑定
(define (sx-find-list-count bindings)
  (if (null? bindings)
      0
      ((lambda (v)
         (if (pair? v) (length v) (sx-find-list-count (cdr bindings))))
       (cdar bindings))))

;; 构造第 i 个子绑定, 镜像 C# subBindings
(define (sx-sub-bindings evars bindings i)
  (if (null? evars)
      '()
      (cons (sx-sub-bindings-cons (car evars) bindings i)
            (sx-sub-bindings (cdr evars) bindings i))))

(define (sx-sub-bindings-cons v bindings i)
  ((lambda (lst)
     (cons v (if (< i (length lst)) (list-ref lst i) '())))
   (cdr (assq v bindings))))

;; ── 顶层展开: 逐规则匹配, 成功则展开模板 ──

(define (sx-dispatch args lits rules)
  (if (null? rules)
      (error "syntax-rules: no match")
      ((lambda (rule)
         ((lambda (pat tmpl)
            ((lambda (pat-args)
               ((lambda (b)
                  (if b
                      (sx-expand tmpl b)
                      (sx-dispatch args lits (cdr rules))))
                (sx-match pat-args args lits)))
             (if (pair? pat) (cdr pat) '())))
          (if (pair? rule) (car rule) '())
          (if (pair? rule) (sx-rule-tmpl rule) '())))
       (car rules))))

;; 取规则的模板部分: (pat tmpl)
(define (sx-rule-tmpl rule)
  (if (pair? (cdr rule)) (cadr rule) '()))

;; ════════════════════════════════════════════════════════════════
;; Phase 3b: R6RS 宏系统形式 (镜像 C# Evaluator 对应 handler)
;;   syntax / syntax-case / with-syntax / generate-temporaries /
;;   let-syntax / letrec-syntax
;; ════════════════════════════════════════════════════════════════

;; 当前 pattern 绑定: syntax-case/with-syntax 设置, syntax 读取。
;; 用全局变量承载, 嵌套时保存/恢复 (对应 C# 的 nenv 链)。
(define *sx-bindings* '())

(define (sx-get-bindings) *sx-bindings*)

(define (sx-set-bindings! b) (set! *sx-bindings* b))

;; 在 b 下求值 thunk, 结束后恢复原绑定
(define (sx-with-bindings b thunk)
  ((lambda (old)
     (sx-set-bindings! b)
     ((lambda (r)
        (sx-set-bindings! old)
        r)
      (thunk)))
   *sx-bindings*))

;; gensym: 用计数器 + string->symbol 生成唯一符号 (对应 C# GensymCounter)
(define *sx-gensym-counter* 0)
(define (sx-gensym)
  (set! *sx-gensym-counter* (+ *sx-gensym-counter* 1))
  (string->symbol (string-append "__t" (number->string *sx-gensym-counter*))))

;; ── syntax: (syntax tmpl) 即 #'tmpl ──
;; 用当前 pattern 绑定展开模板 (对应 C# HSyntax / ExpandTmpl)
(define-macro (syntax tmpl)
  (sx-expand tmpl (sx-get-bindings)))

;; ── generate-temporaries: 生成 n 个唯一符号 (对应 C# HGenerateTemporaries) ──

(define-macro (generate-temporaries lst)
  (list 'sx-gen-temps lst))

(define (sx-gen-temps lst)
  ((lambda (loop) (loop loop (length lst) '()))
   (lambda (loop n acc)
     (if (= n 0)
         acc
         (loop loop (- n 1) (cons (sx-gensym) acc))))))

;; ── syntax-case: (syntax-case expr (lits...) clause ...) ──
;; clause: (pat tmpl) 或 (pat fender tmpl)
;; 镜像 C# HSyntaxCase: 求值 expr, 逐子句匹配, fender 过滤, 展开模板。

(define-macro (syntax-case . args)
  ((lambda (expr lits clauses)
     (list 'sx-syntax-case expr
           (list 'quote lits)
           (list 'quote clauses)))
   (car args) (cadr args) (cddr args)))

(define (sx-syntax-case expr lits clauses)
  ((lambda (datum)
     (if (null? clauses)
         (error "syntax-case: no match")
         ((lambda (cl)
            ((lambda (pat rest-cl)
               ((lambda (has-fender)
                  ((lambda (fender tmpl)
                     ((lambda (b)
                        (if b
                            (if (or (not has-fender) (sx-check-fender fender b))
                                (sx-eval-tmpl tmpl b)
                                (sx-syntax-case datum lits (cdr clauses)))
                            (sx-syntax-case datum lits (cdr clauses))))
                      (sx-match pat datum lits)))
                   (if has-fender (car rest-cl) #f)
                   (if has-fender (cadr rest-cl) (car rest-cl))))
                (if (pair? rest-cl) (pair? (cdr rest-cl)) #f)))
             (car cl)
             (cdr cl)))
          (car clauses)))))
   expr)

;; 求值 fender, 为假则换下一子句
(define (sx-check-fender fender b)
  (sx-with-bindings b (lambda () (not (eq? (eval fender) #f)))))

;; 在绑定 b 下求值模板 (模板通常为 (syntax ...), 读取 *sx-bindings*)
(define (sx-eval-tmpl tmpl b)
  (sx-with-bindings b (lambda () (eval tmpl))))

;; ── with-syntax: (with-syntax ((pat expr) ...) body ...) ──
;; 镜像 C# HWithSyntax: 求值 expr, 匹配 pat, 绑定模式变量, 求值 body。

(define-macro (with-syntax . args)
  ((lambda (bindings body)
     (list 'sx-with-syntax
           (cons 'list
                 (map (lambda (b) (list 'list (list 'quote (car b)) (cadr b)))
                      bindings))
           (list 'quote body)))
   (car args) (cdr args)))

(define (sx-with-syntax pairs body)
  ((lambda (loop) (loop loop pairs '()))
   (lambda (loop ps acc)
     (if (null? ps)
         (sx-with-bindings acc (lambda () (sx-eval-body body)))
         ((lambda (p)
            ((lambda (pat val)
               ((lambda (b)
                  (if b
                      (loop loop (cdr ps) (sx-merge-bindings acc b))
                      (error "with-syntax: no match")))
                (sx-match pat val '())))
             (car p)
             (cadr p)))
          (car ps))))))

;; 顺序求值 body 各形式, 返回最后一个 (对应 C# SeqTailCall)
(define (sx-eval-body body)
  (if (null? body)
      (void)
      ((lambda (loop) (loop loop body #f))
       (lambda (loop bs last)
         (if (null? bs)
             last
             (loop loop (cdr bs) (eval (car bs))))))))

;; ── let-syntax / letrec-syntax: 局部 transformer 绑定 ──
;; 生成一个 lambda, 内部用 define-macro 定义局部宏, 再求值 body。
;; (镜像 C# HLetSyntax/HLetrecSyntax 的局部环境绑定)

(define-macro (let-syntax . args)
  (sx-let-syntax (car args) (cdr args)))

(define-macro (letrec-syntax . args)
  (sx-let-syntax (car args) (cdr args)))

(define (sx-let-syntax bindings body)
  (list (cons 'lambda
              (cons '()
                    (append (map sx-make-macro-binding bindings) body)))))

;; 将 (name transformer) 转为局部 define-macro
;; transformer: (syntax-rules lits rules...) 或 (lambda (stx) body...)
(define (sx-make-macro-binding binding)
  ((lambda (name trans)
     (if (and (pair? trans) (eq? (car trans) 'syntax-rules))
         ((lambda (lits rules)
            (list 'define-macro
                  (cons name 'args)
                  (list 'sx-dispatch 'args (list 'quote lits) (list 'quote rules))))
          (if (pair? (cdr trans)) (cadr trans) '())
          (cddr trans))
         (list 'define-macro
               (cons name 'args)
               (list (cons 'lambda (cdr trans))
                     (list 'cons (list 'quote name) 'args)))))
   (car binding)
   (cadr binding)))

;; ════════════════════════════════════════════════════════════════
;; Phase 4: define-syntax (define-macro)
;;   将 (define-syntax name (syntax-rules ...) | (lambda (stx) ...))
;;   转换为一个 define-macro。
;; ════════════════════════════════════════════════════════════════

(define-macro (define-syntax name trans)
  (sx-make-macro-binding (list name trans)))

(display "=== boot-min.scm 加载完成 ===\n")(newline)
