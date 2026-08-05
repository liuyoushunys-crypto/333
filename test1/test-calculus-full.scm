;;; calculus.scm 完整功能测试套件

(load "./test1/calculus.scm")

(define (approx-equal a b eps)
  (if (and (number? a) (number? b))
      (< (abs (- a b)) eps)
      #f))

(define (check name actual expected eps)
  (if (or (equal? actual expected) (approx-equal actual expected eps))
      (begin (display "[PASS] ") (display name) (newline) #t)
      (begin (display "[FAIL] ") (display name)
             (display " expected: ") (display expected)
             (display " actual: ") (display actual) (newline) #f)))

(define (check-result name expr a b)
  (let ((d (definite-integral expr 'x a b))
        (anti (antideriv expr 'x)))
    (if anti
        (let ((fa (eval-subst anti 'x a))
              (fb (eval-subst anti 'x b)))
          (check name d (- fb fa) 0.0001))
        (begin (display "[SKIP] ") (display name) (newline) #f))))

(define (contains-symbol? expr sym)
  (cond ((number? expr) #f)
        ((symbol? expr) (eq? expr sym))
        ((pair? expr) (or (contains-symbol? (car expr) sym)
                          (contains-symbol? (cadr expr) sym)
                          (and (pair? (cddr expr))
                               (contains-symbol? (caddr expr) sym))))
        (else #f)))

(define P 3.141592653589793)

; ============================================================
; 第一部分：表达式工具 depends?
; ============================================================
(display "===== 1. 表达式工具 depends? =====") (newline)

(check "depends? var in x" (depends? '(+ (* 2 x) 3) 'x) #t 0.0)
(check "depends? const" (depends? 42 'x) #f 0.0)
(check "depends? symbol not var" (depends? 'y 'x) #f 0.0)
(check "depends? x in sin" (depends? '(sin x) 'x) #t 0.0)
(check "depends? no dep" (depends? '(+ 1 2) 'x) #f 0.0)

; ============================================================
; 第二部分：求导 deriv
; ============================================================
(display "===== 2. 求导 deriv =====") (newline)

(check "deriv const" (deriv 42 'x) 0 0.0)
(check "deriv pi" (deriv 3.14 'x) 0 0.0)
(check "deriv x" (deriv 'x 'x) 1 0.0)
(check "deriv y" (deriv 'y 'x) 0 0.0)
(check "deriv x" (simplify (deriv 'x 'x)) 1 0.0)
(check "deriv x+5" (simplify (deriv '(+ x 5) 'x)) 1 0.0)
(check "deriv 3x+5" (simplify (deriv '(+ (* 3 x) 5) 'x)) 3 0.0)
(check "deriv x^2" (simplify (deriv '(expt x 2) 'x)) '(* 2 x) 0.0)
(check "deriv x^3" (simplify (deriv '(expt x 3) 'x)) '(* 3 (expt x 2)) 0.0)
(check "deriv x^2+x" (simplify (deriv '(+ (expt x 2) x) 'x)) '(+ (* 2 x) 1) 0.0)
(check "deriv sin" (simplify (deriv '(sin x) 'x)) '(cos x) 0.0)
(check "deriv cos" (simplify (deriv '(cos x) 'x)) '(- (sin x)) 0.0)
(check "deriv exp" (simplify (deriv '(exp x) 'x)) '(exp x) 0.0)
(check "deriv log" (simplify (deriv '(log x) 'x)) '(/ 1 x) 0.0)
(check "deriv sin(2x) has cos" (contains-symbol? (simplify (deriv '(sin (* 2 x)) 'x)) 'cos) #t 0.0)
(check "deriv exp(3x) has exp" (contains-symbol? (simplify (deriv '(exp (* 3 x)) 'x)) 'exp) #t 0.0)
(check "deriv sin(x^2) has cos" (contains-symbol? (deriv '(sin (expt x 2)) 'x) 'cos) #t 0.0)
(check "deriv sqrt" (simplify (deriv '(sqrt x) 'x)) '(/ 1 (* 2 (sqrt x))) 0.0)

; ============================================================
; 第三部分：化简 simplify
; ============================================================
(display "===== 3. 化简 simplify =====") (newline)

(check "simplify 0+x" (simplify '(+ 0 x)) 'x 0.0)
(check "simplify x+0" (simplify '(+ x 0)) 'x 0.0)
(check "simplify x*1" (simplify '(* x 1)) 'x 0.0)
(check "simplify x*0" (simplify '(* x 0)) 0 0.0)
(check "simplify x^1" (simplify '(expt x 1)) 'x 0.0)
(check "simplify x^0" (simplify '(expt x 0)) 1 0.0)
(check "simplify 0^2" (simplify '(expt 0 2)) 0 0.0)
(check "simplify 1^2" (simplify '(expt 1 2)) 1 0.0)

; ============================================================
; 第四部分：极限 limit
; ============================================================
(display "===== 4. 极限 limit =====") (newline)

(check "limit exp(0)" (limit '(exp x) 'x 0) 1.0 0.0001)
(check "limit sin(0)" (limit '(sin x) 'x 0) 0.0 0.0001)
(check "limit x^2 at 3" (limit '(expt x 2) 'x 3) 9.0 0.0001)
(check "limit log(1) at 1" (limit '(log x) 'x 1) 0.0 0.001)

; ============================================================
; 第五部分：泰勒展开 taylor
; ============================================================
(display "===== 5. 泰勒展开 taylor =====") (newline)

(check "taylor sin 0 n=3" (pair? (taylor '(sin x) 'x 0 3)) #t 0.0)
(check "taylor exp 0 n=2" (pair? (taylor '(exp x) 'x 0 2)) #t 0.0)
(check "taylor x^2 at 0" (pair? (taylor '(expt x 2) 'x 0 2)) #t 0.0)

; ============================================================
; 第六部分：表达式求值 eval-subst
; ============================================================
(display "===== 6. 表达式求值 eval-subst =====") (newline)

(check "eval x=2" (eval-subst 'x 'x 2) 2 0.0)
(check "eval const" (eval-subst 42 'x 2) 42 0.0)
(check "eval 2x+1 at x=3" (eval-subst '(+ (* 2 x) 1) 'x 3) 7 0.0)
(check "eval x^2 at x=5" (eval-subst '(expt x 2) 'x 5) 25 0.0)
(check "eval sin(0)" (eval-subst '(sin x) 'x 0) 0.0 0.0001)
(check "eval exp(0)" (eval-subst '(exp x) 'x 0) 1.0 0.0001)


; ============================================================
; 第八部分：反导数 - 基本类型
; ============================================================
(display "===== 8. 反导数: 基本类型 =====") (newline)

;(check-result "antideriv const" 5 'x 0 1)
;(check-result "antideriv x" 0 1)
(check-result "antideriv x^2" '(expt x 2) 0 1)
(check-result "antideriv x^3" '(expt x 3) 0 1)
(check-result "antideriv 1/x" '(/ 1 x) 1 2)

; ============================================================
; 第九部分：反导数 - 三角函数
; ============================================================
(display "===== 9. 反导数: 三角函数 =====") (newline)

(check-result "antideriv sin" '(sin x) 0 P)
(check-result "antideriv sin(2x)" '(sin (* 2 x)) 0 P)
(check-result "antideriv cos" '(cos x) 0 P)
(check-result "antideriv sin^2" '(expt (sin x) 2) 0 P)
(check-result "antideriv cos^2" '(expt (cos x) 2) 0 P)
;(check-result "antideriv tan" '(tan x) 0 1.0)

; ============================================================
; 第十部分：反导数 - 指数/对数
; ============================================================
(display "===== 10. 反导数: 指数/对数 =====") (newline)

(check-result "antideriv exp" '(exp x) 0 1)
(check-result "antideriv exp(2x)" '(exp (* 2 x)) 0 1)
(check-result "antideriv exp(-x)" '(exp (- x)) 0 1)
(check-result "antideriv log" '(log x) 1 2)
(check-result "antideriv sqrt" '(sqrt x) 0 1)
(check-result "antideriv sqrt(2x+1)" '(sqrt (+ (* 2 x) 1)) 0 1)

; ============================================================
; 第十一部分：反导数 - 分式
; ============================================================
(display "===== 11. 反导数: 分式 =====") (newline)

(check-result "antideriv 1/(2x)" '(/ 1 (* 2 x)) 1 2)
(check-result "antideriv 3/(x+1)" '(/ 3 (+ x 1)) 0 1)
(check-result "antideriv 1/(x^2+1)" '(/ 1 (+ (expt x 2) 1)) 0 1)

(let ((result (definite-integral '(/ 3 (+ (expt x 2) 1)) 'x 0 1))
      (expected (* 3 (/ P 4))))
(check "3/(x^2+1): int_0^1 = 3pi/4" result expected 0.001))

(check-result "antideriv x/(2x+1)" '(/ x (+ (* 2 x) 1)) 0 1)

; ============================================================
; 第十二部分：反导数 - 分部积分
; ============================================================
(display "===== 12. 反导数: 分部积分 =====") (newline)

(check-result "antideriv x*sin(x)" '(* x (sin x)) 0 P)
(check-result "antideriv x^2*sin(x)" '(* (expt x 2) (sin x)) 0 P)
(check-result "antideriv x*sin(2x)" '(* x (sin (* 2 x))) 0 P)
(check-result "antideriv x*cos(x)" '(* x (cos x)) 0 P)
(check-result "antideriv x^2*cos(x)" '(* (expt x 2) (cos x)) 0 1.57)
(check-result "antideriv x*exp(x)" '(* x (exp x)) 0 1)
(check-result "antideriv x^2*exp(x)" '(* (expt x 2) (exp x)) 0 1)
(check-result "antideriv x*exp(2x)" '(* x (exp (* 2 x))) 0 1)
(check-result "antideriv x*log(x)" '(* x (log x)) 1 2)
(check-result "antideriv x^2*log(x)" '(* (expt x 2) (log x)) 1 2)

; ============================================================
; 第十四部分：辅助函数
; ============================================================
(display "===== 14. 输出辅助函数 =====") (newline)

(show-deriv '(expt x 2) 'x)
(show-antideriv '(exp x) 'x)
(show-taylor '(sin x) 'x 0 3)

; ============================================================
; 第十五部分：综合分部积分 — 精确值验证
; ============================================================
(display "===== 15. 综合分部积分测试 =====") (newline)

(let ((result (definite-integral '(* (expt x 2) (sin x)) 'x 0 P))
      (expected (- (* P P) 4)))
  (check "x^2*sin: int_0^pi = pi^2-4" result expected 0.0001))

(let ((result (definite-integral '(* (expt x 2) (cos x)) 'x 0 (/ P 2)))
      (expected (- (/ (* P P) 4) 2)))
  (check "x^2*cos: int_0^{pi/2} = pi^2/4-2" result expected 0.0001))

(let ((result (definite-integral '(* (expt x 2) (exp x)) 'x 0 1))
      (expected (- (exp 1) 2)))
  (check "x^2*exp: int_0^1 = e-2" result expected 0.0001))

(let ((result (definite-integral '(* (expt x 3) (sin x)) 'x 0 P))
      (expected (- (* P P P) (* 6 P))))
  (check "x^3*sin: int_0^pi = pi^3-6pi" result expected 0.0001))

(let ((result (definite-integral '(* x (sin (* 2 x))) 'x 0 P))
      (expected (/ (* -1 P) 2)))
  (check "x*sin(2x): int_0^pi = -pi/2" result expected 0.0001))

(let ((result (definite-integral '(* x (exp (* 2 x))) 'x 0 1))
      (expected (/ (+ (exp 2) 1) 4)))
  (check "x*exp(2x): int_0^1 = (e^2+1)/4" result expected 0.0001))

(let ((result (definite-integral '(* (expt x 2) (log x)) 'x 1 2))
      (expected (- (/ (* 8 (log 2)) 3) (/ 7.0 9.0))))
  (check "x^2*log: int_1^2" result expected 0.0001))

; ============================================================
; 第十六部分：微积分基本定理验证
; ============================================================
(display "===== 16. 微积分基本定理验证 =====") (newline)

(check "fundamental: x^2*sin antideriv" (pair? (antideriv '(* (expt x 2) (sin x)) 'x)) #t 0.0)
(check "fundamental: exp antideriv" (pair? (antideriv '(exp x) 'x)) #t 0.0)

; ============================================================
; 第十七部分：边界情况
; ============================================================
(display "===== 17. 边界情况 =====") (newline)

(check "antideriv unknown #f" (antideriv '(sin (sin x)) 'x) #f 0.0)
(check "antideriv x*sin(x^3) #f" (antideriv '(* x (sin (expt x 3))) 'x) #f 0.0)

; ============================================================
; 第十八部分：三角复合
; ============================================================
(display "===== 18. 三角复合 =====") (newline)

(check-result "antideriv sin(3x+1)" '(sin (+ (* 3 x) 1)) 0 1)
(check-result "antideriv cos(3x+1)" '(cos (+ (* 3 x) 1)) 0 1)

; ============================================================
; 第十九部分：特殊幂函数
; ============================================================
(display "===== 19. 特殊幂函数 =====") (newline)

(check-result "antideriv x^(-1)" '(expt x -1) 1 2)

; ============================================================
; 第十三部分：宏
; ============================================================
(display "===== 13. 宏 =====") (newline)

(check "D (x^2) has deriv" (contains-symbol? (D (expt x 2) x) 'cos) #f 0.0)
(check "d/d (x^3) has deriv" (contains-symbol? (d/d (expt x 3) x) 'cos) #f 0.0)
(check "∫ x dx 0 1" (∫ x x 0 1) 0.5 0.0001)
(check "∫d x^2 returns non-null" (pair? (∫d (expt x 2) x)) #t 0.0)

; ============================================================
; 测试完成
; ============================================================
(newline)
(display "===== 测试完成 =====") (newline)

; ============================================================
; 第七部分：线性表达式工具
; ============================================================
(display "===== 7. 线性表达式工具 =====") (newline)

(check "make-linear x" (make-linear 1 'x 0) 'x 0.0)
(check "make-linear 2x" (make-linear 2 'x 0) '(* 2 x) 0.0)
(check "make-linear x+1" (make-linear 1 'x 1) '(+ x 1) 0.0)
(check "make-linear 2x+3" (make-linear 2 'x 3) '(+ (* 2 x) 3) 0.0)
(check "make-linear -2x" (make-linear -2 'x 0) '(* -2 x) 0.0)
(check "extract-linear x" (extract-linear 'x 'x) (cons 1 0) 0.0)
(check "extract-linear 2x" (extract-linear '(* 2 x) 'x) (cons 2 0) 0.0)
(check "extract-linear x+3" (extract-linear '(+ x 3) 'x) (cons 1 3) 0.0)
(check "extract-linear 2x+5" (extract-linear '(+ (* 2 x) 5) 'x) (cons 2 5) 0.0)
(check "extract-linear sin x" (extract-linear '(sin x) 'x) #f 0.0)
(check "extract-linear x^2" (extract-linear '(expt x 2) 'x) #f 0.0)
