;; 10-multi-clause.scm — 多子句 syntax-case

(define-syntax my-cond
  (lambda (stx)
    (syntax-case stx ()
      ((_ (else body ...))
       (syntax (begin body ...)))
      ((_ (test body ...) rest ...)
       (syntax (if test
                  (begin body ...)
                  (my-cond rest ...)))))))

(define-syntax my-case
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr (else body ...))
       (syntax (let ((v expr)) (begin body ...))))
      ((_ expr ((key ...) body ...) rest ...)
       (syntax (let ((v expr))
                 (if (memv v (list key ...))
                   (begin body ...)
                   (my-case v rest ...))))))))

(define-syntax type-dispatch
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (syntax (error "no match" expr)))
      ((_ expr ((? pred) body ...) rest ...)
       (syntax (if (pred expr)
                  (begin body ...)
                  (type-dispatch expr rest ...)))))))

(define-syntax my-match
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr (pattern body ...) rest ...)
       (syntax (let ((v expr))
                 (if (equal? v 'pattern)
                   (begin body ...)
                   (my-match v rest ...))))))))

(define-syntax make-cond*
  (lambda (stx)
    (syntax-case stx ()
      ((_) (syntax #f))
      ((_ (test => proc) rest ...)
       (syntax (let ((t test)) (if t (proc t) (make-cond* rest ...)))))
      ((_ (test expr ...) rest ...)
       (syntax (if test (begin expr ...) (make-cond* rest ...)))))))

(define-syntax define-variant
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (variant ...))
       (syntax
         (begin
           (define (name? x) #f)
           (define (make-name . args) (error "base"))
           (define (match-name x) #f)
           ...))))))

(define-syntax parse-args
  (lambda (stx)
    (syntax-case stx ()
      ((_) (syntax '()))
      ((_ (key val) rest ...)
       (syntax (cons (cons 'key val) (parse-args rest ...))))
      ((_ key rest ...)
       (syntax (cons 'key (parse-args rest ...)))))))

(define-syntax multi-define
  (lambda (stx)
    (syntax-case stx ()
      ((_) (syntax (begin)))
      ((_ (name val) rest ...)
       (syntax (begin (define name val) (multi-define rest ...)))))))

(my-cond ((> 3 1) "yes") (else "no"))
(my-case 42 ((1 2 3) "small") ((42) "answer") (else "unknown"))
(multi-define (a 1) (b 2) (c 3))
(parse-args (x 10) (y 20))
