;; 01-basic.scm — define-macro 基础用法

(define-macro (twice . body)
  (cons 'begin (append body body)))

(define-macro (when test . body)
  (list 'if test (cons 'begin body) (if #f #f)))

(define-macro (unless test . body)
  (list 'if test (if #f #f) (cons 'begin body)))

(define-macro (swap a b)
  (list 'let (list (list 'tmp a))
    (list 'set! a b)
    (list 'set! b 'tmp)))

(define-macro (defun name args . body)
  (cons 'define (cons (cons name args) body)))

(define-macro (my-let bindings . body)
  (let ((vars (map car bindings))
        (vals (map cadr bindings)))
    (cons (cons 'lambda (cons vars body)) vals)))

(define-macro (my-and . args)
  (cond
    ((null? args) '#t)
    ((null? (cdr args)) (car args))
    (else (list 'if (car args) (cons 'my-and (cdr args)) '#f))))

(twice (display "hello") (newline))
(when (> 3 1) (display "yes") (newline))
(unless (> 1 3) (display "works") (newline))
(define a 1) (define b 2)
(swap a b)
(display a) (newline) (display b) (newline)
(defun greet (name) (display "hello ") (display name) (newline))
(greet "world")
(my-let ((x 10) (y 20)) (+ x y))
