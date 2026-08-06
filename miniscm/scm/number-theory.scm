;; 第十四部分：数论（纯 Scheme 精确算术）
;; ============================================================

(define (scheme-gcd . args)
  (if (null? args) 0
      (let loop ((g (abs (car args))) (rest (cdr args)))
        (if (null? rest) g
            (loop (let rec ((a g) (b (abs (car rest))))
                    (if (= b 0) a (rec b (remainder a b))))
                  (cdr rest))))))

(define (scheme-lcm . args)
  (if (null? args) 1
      (let loop ((acc (abs (car args))) (nums (cdr args)))
        (if (null? nums) acc
            (let ((a acc) (b (abs (car nums))))
              (if (or (= a 0) (= b 0)) 0
                  (loop (quotient (* a b) (scheme-gcd a b))
                        (cdr nums))))))))

(define (prime? n)
  ;; 试除法素性测试
  (cond ((< n 2) #f)
        ((= n 2) #t)
        ((even? n) #f)
        (else
         (let loop ((d 3))
           (cond ((> (* d d) n) #t)
                 ((zero? (remainder n d)) #f)
                 (else (loop (+ d 2))))))))

(define (factor n)
  (let loop ((x n) (p 2) (acc '()))
    (cond ((<= x 1) (reverse acc))
          ((< x (* p p)) (reverse (cons x acc)))
          ((zero? (remainder x p))
           (loop (quotient x p) p (cons p acc)))
          (else (loop x (+ p 1) acc)))))

(define (fib-pair n)
  (cond ((<= n 0) '(0 . 1))
        (else
          (let* ((pair (fib-pair (quotient n 2)))
                (a (car pair)) (b (cdr pair))
                (c (+ (* a (- (* b 2) a))))
                (d (+ (* a a) (* b b))))
            (if (even? n) (cons c d)
                (cons d (+ c d)))))))

(define (fibonacci n)
  ;; O(log n) 的 Fibonacci — 矩阵快速幂
  (car (fib-pair n)))

(define (binomial n k)
  ;; 二项式系数 C(n,k)，纯 Scheme 精确算术
  (if (or (< k 0) (> k n)) 0
      (let ((k (min k (- n k))))
        (let loop ((i 1) (num n) (den 1) (acc 1))
          (if (> i k) acc
              (loop (+ i 1) (- num 1) (+ den 1)
                    (quotient (* acc num) den)))))))

(define (factorial n)
  (if (< n 2) 1
      (let loop ((i 2) (acc 1))
        (if (> i n) acc
            (loop (+ i 1) (* acc i))))))

;; ============================================================
