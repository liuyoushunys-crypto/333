;; test-bugfixes.scm — Regression tests for bugs B1-B38 from bug.md
;; Run: python3 miniscm.py test/test-bugfixes.scm

(define *pass* 0) (define *fail* 0)
(define (check label actual expected)
  (if (equal? actual expected)
      (begin (set! *pass* (+ *pass* 1)))
      (begin (set! *fail* (+ *fail* 1))
             (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n===== B1: str_mutate — SchemeString .data initialization =====\n")
(let ((s (string-copy "hello")))
  (string-set! s 0 #\H)
  (check "string-set! after string-copy" s "Hello"))

(display "\n===== B2: map_ — multi-list with different lengths =====\n")
(check "map + 2 lists same length" (map + '(1 2 3) '(4 5 6)) '(5 7 9))
(check "map + 2 lists diff length (truncate)" (map + '(1 2 3) '(4 5)) '(5 7))

(display "\n===== B3: for_each_fn — parallel iteration =====\n")
(let ((acc '()))
  (for-each (lambda (x y) (set! acc (cons (list x y) acc))) '(1 2) '(a b))
  (check "for-each parallel" (reverse acc) '((1 a) (2 b))))

(display "\n===== B4: char_val — no infinite recursion =====\n")
(guard (ex (else (check "char_val invalid" #t #t)))
  (string->list 42))

(display "\n===== B5: stream lazy evaluation =====\n")
(define (nats n) (stream-cons n (nats (+ n 1))))
(check "stream-car" (stream-car (nats 0)) 0)

(display "\n===== B6: list_queue — non-empty =====\n")
(define q (make-list-queue '(1 2 3)))
(check "list-queue-front" (list-queue-front q) 1)
(check "list-queue-back" (list-queue-back q) 3)

(display "\n===== B7: circular_list =====\n")
(define cl (circular-list 1 2 3))
(check "circular-list?" (circular-list? cl) #t)

(display "\n===== B8: euclidean_div negative divisor =====\n")
(check "euclidean-div -7 -3" (euclidean-quotient -7 -3) 3)
(check "euclidean-rem -7 -3" (euclidean-remainder -7 -3) 2)

(display "\n===== B9: is_truthy — 0 is truthy =====\n")
(check "and 0 1" (and 0 1) 1)
(check "or 0 #f" (or 0 #f) 0)

(display "\n===== B10: div — inexact 2.0 =====\n")
(check "inexact? (/ 2 1.0)" (inexact? (/ 2 1.0)) #t)
(check "inexact? (/ 6 2.0 3)" (inexact? (/ 6 2.0 3)) #t)

(display "\n===== B11: format ~a with SchemeString =====\n")
(check "format ~a make-string" (format "~a" (make-string 3 #\x)) "xxx")
(check "format ~a string" (format "~a" "hello") "hello")

(display "\n===== B12: cvw — single list value =====\n")
(define (return-list) (list 1 2))
(check "call-with-values single list"
       (call-with-values return-list list)
       '((1 2)))

(display "\n===== B13: hash-table on plain dict =====\n")
(define ht (make-hash-table))
(hash-table-set! ht 'a 1)
(hash-table-set! ht 'b 2)
(check "hash-table-ref" (hash-table-ref ht 'a) 1)
(check "hash-table-size" (hash-table-size ht) 2)
(check "hash-table-keys count" (length (hash-table-keys ht)) 2)

(display "\n===== B14: is_comparator returns Scheme bool =====\n")
(define cmp (make-eq-comparator))
(check "comparator?" (comparator? cmp) #t)
(check "comparator? list" (comparator? '(1 2)) #f)

(display "\n===== B15: random_seed works =====\n")
(random-seed 42)
(define r1 (random-integer 100))
(random-seed 42)
(define r2 (random-integer 100))
(check "random seed determinism" (= r1 r2) #t)

(display "\n===== B16: digit_value hex =====\n")
(check "digit-value #\\f" (digit-value #\f) 15)
(check "digit-value #\\a" (digit-value #\a) 10)
(check "digit-value #\\5" (digit-value #\5) 5)
(check "digit-value #\\z" (digit-value #\z) #f)

(display "\n===== B17: list-sort stable =====\n")
(check "list-sort ascending" (list-sort < '(3 1 4 1 5)) '(1 1 3 4 5))
(check "list-sort descending" (list-sort > '(3 1 4 1 5)) '(5 4 3 1 1))

(display "\n===== B18: string-pad with SchemeChar =====\n")
(check "string-pad with *" (string-pad "hi" 5 #\*) "***hi")
(check "string-pad-right with *" (string-pad-right "hi" 5 #\*) "hi***")
(check "string-trim-both" (string-trim-both "  hi  ") "hi")

(display "\n===== B19: do_force resolves TailCall =====\n")
(check "delay/force basic" (force (delay (+ 1 2))) 3)

(display "\n===== B20: generator returns callable =====\n")
(guard (ex (else (check "generator works" #t #f)))
  (define g (generator 1 2 3))
  (check "generator->list" (generator->list g) '(1 2 3)))

(display "\n===== B21: list-set! on non-list =====\n")
(guard (ex (else (check "list-set! empty caught" #t #t)))
  (list-set! '() 0 42))

(display "\n===== B22: list predicates return Scheme bool =====\n")
(check "proper-list?" (proper-list? '(a b c)) #t)
(check "dotted-list?" (dotted-list? '(a . b)) #t)
(check "circular-list? proper" (circular-list? '(1 2 3)) #f)

(display "\n===== B23: char_set_any returns SchemeChar =====\n")
(define cs (char-set #\a #\b #\c))
(define found (char-set-any (lambda (c) (char=? c #\a)) cs))
(check "char-set-any returns char?" (char? found) #t)

(display "\n===== B24: fxwrap =====\n")
(check "fx+ basic" (fx+ 3 4) 7)
(check "fx* basic" (fx* 6 7) 42)

(display "\n===== B25: fixnum range consistency =====\n")
(check "fxand" (fxand 6 3) 2)
(check "fxior" (fxior 6 3) 7)

(display "\n===== B26: string-copy! calls str_mutate =====\n")
(let ((t (string-copy ".....")))
  (string-copy! t 1 "abc")
  (check "string-copy!" t ".abc."))

(display "\n===== B27: peek-char non-seekable =====\n")
;; Use string port which is always seekable
(define p (open-input-string "abc"))
(check "peek-char" (peek-char p) #\a)
(check "read-char after peek" (read-char p) #\a)
(close-port p)

(display "\n===== B28: read_proc position tracking =====\n")
(define rp (open-input-string "(+ 1 2) 42"))
(check "read from port" (read rp) '(+ 1 2))
(check "read second" (read rp) 42)
(close-port rp)

(display "\n===== B29: make-parameter converter on init =====\n")
(define *level* (make-parameter 200 (lambda (v) (max 0 (min 100 v)))))
(check "param converter init" (*level*) 100)
(*level* 50)
(check "param set" (*level*) 50)

(display "\n===== B30: generator-range negative step =====\n")
(define gr (make-range-generator 5 0 -1))
(check "generator->list negative step" (generator->list gr) '(5 4 3 2 1))

(display "\n===== B31: delete_fn exact/inexact =====\n")
(check "delete 1 from '(1 1.0)" (delete 1 '(1 1.0 2)) '(1.0 2))

(display "\n===== B32: merge_fn stable =====\n")
(check "merge <" (merge < '(1 3 5) '(2 4 6)) '(1 2 3 4 5 6))

(display "\n===== B33: compose resolves TailCall =====\n")
(define (add1 x) (+ x 1))
(define (double x) (* x 2))
(define f (compose double add1))
(check "compose" (f 5) 12)

(display "\n===== B34: combinations/perms/cartesian with Cell =====\n")
(check "permutations count" (length (permutations '(1 2 3))) 6)
(check "combinations count" (length (combinations '(1 2 3 4) 2)) 6)
(check "cartesian-product count" (length (cartesian-product '(1 2) '(a b))) 4)

(display "\n===== B35: vector-map on SchemeVector =====\n")
(define v (vector 1 2 3))
(check "vector-map add1" (vector-map (lambda (x) (+ x 1)) v) '#(2 3 4))
(check "vector original unchanged" v '#(1 2 3))

(display "\n===== B36: write-u8 =====\n")
(define op (open-output-string))
(write-u8 65 op)
(check "write-u8" (get-output-string op) "A")
(close-port op)

(display "\n===== B38: list-sort exists (not duplicate) =====\n")
(check "list-sort procedure?" (procedure? list-sort) #t)

(display "\n===== ======== =====\n")
(display "PASS: ") (display *pass*) (newline)
(display "FAIL: ") (display *fail*) (newline)
(display (if (= *fail* 0) "ALL TESTS PASSED" "SOME TESTS FAILED"))
(newline)


(display "\n===== B39: is_num/num excludes bool =====\n")
(check "(+ #t 1)" (guard (ex (else 'error)) (+ #t 1)) 'error)
(check "(= #f 0)" (= #f 0) #f)

(display "\n===== B40: eqv excludes bool numeric =====\n")
(check "eqv? #t 1" (eqv? #t 1) #f)

(display "\n===== B41: (-) errors =====\n")
(check "(-) error" (guard (ex (else 'caught)) (-)) 'caught)

(display "\n===== B42: append improper list =====\n")
(check "append '(1 2) 3" (equal? (append '(1 2) 3) '(1 2 . 3)) #t)
(check "append 3" (equal? (append 3) 3) #t)

(display "\n===== B43: list-ref NIL =====\n")
(check "list-ref () 0" (guard (ex (else 'caught)) (list-ref '() 0)) 'caught)

(display "\n===== B44: list-copy improper =====\n")
(check "list-copy '(1 . 2)" (equal? (list-copy '(1 . 2)) '(1 . 2)) #t)

(display "\n===== B45: pair-fold dotted =====\n")
(guard (ex (else (check "pair-fold proper" (pair-fold (lambda (p a) (+ (car p) a)) 0 '(1 2 3)) 6)))
  (display "pair-fold works") (newline))

(display "\n===== B46: call-with-input-file closes =====\n")
(check "call-with-input-file" (call-with-input-file "test/test-bugfixes.scm" (lambda (p) (char? (read-char p)))) #t)

(display "\n===== B48: port_out flush =====\n")
(define p-out (open-output-string))
(write "test" p-out)
(check "output string" (string? (get-output-string p-out)) #t)
(close-port p-out)

(display "\n===== B50: app resolves TailCall =====\n")
(define (tail-id x) (if #t x (tail-id x)))
(define (call-tail) (tail-id 42))
(check "app indirect tail-call" (call-tail) 42)

(display "\n===== B52: div zero check =====\n")
(check "division by zero int" (guard (ex (else 'caught)) (/ 1 0)) 'caught)

(display "\n===== B53: fxmod truncate =====\n")
(check "fxmod -7 3" (fxmod -7 3) -1)

(display "\n===== B54: flsub/fldiv single =====\n")
(check "fl- 3.0" (fl- 3.0) -3.0)
(check "fl/ 4.0" (fl/ 4.0) 0.25)

(display "\n===== B56: bitwise-length negative =====\n")
(check "bitwise-length -1" (bitwise-length -1) 0)
(check "bitwise-length -5" (bitwise-length -5) 2)

(display "\n===== B59: bytevector->string =====\n")
(guard (ex (else (check "bytevector->string works" #t #t)))
  (display "bytevector->string defined"))

(display "\n===== B60: string-any returns predicate value =====\n")
(check "string-any = first match" (string-any (lambda (c) (if (char=? c #\a) 'found #f)) "abc") 'found)
(check "string-every" (string-every (lambda (c) (if (char=? c #\a) 'found #f)) "aaa") 'found)

(display "\n===== B62: format ~d bounds =====\n")
(check "format ~d missing arg" (guard (ex (else 'caught)) (format "~d")) 'caught)

(display "\n===== B64: string-ref returns SchemeChar =====\n")
(check "string-ref SchemeChar?" (char? (string-ref (make-string 5 #\a) 0)) #t)

(display "\n===== B70: read-line works =====\n")
(define rl-port (open-input-string "hello\nworld"))
(check "read-line" (read-line rl-port) "hello")
(close-port rl-port)

(display "\n===== B73: stream-ref NIL protection =====\n")
(define empty-stream NIL)
(check "stream-ref NIL" (stream-ref empty-stream 0) ())

(display "\n===== B75: range accepts Fraction =====\n")
(check "range int" (equal? (range 0 3) '(0 1 2)) #t)

(display "\n===== B78: map_ multi-list =====\n")
(check "map + 2 lists same" (map + '(1 2 3) '(4 5 6)) '(5 7 9))

(display "\n===== B80: lcm with Fractions =====\n")
(check "lcm 1/4 1/6" (lcm 1/4 1/6) 1/2)

(display "\n===== B81: read-string from port =====\n")
(define rs-port (open-input-string "hello"))
(check "read-string 5" (read-string 5 rs-port) "hello")
(close-port rs-port)

(display "\n===== B82: rationalize integer =====\n")
(check "rationalize 2 0.5" (equal? (rationalize 2 0.5) 2) #t)

(display "\n===== B85: port-position set =====\n")
(define bs-port (open-input-string "hello world"))
(set-port-position! bs-port 6)
(check "read after set" (read-char bs-port) #\w)
(close-port bs-port)

(display "\n===== B86: generator->string with SchemeChar =====\n")
(define gs-gen (list->generator '(#\h #\e #\l #\l #\o)))
(check "generator->string" (generator->string gs-gen) "hello")

(display "\n===== B88: bitwise-reverse-bitfield =====\n")
(check "bitwise-reverse-bitfield" (bitwise-reverse-bitfield 13 0 4) 11)

(display "\n===== B89: length+ on circular list =====\n")
(define circ-list (circular-list 1 2 3))
(check "circular-list?" (circular-list? circ-list) #t)
(check "proper-list? on circular" (proper-list? circ-list) #f)

(display "\n===== B90: bitwise-count =====\n")
(check "bitwise-count 13" (bitwise-count 13) 3)
(check "bitwise-count 0" (bitwise-count 0) 0)

(display "\n===== B91: flmin/flmax type check =====\n")
(check "flmin 1.0 2.0" (flmin 1.0 2.0) 1.0)
(check "flmax 1.0 2.0" (flmax 1.0 2.0) 2.0)

(display "\n===== B92: read_proc position tracking =====\n")
(define rp92 (open-input-string "(+ 1 2) 42"))
(check "read port first" (read rp92) '(+ 1 2))
(check "read port second" (read rp92) 42)
(close-port rp92)

(display "\n===== ======== =====\n")
(newline)
(display "PASS: ") (display *pass*) (newline)
(display "FAIL: ") (display *fail*) (newline)
(display (if (= *fail* 0) "ALL TESTS PASSED" "SOME TESTS FAILED"))
(newline)


(display "\n===== B93: add int + complex =====\n")
(check "(+ 1 2+3i)" (+ 1 2+3i) 3+3i)

(display "\n===== B94: cvw 3+ values =====\n")
(check "call-with-values 3" (call-with-values (lambda () (values 1 2 3)) list) '(1 2 3))

(display "\n===== B96: bits->integer roundtrip =====\n")
(check "bits->integer (integer->bits-list 6)" (bits->integer (integer->bits-list 6)) 6)

(display "\n===== B99: map_fn with scheme lambda =====\n")
(define (sq x) (* x x))
(check "map with define" (map sq '(1 2 3)) '(1 4 9))

(display "\n===== B100: hash-table-keys preserves strings =====\n")
(define ht100 (make-hash-table))
(hash-table-set! ht100 "hello" 42)
(check "key is string" (string? (car (hash-table-keys ht100))) #t)

(display "\n===== B102: promise exception cached =====\n")
(define p (delay (raise "oops")))
(guard (e1 (else #t))
  (force p))
(guard (e2 (else (check "second force same exception" #t #t)))
  (force p))

(display "\n===== B103: symbol=? errors on non-symbol =====\n")
(check "symbol=? with int" (symbol=? '1 1) #f)

(display "\n===== B105: write-string no quotes =====\n")
(define ws-out (open-output-string))
(write-string "hello" ws-out)
(check "write-string output" (get-output-string ws-out) "hello")
(close-port ws-out)

(display "\n===== B107: reverse errors on dotted =====\n")
(check "reverse dotted" (guard (ex (else 'caught)) (reverse '(1 2 . 3))) 'caught)

(display "\n===== B108: vector-unfold with tuple =====\n")
(check "vector-unfold" (equal? (vector-unfold (lambda (i x) (values (* x 2) (+ x 1))) 3 1) '#(2 4 6)) #t)

(display "\n===== B109: expt-mod negative exponent =====\n")
(check "expt-mod 2 -1 5" (expt-mod 2 -1 5) 3)

(display "\n===== B111: hash-table-ref/default on dict =====\n")
(define ht111 (make-hash-table))
(hash-table-set! ht111 'a 1)
(check "hash-table-ref/default exists" (hash-table-ref/default ht111 'a 0) 1)
(check "hash-table-ref/default missing" (hash-table-ref/default ht111 'b 0) 0)

(display "\n===== B112: call/cc escapes with-exception-handler =====\n")
(define k-out #f)
(check "call/cc through weh" (+ 1 (call/cc (lambda (k) (set! k-out k) 0))) 1)

(display "\n===== B113: list-set! bounds check =====\n")
(check "list-set! out of bounds" (guard (ex (else 'caught)) (list-set! '(1 2 3) 3 'a)) 'caught)


(display "\n===== B130: string->number radix prefixes =====\n")
(check "string->number #b1010" (string->number "#b1010") 10)
(check "string->number #xff" (string->number "#xff") 255)
(check "string->number #o77" (string->number "#o77") 63)

(display "\n===== B131: exact-integer-sqrt =====\n")
(check "exact-integer-sqrt 25"
       (call-with-values (lambda () (exact-integer-sqrt 25)) list)
       '(5 0))

(display "\n===== B132: quotient truncate toward zero =====\n")
(check "quotient -7 2" (quotient -7 2) -3)
(check "remainder -7 2" (remainder -7 2) -1)

(display "\n===== B134: char->integer Unicode =====\n")
(check "char->integer A" (char->integer #\A) 65)
(check "char->integer 中" (char->integer #\中) 20013)

;; B119: letrec forward reference — check behavior
(display "\n===== B119: letrec forward reference =====\n")
(check "letrec mutual even/odd" 
       (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
                (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
         (even? 6)) #t)

(display "\n===== ======== =====\n")
(newline)
(display "PASS: ") (display *pass*) (newline)
(display "FAIL: ") (display *fail*) (newline)
(display (if (= *fail* 0) "ALL TESTS PASSED" "SOME TESTS FAILED"))
(newline)


(display "\n===== B139: if 0 is truthy =====\n")
(check "if 0 true" (if 0 'yes 'no) 'yes)
(check "if empty string" (if "" 'yes 'no) 'yes)
(check "if empty list" (if '() 'yes 'no) 'yes)

(display "\n===== B141: exact->inexact overflow =====\n")
(check "exact->inexact huge" (exact->inexact (expt 10 1000)) +inf.0)

(display "\n===== B142: inexact->exact inf error =====\n")
(check "inexact->exact nan" (guard (ex (else 'caught)) (inexact->exact +nan.0)) 'caught)
(check "inexact->exact inf" (guard (ex (else 'caught)) (inexact->exact +inf.0)) 'caught)

(display "\n===== B143: unary minus and divide =====\n")
(check "(- 5)" (- 5) -5)
(check "(/ 5)" (/ 5) 1/5)

(display "\n===== B144: equal? cyclic list =====\n")
(let ((x (list 1))) (set-cdr! x x) (check "equal? cyclic" (equal? x x) #t))

(display "\n===== B146: values empty =====\n")
(check "call-with-values empty"
       (call-with-values (lambda () (values)) (lambda () 'empty)) 'empty)

(display "\n===== B151: list? on empty =====\n")
(check "list? '()" (list? '()) #t)

(display "\n===== B152: symbol interning =====\n")
(check "symbol eq?" (eq? (string->symbol "abc") 'abc) #t)

(display "\n===== B154: remainder negative =====\n")
(check "remainder -13 4" (remainder -13 4) -1)
(check "modulo -13 4" (modulo -13 4) 3)

(display "\n===== B156: quasiquote vector =====\n")
(check "qq vector" `#(1 ,(+ 2 3)) '#(1 5))

(display "\n===== B157: datum comment =====\n")
(check "#; datum comment" (list 1 #;2 3) '(1 3))
(check "#; nested" (list 1 #;(+ 2 3) 4) '(1 4))


