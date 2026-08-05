;; 03-rest-args.scm — 可变参数与解构

(define-macro (define-keyword name (key val) . rest)
  (let ((body (if (null? rest) val (cons 'begin rest))))
    `(begin
       (define ,name (make-hash-table))
       (hash-table-set! ,name (quote ,key) ,body))))

(define-macro (my-match val . clauses)
  (let loop ((cs clauses))
    (if (null? cs)
      (if #f #f)
      (let ((c (car cs)))
        `(if (equal? ,val (quote ,(car c)))
           (begin ,@(cdr c))
           ,(loop (cdr cs)))))))

(define-macro (my-letrec bindings . body)
  (let ((vars (map car bindings))
        (vals (map (lambda (b) (if (pair? (cadr b)) (list 'lambda (cdadr b) (car (cddadr b))) (cadr b))) bindings)))
    (cons (cons 'letrec (cons (map list vars vals) nil)) body)))

(define-macro (my-delay expr)
  `(make-promise (lambda () ,expr)))

(define-macro (my-parameterize ((param val)) . body)
  `(let ((old ,param))
     (set! ,param ,val)
     (let ((result (begin ,@body)))
       (set! ,param old)
       result)))

(define-macro (my-inc! var . rest)
  (let ((amount (if (null? rest) 1 (car rest))))
    `(set! ,var (+ ,var ,amount))))

(define-macro (my-push! val list-var)
  `(set! ,list-var (cons ,val ,list-var)))

(define-macro (my-pop! list-var)
  (let ((tmp (gensym)))
    `(let ((,tmp (car ,list-var)))
       (set! ,list-var (cdr ,list-var))
       ,tmp)))

(define count 0)
(my-inc! count)
(my-inc! count 5)
(display count) (newline)

(define my-list (quote ()))
(my-push! 1 my-list)
(my-push! 2 my-list)
(display my-list) (newline)
(display (my-pop! my-list)) (newline)
(display my-list) (newline)
