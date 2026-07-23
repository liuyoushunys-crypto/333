;; 10-comprehensive.scm — 综合示例

(define-macro (my-define-logged name args . body)
  `(define (,name ,@args)
     (display "calling ") (display (quote ,name)) (display ": ") (newline)
     (let ((result (begin ,@body)))
       (display "result: ") (display result) (newline)
       result)))

(define-macro (my-define-cached name args . body)
  `(begin
     (define ,name (let ((cache (make-hash-table)))
                     (lambda ,args
                       (let ((key (list ,@args)))
                         (or (hash-table-ref/default cache key #f)
                             (let ((val (begin ,@body)))
                               (hash-table-set! cache key val)
                               val))))))))

(my-define-logged greet (name)
  (string-append "hello " name))
(display (greet "world")) (newline)

(my-define-cached fib (n)
  (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))
(display (fib 10)) (newline)
