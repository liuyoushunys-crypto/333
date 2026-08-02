(unless (defined? 'zero?)
  (define zero? (cut = <> 0)))
(unless (defined? 'abs)
  (define (abs x) (if (< x 0) (- x) x)))
(define (add1 n) (+ n 1))
(define (sub1 n) (- n 1))
(define reciprocal (cut / 1 <>))

(define exact-nonnegative-integer?
  (lambda (n) (and (integer? n) (not (inexact? n)) (>= n 0))))

(define exact-rational?
  (lambda (n) (and (number? n) (not (complex? n)) (exact? n))))

(define (ceiling->exact x) (inexact->exact (ceiling x)))
(define (floor->exact x)   (inexact->exact (floor x)))
(define (truncate->exact x) (inexact->exact (truncate x)))
(define (round->exact x)   (inexact->exact (round x)))

;; ============= 位操作 =============
(define (bit-set? n i) (not (zero? (bit-and n (arithmetic-shift 1 i)))))
(define (bitwise-any-bit-set? n m) (not (zero? (bit-and n m))))
(define (copy-bit n i v)
  (if (not (zero? v)) (bitwise-ior n (arithmetic-shift 1 i))
        (bit-and n (bit-not (arithmetic-shift 1 i)))))
(define (bit-field n s e)
  (bit-and (arithmetic-shift n (- s))
               (- (arithmetic-shift 1 (- e s)) 1)))

;; ============= 复数 / 双曲线 =============
(unless (defined? 'magnitude)
  (define magnitude
    (lambda (z) (sqrt (+ (* (real-part z) (real-part z))
                         (* (imag-part z) (imag-part z)))))))

(unless (defined? 'make-rectangular)
  (define make-rectangular
    (lambda (r i) (+ r (* i 1i)))))

(define (sinh x)  (/ (- (exp x) (exp (- x))) 2))
(define (cosh x)  (/ (+ (exp x) (exp (- x))) 2))
(define (tanh x)  (/ (- (exp x) (exp (- x))) (+ (exp x) (exp (- x)))))
(define (sech x)  (/ 1 (cosh x)))
(define (csch x)  (/ 1 (sinh x)))
(define (coth x)  (/ (cosh x) (sinh x)))
(define (log10 x)
  (if (and (integer? x) (exact? x))
      (let loop ((n 0) (p 1))
        (if (= p x) n
            (if (> p x) (/ (log x) (log 10))
                (loop (+ n 1) (* p 10)))))
      (/ (log x) (log 10))))
(define (log2 x)  (/ (log x) (log 2)))

;; ============= 列表 =============
(define (last-pair lst)
  (if (null? (cdr lst)) lst (last-pair (cdr lst))))
(define last (lambda (lst) (car (last-pair lst))))
(define (but-last lst)
  (if (null? lst) '()
      (let loop ((xs lst) (acc '()))
        (if (null? (cdr xs)) (reverse acc)
            (loop (cdr xs) (cons (car xs) acc))))))
(define list-copy (cut map values <>))
(define (cons* x . xs)
  (if (null? xs) x (cons x (apply cons* xs))))
(define (flip f) (lambda (a b) (f b a)))
(define xcons (flip cons))

(define (make-list n . fill)
  (let ((v (if (pair? fill) (car fill) 0)))
    (do ((i 0 (+ i 1)) (r '() (cons v r))) ((= i n) r))))

(define (iota n . args)
  (let ((s (if (null? args) 0 (car args)))
        (t (if (or (null? args) (null? (cdr args))) 1 (cadr args))))
    (do ((i 0 (+ i 1)) (r '() (cons (+ s (* i t)) r))) ((= i n) (reverse r)))))


(define (const x) (lambda _ x))
(define (iterate f n x)
  (let lp ((i 0) (r x)) (if (= i n) r (lp (+ i 1) (f r)))))


;; ============= 可推导的算术（基于 +-*/<>=）=============
(unless (defined? 'nan?)
  (define nan? (lambda (x) (and (number? x) (not (= x x))))))
(define (infinite? x) (and (real? x) (or (= x +inf.0) (= x -inf.0))))
(unless (defined? 'finite?)
  (define (finite? x) (and (number? x) (not (infinite? x)) (not (nan? x)))))
(unless (defined? 'exact-integer?)
  (define (exact-integer? n) (and (integer? n) (exact? n))))
(define (floor-quotient a b) (floor (/ a b)))
(define (floor-remainder a b) (- a (* b (floor-quotient a b))))
(define (floor/ a b) (values (floor-quotient a b) (floor-remainder a b)))
(define (truncate-quotient a b) (truncate (/ a b)))
(define (truncate-remainder a b) (- a (* b (truncate-quotient a b))))
(define (truncate/ a b) (values (truncate-quotient a b) (truncate-remainder a b)))

(define (integer-length n)
  (if (negative? n) (integer-length (- n))
      (do ((m n (quotient m 2)) (l 0 (+ l 1))) ((zero? m) l))))

(define (bit-count n)
  (if (negative? n) (bit-count (- n))
      (let lp ((m n) (c 0)) (if (zero? m) c (lp (quotient m 2) (+ c (modulo m 2)))))))


(define (boolean->string b) (if b "#t" "#f"))
