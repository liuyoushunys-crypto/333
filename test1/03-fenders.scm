;; 03-fenders.scm — syntax-case 护卫

(define-syntax assert-type
  (lambda (stx)
    (syntax-case stx ()
      ((_ name val type)
       (syntax (let ((v val))
                 (unless (type v)
                   (error (quote name) "type mismatch" v))
                 v))))))

(define-syntax define-checked
  (lambda (stx)
    (syntax-case stx ()
      ((_ (name arg ...) body body* ...)
       (syntax (define name (lambda (arg ...) body body* ...)))))))

(define-syntax lambda/arity
  (lambda (stx)
    (syntax-case stx ()
      ((_ (a b) body)
       (syntax (lambda (a b) body))))))

(define-syntax define-option
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (opt val) ...)
       (syntax (define name (list (quote opt) ...)))))))

(define-syntax check-positive
  (lambda (stx)
    (syntax-case stx ()
      ((_ val)
       (syntax (let ((v val)) (if (< v 0) (error "negative" v) v)))))))

(assert-type my-add 42 number?)
(define-checked (double x) (* x 2))
(lambda/arity (a b) (+ a b))
(define-option config (host "localhost") (port 8080))
(check-positive 5)
