;; =========================================================================
;; 5. Ellipsis (...) 多值匹配与展开 (Recursive or Macro)
;; =========================================================================
(display "Test 5: Ellipsis (...) nested expansion ... ")
(define-syntax my-or
  (lambda (x)
    (syntax-case x ()
      ((_) #f)
      ((_ e) #'e)
      ((_ e1 e2 ...)
       #'(let ((temp e1))
           (if temp temp (my-or e2 ...)))))))

(if (and (eq? (my-or #f #f 'yes #f) 'yes)
         (eq? (my-or #f) #f))
    (display "PASS\n")
    (display "FAIL\n"))

(newline)
;; =========================================================================
;; 2. 基础语法宏包装器 (Simple Wrapper Macro)
;; =========================================================================
(display "Test 2: Simple wrapper macro ... ")
(define-syntax my-quote
  (lambda (x)
    (syntax-case x ()
      ((_ arg) #'(quote arg)))))

(if (eq? (my-quote hello-world) 'hello-world)
    (display "PASS\n")
    (display "FAIL\n"))


(newline)


;; =========================================================================
;; 6. quasisyntax (#`) 与 unsyntax (#,) 测试
;; =========================================================================
(display "Test 6: quasisyntax (#`) & unsyntax (#,) ... ")
(define-syntax quasi-add
  (lambda (x)
    (syntax-case x ()
      ((_ arg)
       #`(list #,(datum->syntax #'arg 100) arg)))))

(let ((res (quasi-add 200)))
  (if (equal? res '(100 200))
      (display "PASS\n")
      (display "FAIL\n")))
(newline)


;; =========================================================================
;; 8. 标识符比对判定 (bound-identifier=? & free-identifier=?)
;; =========================================================================
(display "Test 8: bound & free identifier comparisons ... ")
(define-syntax check-identifiers
  (lambda (x)
    (syntax-case x ()
      ((_ id1 id2)
       #`(list (bound-identifier=? #'id1 #'id2)
               (free-identifier=? #'id1 #'id2))))))

(let ((res (check-identifiers foo foo)))
  (if (equal? res '(#t #t))
      (display "PASS\n")
      (display "FAIL\n")))

(newline)

;; =========================================================================
;; 9. 综合测试：带 Fender (Guard) 守卫分支的宏
;; =========================================================================
(display "Test 9: syntax-case guard/fender condition ... ")
(define-syntax cond-even
  (lambda (x)
    (syntax-case x ()
      ((_ num expr)
       (integer? (syntax->datum #'num)) ; Fender condition
       #'(if (even? num) expr 'not-even))
      ((_ num expr)
       #'(error "Only constant integers are supported")))))

(if (and (eq? (cond-even 4 'yes) 'yes)
         (eq? (cond-even 3 'yes) 'not-even))
    (display "PASS\n")
    (display "FAIL\n"))

(newline)

;; =========================================================================
;; 3. 多分支模式匹配与副作用 (Multiple Clause Swap Macro)
;; =========================================================================
(display "Test 3: Multi-clause Swap Macro ... ")
(define-syntax swap!
  (lambda (x)
    (syntax-case x ()
      ((_ a b) #'(let ((temp a))
                   (set! a b)
                   (set! b temp))))))

(let ((x 10) (y 20))
  (swap! x y)
  (if (and (= x 20) (= y 10))
      (display "PASS\n")
      (display "FAIL\n")))
(newline)

;; =========================================================================
;; 4. with-syntax 与临时绑定测试
;; =========================================================================
(display "Test 4: with-syntax binding ... ")
(define-syntax construct-identity
  (lambda (x)
    (syntax-case x ()
      ((_ val)
       (with-syntax ((temp (datum->syntax #'val 'tmp-var)))
         #'(let ((temp val)) temp))))))

(if (= (construct-identity 42) 42)
    (display "PASS\n")
    (display "FAIL\n"))
(newline)


;; =========================================================================
;; 7. generate-temporaries 卫生宏别名测试
;; =========================================================================
(display "Test 7: generate-temporaries ... ")
(define-syntax make-alias
  (lambda (x)
    (syntax-case x ()
      ((_ id val)
       (with-syntax (((temp) (generate-temporaries #'(id))))
         #'(let ((temp val)) (let ((id temp)) id)))))))

(if (= (make-alias foo 99) 99)
    (display "PASS\n")
    (display "FAIL\n"))

(newline)

;; =========================================================================
;; 1. 基础语法对象与 datum 转换测试
;; =========================================================================
(display "Test 1: syntax->datum and syntax? ... ")
(let* ((stx #'hello)
       (is-stx (syntax? stx))
       (datum (syntax->datum stx)))
  (if (and is-stx (eq? datum 'hello))
      (display "PASS\n")
      (display "FAIL\n")))
(newline)
