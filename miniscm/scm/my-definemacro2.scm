;; my-definemacro2.scm — 从 .mscm_cache 生成的最小原语版 (微解释器)
;; 普通函数用缓存 Body (if/lambda/begin/set!/quote/应用) 重建;
;; define-macro 宏自举定义保留 (原版语法, 经 my-definemacro 注册)。

(define (my-bind-pattern pattern args) (if (symbol? pattern) (begin (if (eq? pattern (quote _)) (quote ()) (list (cons pattern args)))) (if (not (pair? pattern)) (begin (quote ())) (if (null? pattern) (begin (quote ())) (begin (append (my-bind-elem (car pattern) (car args)) (my-bind-pattern (cdr pattern) (cdr args))))))))
(define (my-bind-elem elem arg) (if (eq? elem (quote _)) (begin (quote ())) (if (symbol? elem) (begin (list (cons elem arg))) (if (if (pair? elem) (if (symbol? (car elem)) (null? (cdr elem)) #f) #f) (begin (list (cons (car elem) arg))) (if (pair? elem) (begin (my-bind-pattern elem arg)) (begin (quote ())))))))
(define (sx-macro-expand pattern body args callenv) ((lambda (bindings) ((lambda (app-form) (begin (eval app-form callenv))) (cons (cons (quote lambda) (cons (map (lambda (b) (car b)) bindings) body)) (map (lambda (b) (list (quote quote) (cdr b))) bindings)))) (my-bind-pattern pattern args)))
(define (my-definemacro name-pat . body) ((lambda (name pat) (sx-defmacro name (quote args) (list (list (quote sx-macro-expand) (list (quote quote) pat) (list (quote quote) body) (quote args) (quote (sx-expand-env)))) (sx-expand-env))) (car name-pat) (cdr name-pat)))
(my-definemacro '(define-macro name-pat . dm-body)
  '(cons 'my-definemacro
          (cons (list 'quote name-pat)
                (map (lambda (b) (list 'quote b)) dm-body))))
