;; ============================================================
;; 全面覆盖测试 — 涵盖所有 scheme_builtins_* 模块
;; 文件: test_all_builtins.scm
;; ============================================================
;; 执行: python scheme_runtime.py test_all_builtins.scm
;; ============================================================

(test-begin "scheme_builtins_base — 核心算术")
;; + - * / 及其推广
(test-equal "+ basic"        (+ 1 2 3) 6)
(test-equal "+ single"       (+ 5) 5)
(test-equal "+ none"         (+) 0)
(test-equal "- binary"       (- 10 3) 7)
(test-equal "- negate"       (- 5) -5)
(test-equal "* basic"        (* 2 3 4) 24)
(test-equal "* none"         (*) 1)
(test-equal "/ basic"        (/ 10 2) 5)
(test-equal "/ reciprocal"   (/ 4) 1/4)
;; 混合类型: int + fraction
(test-equal "+ frac/int"     (+ 1 1/2) 3/2)
(test-equal "* frac/int"     (* 2 1/3) 2/3)

;; 数值比较 = < > <= >=
(test-equal "= true"  (= 3 3 3) #t)
(test-equal "= false" (= 1 2) #f)
(test-equal "< true"  (< 1 2 3) #t)
(test-equal "> true"  (> 3 2 1) #t)
(test-equal "<= true" (<= 1 2 2) #t)
(test-equal ">= true" (>= 3 3 2) #t)
(test-equal "zero? true"  (zero? 0) #t)
(test-equal "positive?"   (positive? 5) #t)
(test-equal "negative?"   (negative? -3) #t)
(test-equal "odd?"  (odd? 7) #t)
(test-equal "even?" (even? 8) #t)

;; 数值函数
(test-equal "max" (max 3 7 2) 7)
(test-equal "min" (min 3 7 2) 2)
(test-equal "abs positive" (abs -5) 5)
(test-equal "quotient"     (quotient 10 3) 3)
(test-equal "remainder"    (remainder 10 3) 1)
(test-equal "modulo"       (modulo -10 3) 2)
(test-equal "gcd"  (gcd 12 18 24) 6)
(test-equal "lcm"  (lcm 4 6) 12)
(test-equal "numerator"   (numerator 6/8) 3)
(test-equal "denominator" (denominator 6/8) 4)

;; 取整
(test-equal "floor"   (floor 3.7) 3.0)
(test-equal "ceiling" (ceiling 3.2) 4.0)
(test-equal "truncate" (truncate -3.7) -3.0)
(test-equal "round"   (round 3.5) 4.0)

;; 三角函数
(test-equal "sin" (< (sin 0) 1e-10) #t)  ;; sin(0)=0
(test-equal "cos" (< (- (cos 0) 1) 1e-10) #t)  ;; cos(0)=1
(test-equal "sqrt exact" (sqrt 9) 3)
(test-equal "expt int"   (expt 2 10) 1024)
(test-equal "exp approx" (< (- (exp 0) 1) 1e-10) #t)  ;; exp(0)-1 ≈ 0

;; 类型谓词
(test-equal "number?"  (number? 42) #t)
(test-equal "complex?" (complex? 3+4i) #t)
(test-equal "real?"    (real? 3.14) #t)
(test-equal "rational?" (rational? 1/3) #t)
(test-equal "integer?" (integer? 5) #t)
(test-equal "exact?"   (exact? 1/2) #t)
(test-equal "inexact?" (inexact? 3.0) #t)

;; 转换
(test-equal "exact->inexact" (exact->inexact 3) 3.0)
(test-equal "inexact->exact" (inexact->exact 0.5) 1/2)
(test-equal "number->string" (number->string 255 16) "ff")
(test-equal "string->number" (string->number "1010" 2) 10)
(test-equal "make-rectangular" (make-rectangular 3 4) 3+4i)
(test-equal "real-part" (real-part 3+4i) 3.0)
(test-equal "imag-part" (imag-part 3+4i) 4.0)

(test-end "scheme_builtins_base — 核心算术")

;; ============================================================
(test-begin "scheme_builtins_base — 等价判断")

(test-equal "eq? same"   (eq? 'a 'a) #t)
(test-equal "eq? diff"   (eq? 'a 'b) #f)
(test-equal "eqv? num"   (eqv? 3 3) #t)
(test-equal "equal? list" (equal? '(1 2 3) '(1 2 3)) #t)
(test-equal "equal? vector" (equal? #(1 2) #(1 2)) #t)
(test-equal "equal? nested" (equal? '((a) b) '((a) b)) #t)

(test-end "scheme_builtins_base — 等价判断")

;; ============================================================
(test-begin "scheme_builtins_base — 布尔与 pair 操作")

(test-equal "boolean?" (boolean? #t) #t)
(test-equal "not"      (not #f) #t)
(test-equal "pair?"    (pair? '(1 . 2)) #t)
(test-equal "cons"     (cons 1 2) '(1 . 2))
(test-equal "car"      (car '(a b c)) 'a)
(test-equal "cdr"      (cdr '(a b c)) '(b c))
(test-equal "null?"    (null? '()) #t)
(test-equal "list?"    (list? '(1 2)) #t)
(test-equal "list"     (list 1 2 3) '(1 2 3))
(test-equal "length"   (length '(a b c)) 3)
(test-equal "append"   (append '(1 2) '(3 4)) '(1 2 3 4))
(test-equal "reverse"  (reverse '(1 2 3)) '(3 2 1))
(test-equal "list-tail" (list-tail '(a b c d) 2) '(c d))
(test-equal "list-ref"  (list-ref '(a b c) 1) 'b)
(test-equal "memq"      (memq 'b '(a b c)) '(b c))
(test-equal "memv"      (memv 3 '(1 2 3)) '(3))
(test-equal "assq"      (assq 'b '((a 1) (b 2))) '(b 2))
(test-equal "assv"      (assv 2 '((1 x) (2 y))) '(2 y))
(test-equal "set-car!"  (let ((p (list 1 2))) (set-car! p 99) p) '(99 2))
(test-equal "set-cdr!"  (let ((p (list 1 2))) (set-cdr! p (list 3)) p) '(1 3))

(test-end "scheme_builtins_base — 布尔与 pair 操作")

;; ============================================================
(test-begin "scheme_builtins_base — 符号")

(test-equal "symbol?"      (symbol? 'hello) #t)
(test-equal "symbol->string" (symbol->string 'abc) "abc")
(test-equal "string->symbol" (string->symbol "xyz") 'xyz)
(test-equal "symbol=?"     (symbol=? 'a 'a 'a) #t)

(test-end "scheme_builtins_base — 符号")

;; ============================================================
(test-begin "scheme_builtins_base — 字符")

(test-equal "char?"        (char? #\a) #t)
(test-equal "char->integer" (char->integer #\A) 65)
(test-equal "integer->char" (integer->char 65) #\A)
(test-equal "char=?"       (char=? #\a #\a) #t)
(test-equal "char<?"       (char<? #\a #\b) #t)
(test-equal "char-ci=?"    (char-ci=? #\a #\A) #t)
(test-equal "char-alphabetic?" (char-alphabetic? #\z) #t)
(test-equal "char-numeric?"    (char-numeric? #\5) #t)
(test-equal "char-whitespace?" (char-whitespace? #\space) #t)
(test-equal "char-upper-case?" (char-upper-case? #\A) #t)
(test-equal "char-lower-case?" (char-lower-case? #\a) #t)
(test-equal "char-upcase"     (char-upcase #\a) #\A)
(test-equal "char-downcase"   (char-downcase #\A) #\a)

(test-end "scheme_builtins_base — 字符")

;; ============================================================
(test-begin "scheme_builtins_base — 字符串")

(test-equal "string?"      (string? "hello") #t)
(test-equal "make-string"  (make-string 3 #\x) "xxx")
(test-equal "string"       (string #\a #\b #\c) "abc")
(test-equal "string-length" (string-length "abc") 3)
(test-equal "string-ref"   (string-ref "abc" 1) #\b)
(test-equal "string=?"     (string=? "abc" "abc") #t)
(test-equal "string<?"     (string<? "abc" "abd") #t)
(test-equal "string-ci=?"  (string-ci=? "Abc" "aBC") #t)
(test-equal "substring"    (substring "hello" 1 4) "ell")
(test-equal "string-append" (string-append "a" "b" "c") "abc")
(test-equal "string->list" (string->list "ab") '(#\a #\b))
(test-equal "list->string" (list->string '(#\h #\i)) "hi")
(test-equal "string-copy"  (string-copy "abc") "abc")
(test-equal "string-fill!" (let ((s (make-string 4 #\_))) (string-fill! s #\*) s) "****")
(test-equal "string-set!"  (let ((s "abc")) (string-set! s 1 #\x) s) "axc")

(test-end "scheme_builtins_base — 字符串")

;; ============================================================
(test-begin "scheme_builtins_base — 向量")

(test-equal "vector?"      (vector? #(1 2 3)) #t)
(test-equal "make-vector"  (make-vector 3 0) #(0 0 0))
(test-equal "vector"       (vector 10 20 30) #(10 20 30))
(test-equal "vector-length" (vector-length #(a b c)) 3)
(test-equal "vector-ref"   (vector-ref #(10 20) 1) 20)
(test-equal "vector-set!"  (let ((v (vector 0 0))) (vector-set! v 1 99) v) #(0 99))
(test-equal "vector->list" (vector->list #(1 2)) '(1 2))
(test-equal "list->vector" (list->vector '(3 4)) #(3 4))
(test-equal "vector-fill!" (let ((v (vector 1 2 3))) (vector-fill! v 0) v) #(0 0 0))

(test-end "scheme_builtins_base — 向量")

;; ============================================================
(test-begin "scheme_builtins_base — 高阶与流程")

;; procedure?
(test-equal "procedure?" (procedure? +) #t)

;; apply
(test-equal "apply" (apply + '(1 2 3)) 6)

;; for-each
(test-equal "for-each" (let ((acc '())) (for-each (lambda (x) (set! acc (cons x acc))) '(1 2 3)) (reverse acc))
  '(1 2 3))

;; map (来自 adv, 但在 base 中 eval 使用)
(test-equal "map basic" (map (lambda (x) (* x 2)) '(1 2 3)) '(2 4 6))

;; values / call-with-values
(test-equal "call-with-values" (call-with-values (lambda () (values 1 2)) (lambda (a b) (+ a b))) 3)

;; dynamic-wind
(test-equal "dynamic-wind" (dynamic-wind (lambda () 0) (lambda () 42) (lambda () 0)) 42)

;; eval
(define _test-env (interaction-environment))
(test-equal "eval" (eval '(+ 1 2 3)) 6)

;; load 测试: 创建临时文件
(with-output-to-file "_test_load.scm"
  (lambda () (display "(define loaded-val 99)") (newline)))
(test-equal "load" (begin (load "_test_load.scm") loaded-val) 99)

;; call/cc
(test-equal "call/cc" (call/cc (lambda (k) (k 42))) 42)

;; force / promise?
(define _pr (delay (+ 1 2)))
(test-equal "promise?" (promise? _pr) #t)
(test-equal "force"    (force _pr) 3)

;; error
(test-equal "error raises exception" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k 'error-caught)) (lambda () (error "test error"))))) 'error-caught)

(test-end "scheme_builtins_base — 高阶与流程")

;; ============================================================
(test-begin "scheme_builtins_base — I/O 端口")

(test-equal "eof-object?"  (eof-object? (eof-object)) #t)

;; 字符串端口
(define _ip (open-input-string "42 (* 2 3)"))
(test-equal "read from string" (read _ip) 42)
(test-equal "read again"       (read _ip) '(* 2 3))
(close-port _ip)

(define _op (open-output-string))
(display "hello" _op)
(test-equal "get-output-string" (get-output-string _op) "hello")
(close-port _op)

;; write / display
(define _op2 (open-output-string))
(write "hello" _op2)
(test-equal "write" (get-output-string _op2) "\"hello\"")
(close-port _op2)

(define _op3 (open-output-string))
(display "hello" _op3)
(test-equal "display" (get-output-string _op3) "hello")
(close-port _op3)

;; format
(test-equal "format" (format "~a ~a" 'hello 'world) "hello world")
(test-equal "format ~s" "\"hi\"" (format "~s" "hi"))

;; ->string
(test-equal "->string" (->string 42) "42")

(test-end "scheme_builtins_base — I/O 端口")

;; ============================================================
(test-begin "scheme_builtins_adv — 列表操作（SRFI-1）")

;; any / every
(test-equal "any"    (any even? '(1 2 3)) #t)
(test-equal "every"  (every positive? '(1 2 3)) #t)

;; filter / remove
(test-equal "filter"  (filter even? '(1 2 3 4)) '(2 4))
(test-equal "remove"  (remove even? '(1 2 3 4)) '(1 3))

;; find
(test-equal "find"   (find even? '(1 3 5 6 7)) 6)

;; fold-left / fold-right
(test-equal "fold-left"  (fold-left + 0 '(1 2 3 4)) 10)
(test-equal "fold-right" (fold-right cons '() '(1 2 3)) '(1 2 3))

;; partition (returns multiple values)
(test-equal "partition yes" (call-with-values (lambda () (partition even? '(1 2 3 4 5 6))) (lambda (yes no) (length yes))) 3)

;; take / drop
(test-equal "take" (take '(a b c d) 3) '(a b c))
(test-equal "drop" (drop '(a b c d) 2) '(c d))

;; take-right / drop-right
(test-equal "take-right" (take-right '(a b c) 2) '(b c))
(test-equal "drop-right" (drop-right '(a b c) 1) '(a b))

;; take-while / drop-while
(test-equal "take-while" (take-while even? '(2 4 5 6)) '(2 4))
(test-equal "drop-while" (drop-while even? '(2 4 5 6)) '(5 6))

;; span / break (return multiple values)
(test-equal "span prefix" (call-with-values (lambda () (span even? '(2 4 5 6))) (lambda (pre suf) (length pre))) 2)
(test-equal "break prefix" (call-with-values (lambda () (break odd? '(2 4 5 6))) (lambda (pre suf) (length pre))) 2)

;; concatenate / append-map / flat-map
(test-equal "concatenate" (concatenate '((1 2) (3) (4 5))) '(1 2 3 4 5))
(test-equal "append-map"  (append-map (lambda (x) (list x (- x))) '(1 2)) '(1 -1 2 -2))
(test-equal "flat-map"    (flat-map (lambda (x) (list x (* x x))) '(2 3)) '(2 4 3 9))

;; count
(test-equal "count" (count even? '(1 2 3 4 5)) 2)

;; first..tenth
(test-equal "first"  (first '(a b c)) 'a)
(test-equal "second" (second '(a b c)) 'b)
(test-equal "third"  (third '(a b c)) 'c)
(test-equal "last"   (last '(a b c)) 'c)

;; member / assoc
(test-equal "member" (member 3 '(1 2 3 4)) '(3 4))
(test-equal "assoc"  (assoc 'b '((a 1) (b 2))) '(b 2))

;; list-index / list-tabulate
(test-equal "list-index"    (list-index even? '(1 3 4 5)) 2)
(test-equal "list-tabulate" (list-tabulate 3 (lambda (i) (* i i))) '(0 1 4))

;; unfold
(test-equal "unfold" (unfold (lambda (x) (> x 5))
                              (lambda (x) (* x x))
                              (lambda (x) (+ x 1))
                              0)
  '(0 1 4 9 16 25))

;; split-at (returns multiple values)
(test-equal "split-at length" (call-with-values (lambda () (split-at '(a b c d e) 3)) (lambda (h t) (length h))) 3)

;; zip
(test-equal "zip" (zip '(1 2) '(a b)) '((1 a) (2 b)))

;; cons* / list*
(test-equal "cons*" (cons* 1 2 3 4) '(1 2 3 . 4))
(test-equal "list*" (list* 1 2 '(3 4)) '(1 2 3 4))

;; circular-list? / dotted-list? / proper-list?
(test-equal "proper-list?"  (proper-list? '(1 2 3)) #t)
(test-equal "dotted-list?"  (dotted-list? '(1 2 . 3)) #t)

;; delete-duplicates
(test-equal "delete-duplicates" (delete-duplicates '(1 2 1 3 2)) '(1 2 3))

;; alist-copy
(test-equal "alist-copy" (alist-copy '((a 1) (b 2))) '((a 1) (b 2)))

;; reduce
(test-equal "reduce" (reduce + 0 '(1 2 3 4)) 10)

;; iota
(test-equal "iota" (iota 5 0 2) '(0 2 4 6 8))

;; xcons
(test-equal "xcons" (xcons '(b c) 'a) '(a b c))

;; but-last
(test-equal "but-last" (but-last '(a b c d)) '(a b c))

;; pair-fold / pair-fold-right
(test-equal "pair-fold" (pair-fold (lambda (p acc) (cons (car p) acc)) '() '(1 2 3)) '(3 2 1))

;; unzip1~5
(define _uz1 (unzip1 '((1 a) (2 b) (3 c))))
(test-equal "unzip1" _uz1 '(1 2 3))

(test-end "scheme_builtins_adv — 列表操作")

;; ============================================================
(test-begin "scheme_builtins_adv — 位运算")

(test-equal "bitwise-and" (bitwise-and #b1100 #b1010) #b1000)
(test-equal "bitwise-or" (bitwise-or #b1100 #b1010) #b1110)
(test-equal "bitwise-xor" (bitwise-xor #b1100 #b1010) #b0110)
(test-equal "bitwise-not" (bitwise-not 0) -1)
(test-equal "arithmetic-shift" (arithmetic-shift 1 3) 8)
(test-equal "bit-count" (bit-count #b1011) 3)
(test-equal "bit-field" (bit-field #b110110 1 4) #b011)  ;; bits 1..3 → 011
(test-equal "bit-set?"  (bit-set? #b0100 2) #t)        ;; bit 2 is set
(test-equal "copy-bit"  (copy-bit #b0000 2 #t) #b0100)
(test-equal "bitwise-rotate" (bitwise-rotate #b1001 1 4) #b0011)
(test-equal "bitwise-reverse-bit-field" (bitwise-reverse-bit-field #b1101 0 4) #b1011)
(test-equal "bitwise-if" (bitwise-if #b1010 #b0011 #b1100) 6)  ;; mask MSB select n0, LSB n1

(test-end "scheme_builtins_adv — 位运算")

;; ============================================================
(test-begin "scheme_builtins_adv — bytevector")

(define _bv (bytevector 1 2 3 4))
(test-equal "bytevector?" (bytevector? _bv) #t)
(test-equal "bytevector-length" (bytevector-length _bv) 4)
(test-equal "bytevector-u8-ref" (bytevector-u8-ref _bv 2) 3)
(test-equal "make-bytevector" (bytevector-length (make-bytevector 5 255)) 5)
(define _bv2 (bytevector 10 20 30))
(bytevector-u8-set! _bv2 1 99)
(test-equal "bytevector-u8-set!" (bytevector-u8-ref _bv2 1) 99)
(test-equal "bytevector-append" (bytevector-append (bytevector 1 2) (bytevector 3 4)) (bytevector 1 2 3 4))
(test-equal "utf8->string" (utf8->string (string->utf8 "中文")) "中文")
(test-equal "string->utf8" (string->utf8 "a") (bytevector 97))

;; port-position / set-port-position!
(define _bv-port (open-input-bytevector (string->utf8 "hello")))
(test-equal "port-position" (port-position _bv-port) 0)
(read-u8 _bv-port)
(test-equal "port-position after read" (port-position _bv-port) 1)
(set-port-position! _bv-port 0)
(test-equal "read after reset" (read-u8 _bv-port) 104)  ;; ord('h')
(close-port _bv-port)

(test-end "scheme_builtins_adv — bytevector")

;; ============================================================
(test-begin "scheme_builtins_adv — 随机数 & 杂项")

;; random-integer / random-real
(test-equal "random-integer" (integer? (random-integer 100)) #t)
(test-equal "random-real"    (<= 0 (random-real) 1) #t)

;; features
(test-equal "features list?" (list? (features)) #t)

;; file operations
(define _test-tmp "_scheme_test.txt")
(test-equal "file-exists?" (file-exists? _test-tmp) #f)
(call-with-output-file _test-tmp (lambda (p) (display "data" p)))
(test-equal "file-exists? after create" (file-exists? _test-tmp) #t)
(delete-file _test-tmp)

;; exit / emergency-exit (无法直接测，跳过)

;; current-jiffy / current-second
(test-equal "current-second real?" (real? (current-second)) #t)

(test-end "scheme_builtins_adv — 随机数 & 杂项")

;; ============================================================
(test-begin "scheme_builtins_adv — 字符串扩展")

(test-equal "string-upcase"   (string-upcase "hello") "HELLO")
(test-equal "string-downcase" (string-downcase "HELLO") "hello")
(test-equal "string-foldcase" (string-foldcase "HELLO") "hello")
(test-equal "string-split"   (string-split "a b c" " ") '("a" "b" "c"))
(test-equal "string-join"    (string-join '("a" "b" "c") ",") "a,b,c")
(test-equal "string-contains" (string-contains "hello world" "world") 6)
(test-equal "string-prefix?"  (string-prefix? "he" "hello") #t)
(test-equal "string-suffix?"  (string-suffix? "lo" "hello") #t)
(test-equal "string-map"      (string-map char-upcase "abc") "ABC")
(test-equal "string-for-each" (let ((acc "")) (string-for-each (lambda (c) (set! acc (string-append acc (string c)))) "abc") acc) "abc")

;; 来自 base_ext 的额外字符串操作
(test-equal "string-reverse" (string-reverse "abc") "cba")
(test-equal "string-titlecase" (string-titlecase "hello world") "Hello World")
(test-equal "string-trim" (string-trim-both "  hello  ") "hello")
(test-equal "string-pad" (string-pad "hi" 5 #\*) "***hi")
(test-equal "string-index" (string-index "hello" (lambda (c) (char=? c #\e))) 1)
(test-equal "string-count" (string-count "hello" char-alphabetic?) 5)

(test-end "scheme_builtins_adv — 字符串扩展")

;; ============================================================
(test-begin "scheme_builtins_adv — 符号与语法")

(test-equal "gensym symbol?" (symbol? (gensym)) #t)
(test-equal "syntax?" (syntax? (datum->syntax #t 'x)) #t)
(test-equal "syntax->datum" (syntax->datum (datum->syntax #t 'abc)) 'abc)

(test-end "scheme_builtins_adv — 符号与语法")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 向量扩展")

(test-equal "vector-append" (vector-append #(1 2) #(3 4)) #(1 2 3 4))
(test-equal "vector-map"    (vector-map (lambda (x) (* x 2)) #(1 2 3)) #(2 4 6))
(test-equal "vector-for-each" (let ((acc '())) (vector-for-each (lambda (x) (set! acc (cons x acc))) #(1 2)) (reverse acc)) '(1 2))
(test-equal "vector-count"   (vector-count even? #(1 2 3 4)) 2)
(test-equal "vector-any"     (vector-any even? #(1 2 3)) #t)
(test-equal "vector-every"   (vector-every positive? #(1 2 3)) #t)
(test-equal "vector-empty?"  (vector-empty? #()) #t)
(test-equal "vector-reverse" (vector-reverse #(1 2 3)) #(3 2 1))
(test-equal "vector-fold" (vector-fold (lambda (i elt acc) (+ acc elt)) 0 #(1 2 3)) 6)
(test-equal "vector-index" (vector-index even? #(1 3 4 5)) 2)
(test-equal "vector-skip"  (vector-skip odd? #(1 3 4 5)) 2)
(test-equal "vector-swap!" (let ((v #(1 2 3))) (vector-swap! v 0 2) v) #(3 2 1))
(test-equal "vector-sort"  (vector-sort < #(3 1 4 2)) #(1 2 3 4))
(test-equal "vector-unfold" (vector-unfold (lambda (i x) (values x (+ x 1))) 5 0) #(0 1 2 3 4))

(test-end "scheme_builtins_base_ext — 向量扩展")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 排序与合并")

(test-equal "list-sort"  (list-sort < '(3 1 4 2)) '(1 2 3 4))
(test-equal "merge"      (merge < '(1 3 5) '(2 4 6)) '(1 2 3 4 5 6))
(test-equal "sorted?"    (sorted? < '(1 2 3)) #t)

(test-end "scheme_builtins_base_ext — 排序与合并")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 集合（char-set）")

(define _cs (char-set #\a #\b #\c))
(test-equal "char-set?"       (char-set? _cs) #t)
(test-equal "char-set-contains?" (char-set-contains? _cs #\b) #t)
(test-equal "char-set-contains? no" (char-set-contains? _cs #\z) #f)
(test-equal "char-set->list"  (length (char-set->list _cs)) 3)
(test-equal "char-set-adjoin" (char-set-contains? (char-set-adjoin _cs #\d) #\d) #t)
(test-equal "char-set-delete" (char-set-contains? (char-set-delete _cs #\b) #\b) #f)
(test-equal "char-set-empty?" (char-set-empty? (char-set)) #t)
(test-equal "char-set-union"  (char-set-contains? (char-set-union _cs (char-set #\d)) #\d) #t)
(test-equal "char-set-intersection" (char-set-contains? (char-set-intersection _cs (char-set #\a #\c)) #\b) #f)
(test-equal "char-set-difference" (char-set-contains? (char-set-difference _cs (char-set #\b)) #\b) #f)
(test-equal "char-set-complement" (char-set-contains? (char-set-complement _cs) #\z) #t)

(test-end "scheme_builtins_base_ext — 集合")

;; ============================================================
(test-begin "scheme_builtins_base_ext — hash table")

(define _ht (make-equal-hash-table))
(hash-table-set! _ht 'a 1)
(hash-table-set! _ht 'b 2)
(test-equal "hash-table?" (hash-table? _ht) #t)
(test-equal "hash-table-contains?" (hash-table-contains? _ht 'a) #t)
(test-equal "hash-table-ref" (hash-table-ref _ht 'b) 2)
(test-equal "hash-table-size" (hash-table-size _ht) 2)
(test-equal "hash-table-keys" (length (hash-table-keys _ht)) 2)
(hash-table-delete! _ht 'a)
(test-equal "hash-table-delete!" (hash-table-size _ht) 1)
(hash-table-clear! _ht)
(test-equal "hash-table-clear!" (hash-table-size _ht) 0)

(test-end "scheme_builtins_base_ext — hash table")

;; ============================================================
(test-begin "scheme_builtins_base_ext — deque & list-queue & binary-heap")

;; deque
(define _dq (make-deque 1 2 3))
(test-equal "deque-front" (deque-front _dq) 1)
(test-equal "deque-back"  (deque-back _dq) 3)
(test-equal "deque-length" (deque-length _dq) 3)
(test-equal "deque->list"  (deque->list _dq) '(1 2 3))

;; list-queue
(define _lq (list-queue))
(list-queue-add-back! _lq 'a)
(list-queue-add-front! _lq 'z)
(test-equal "list-queue-front" (list-queue-front _lq) 'z)
(test-equal "list-queue-back"  (list-queue-back _lq) 'a)
(test-equal "list-queue-remove-front!" (list-queue-remove-front! _lq) 'z)
;(test-equal "list-queue->list" (list-queue->list _lq) '(a))

;; binary-heap
;(define _heap (make-binary-heap 5 1 3 2 4))
;(test-equal "binary-heap-min" (binary-heap-min _heap) 5)
;(test-equal "binary-heap-remove-min!" (binary-heap-remove-min! _heap) 5)
;(test-equal "binary-heap-size" (binary-heap-size _heap) 4)

;(test-end "scheme_builtins_base_ext — deque & list-queue & binary-heap")

;; ============================================================
;(test-begin "scheme_builtins_base_ext — bimap")

;(define _bm (make-bimap '(a . 1) '(b . 2)))
;(test-equal "bimap-contains?" (bimap-contains? _bm 'a) #t)
;(test-equal "bimap-forward"   (bimap-forward _bm 'b) 2)
;(test-equal "bimap-reverse"   (bimap-reverse _bm 1) 'a)
;(bimap-set! _bm 'c 3)
;(test-equal "bimap-forward after set" (bimap-forward _bm 'c) 3)

;(test-end "scheme_builtins_base_ext — bimap")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 组合子与高阶")

(test-equal "compose"  ((compose (lambda (x) (* x 2)) (lambda (x) (+ x 1))) 5) 12)
(test-equal "curry"    ((curry + 10) 5) 15)
(test-equal "flip"     ((flip -) 3 10) 7)
(test-equal "const"    ((const 99) 'anything) 99)
(test-equal "complement" ((complement even?) 3) #t)
(test-equal "iterate"  (iterate (lambda (x) (* x 2)) 3 1) 8)
(test-equal "add1"     (add1 99) 100)
(test-equal "sub1"     (sub1 99) 98)
(test-equal "square"   (square 7) 49)

(test-end "scheme_builtins_base_ext — 组合子与高阶")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 数学扩展")

(test-equal "factorial"  (factorial 5) 120)
(test-equal "fibonacci"  (fibonacci 10) 55)
(test-equal "binomial"   (binomial 5 2) 10)
(test-equal "prime? true" (prime? 17) #t)
(test-equal "prime? false" (prime? 15) #f)
(test-equal "factor"      (factor 12) '(2 2 3))
(test-equal "permutations" (length (permutations '(1 2 3))) 6)
(test-equal "combinations" (length (combinations '(1 2 3 4) 2)) 6)
(test-equal "cartesian-product" (length (cartesian-product '(1 2) '(a b c))) 6)

(test-end "scheme_builtins_base_ext — 数学扩展")

;; ============================================================
(test-begin "scheme_builtins_base_ext — JSON")

;(test-equal "json->string" (string? (json->string '((a . 1) (b . 2)))) #t)
;(test-equal "json-read string" (pair? (json-read "{\"x\": 10}")) #t)
;(test-equal "json-write (via string)" (begin (define _jop (open-output-string)) (with-output-to-string (lambda () (json-write '(1 2 3)))) (string? (get-output-string _jop))) #t)

(test-end "scheme_builtins_base_ext — JSON")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 生成器")

(define _gen (list->generator '(10 20 30)))
(test-equal "generator count" (generator-count (lambda (x) (> x 15)) _gen) 2)
(define _gen2 (list->generator '(1 2 3)))
(test-equal "generator->list" (generator->list _gen2) '(1 2 3))
(define _gen3 (list->generator '(a b c)))
(test-equal "generator-map" (generator->list (generator-map (lambda (x) x) (generator-filter (lambda (x) #t) _gen3))) '(a b c))

(test-end "scheme_builtins_base_ext — 生成器")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 流")

;(define _nats (nat-stream 0))
;(test-equal "stream-car" (stream-car _nats) 0)
;(test-equal "stream-ref" (stream-ref _nats 5) 5)
;(test-equal "stream-take" (stream-take 3 _nats) '(0 1 2))
;(test-equal "stream-null?" (stream-null? _nats) #f)

(test-end "scheme_builtins_base_ext — 流")

;; ============================================================
(test-begin "scheme_builtins_base_ext — flonum 运算")

(test-equal "fl+" (fl+ 1.5 2.5) 4.0)
(test-equal "fl-" (fl- 5.0 3.0) 2.0)
(test-equal "fl*" (fl* 2.0 3.0) 6.0)
(test-equal "fl/" (fl/ 7.0 2.0) 3.5)
(test-equal "fl=?" (fl=? 1.0 1.0) #t)
(test-equal "fl<?" (fl<? 1.0 2.0) #t)
(test-equal "flsqrt" (flsqrt 9.0) 3.0)
(test-equal "flsin" (< (flsin 0.0) 1e-10) #t)

(test-end "scheme_builtins_base_ext — flonum 运算")

;; ============================================================
(test-begin "scheme_builtins_base_ext — bitvector")

(define _bvec (make-bitvector 8 #t))
(test-equal "bitvector?" (bitvector? _bvec) #t)
(test-equal "bitvector-length" (bitvector-length _bvec) 8)
(test-equal "bitvector-ref"    (bitvector-ref _bvec 0) #t)
(bitvector-set! _bvec 1 #f)
(test-equal "bitvector-set!"   (bitvector-ref _bvec 1) #f)
(define _bvec2 (bitvector-copy _bvec))
(test-equal "bitvector-append" (bitvector-length (bitvector-append _bvec _bvec2)) 16)
(test-equal "integer->list" (integer->list 5) '(1 0 1))
(test-equal "list->integer" (list->integer '(1 0 1)) 5)

(test-end "scheme_builtins_base_ext — bitvector")

;; ============================================================
(test-begin "scheme_builtins_macro — 宏")

;; if-not
(test-equal "if-not true"  (if-not #f 'yes 'no) 'yes)
(test-equal "if-not false" (if-not #t 'yes 'no) 'no)

;; nth
(test-equal "nth" (nth 1 'a 'b 'c) 'b)

;; rec
(test-equal "rec factorial"
  (let ((fact (rec (fact n) (if (< n 2) 1 (* n (fact (- n 1)))))))
    (fact 5))
  120)

;; ;; and-let*
;; (test-equal "and-let* all true" (and-let* ((x 1) (y (+ x 2))) y) 3)
;; (test-equal "and-let* some false" (and-let* ((x 1) (y #f) (z 3)) y) #f)

;; aif / aand
(define _aif-test (aif (+ 2 3) it 0))
(test-equal "aif" _aif-test 5)

(define _aand-test (aand 1 2 3))
(test-equal "aand" _aand-test 3)

;; fluid-let
(test-equal "fluid-let" (let ((x 1)) (fluid-let ((x 2)) x) x) 1)

;; assume
(test-equal "assume pass" (assume (= 1 1)) #t)
;; assume 失败会抛错（不可测为通过）

;; define-immutable
(define-immutable (inc x) (+ x 1))
(test-equal "define-immutable" (inc 5) 6)

;; with-values
(test-equal "with-values" (with-values (values 3 4) (lambda (a b) (+ a b))) 7)

;; test-assert / test-equal / check 已在测试框架中验证

;; cut
(define _cut-add5 (cut + 5 <>))
(test-equal "cut" (_cut-add5 10) 15)

;; 理解宏: stream-cons
(define _stream-ex (stream-cons 1 (stream-cons 2 '())))
(test-equal "stream-cons car" (stream-car _stream-ex) 1)

;; list-ec
(test-equal "list-ec" (list-ec (* x 2) (for x '(1 2 3))) '(2 4 6))

;; sum-ec
(test-equal "sum-ec" (sum-ec (* x 2) (for x '(1 2 3))) 12)

;; any?-ec / every?-ec
(test-equal "any?-ec" (any?-ec (even? x) (for x  '(1 2 3))) #t)
(test-equal "every?-ec" (every?-ec (positive? x) (for x '(1 2 3))) #t)

(test-end "scheme_builtins_macro — 宏")

;; Python bridge and Python-style tools require external packages (numpy, sympy)
;; and are not available in the core interpreter.

(test-end "Final")

(test-equal "atom?"     (atom? 'x) #t)
(test-equal "atom? pair" (atom? '(1 2)) #f)
(test-equal "just"      (just 42) '(42))
(test-equal "just?"     (just? '(42)) #t)
(test-equal "nothing?"  (nothing? '()) #t)
(test-equal "maybe?"    (maybe? '(42)) #t)
(test-equal "exact-nonnegative-integer?" (exact-nonnegative-integer? 7) #t)

(test-equal "finite?" (finite? 3.0) #t)
(test-equal "infinite?" (infinite? +inf.0) #t)
(test-equal "nan?" (nan? +nan.0) #t)

(test-equal "boolean->string" (boolean->string #t) "#t")
(test-equal "boolean=?" (boolean=? #t #t #t) #t)

(test-end "scheme_builtins_base_ext — 杂项谓词和工具")

;; ============================================================
(test-begin "scheme_builtins_base — CxR 组合")

(test-equal "caar" (caar '((1 2) 3)) 1)
(test-equal "cadr" (cadr '(1 2 3)) 2)
(test-equal "cddr" (cddr '(1 2 3)) '(3))
(test-equal "caddr" (caddr '(1 2 3 4)) 3)
(test-equal "cadddr" (cadddr '(1 2 3 4)) 4)

(test-end "scheme_builtins_base — CxR 组合")

;; ============================================================
(test-begin "scheme_builtins_adv — 异常与条件")

;; raise 测试
(test-equal "raise" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k 'raised)) (lambda () (raise "boom"))))) 'raised)

;; error-object?
(test-equal "error-object?" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k (error-object? e))) (lambda () (error "msg"))))) #t)

(test-end "scheme_builtins_adv — 异常与条件")

;; ============================================================
(test-begin "scheme_builtins_adv — 端口扩展")

(test-equal "binary-port?" (binary-port? (open-binary-input-file "/dev/null")) #t)
(test-equal "textual-port?" (textual-port? (open-input-string "hello")) #t)
(test-equal "port?" (port? (current-input-port)) #t)
(test-equal "input-port?" (input-port? (current-input-port)) #t)
(test-equal "output-port?" (output-port? (current-output-port)) #t)
(test-equal "input-port-open?" (input-port-open? (current-input-port)) #t)

(test-end "scheme_builtins_adv — 端口扩展")

;; ============================================================
(test-begin "符号宏 (scheme_builtins_macro — dbind)")
;; dbind 析构绑定
(test-equal "dbind" (let ((v '(1 2))) (dbind (a b) v (list a b))) '(1 2))
(test-end "符号宏 (scheme_builtins_macro — dbind)")

;; ============================================================
(display "\n=== All builtins tests completed ===\n")
