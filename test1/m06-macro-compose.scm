;; 06-macro-compose.scm — 宏组合

(define-macro (my-let-it val . body)
  `(let ((it ,val)) ,@body))

(define-macro (my-when test . body)
  `(if ,test (begin ,@body) (if #f #f)))

(define-macro (my-aif test then else)
  `(let ((it ,test)) (if it ,then ,else)))

(define-macro (my-awhen test . body)
  `(let ((it ,test)) (if it (begin ,@body) (if #f #f))))

(define-macro (my-define-func name args . body)
  `(define (,name ,@args) ,@body))

(define-macro (my-with-open-file (var filename) . body)
  `(let ((,var (open-input-file ,filename)))
     (let ((result (begin ,@body)))
       (close-port ,var)
       result)))

(define-macro (my-time . body)
  `(let ((start (current-second)))
     (let ((result (begin ,@body)))
       (display "elapsed: ")
       (display (- (current-second) start))
       (newline)
       result)))

(define-macro (my-assert-expr expr)
  `(if (not ,expr)
     (error "assertion failed:" (quote ,expr))
     (if #f #f)))

(define-macro (my-ensure test . body)
  `(if (not ,test)
     (error "ensure failed:" (quote ,test))
     (begin ,@body)))

(define-macro (my-named-let name bindings . body)
  (let ((vars (map car bindings))
        (vals (map cadr bindings)))
    `(letrec ((,name (lambda ,vars ,@body)))
       (,name ,@vals))))

(my-define-func double (x) (* x 2))
(display (double 5)) (newline)
(my-assert-expr (> 3 1))
(my-ensure (= 2 2) (display "ok") (newline))
(my-named-let loop ((i 0) (acc 1)) (if (< i 5) (loop (+ i 1) (* acc 2)) acc))
(display (my-time (* 1 2 3 4 5 6))) (newline)
