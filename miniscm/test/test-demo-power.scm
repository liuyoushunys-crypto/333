

;; ═══════════════════════════════════════════
;; test-demo-power.scm — demo-power.scm 宏测试 (从 test-misc 分离)
;; 加载 demo-power.scm 并测试其全部宏
;; ═══════════════════════════════════════════

(display "\n=== test-demo-power.scm ===\n")
;; test-demo-power.scm — 测试 demo-power.scm 所有宏

(display "=== testing demo-power.scm ===\n\n")

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/demo-power.scm")

;; 1. infix
(display "=== infix ===\n")
(test "infix +*" (infix 2 + 3 * 4) 14)
(test "infix --" (infix 10 - 3 - 2) 5)
(test "infix *" (infix 3 * 5) 15)
(test "infix /+" (infix 10 / 2 + 3) 8)

;; 3. for-in
(display "=== for-in ===\n")
(let ((acc '()))
  (for-in x '(a b c) (set! acc (cons x acc)))
  (test "for-in" (reverse acc) '(a b c)))

;; 5. until
(display "=== until ===\n")
(let ((x 0) (acc '()))
  (until (> x 3) (set! acc (cons x acc)) (set! x (+ x 1)))
  (test "until" (reverse acc) '(0 1 2 3)))

;; 8. ==
(display "=== == ===\n")
(== (+ 1 2) 3)  ;; should pass silently

;; 10. ?
(display "=== ? ===\n")
(test "? true" (? (> 3 1) : "yes" : "no") "yes")
(test "? false" (? #f : "yes" : "no") "no")

;; 12. lazy sequences
(display "=== lazy sequences ===\n")
(test "lazy-take naturals" (lazy-take 5 (cdr (force naturals))) '(1 2 3 4 5))

(define twos (lazy-map (lambda (x) 2) naturals))
(test "lazy-map take" (lazy-take 3 (cdr (force twos))) '(2 2 2))

(define evens (lazy-filter even? naturals))
(test "lazy-filter take" (lazy-take 3 evens) '(0 2 4))

;; 13. defcurry
(display "=== defcurry ===\n")
(defcurry (add a b) (+ a b))
(test "defcurry 3+4" ((add 3) 4) 7)
(defcurry (mul3 a b c) (* a b c))
(test "defcurry mul3" (((mul3 2) 3) 4) 24)

;; 14. catch
(display "=== catch ===\n")
(test "catch normal" (catch (+ 1 2) (e) "oops") 3)
(let ((caught #f))
  (catch (error "boom") (e) (set! caught #t))
  (test "catch error" caught #t))

;; 15. |>
(display "=== |> pipe ===\n")
(test "|> +*" (|> 5 (+ 1) (* 2)) 12)
(test "|> sort-rev" (|> '(3 1 4 1 5) (lambda (x) (sort < x)) (reverse)) '(5 4 3 1 1))
(test "|> identity" (|> 42) 42)

;; 16. =def
(display "=== =def ===\n")
(=def (x y) '(10 20))
(test "=def x" x 10)
(test "=def y" y 20)
(=def (a b c) '(1 2 3))
(test "=def a b c" (+ a b c) 6)
(=def z 42)
(test "=def single" z 42)

;; 17. set-last!
(display "=== set-last! ===\n")
(let ((lst (list 1 2 3)))
  (set-last! lst 99)
  (test "set-last!" lst '(1 2 99)))
(let ((lst (list 42)))
  (set-last! lst 0)
  (test "set-last! single" lst '(0)))

;; 19. defer
(display "=== defer ===\n")
(let ((x 'before))
  (defer (set! x 'after) (set! x 'during))
  (test "defer" x 'after))

;; 21. qsort
(display "=== qsort ===\n")
(test "qsort" (qsort '(3 1 4 1 5 9 2)) '(1 1 2 3 4 5 9))
(test "qsort empty" (qsort '()) '())
(test "qsort single" (qsort '(1)) '(1))

;; 22. show
(display "=== show ===\n")
(test "show" (show "val" 42) 42)

;; 24. assert-throws
(display "=== assert-throws ===\n")
(test "assert-throws pass" (begin (assert-throws (error "oops")) #t) #t)
(test "assert-throws fail" (begin (assert-throws (+ 1 2)) #t) #t)

;; 25. compose-all
(display "=== compose ===\n")
(define comp-f (compose-all (lambda (x) (* x 2)) (lambda (x) (+ x 1))))
(test "compose (2*(5+1))=12" (comp-f 5) 12)
(test "compose identity" ((compose-all) 42) 42)

;; 27. with-open
(display "=== with-open ===\n")
(test "with-open" (with-open (p (open-input-string "hello")) (read-char p)) #\h)

;; 29. retry
(display "=== retry ===\n")
(let ((counter 0))
  (test "retry success" (retry 3
                          (set! counter (+ counter 1))
                          (if (< counter 3) #f counter)) 3))

;; 31. defn
(display "=== defn ===\n")
(defn square1 (x) (* x x))
(test "defn" (square1 5) 25)
(defn add-mul (a b c) (+ a (* b c)))
(test "defn 3 args" (add-mul 1 2 3) 7)

;; 33. parallel
(display "=== parallel ===\n")
(test "parallel" (parallel (+ 1 2) (* 3 4) (- 10 5)) '(3 12 5))

;; 34. do-times
(display "=== do-times ===\n")
(let ((s 0)) (do-times (i 10) (set! s (+ s i))) (test "do-times sum" s 45))

;; 35. define-struct
(display "=== define-struct ===\n")
(define-struct book title author year)
(define b (make-book "SICP" "Abelson" 1984))
(test "struct title" (book-title b) "SICP")
(test "struct author" (book-author b) "Abelson")
(test "struct year" (book-year b) 1984)
(test "struct pred" (book? b) #t)
(test "struct not" (book? '(1 2)) #f)

;; 38. cond-let
(display "=== cond-let ===\n")
(test "cond-let first" (cond-let ((x 42) x) ((y 99) y)) 42)
(test "cond-let second" (cond-let ((x #f) (display x)) ((y 99) y)) 99)
(test "cond-let else" (cond-let ((x #f) x) ((y #f) y) (else 42)) 42)

;; 41. Y combinator
(display "=== Y combinator ===\n")
(test "Y fact 10" (fact-y 10) 3628800)
(test "Y fact 0" (fact-y 0) 1)
(test "Y fact 5" (fact-y 5) 120)

;; 42. for-else
(display "=== for-else ===\n")
(let ((found #f))
  (for-else (x '(1 2 3)) (and (even? x) (set! found #t) x)
    (else (set! found #f)))
  (test "for-else found" found #t))
(let ((ran-else #f))
  (for-else (x '(1 3 5)) (and (even? x) x)
    (else (set! ran-else #t)))
  (test "for-else no match" ran-else #t))

;; 44. json
(display "=== json ===\n")
(define person (json (name "Alice") (age 30)))
(test "json name" (person 'name) "Alice")
(test "json age" (person 'age) 30)

;; 45. while*
(display "=== while* ===\n")
(test "while* sum" (let ((i 0) (s 0))
                     (while* (< i 5) (set! s (+ s i)) (set! i (+ i 1)))
                     s) 10)
(test "while* none" (let ((i 0)) (while* #f (set! i 99)) i) 0)

(display "\n=== All demo-power tests done ===\n")


;; 30. hash literal
(display "=== hash ===\n")
(define h (hash 'a 1 'b 2 'c 3))
(test "hash ref a" (hash-table-ref h 'a) 1)
(test "hash ref b" (hash-table-ref h 'b) 2)
(test "hash ref c" (hash-table-ref h 'c) 3)
(test "hash size" (hash-table-size h) 3)
