;;;; ============================================================
;;;; Enterprise Scheme — 完整功能测试套件 (mode 1 原生求值器)
;;;; ============================================================

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (display expected)
             (display "  actual: ") (display actual) (newline))))


; define-macro 中使用模式匹配
(define-macro (my-cond . clauses)
  (if (null? clauses) ''undefined
      (let ((c (car clauses)) (rest (cdr clauses)))
        (if (equal? (car c) 'else)
            `(begin ,@(cdr c))
            `(if ,(car c) (begin ,@(cdr c)) (my-cond ,@rest))))))

(define (classify n)
  (my-cond ((< n 0) 'negative)
           ((= n 0) 'zero)
           (else 'positive)))
(check "my-cond negative" (classify -5) 'negative)
(check "my-cond zero" (classify 0) 'zero)
(check "my-cond positive" (classify 5) 'positive)

