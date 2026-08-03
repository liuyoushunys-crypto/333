;; test-lang-sh.scm — isolated tests for lang-sh.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-sh.scm")

(t "sh #{x + 1}" 6 ((lambda (x) #{x + 1}) 5))
(t "sh #{i < 5}" #t ((lambda (i) #{i < 5}) 3))
(t "sh #{x > 0}" #f ((lambda (x) #{x > 0}) -1))

(display "\n=== test-lang-sh.scm ===\n")
;; test-lang-sh.scm — Test Shell-like language (simplified)

(display "\n--- echo ---\n")
(echo "hello from Shell")
(t "echo result" (begin (echo "ok") "ok") "ok")

(display "\n--- sh-var ---\n")
(sh-var name = "Scheme")
(t "sh-var string" name "Scheme")
(sh-var count = 42)
(t "sh-var number" count 42)

(display "\n--- test ---\n")
(t "sh test =" (test 42 = 42) #t)
(t "sh test >" (test 10 > 5) #t)
(t "sh test <" (test 3 < 7) #t)

(display "\n--- for loop ---\n")
(let ((acc '()))
  (sh-for x in '(a b c) do
    (set! acc (cons x acc))
  done)
  (t "sh-for" (reverse acc) '(a b c)))

(display "\n=== All Shell demos done ===\n")
