;; test-boot-sugar-usage.scm — Comprehensive usage examples for all macros in boot-sugar.scm
;; Run: python3 miniscm.py test/test-boot-sugar-usage.scm

;; 覆盖版本 check：本文件用 2 参形式 (check actual expected)，boot-sugar 的
;; check 是 3 参函数 (check label actual expected)。宏版本同时支持两种形式，
;; 2 参时用实际表达式源码作为标签。
(define-syntax check
  (syntax-rules ()
    ((_ actual expected)
     (if (equal? actual expected)
         (begin (display "[PASS] ") (display 'actual) (newline))
         (begin (display "[FAIL] ") (display 'actual)
                (display "  expected: ") (write expected)
                (display "  actual: ") (write actual) (newline))))
    ((_ label actual expected)
     (if (equal? actual expected)
         (begin (display "[PASS] ") (display label) (newline))
         (begin (display "[FAIL] ") (display label)
                (display "  expected: ") (write expected)
                (display "  actual: ") (write actual) (newline))))))

(display "=== Phase 5 — User Macros ===\n\n")

(display "-- nth: positional access\n")
(check (nth 0 'a 'b 'c) 'a)
(check (nth 1 'a 'b 'c) 'b)
(check (nth 2 'a 'b 'c) 'c)
(check (nth 3 10 20 30 40) 40)

(display "-- if-not: inverted condition\n")
(check (if-not #t 'yes 'no) 'no)
(check (if-not #f 'yes 'no) 'yes)
(check (if-not (> 3 5) 'yes 'no) 'yes)

(display "-- stream-cons: lazy cons cell\n")
(define s1 (stream-cons 1 (list 2 3)))
(check (car s1) 1)
(check (force (cdr s1)) '(2 3))
(define s2 (stream-cons 'a '()))
(check (car s2) 'a)

(display "-- fluid-let: dynamic scoping\n")
(define fl-var 10)
(fluid-let ((fl-var 99)) (check fl-var 99))
(check fl-var 10)
(fluid-let ((fl-var 1) (fl-var 2)) (check fl-var 2))
(check fl-var 10)

(display "-- receive: multi-value binding\n")
(receive (a b) (values 1 2) (check (+ a b) 3))
(receive (x y z) (values 10 20 30) (check (* x y z) 6000))
(receive (n) (values 42) (check n 42))

(display "-- with-values: multi-value consumer\n")
(with-values (values 3 4) (lambda (a b) (check (* a b) 12)))
(with-values (values 5 6 7) (lambda (a b c) (check (+ a b c) 18)))
(with-values (values 42) (lambda (n) (check n 42)))
(with-values (values 1 2 3) (lambda (a b c) (check (+ a b c) 6)))

(display "-- assume: assertion with error\n")
(check (assume (= 1 1)) #t)

(display "-- and-let*: sequential AND with binding\n")
(check (and-let*) #t)
(check (and-let* () 42) 42)
(check (and-let* ((#t)) 'ok) 'ok)
(check (and-let* ((#f)) 'ok) #f)
(check (and-let* ((a 1) (b 2)) (+ a b)) 3)
(check (and-let* ((a #f) (b 2)) (+ a b)) #f)
(check (and-let* ((a 1) (b 2) (c 3)) (* a b c)) 6)

(display "-- rec: recursive lambda\n")
(define rec-fact (rec (fact n) (if (= n 0) 1 (* n (fact (- n 1))))))
(check (rec-fact 5) 120)
(check (rec-fact 0) 1)
(define rec-even (rec (even n) (if (= n 0) #t (rec-odd (- n 1)))))
(define rec-odd (rec (odd n) (if (= n 0) #f (rec-even (- n 1)))))
(check (rec-even 4) #t)
(check (rec-odd 4) #f)

(display "-- do-ec: imperative comprehension\n")
(define do-ec-sum 0)
(do-ec (set! do-ec-sum (+ do-ec-sum x)) (for x '(1 2 3 4 5)))
(check do-ec-sum 15)
(define do-ec-acc '())
(do-ec (set! do-ec-acc (cons (* x 2) do-ec-acc)) (for x '(1 2 3)))
(check (reverse do-ec-acc) '(2 4 6))

(display "-- list-ec: list comprehension\n")
(check (list-ec (* x 2) (for x '(1 2 3 4))) '(2 4 6 8))
(check (list-ec x (for x '(1 2 3 4 5)) (if (> x 2))) '(3 4 5))
(check (list-ec (+ x y) (for x '(1 2)) (for y '(10 20))) '(11 21 12 22))
(check (list-ec x (for x '())) '())

(display "-- sum-ec: sum comprehension\n")
(check (sum-ec x (for x '(1 2 3 4 5))) 15)
(check (sum-ec x (for x '(1 2 3 4 5)) (if (> x 2))) 12)
(check (sum-ec 42) 42)

(display "-- any?-ec / every?-ec: quantified comprehension\n")
(check (any?-ec (even? x) (for x '(1 3 5 7))) #f)
(check (any?-ec (even? x) (for x '(1 2 5 7))) #t)
(check (every?-ec (odd? x) (for x '(1 3 5 7))) #t)
(check (every?-ec (odd? x) (for x '(1 2 5 7))) #f)

(display "-- aif: anaphoric if (it binding)\n")
(check (aif (+ 1 2) (* it 2) 'nope) 6)
(check (aif #f 'then 'else) 'else)
(check (aif (memq 2 '(1 2 3 4)) (car it) 'nope) 2)

(display "-- aand: anaphoric and\n")
(check (aand) #t)
(check (aand 42) 42)
(check (aand 1 2 3) 3)
(check (aand 1 #f 3) #f)
(check (aand 1 2 (+ it 3)) 5)

(display "-- alet: anaphoric let\n")
(check (alet ((x 1) (y 2)) (+ x y)) 3)
(check (alet ((a 10)) (* a 2)) 20)
(check (alet ((x 3) (y 4) (z 5)) (* x y z)) 60)

(display "-- test-assert / test-equal: testing utilities\n")
(test-assert "positive" (positive? 5))
(test-assert "negative" (negative? -1))
(test-equal "add" (+ 2 3) 5)
(test-equal "mul" (* 3 4) 12)
(test-equal "concat" (string-append "a" "b") "ab")

(display "-- define-immutable: define without set!\n")
(define-immutable (im-add a b) (+ a b))
(check (im-add 3 4) 7)
(define-immutable (im-square x) (* x x))
(check (im-square 5) 25)
(define-immutable (im-fact n)
  (if (= n 0) 1 (* n (im-fact (- n 1)))))
(check (im-fact 5) 120)

(display "-- dbind: destructuring bind\n")
(dbind () 42 (check 'ok 'ok))
(dbind (a) 1 (check a 1))
(dbind (a b) '(10 20) (check (+ a b) 30))
(dbind (a b c) '(1 2 3) (check (* a b c) 6))
(dbind (a . b) '(1 2 3) (check a 1) (check b '(2 3)))

(display "\n=== Phase 6 — Syntax Sugar ===\n\n")

(display "-- λ: lambda shorthand\n")
(define λ-add (λ (a b) (+ a b)))
(check (λ-add 3 4) 7)
(check ((λ (x) (* x x)) 5) 25)
(check ((λ (x y z) (+ x y z)) 1 2 3) 6)

(display "-- inc / dec: increment/decrement\n")
(define inc-x 5) (inc inc-x)   (check inc-x 6)
(inc inc-x 3)                  (check inc-x 9)
(define dec-x 10) (dec dec-x)  (check dec-x 9)
(dec dec-x 4)                  (check dec-x 5)

(display "-- while: while loop\n")
(define w-i 0) (define w-acc '())
(while (< w-i 5)
  (set! w-acc (cons w-i w-acc))
  (set! w-i (+ w-i 1)))
(check (reverse w-acc) '(0 1 2 3 4))

(display "-- for: for-each iteration\n")
(define for-acc '())
(for x in '(a b c) (set! for-acc (cons x for-acc)))
(check (reverse for-acc) '(a b c))
(define for-sum 0)
(for n in '(1 2 3 4 5) (set! for-sum (+ for-sum n)))
(check for-sum 15)

(display "-- some->: conditional threading\n")
(check (some-> 5 (lambda (x) (* x 2))) 10)
(check (some-> #f (lambda (x) (* x 2))) #f)
(check (some-> 3 (lambda (x) (+ x 1)) (lambda (x) (* x 2))) 8)
(check (some-> (memq 3 '(1 2 3 4)) (λ (x) (cdr x)) car) 4)

(display "-- and=>: conditional apply\n")
(check (and=> 5 (lambda (x) (* x 2))) 10)
(check (and=> #f (lambda (x) (* x 2))) #f)
(check (and=> (memq 3 '(1 2 3)) cdr car) 4)
(check (and=> 2 (λ (x) (* x 3)) (λ (x) (+ x 1))) 7)

(display "-- swap!: value swap\n")
(define sw-a 1) (define sw-b 2)
(swap! sw-a sw-b)
(check sw-a 2) (check sw-b 1)

(display "-- assert: assertion\n")
(check (assert (= 1 1)) (if #f #f))
(check (assert (positive? 5)) (if #f #f))

(display "-- if-let / when-let: conditional binding\n")
(check (if-let (x 42) (* x 2)) 84)
(check (if-let (x #f) 'then 'else) 'else)
(check (if-let (x 10) (+ x 5) 'fallback) 15)
(check (when-let (x 3) (* x x)) 9)

(display "-- list-of: list comprehension\n")
(check (list-of (* x 2) for x in '(1 2 3 4)) '(2 4 6 8))
(check (list-of x for x in '(1 2 3 4 5) if (odd? x)) '(1 3 5))
(check (list-of (* n n) for n in '(1 2 3)) '(1 4 9))

(display "-- ensure: postcondition\n")
(check (ensure (+ 2 3) (lambda (x) (= x 5))) 5)
(check (ensure 42 (lambda (x) (positive? x))) 42)

(display "-- timeit: execution timing\n")
(check (timeit (* 2 3)) 6)
(check (timeit (string-append "a" "b")) "ab")

(display "\n=== Phase 7 — C# Style ===\n\n")

(display "-- ?? : null coalescing\n")
(check (?? 42 0) 42)
(check (?? #f 'default) 'default)
(check (?? (+ 1 2) 0) 3)

(display "-- ??= : null coalescing assign\n")
(define nc-a 42) (??= nc-a 99) (check nc-a 42)
(define nc-b #f) (??= nc-b 99) (check nc-b 99)

(display "-- match: pattern matching\n")
(check (match 1 (1 'one) (2 'two)) 'one)
(check (match 2 (1 'one) (2 'two)) 'two)
(check (match 3 (1 'one) (2 'two) (else 'other)) 'other)
(check (match 5 (1 'one) (2 'two) (else 'other)) 'other)
(check (match 1 (1 'one) (2 'two)) 'one)

(display "-- using: resource management\n")
(using (p (open-input-string "hello using"))
  (check (read-char p) #\h)
  (check (read-char p) #\e))

(display "-- repeat: repeat loop\n")
(define rpt-acc '())
(repeat 5 (set! rpt-acc (cons 'x rpt-acc)))
(check (length rpt-acc) 5)

(display "-- do-while: post-test loop\n")
(define dw-i 0) (define dw-acc '())
(do-while (set! dw-acc (cons dw-i dw-acc))
          (set! dw-i (+ dw-i 1))
          (< dw-i 5))
(check (reverse dw-acc) '(0 1 2 3 4))

(display "-- range: numeric range\n")
(check (range 0 5) '(0 1 2 3 4))
(check (range 2 7) '(2 3 4 5 6))
(check (range 0 10 3) '(0 3 6 9))

(display "-- nameof: symbol capture\n")
(check (nameof x) 'x)
(check (nameof my-var) 'my-var)
(check (nameof +) '+)

(display "-- cond?: ternary\n")
(check (cond? #t 'yes 'no) 'yes)
(check (cond? #f 'yes 'no) 'no)

(display "-- try-finally: cleanup guarantee\n")
(define tf-flag #f)
(try-finally (set! tf-flag 'body) (set! tf-flag 'cleanup))
(check tf-flag 'cleanup)

(display "-- try-catch: exception handling\n")
(define tc-result #f)
(try-catch (error "test error") (exn (set! tc-result 'caught)))
(check tc-result 'caught)
(define tc-ok 'ok)
(try-catch (+ 1 2) (exn (set! tc-ok 'error)))
(check tc-ok 'ok)

(display "\n=== Phase 8 — D Style ===\n\n")

(display "-- scope-exit: scope exit guard\n")
(define se-flag 'start)
(scope-exit (set! se-flag 'exited) (set! se-flag 'body-run))
(check se-flag 'exited)

(display "-- scope-success: success guard\n")
(define ss-flag 'start)
(scope-success (set! ss-flag 'success) (set! ss-flag 'body))
(check ss-flag 'success)

(display "-- countdown: reverse iteration\n")
(define cd-acc '())
(countdown i 0 5 (set! cd-acc (cons i cd-acc)))
(check cd-acc '(0 1 2 3 4))

(display "-- times: indexed repeat\n")
(define tm-acc '())
(times 4 (set! tm-acc (cons i tm-acc)))
(check (length tm-acc) 4)
(check tm-acc '(3 2 1 0))

(display "-- with: method chain\n")
(define wth-pair (cons 1 2))
(with wth-pair (set-car! 10) (set-cdr! 20))
(check (car wth-pair) 10) (check (cdr wth-pair) 20)

(display "-- static-if: static conditional\n")
(check (static-if #t 'yes 'no) 'yes)
(check (static-if #f 'yes 'no) 'no)

(display "-- tap: value passthrough\n")
(define tp-acc '())
(define tp-result (tap 42 (lambda (x) (set! tp-acc (cons x tp-acc)))))
(check tp-result 42)

(display "-- lazy: delayed evaluation\n")
(define lz-val (lazy (+ 1 2)))
(check (promise? lz-val) #t)
(check (force lz-val) 3)
(define lz-hello (lazy (string-append "hello" " world")))
(check (force lz-hello) "hello world")

(display "-- memo: memoization\n")
(memo (memo-fn) 42)
(check (memo-fn) 42)
(check (memo-fn) 42)

(display "-- once: one-time execution\n")
(define once-cnt 0)
(define once-fn (once (set! once-cnt (+ once-cnt 1))))
(once-fn) (check once-cnt 1)
(once-fn) (check once-cnt 1)

(display "-- either: ternary\n")
(check (either #t 'yes 'no) 'yes)
(check (either #f 'yes 'no) 'no)

(display "-- tuple: multi-value shorthand\n")
(receive (a b) (tuple 10 20) (check (+ a b) 30))
(receive (x y z) (tuple 1 2 3) (check (* x y z) 6))

(display "-- str-join: string append\n")
(check (str-join "hello" " " "world") "hello world")
(check (str-join "a" "b" "c") "abc")
(check (str-join "x=" 42) "x=42")

(display "-- enumerate: indexed iteration\n")
(define en-acc '())
(enumerate (i v '(a b c)) (set! en-acc (cons (list i v) en-acc)))
(check (reverse en-acc) '((0 a) (1 b) (2 c)))

(display "\n=== Phase 9 — Polyglot Sugar ===\n\n")

(display "-- $ : apply operator\n")
(check ($ + 1 2) 3)
(check ($ * 2 3 4) 24)
(check ($ string-append "a" "b") "ab")

(display "-- o: function composition\n")
(define o-fn (o (lambda (x) (* x 2)) (lambda (x) (+ x 1))))
(check (o-fn 5) 12)
(define o-fn2 (o (lambda (x) (list x))
                 (lambda (x) (* x 2))
                 (lambda (x) (+ x 1))))
(check (o-fn2 3) '(8))
(check ((o car cdr) '((1 2) 3)) 3)

(display "-- const: constant function\n")
(define const-5 (const 5))
(check (const-5) 5)
(check (const-5 1 2 3) 5)

(display "-- identity: identity function\n")
(check (identity 42) 42)
(check (identity '(a b c)) '(a b c))

(display "-- cond->: conditional threading\n")
(check (cond-> 5
         (#t (lambda (x) (+ x 1)))
         (#f (lambda (x) (* x 2)))) 6)
(check (cond-> 0 (#t (lambda (x) (+ x 5))) (#t (lambda (x) (* x 2)))) 10)

(display "-- as->: named threading\n")
(check (as-> 5 x (+ x 1) (* x 2)) 12)
(check (as-> "hello" s (string-length s) (+ s 1)) 6)
(check (as-> 10 n (* n 3) (/ n 2)) 15)

(display "-- juxt: parallel application\n")
(define j-fn (juxt (λ (x) (* x 2)) (λ (x) (+ x 1))))
(check (j-fn 5) '(10 6))
(define j-fn2 (juxt car cdr))
(check (j-fn2 '(1 2 3)) '(1 (2 3)))

(display "-- let-it: Kotlin let\n")
(check (let-it '(1 2 3) (car it)) 1)
(check (let-it "hello" (string-length it)) 5)
(check (let-it 42 (+ it 8)) 50)

(display "-- also: Kotlin also\n")
(define als-acc '())
(define als-result (also 42 (set! als-acc (cons 1 als-acc))))
(check als-result 42)

(display "-- run: Kotlin run\n")
(check (run 5 (* it 2)) 10)
(check (run "abc" (string-length it)) 3)

(display "-- unwrap / expect: Rust unwrap\n")
(check (unwrap 42) 42)
(check (expect 42 "not nil") 42)

(display "-- with-chain: Elixir with\n")
(check (with-chain (x 42) do (* x 2)) 84)
(check (with-chain (x #f) do 'body else 'fallback) 'fallback)

(display "-- all? / any?: Python all/any\n")
(check (all? positive? '(1 2 3 4)) #t)
(check (all? positive? '(1 -2 3)) #f)
(check (any? even? '(1 3 5 7)) #f)
(check (any? even? '(1 2 3 5)) #t)

(display "-- comment: CL comment block\n")
(check (comment this is ignored) (if #f #f))

(display "-- prog1 / prog2: CL progn\n")
(check (prog1 1 2 3) 1)
(check (prog1 (+ 1 2)) 3)
(check (prog2 1 2 3) 2)
(check (prog2 'a 'b 'c) 'b)

(display "-- value->: value threading\n")
(define v-double (lambda (x) (* x 2)))
(define v-add1 (lambda (x) (+ x 1)))
(check (value-> 5 (v-double)) 10)
(check (value-> 3 (v-add1) (v-double)) 8)
(check (value-> '(1 2 3) car) 1)

(display "-- nlet: named let (tail-recursive)\n")
(check (nlet loop ((i 5) (acc 1))
         (if (= i 0) acc (loop (- i 1) (* acc i)))) 120)
(check (nlet sum ((n 3) (s 0))
         (if (< n 0) s (sum (- n 1) (+ s n)))) 6)
(check (nlet rev ((lst '(1 2 3)) (acc '()))
         (if (null? lst) acc (rev (cdr lst) (cons (car lst) acc)))) '(3 2 1))

(display "-- let1: single-value let\n")
(check (let1 x 5 (* x 2)) 10)
(check (let1 s "hello" (string-length s)) 5)

(display "-- letr: recursive let\n")
(define letr-even
  (letr ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
         (odd? (lambda (n) (if (= n 0) #f (even? (- n 1))))))
    even?))
(check (letr-even 4) #t) (check (letr-even 5) #f)

(display "-- tf: ternary if\n")
(check (tf #t 'yes 'no) 'yes)
(check (tf #f 'yes 'no) 'no)

(display "-- true? / false?: boolean checks\n")
(check (true? #t) #t) (check (true? #f) #f) (check (true? 42) #f)
(check (false? #f) #t) (check (false? #t) #f) (check (false? 0) #f)

(newline) (display "=== boot-sugar.scm usage examples complete ===\n")
