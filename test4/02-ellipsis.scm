;; 02-ellipsis.scm — syntax-case 省略号模式

(define-syntax explain
  (lambda (stx)
    (syntax-case stx ()
      ((_ tag a ...)
       (syntax (begin (display tag) (display ": ") (display (list a ...)) (newline)))))))

(define-syntax list-of
  (lambda (stx)
    (syntax-case stx ()
      ((_ elt ...)
       (syntax (list elt ...))))))

(define-syntax define-vector
  (lambda (stx)
    (syntax-case stx ()
      ((_ name elt ...)
       (syntax (define name (vector elt ...)))))))

(define-syntax define-enum
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (member ...))
       (syntax (define name (quote (member ...))))))))

(define-syntax for
  (lambda (stx)
    (syntax-case stx ()
      ((_ (i from to) body ...)
       (syntax (do ((i from (+ i 1))) ((> i to)) body ...))))))

(define-syntax multiple-set!
  (lambda (stx)
    (syntax-case stx ()
      ((_ (var ...) (val ...))
       (syntax (begin (set! var val) ...))))))

(define-syntax match-pairs
  (lambda (stx)
    (syntax-case stx ()
      ((_ (key val) ...)
       (syntax '((key . val) ...))))))

(explain "ellipsis" 10 20 30)
(list-of 1 2 3 4 5)
(define a 'a) (define b 'b) (define c 'c)
(define-vector vec-abc a b c)
(define-enum color (red green blue))
(for (i 1 5) (display i) (newline))
(define a 0) (define b 0) (multiple-set! (a b) (1 2))
