;; 01-basic-patterns.scm — syntax-case 基础模式匹配

(define-syntax swap
  (lambda (stx)
    (syntax-case stx ()
      ((_ a b)
       (syntax (let ((tmp a)) (set! a b) (set! b tmp)))))))

(define-syntax when
  (lambda (stx)
    (syntax-case stx ()
      ((_ test body ...)
       (syntax (if test (begin body ...)))))))

(define-syntax unless
  (lambda (stx)
    (syntax-case stx ()
      ((_ test body ...)
       (syntax (if (not test) (begin body ...)))))))

(define-syntax or*
  (lambda (stx)
    (syntax-case stx ()
      ((_) (syntax #f))
      ((_ x) (syntax x))
      ((_ x y ...)
       (syntax (let ((t x)) (if t t (or* y ...))))))))

(define-syntax define-curried
  (lambda (stx)
    (syntax-case stx ()
      ((_ (f a ...) body body* ...)
       (syntax (define f (lambda (a ...) body body* ...)))))))

(define x 1)
(define y 2)
(when (> x 0) (set! x (+ x 1)))
(unless (> y 10) (set! y (+ y 10)))
(swap x y)
(or* #f #f 42)
(define-curried (add3 a b c) (+ a b c))
