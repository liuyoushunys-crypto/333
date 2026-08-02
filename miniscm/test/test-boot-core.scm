;; test-boot-core.scm — boot-core.scm 所有宏全面测试
;; Run: python3 miniscm.py test/test-boot-core.scm
;;
;; 测试策略: 覆盖 boot-core.scm 中所有宏的正常路径、边界情况、错误路径。
;; 测试分组:
;;   1. let / let* / letrec
;;   2. and / or
;;   3. when / unless
;;   4. cond
;;   5. case
;;   6. do
;;   7. define-values
;;   8. delay / force
;;   9. let-values / let*-values
;;  10. parameterize
;;  11. guard
;;  12. define-record-type
;;  13. cut / cute
;;  14. include / cond-expand
;;  15. atom? / void?

(display "\n=== boot-core.scm 全面测试 ===\n\n")

;; ════════════════════════════════════════════════════════════════
;; 1. let / let* / letrec
;; ════════════════════════════════════════════════════════════════

(display "-- let\n")

(test-equal "let basic" 3 (let ((x 1) (y 2)) (+ x y)))
(test-equal "let single binding" 42 (let ((x 42)) x))
(test-equal "let empty body" #t (let ((x 1))))
(test-equal "let nested" 6 (let ((x 1)) (let ((y 2)) (let ((z 3)) (+ x y z)))))

(display "-- let named (recursive)\n")

(define let-fact
  (let fact ((n 5) (acc 1))
    (if (= n 0) acc (fact (- n 1) (* acc n)))))
(test-equal "let named factorial" 120 let-fact)

(define let-rev-list
  (let rev ((lst '(1 2 3 4)) (acc '()))
    (if (null? lst) acc (rev (cdr lst) (cons (car lst) acc)))))
(test-equal "let named reverse" '(4 3 2 1) let-rev-list)

(test-equal "let named sum" 15
  (let sum ((i 5) (s 0))
    (if (= i 0) s (sum (- i 1) (+ s i)))))

(display "-- let*\n")

(test-equal "let* sequential" 6 (let* ((x 3) (y (* x 2))) y))
(test-equal "let* chain" 10 (let* ((a 1) (b (+ a 2)) (c (+ b 3)) (d (+ c 4))) d))
(test-equal "let* empty" 42 (let* () 42))
(test-equal "let* single" 5 (let* ((x 5)) x))

(display "-- letrec\n")

(test-equal "letrec factorial" 120
  (letrec ((f (lambda (n) (if (< n 2) 1 (* n (f (- n 1)))))))
    (f 5)))
(test-equal "letrec even/odd" #t
  (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
           (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
    (even? 4)))
(test-equal "letrec even/odd false" #f
  (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
           (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
    (odd? 4)))
(test-equal "letrec mutual" 15
  (letrec ((f (lambda (n) (if (= n 0) 0 (+ n (g (- n 1))))))
           (g (lambda (n) (if (= n 0) 0 (+ n (f (- n 1)))))))
    (f 5)))
(test-equal "letrec single" 10 (letrec ((x 10)) x))

;; ════════════════════════════════════════════════════════════════
;; 2. and / or
;; ════════════════════════════════════════════════════════════════

(display "-- and\n")

(test-equal "and empty" #t (and))
(test-equal "and single true" 42 (and 42))
(test-equal "and single false" #f (and #f))
(test-equal "and two true" 3 (and 1 3))
(test-equal "and short circuit" #f (and #f (error "should not eval")))
(test-equal "and 3 true" 5 (and 1 2 5))
(test-equal "and all true return last" 99 (and #t 42 99))
(test-equal "and middle false" #f (and 1 #f 3))
(test-equal "and comparison" #t (and (> 3 2) (< 1 5)))

(display "-- or\n")

(test-equal "or empty" #f (or))
(test-equal "or single false" #f (or #f))
(test-equal "or single true" 42 (or 42))
(test-equal "or short circuit" 42 (or #f 42))
(test-equal "or first true" 1 (or 1 2 3))
(test-equal "or multiple false" 42 (or #f #f 42))
(test-equal "or all false" #f (or #f #f #f))
(test-equal "or early true" 'yes (or #f #f 'yes 'no))
(test-equal "or comparison" #t (or #f (> 3 2)))

;; test-and/or with side effects (temp var trick for or)
(display "-- and/or side-effect\n")
(define and-or-seen '())
(define (and-or-track x) (set! and-or-seen (cons x and-or-seen)) x)

;; and short-circuits on #f
(set! and-or-seen '())
(test-equal "and side effect short circuit" #f (and #f (and-or-track 'a)))
(test-equal "and side effect not evaluated" '() and-or-seen)

;; or temp var: each test evaluated at most once
(set! and-or-seen '())
(test-equal "or side effect" 42 (or (and-or-track 42)))
(test-equal "or captured" '(42) and-or-seen)

;; or stops after first truthy
(set! and-or-seen '())
(test-equal "or side effect short circuit" 42 (or (and-or-track 42) (and-or-track 'bad)))
(test-equal "or short circuit captured" '(42) and-or-seen)

;; ════════════════════════════════════════════════════════════════
;; 3. when / unless
;; ════════════════════════════════════════════════════════════════

(display "-- when\n")

(test-equal "when true" 42 (when #t 42))
(test-equal "when false" #f (when #f 42))
(test-equal "when multi" 3 (when #t (define when-a 1) (define when-b 2) (+ when-a when-b)))
(test-equal "when condition" 5 (when (< 1 5) 5))

(display "-- unless\n")

(test-equal "unless false" 42 (unless #f 42))
(test-equal "unless true" #f (unless #t 42))
(test-equal "unless multi" 3 (unless #f (define unless-a 1) (define unless-b 2) (+ unless-a unless-b)))
(test-equal "unless condition" 5 (unless (> 1 5) 5))

;; ════════════════════════════════════════════════════════════════
;; 4. cond
;; ════════════════════════════════════════════════════════════════

(display "-- cond basic\n")

(test-equal "cond first match" 'second
  (cond ((> 5 10) 'first) ((< 5 10) 'second)))
(test-equal "cond else" 'other
  (cond ((> 5 10) 'first) (else 'other)))
(test-equal "cond multi body" 4
  (cond ((> 5 10) 1 2) ((< 5 10) 3 4) (else 0)))

(display "-- cond => arrow\n")

(test-equal "cond => true" '(3 4)
  (cond ((memq 2 '(1 2 3 4)) => cdr) (else #f)))
(test-equal "cond => false" 'no
  (cond ((memq 5 '(1 2 3 4)) => cdr) (else 'no)))
(test-equal "cond => number?" #t
  (cond ((+ 1 2) => number?) (else #f)))

(display "-- cond test-only\n")

(test-equal "cond test-only true" 3
  (cond (3) ((> 5 10) 'nope)))
(test-equal "cond test-only false" 42
  (cond (#f 'nope) (42)))

(display "-- cond mixed\n")

(test-equal "cond mixed" 'big
  (cond ((> 3 5) 'small) ((< 3 5) 'big) (else 'unknown)))
(test-equal "cond single clause" 42 (cond (42)))
(test-equal "cond else only" 99 (cond (else 99)))
(test-equal "cond false else" 100 (cond (#f 1) (else 100)))

(display "-- cond edge\n")

(test-equal "cond arrow complex" 'b
  (cond ((assq 'b '((a 1) (b 2) (c 3))) => car) (else #f)))
(test-equal "cond test-only skip #f" 100
  (cond (#f) (#f) (100)))

;; ════════════════════════════════════════════════════════════════
;; 5. case
;; ════════════════════════════════════════════════════════════════

(display "-- case\n")

(test-equal "case single key" 'one (case 1 ((1) 'one) ((2) 'two)))
(test-equal "case multi key" 'small (case 2 ((1 2 3) 'small) ((4 5) 'big)))
(test-equal "case else" 'other (case 5 ((1) 'one) (else 'other)))
(test-equal "case symbols" 'b (case 'b ((a) 'a) ((b) 'b) ((c) 'c)))
(test-equal "case numbers" 2 (case 3 ((1) 1) ((3 5 7) 2) (else 0)))

(display "-- case-edge\n")

(test-equal "case single key match" 'yes (case 'x ((x) 'yes) (else 'no)))
(test-equal "case else only" 'always (case 42 (else 'always)))
(test-equal "case 3rd branch" 'c (case 'c ((a) 'a) ((b) 'b) ((c) 'c) ((d) 'd)))
(test-equal "case multi body" '(2 3)
  (case 'x ((x) (list 2 3)) (else #f)))

;; ════════════════════════════════════════════════════════════════
;; 6. do
;; ════════════════════════════════════════════════════════════════

(display "-- do\n")

(test-equal "do simple count" 'done
  (do ((i 0 (+ i 1))) ((= i 5) 'done)))
(test-equal "do accumulate" 10
  (do ((i 0 (+ i 1)) (sum 0 (+ sum i))) ((> i 4) sum)))
(test-equal "do command" 15
  (let ((acc 0))
    (do ((i 1 (+ i 1))) ((> i 5) acc)
      (set! acc (+ acc i)))))
(test-equal "do with step" 11
  (do ((i 1 (+ i 2))) ((> i 9) i)))
(test-equal "do no step" 40
  (let ((x 5))
    (do ((i 0 (+ i 1))) ((= i 3) x)
      (set! x (* x 2)))))

(display "-- do edge\n")

(display "-- do single var\n")
(test-equal "do single var" 10
  (do ((x 0 (+ x 1))) ((= x 10) x)))

(display "-- do empty body\n")
(test-equal "do empty body" 5
  (do ((i 0 (+ i 1))) ((= i 5) 5)))

;; ════════════════════════════════════════════════════════════════
;; 7. define-values
;; ════════════════════════════════════════════════════════════════

(display "-- define-values\n")

(let ((dv-sym (gensym)))
  (eval `(begin
           (define-values (,(gensym) ,(gensym)) (values 10 20))
           (define-values (,(gensym)) (values 1 2 3))
           (define-values () (values)))))
;; Note: define-values modifies global env, hard to test cleanly in shared env.
;; Instead test the pattern via let-values equivalent:

(test-equal "define-values pattern" 30
  (call-with-values (lambda () (values 10 20))
    (lambda (a b) (+ a b))))

(display "-- define-values named\n")

(define-values (dv-a dv-b dv-c) (values 1 2 3))
(test-equal "define-values a" 1 dv-a)
(test-equal "define-values b" 2 dv-b)
(test-equal "define-values c" 3 dv-c)

(define-values (dv-x dv-y) (values 10 20))
(test-equal "define-values x" 10 dv-x)
(test-equal "define-values y" 20 dv-y)

;; ════════════════════════════════════════════════════════════════
;; 8. delay / force
;; ════════════════════════════════════════════════════════════════

(display "-- delay\n")

(define dl-promise (delay (+ 1 2)))
(test-equal "delay promise?" #t (promise? dl-promise))
(test-equal "delay force" 3 (force dl-promise))
(test-equal "delay force again (cached)" 3 (force dl-promise))

(display "-- delay side-effect once\n")

(define dl-counter 0)
(define dl-once (delay (begin (set! dl-counter (+ dl-counter 1)) 42)))
(test-equal "delay before force" 0 dl-counter)
(test-equal "delay force 1" 42 (force dl-once))
(test-equal "delay side effect 1" 1 dl-counter)
(test-equal "delay force 2 (cached)" 42 (force dl-once))
(test-equal "delay side effect still 1" 1 dl-counter)

(test-equal "delay empty (lazy string)" "hello world"
  (force (delay (string-append "hello" " world"))))

;; ════════════════════════════════════════════════════════════════
;; 9. let-values / let*-values
;; ════════════════════════════════════════════════════════════════

(display "-- let-values\n")

(test-equal "let-values single" 3
  (let-values (((a b) (values 1 2))) (+ a b)))
(test-equal "let-values double" 6
  (let-values (((a b) (values 1 2)) ((c) (values 3)))
    (+ a b c)))
(test-equal "let-values empty" 42
  (let-values () 42))
(test-equal "let-values single var" 42
  (let-values (((x) (values 42))) x))

(display "-- let*-values\n")

(test-equal "let*-values basic" 3
  (let*-values (((a b) (values 1 2)) ((c) (values (+ a b))))
    c))
(test-equal "let*-values sequential" 5
  (let*-values (((a b) (values 1 2)) ((c d) (values (+ a b) (* a b))))
    (+ c d)))
(test-equal "let*-values empty" 99
  (let*-values () 99))

;; ════════════════════════════════════════════════════════════════
;; 10. parameterize
;; ════════════════════════════════════════════════════════════════

(display "-- parameterize\n")

(define p-param (make-parameter 0))
(test-equal "parameterize initial" 0 (p-param))

(parameterize ((p-param 5))
  (test-equal "parameterize inside" 5 (p-param)))
(test-equal "parameterize restored" 0 (p-param))

(parameterize ((p-param 10))
  (parameterize ((p-param 20))
    (test-equal "parameterize nested inside" 20 (p-param)))
  (test-equal "parameterize nested restored" 10 (p-param)))
(test-equal "parameterize outer restored" 0 (p-param))

(test-equal "parameterize empty" 42
  (parameterize () 42))

(define p-str (make-parameter "default"))
(parameterize ((p-str "overridden"))
  (test-equal "parameterize string" "overridden" (p-str)))
(test-equal "parameterize string restored" "default" (p-str))

;; ════════════════════════════════════════════════════════════════
;; 11. guard
;; ════════════════════════════════════════════════════════════════

(display "-- guard\n")

(test-equal "guard no error" 42
  (guard (e (else 'error))
    (+ 1 41)))
(test-equal "guard catch error" 'caught
  (guard (e (else 'caught))
    (error "test error")))
(test-equal "guard with condition" 'io-error
  (guard (e ((error-object? e) 'io-error) (else 'other))
    (error "I/O error")))
(test-equal "guard body result" 99
  (guard (e (else -1))
    99))

;; ════════════════════════════════════════════════════════════════
;; 12. define-record-type
;; ════════════════════════════════════════════════════════════════

(display "-- define-record-type\n")

(define-record-type pare (kons x y) pare?
  (kar kar)
  (kdr kdr))

(test-equal "record constructor" #t (pare? (kons 1 2)))
(test-equal "record type predicate true" #t (pare? (kons 'a 'b)))
(test-equal "record type predicate false" #f (pare? '(not a pare)))
(test-equal "record accessor kar" 10 (kar (kons 10 20)))
(test-equal "record accessor kdr" 30 (kdr (kons 20 30)))
(test-equal "record accessor mixed" 'a (kar (kons 'a 'b)))
(test-equal "record accessor second" 'b (kdr (kons 'a 'b)))

(define-record-type point (make-point x y) point?
  (x x)
  (y y))

(define pt (make-point 3 4))
(test-equal "point x" 3 (x pt))
(test-equal "point y" 4 (y pt))
(test-equal "point predicate" #t (point? pt))
(test-equal "point predicate fail" #f (point? (kons 'a 'b)))

(define-record-type book (make-book title author) book?
  (title title)
  (author author))

(define b (make-book "CLRS" "Cormen"))
(test-equal "book title" "CLRS" (title b))
(test-equal "book author" "Cormen" (author b))

;; ════════════════════════════════════════════════════════════════
;; 13. cut / cute
;; ════════════════════════════════════════════════════════════════

(display "-- cut\n")

(define cut-add5 (cut + 5 <>))
(test-equal "cut add5" 8 (cut-add5 3))

(define cut-add (cut + <> <>))
(test-equal "cut add" 10 (cut-add 3 7))

(define cut-mul3 (cut * 3 4 5))
(test-equal "cut fixed" 60 (cut-mul3))

(define cut-cons (cut cons <> <>))
(test-equal "cut cons" '(a . b) (cut-cons 'a 'b))

(define cut-max-lst (cut max <...>))
(test-equal "cut rest" 9 (cut-max-lst 1 9 3 5 2))

(define cut-list (cut list <> <...>))
(test-equal "cut mix" '(1 2 3 4) (cut-list 1 2 3 4))

(define cut-map (cut map <> '()))
(test-equal "cut mapped" '() (cut-map car))

(test-equal "cut inline" 7 ((cut + 3 <>) 4))

(display "-- cute\n")

(define cute-add5 (cute + 5 <>))
(test-equal "cute add5" 8 (cute-add5 3))

(define cute-add (cute + <> <>))
(test-equal "cute add" 10 (cute-add 3 7))

(define cute-mul3 (cute * 3 4 5))
(test-equal "cute fixed" 60 (cute-mul3))

;; ════════════════════════════════════════════════════════════════
;; 14. include / cond-expand
;; ════════════════════════════════════════════════════════════════

(display "-- include\n")

(test-assert "include loads file"
  (begin
    (include "scm/boot-core.scm")
    #t))

(display "-- cond-expand\n")

(test-equal "cond-expand first branch" 'lib-loaded
  (cond-expand (srfi-1 'lib-loaded) (else 'not-found)))
(test-equal "cond-expand fallback" 'yes
  (cond-expand (nonexistent 'yes) (else 'not-found)))

;; ════════════════════════════════════════════════════════════════
;; 15. atom? / void?
;; ════════════════════════════════════════════════════════════════

(display "-- atom?\n")

(test-equal "atom? number" #t (atom? 42))
(test-equal "atom? string" #t (atom? "hello"))
(test-equal "atom? symbol" #t (atom? 'x))
(test-equal "atom? bool" #t (atom? #t))
(test-equal "atom? null" #t (atom? '()))
(test-equal "atom? pair" #f (atom? '(1 2)))
(test-equal "atom? nested pair" #f (atom? '(1 (2 3))))
(test-equal "atom? vector" #t (atom? '#(1 2)))
(test-equal "atom? function" #t (atom? (lambda (x) x)))

(display "-- void?\n")

(test-equal "void? of void" #t (void? (if #f 42)))
(test-equal "void? of value" #f (void? (if #t 42)))
(test-equal "void? of number" #f (void? 0))
(test-equal "void? of list" #f (void? '()))
(test-equal "void? of bool" #f (void? #f))
(test-equal "void? sentinel" #t (void? (void)))

;; ════════════════════════════════════════════════════════════════
;; 16. Integration: macros compose
;; ════════════════════════════════════════════════════════════════

(display "\n-- 宏组合测试\n")

(test-equal "do + let + cond" 10
  (do ((i 0 (+ i 1)) (acc 0 (let ((x i)) (+ acc x))))
      ((= i 5) acc)))

(test-equal "guard + do" 'done
  (guard (e (else 'error))
    (do ((i 0 (+ i 1))) ((= i 3) 'done))))

(test-equal "case + let" 'yes
  (let ((x 2))
    (case x ((1) 'no) ((2) 'yes) (else 'maybe))))

(define-record-type tree (make-tree val left right) tree?
  (val val)
  (left left)
  (right right))

(define t (make-tree 5 (make-tree 3 '() '()) (make-tree 8 '() '())))
(test-equal "tree val" 5 (val t))
(test-equal "tree left val" 3 (val (left t)))
(test-equal "tree right val" 8 (val (right t)))

;; ════════════════════════════════════════════════════════════════
;; 17. Edge cases
;; ════════════════════════════════════════════════════════════════

(display "\n-- 边界与副作用\n")

;; do with complex step - collect x,y at each iteration
(test-equal "do parallel step" '(1 0 2 1 4 2)
  (let ((acc '()))
    (do ((x 0 (+ x 1)) (y 1 (* y 2))) ((= x 3) (reverse acc))
      (set! acc (cons x (cons y acc))))))

;; letrec with self-reference
(test-equal "letrec self" 24
  (letrec ((f (lambda (n) (if (= n 0) 1 (* n (f (- n 1)))))))
    (f 4)))

;; delay in list
(test-equal "delay in list" '(1 2 3)
  (let* ((p (delay 3))
         (l (list 1 2 (force p))))
    l))

;; parameterize with exception
(test-equal "parameterize unwind on error" 0
  (let ((p (make-parameter 0)))
    (guard (e (else #f))
      (parameterize ((p 99))
        (error "boom")))
    (p)))

(display "\n=== boot-core.scm 测试完成 ===\n")
