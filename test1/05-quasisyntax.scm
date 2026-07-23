;; 05-quasisyntax.scm — quasisyntax / unsyntax / unsyntax-splicing

(define-syntax define-infix
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (left op right) body)
       (quasisyntax
         (define name
           (lambda (left right)
             (unsyntax body))))))))

(define-syntax splice-example
  (lambda (stx)
    (syntax-case stx ()
      ((_ a b c)
       (quasisyntax
         (list (unsyntax #'a) (unsyntax #'b) (unsyntax #'c)))))))

(define-syntax def-wrapper
  (lambda (stx)
    (syntax-case stx ()
      ((_ name value)
       (quasisyntax
         (begin
           (display "defining ")
           (display (quote (unsyntax #'name)))
           (newline)
           (define name (unsyntax #'value))))))))

(define-syntax labeled-lambda
  (lambda (stx)
    (syntax-case stx ()
      ((_ name args body)
       (quasisyntax
         (letrec ((name (lambda args
                          (unsyntax #'body))))
           name))))))

(define-infix add (a + b) (+ a b))
(define x 1) (define y 2) (define z 3)
(splice-example x y z)
(def-wrapper greet "hello")
(define add2 (labeled-lambda add2 (x) (+ x 2)))
