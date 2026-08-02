;; 08-computed.scm — 展开时计算

(define-macro (my-macro-time-add a b)
  (let ((sum (+ a b)))
    `(quote ,sum)))

(define-macro (my-factorial-computed n)
  (let loop ((i n) (acc 1))
    (if (= i 0)
      `(quote ,acc)
      (loop (- i 1) (* acc i)))))

(define-macro (my-table . pairs)
  `(quote (,@pairs)))

(display (my-macro-time-add 40 2)) (newline)
(display (my-factorial-computed 5)) (newline)
(display (my-table a b c)) (newline)
