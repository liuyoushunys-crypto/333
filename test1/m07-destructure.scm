;; 07-destructure.scm — 手动解构参数

(define-macro (my-with-car+cdr pair-expr . body)
  `(let ((tmp ,pair-expr))
     (let ((a (car tmp)) (d (cdr tmp)))
       ,@body)))

(define-macro (my-let-values vars expr . body)
  `(call-with-values (lambda () ,expr)
     (lambda ,vars ,@body)))

(my-with-car+cdr (cons 1 2) (display a) (display d) (newline))
(my-let-values (x y) (values 1 2) (display x) (display y) (newline))
