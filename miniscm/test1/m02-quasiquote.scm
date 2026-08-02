;; 02-quasiquote.scm — 反引用/逗号模式构建代码

(define-macro (my-if test then else)
  `(if ,test ,then ,else))

(define-macro (my-cond . clauses)
  (if (null? clauses)
    (if #f #f)
    (let ((first (car clauses)))
      (if (eq? (car first) 'else)
        `(begin ,@(cdr first))
        `(if ,(car first)
           (begin ,@(cdr first))
           (my-cond ,@(cdr clauses)))))))

(define-macro (my-let* bindings . body)
  (if (null? bindings)
    `(begin ,@body)
    (let ((b (car bindings)))
      `(let ((,(car b) ,(cadr b)))
         (my-let* ,(cdr bindings) ,@body)))))

(define-macro (my-lambda args . body)
  `(lambda ,args ,@body))

(define-macro (defun* name (arg . rest) . body)
  `(define ,name
     (lambda (,arg . ,rest) ,@body)))

(define-macro (with-gensyms (syms) . body)
  (let ((news (map (lambda (s) (list s (list 'gensym))) syms)))
    `(let ,news ,@body)))

(define-macro (my-dotimes (var count) . body)
  `(do ((,var 0 (+ ,var 1))) ((>= ,var ,count)) ,@body))

(define-macro (my-do-ec . clauses)
  (let ((header (car clauses))
        (body (cdr clauses)))
    `(begin (do ((,(car header) 0 (+ ,(car header) 1)))
                ((>= ,(car header) ,(cadr header))))
             ,@body)))

(define-macro (my-while test . body)
  `(let loop ()
     (if ,test
       (begin ,@body (loop))
       (if #f #f))))

(define-macro (my-until test . body)
  `(let loop ()
     (begin ,@body
       (if ,test (if #f #f) (loop)))))

(my-if (> 3 1) (display "true") (display "false"))
(newline)
(my-cond ((> 3 1) (display "a")) ((> 1 3) (display "b")) (else (display "c")))
(newline)
(my-let* ((x 1) (y (+ x 1))) (display y)) (newline)
(my-while #f (display "never"))
(define counter 3)
(my-until (= counter 0) (display counter) (set! counter (- counter 1)))
(newline)
