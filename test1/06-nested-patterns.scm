;; 06-nested-patterns.scm — 嵌套模式匹配

(define-syntax destructure-let
  (lambda (stx)
    (syntax-case stx ()
      ((_ ((a . b) expr) body ...)
       (syntax (let ((tmp expr)) (let ((a (car tmp)) (b (cdr tmp))) body ...)))))))

(define-syntax destructure-list
  (lambda (stx)
    (syntax-case stx ()
      ((_ ((a b . rest) expr) body ...)
       (syntax (let ((tmp expr)) (let ((a (car tmp)) (b (cadr tmp)) (rest (cddr tmp))) body ...)))))))

(define-syntax match-tree
  (lambda (stx)
    (syntax-case stx ()
      ((_ ((left . right) expr) body ...)
       (syntax
         (let ((t expr))
           (let ((left (car t)) (right (cdr t)))
             body ...)))))))

(define-syntax define-ppair
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (car-expr . cdr-expr))
       (syntax (define name (cons car-expr cdr-expr)))))))

(define-syntax pattern-match
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr
         ((a b) body1)
         ((a . b) body2)
         (_ body3))
       (syntax
         (let ((v expr))
           (cond
             ((and (list? v) (= (length v) 2)) (let ((a (car v)) (b (cadr v))) body1))
             ((pair? v) (let ((a (car v)) (b (cdr v))) body2))
             (else body3))))))))

(define-syntax nested-let
  (lambda (stx)
    (syntax-case stx ()
      ((_ (((a b) (c d)) expr) body ...)
       (syntax
         (let ((tmp expr))
           (let ((a (caar tmp)) (b (cdar tmp))
                 (c (caadr tmp)) (d (cdadr tmp)))
             body ...)))))))

(destructure-let ((x . y) (cons 1 2)) (list x y))
(destructure-list ((a b . rest) (list 1 2 3 4)) (list a b rest))
(match-tree ((a . b) (cons 'x 'y)) (list a b))
(define-ppair my-pair (1 . 2))
(nested-let (((a b) (c d)) '((1 . 2) (3 . 4))) (list a b c d))
