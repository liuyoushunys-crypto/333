;; ============================================================
;; 第十七部分：无穷流（惰性求值演示）
;; ============================================================
;; stream-car/cdr/null?/ref/map/filter/take 由 initenv.py 的 Python builtin 提供

(define (nat-stream n)
  (stream-cons n (nat-stream (+ n 1))))

(define (naturals . start)
  (nat-stream (if (null? start) 0 (car start))))

(define (sieve s)
  (stream-cons (stream-car s)
               (sieve (stream-filter
                       (lambda (x) (not (zero? (remainder x (stream-car s)))))
                       (stream-cdr s)))))

(define primes (sieve (nat-stream 2)))

;; ============================================================
;; 第十八部分：vector/string 辅助
;; string-upcase/downcase 的泛化版本
;; ============================================================

(define (string-tabulate n f)
  (let ((r (make-string n)))
    (do ((i 0 (+ i 1)))
        ((>= i n) r)
      (string-set! r i (f i)))))

(define (string-reverse str)
  (string-tabulate (string-length str)
    (lambda (i)
      (string-ref str (- (string-length str) i 1)))))

(define (vector-count pred vec)
  (let ((n (vector-length vec)))
    (let loop ((i 0) (c 0))
      (if (>= i n) c
          (if (pred (vector-ref vec i))
              (loop (+ i 1) (+ c 1))
              (loop (+ i 1) c))))))
