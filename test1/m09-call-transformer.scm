;; 09-call-transformer.scm — 调用其他宏的宏

(define-macro (my-define-curried name args . body)
  (letrec ((make-curry
             (lambda (args body)
               (if (null? args) body
                 `(lambda (,(car args))
                    ,(make-curry (cdr args) `(begin ,@body)))))))
    `(define ,name ,(make-curry args `(begin ,@body)))))

(define-macro (my-call-with-progress . body)
  `(begin
     (display "starting...") (newline)
     (let ((result (begin ,@body)))
       (display "done") (newline)
       result)))

(define-macro (my-repeat-times n . body)
  `(do ((i 0 (+ i 1))) ((>= i ,n)) ,@body))

(define-macro (my-define-counter name)
  (let ((counter (gensym)))
    `(begin
       (define ,counter 0)
       (define (,name)
         (let ((current ,counter))
           (set! ,counter (+ ,counter 1))
           current)))))

(define-macro (my-thunk . body)
  `(lambda () ,@body))

(my-call-with-progress (display "working") (newline))
(my-repeat-times 3 (display "hi ") (newline))

(my-define-counter next-id)
(display (next-id)) (display (next-id)) (display (next-id)) (newline)

(define my-add2 (my-thunk (+ 1 2)))
(display (my-add2)) (newline)
