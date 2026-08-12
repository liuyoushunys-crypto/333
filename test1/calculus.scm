;; ============================================================================
;; 符号计算库 — 微积分 (数据导向设计范式重构版 - 多算子扩展版)
;; ============================================================================

(display "Loading extended data-directed calculus.scm...") (newline)

;; ============================================================================
;; 1. 数值计算辅助函数
;; ============================================================================
;; 标准 Scheme 规范中通常内置了 sin, cos, tan, sinh, cosh, tanh, asin, acos
;; 但未内置 sec, csc, cot。为了使极限和定积分求值能正常进行数值计算，在此处定义它们：
(define (sec x) (/ 1 (cos x)))
(define (csc x) (/ 1 (sin x)))
(define (cot x) (/ 1 (tan x)))
;;(define (sinh x) (/ 2 (- (exp x) (/ 1 (exp x)))))
;;(define (cosh x) (/ 2 (+ (exp x) (/ 1 (exp x)))))
;;(define (tanh x) (/ (- (exp x) (/ 1 (exp x))) (+ (exp x) (/ 1 (exp x)))))
;; 1. 常数定义
(define pi 3.141592653589793)

;; 2. 反双曲函数数值实现 (利用对数关系表示)
(define (asinh x) (log (+ x (sqrt (+ (* x x) 1)))))
(define (acosh x) (if (< x 1) (error 'acosh "定义域错误" x) (log (+ x (sqrt (- (* x x) 1))))))
(define (atanh x) (if (>= (abs x) 1) (error 'atanh "定义域错误" x) (* 0.5 (log (/ (+ 1 x) (- 1 x))))))

;; 3. 倒数双曲函数数值实现
(define (sech x)  (/ 1 (cosh x)))
(define (csch x)  (/ 1 (sinh x)))
(define (coth x)  (/ (cosh x) (sinh x)))

;; 4. 固定底数对数
(define (log10 x) (/ (log x) (log 10)))
(define (log2 x)  (/ (log x) (log 2)))

;; 5. 误差函数 erf 的数值逼近 (采用高精度的 Abramowitz and Stegun 近似公式)
(define (erf x)
  (let ((abs-x (abs x)))
    (let* ((p 0.3275911)
           (a1 0.254829592) (a2 -0.284496736) (a3 1.421413741)
           (a4 -1.453152027) (a5 1.061405429)
           (t (/ 1 (+ 1 (* p abs-x))))
           ;; 逼近计算公式
           (y (- 1 (* (+ (* (+ (* (+ (* (+ (* a5 t) a4) t) a3) t) a2) t) a1)
                      t (exp (- (* abs-x abs-x)))))))
      (if (>= x 0) y (- y)))))

;; 6. 兰伯特 W 函数的数值求解 (采用牛顿-拉弗森迭代法 Newton's Method)
(define (W x)
  (if (< x (- (/ 1 (exp 1))))
      (error 'W "超出分支定义域下限 -1/e" x)
      (let loop ((w (if (> x 1) (log x) 0.0)) (i 0))
        (if (>= i 100) w
            (let* ((ew (exp w))
                   (f (- (* w ew) x))
                   (df (* ew (+ w 1))))
              (if (< (abs f) 1e-12) w
                  (loop (- w (/ f df)) (+ i 1))))))))
                  
;; ============================================================================
;; 2. 语法宏定义 (提供直观的微积分数学语法)
;; ============================================================================

(define-macro (D expr var)           `(deriv ',expr ',var))
(define-macro (d/d expr var)         `(simplify (deriv ',expr ',var)))
(define-macro (∫ expr var a b)       `(definite-integral ',expr ',var ,a ,b))
(define-macro (∫d expr var)         `(antideriv ',expr ',var))
(define-macro (lim expr var val)     `(limit ',expr ',var ,val))
(define-macro (taylor-series expr var at n) `(taylor ',expr ',var ,at ,n))

(define-macro (show label expr)
  `(begin (display ,label) (display " = ") (display ,expr) (newline)))


(define (depends? expr var)
  (cond ((number? expr) #f)
        ((symbol? expr) (eq? expr var))
        ((pair? expr) (or (depends? (cadr expr) var)
                          (and (pair? (cddr expr))
                               (depends? (caddr expr) var))))
        (#f)))

;; ============================================================================
;; 3. 中央操作注册表 (实现算子行为的动态绑定)
;; ============================================================================

(define *op-registry* '())

;; 注册函数：向注册表中添加或覆盖一个算子的行为
(define (register-op! op deriv-proc simplify-proc anti-proc)
  (set! *op-registry* (cons (list op deriv-proc simplify-proc anti-proc) *op-registry*)))

;; 查询函数
(define (lookup-op op)               (assq op *op-registry*))

;; 属性选择器
(define (op-deriv entry)             (cadr entry))    ; 求导函数
(define (op-simplify entry)          (caddr entry))   ; 化简函数
(define (op-antideriv entry)         (cadddr entry))  ; 不定积分函数


;; ============================================================================
;; 4. 核心分发器 (基于高阶函数与 cond => 语法糖实现)
;; ============================================================================

(define (deriv-args exprs var) (map (lambda (e) (deriv e var)) exprs))

;; 核心求导入口
(define (deriv expr var)
  (cond ((number? expr) 0) 
        ((symbol? expr) (if (eq? expr var) 1 0))
        ((pair? expr)
         (cond ((lookup-op (car expr)) => (lambda (entry) ((op-deriv entry) (cdr expr) var)))
               (else (error 'deriv "不支持的算子" (car expr)))))
        (else (error 'deriv "无效的表达式"))))

;; 核心化简入口
(define (simplify expr)
  (let ((s (simplify1 expr)))
    (if (equal? s expr) s (simplify s))))

;; 单步化简器 (自动适配任何新注册的算子，无需硬编码操作符列表)
(define (simplify1 expr)
  (if (pair? expr)
      (let ((op (car expr))
            (args (map simplify1 (cdr expr))))
        (cond ((lookup-op op) => (lambda (entry) ((op-simplify entry) args)))
              (else (cons op args))))
      expr))

;; 核心不定积分入口
(define (antideriv expr var)
  (cond ((number? expr) `(* ,expr ,var))
        ((eq? expr var) `(/ (expt ,var 2) 2))
        ((pair? expr)
         (cond ((and (lookup-op (car expr)) (op-antideriv (lookup-op (car expr)))) =>
                (lambda (anti-proc) (anti-proc (cdr expr) var)))
               (else (antideriv-single expr var))))
        (else (antideriv-single expr var))))


;; ============================================================================
;; 5. 算子行为声明 (包含新扩展的 8 个算子)
;; ============================================================================

;; 链式法则（Chain Rule）注册器
;; op: 算子符号 (如 'sin)
;; outer-deriv: 算子自身的导数 f'(u) 的生成函数 (接收 u，返回表达式)
(define (register-chain-rule! op outer-deriv)
  (register-op! op
    ;; 自动生成求导行为： f'(u) * u'
    (lambda (args var)
      (let ((u (car args)))
        `(* ,(outer-deriv u) ,(deriv u var))))
    ;; 自动生成默认化简行为： (op args)
    (lambda (args) (cons op args))
    ;; 默认无通用多元积分方法
    #f))


;; ============================================================
;; 算子声明 (利用链式法则注册器高度精炼)
;; ============================================================

;; 基础多元算子 (保持原样)
(register-op! '+
  (lambda (args var) (cons '+ (deriv-args args var)))
  (lambda (args)
    (let ((nz (filter (lambda (a) (not (eqv? a 0))) args)))
      (cond ((null? nz) 0) ((null? (cdr nz)) (car nz)) (else (cons '+ nz)))))
  (lambda (args var) (cons '+ (anti-map args var))))

(register-op! '-
  (lambda (args var) (if (null? (cdr args)) `(- ,(deriv (car args) var)) (cons '- (deriv-args args var))))
  (lambda (args)
    (if (null? (cdr args))
        (if (number? (car args)) (- (car args)) `(- ,(car args)))
        (let ((a (car args)) (b (cadr args)))
          (if (and (number? a) (number? b) (null? (cddr args))) (- a b) (if (eqv? b 0) a (cons '- args))))))
  (lambda (args var) (if (null? (cdr args)) `(- ,(antideriv (car args) var)) (cons '- (anti-map args var)))))

(register-op! '*
  (lambda (args var) (let ((u (car args)) (v (cadr args))) `(+ (* ,u ,(deriv v var)) (* ,v ,(deriv u var)))))
  (lambda (args)
    (let ((n1 (filter (lambda (a) (not (eqv? a 1))) args)) (z (filter (lambda (a) (eqv? a 0)) args)))
      (cond ((not (null? z)) 0) ((null? n1) 1) ((null? (cdr n1)) (car n1)) (else (cons '* n1)))))
  (lambda (args var) (antideriv-times args var)))

(register-op! '/
  (lambda (args var) (let ((u (car args)) (v (cadr args))) `(/ (- (* ,v ,(deriv u var)) (* ,u ,(deriv v var))) (expt ,v 2))))
  (lambda (args) (if (and (= (length args) 2) (eqv? (cadr args) 1)) (car args) (if (and (= (length args) 2) (number? (car args)) (number? (cadr args))) (/ (car args) (cadr args)) (cons '/ args))))
  #f)

(register-op! 'expt
  (lambda (args var) (let ((u (car args)) (n (cadr args))) `(* (* ,n (expt ,u ,(- n 1))) ,(deriv u var))))
  (lambda (args) (let ((base (car args)) (power (cadr args))) (cond ((eqv? power 0) 1) ((eqv? power 1) base) ((eqv? base 1) 1) ((eqv? base 0) 0) (else (cons 'expt args)))))
  #f)

;; ------------------------------------------------------------
;; 一元超越函数：利用 register-chain-rule! 声明其导数内核
;; ------------------------------------------------------------

;; 经典三角与超越函数
(register-chain-rule! 'sin  (lambda (u) `(cos ,u)))
(register-chain-rule! 'cos  (lambda (u) `(- (sin ,u))))
(register-chain-rule! 'tan  (lambda (u) `(/ 1 (expt (cos ,u) 2))))
(register-chain-rule! 'exp  (lambda (u) `(exp ,u)))
(register-chain-rule! 'log  (lambda (u) `(/ 1 ,u)))
(register-chain-rule! 'sqrt (lambda (u) `(/ 1 (* 2 (sqrt ,u)))))
(register-chain-rule! 'atan (lambda (u) `(/ 1 (+ 1 (expt ,u 2)))))

;; 反三角函数
(register-chain-rule! 'asin (lambda (u) `(/ 1 (sqrt (- 1 (expt ,u 2))))))
(register-chain-rule! 'acos (lambda (u) `(- (/ 1 (sqrt (- 1 (expt ,u 2)))))))

;; 双曲函数
(register-chain-rule! 'sinh (lambda (u) `(cosh ,u)))
(register-chain-rule! 'cosh (lambda (u) `(sinh ,u)))
(register-chain-rule! 'tanh (lambda (u) `(/ 1 (expt (cosh ,u) 2))))

;; 倒数三角函数
(register-chain-rule! 'sec  (lambda (u) `(* (sec ,u) (tan ,u))))
(register-chain-rule! 'csc  (lambda (u) `(- (* (csc ,u) (cot ,u)))))
(register-chain-rule! 'cot  (lambda (u) `(- (/ 1 (expt (sin ,u) 2)))))


;; ------------------------------------------------------------
;; 反双曲函数 (Inverse Hyperbolic Functions)
;; ------------------------------------------------------------

;; asinh u 导数: 1 / sqrt(u^2 + 1)
(register-chain-rule! 'asinh (lambda (u) `(/ 1 (sqrt (+ (expt ,u 2) 1)))))

;; acosh u 导数: 1 / sqrt(u^2 - 1)
(register-chain-rule! 'acosh (lambda (u) `(/ 1 (sqrt (- (expt ,u 2) 1)))))

;; atanh u 导数: 1 / (1 - u^2)
(register-chain-rule! 'atanh (lambda (u) `(/ 1 (- 1 (expt ,u 2)))))


;; ------------------------------------------------------------
;; 双曲函数的倒数 (Secant/Cosecant/Cotangent Hyperbolic)
;; ------------------------------------------------------------

;; sech u 导数: -sech(u) * tanh(u)
(register-chain-rule! 'sech  (lambda (u) `(- (* (sech ,u) (tanh ,u)))))

;; csch u 导数: -csch(u) * coth(u)
(register-chain-rule! 'csch  (lambda (u) `(- (* (csch ,u) (coth ,u)))))

;; coth u 导数: -1 / sinh(u)^2
(register-chain-rule! 'coth  (lambda (u) `(- (/ 1 (expt (sinh ,u) 2)))))


;; ------------------------------------------------------------
;; 常用特殊函数 (Special Functions)
;; ------------------------------------------------------------

;; 误差函数 erf u 导数: 2 / sqrt(pi) * e^(-u^2)
(register-chain-rule! 'erf   (lambda (u) `(* (/ 2 (sqrt pi)) (exp (- (expt ,u 2))))))

;; 兰伯特 W 函数 W u (Lambert W Function)
;; 其满足 W(u) * e^W(u) = u，其导数公式为: W(u) / (u * (1 + W(u)))
(register-chain-rule! 'W     (lambda (u) `(/ (W ,u) (* ,u (+ 1 (W ,u))))))


;; ------------------------------------------------------------
;; 固定底数对数 (Fixed-base Logarithms)
;; ------------------------------------------------------------

;; log10 u 导数: 1 / (u * ln 10)
(register-chain-rule! 'log10 (lambda (u) `(/ 1 (* ,u (log 10)))))

;; log2 u 导数: 1 / (u * ln 2)
(register-chain-rule! 'log2  (lambda (u) `(/ 1 (* ,u (log 2)))))


;; ============================================================================
;; 6. 积分代数演算系统 (等价代数解析部分)
;; ============================================================================

(define (anti-map exprs var)   (map (lambda (t) (antideriv t var)) exprs))

;; 乘积积分处理器
(define (antideriv-times args var)
  (let* ((result (extract-constants args))
         (const-factor (car result)) 
         (non-consts (cdr result)))  
    (cond ((null? non-consts) `(* ,(apply * (or args '(1))) ,var)) 
          ((and (null? (cdr non-consts)) (antideriv (car non-consts) var)) =>
           (lambda (anti) `(* ,const-factor ,anti)))
          ((null? (cddr non-consts))
           (or (integrate-product (car non-consts) (cadr non-consts) var)
               (integrate-product (cadr non-consts) (car non-consts) var)))
          (else #f))))

;; 构造线性表达式: a * var + b
(define (make-linear a var b)
  (cond ((and (eqv? a 1) (eqv? b 0)) var)
        ((eqv? b 0) `(* ,a ,var))
        ((eqv? a 1) `(+ ,var ,b))
        (else `(+ (* ,a ,var) ,b))))

;; 常数缩放
(define (scale-result base a)
  (if (= 1 a) base `(/ ,base ,a)))

;; 从表达式中提取线性函数特征 (判断并提取 a*x + b 形式)
(define (extract-linear expr var)
  (cond ((eq? expr var) '(1 . 0))
        ((pair? expr)
         (case (car expr)
           ((*) (let ((a (cadr expr)) (b (caddr expr)))
                  (or (and (number? a) (eq? b var) (cons a 0))
                      (and (number? b) (eq? a var) (cons b 0)))))
            ((+) (let* ((terms (cdr expr))
                        (consts (filter number? terms))
                        (vars (filter (lambda (t) (not (number? t))) terms)))
                   (and (= (length vars) 1)
                        (<= (length consts) 1)
                        (cond
                          ((extract-linear (car vars) var) =>
                           (lambda (lin)
                             (cons (car lin)
                                   (if (null? consts) 0 (car consts)))))
                          (else #f)))))
           ((-) (if (null? (cddr expr))
                    (let ((inner (cadr expr)))
                      (or (and (pair? inner) (eq? (car inner) '*) (number? (cadr inner)) (eq? (caddr inner) var)
                               (cons (- (cadr inner)) 0))
                          (and (eq? inner var) '(-1 . 0))))
                    (extract-linear `(+ ,(cadr expr) (- ,(caddr expr))) var)))
           (else #f)))
        (else #f)))

;; 辅助判定
(define (power-of-x expr var)
  (and (pair? expr) (eq? (car expr) 'expt) (eq? (cadr expr) var)
       (number? (caddr expr)) (>= (caddr expr) 1) (caddr expr)))

(define (var? x var) (eq? x var))

(define (power-of-x-at-least-2 x var)
  (and (pair? x) (eq? (car x) 'expt) (eq? (cadr x) var)
       (number? (caddr x)) (>= (caddr x) 2)))

(define (extract-n x var) (if (var? x var) 1 (caddr x)))

;; 经典分部积分公式归纳
(define (int-xn-sin n a b var)
  (if (= n 0)
      (scale-result `(- (cos ,(make-linear a var b))) a)
      (let* ((xn `(expt ,var ,n))
             (u  `(cos ,(make-linear a var b)))
             (term1 (scale-result `(* (- ,xn) ,u) a))
             (rest  (int-xn-cos (- n 1) a b var)))
        `(+ ,term1 ,(scale-result `(* ,n ,rest) a)))))

(define (int-xn-cos n a b var)
  (if (= n 0)
      (scale-result `(sin ,(make-linear a var b)) a)
      (let* ((xn `(expt ,var ,n))
             (v  `(sin ,(make-linear a var b)))
             (term1 (scale-result `(* ,xn ,v) a))
             (rest  (int-xn-sin (- n 1) a b var)))
        `(- ,term1 ,(scale-result `(* ,n ,rest) a)))))

(define (int-xn-exp n a b var)
  (if (= n 0)
      (scale-result `(exp ,(make-linear a var b)) a)
      (let* ((xn `(expt ,var ,n))
             (e  `(exp ,(make-linear a var b)))
             (term1 (scale-result `(* ,xn ,e) a))
             (rest  (int-xn-exp (- n 1) a b var)))
        `(- ,term1 ,(scale-result `(* ,n ,rest) a)))))

;; 识别 arctan 积分模式
(define (try-atan-form num den var)
  (and (pair? den) (eq? (car den) '+) (= (length (cdr den)) 2)
       (let* ((terms (cdr den)) (sq (car terms)) (c (cadr terms)))
         (and (pair? sq) (eq? (car sq) 'expt) (eq? (cadr sq) var) (= (caddr sq) 2)
              (number? c) (> c 0)
              (let ((a (sqrt c)))
                `(* (/ ,num ,a) (atan (/ ,var ,a))))))))

(define (try-int a b func-sym int-func var)
  (and (pair? b) (eq? (car b) func-sym)
       (cond ((extract-linear (cadr b) var) =>
              (lambda (lin) (int-func (extract-n a var) (car lin) (cdr lin) var)))
             (else #f))))

;; 积积分主逻辑
(define (integrate-product a b var)
  (or (try-int a b 'sin int-xn-sin var)
      (try-int a b 'cos int-xn-cos var)
      (try-int a b 'exp int-xn-exp var)
      (and (or (var? a var) (power-of-x-at-least-2 a var))
           (pair? b) (eq? (car b) 'log) (eq? (cadr b) var)
           (let ((n (extract-n a var)))
             `(/ (* (expt ,var ,(+ n 1)) (- (* ,(+ n 1) (log ,var)) 1))
                 ,(expt (+ n 1) 2))))))

(define (handle-unary expr op handler var)
  (and (pair? expr) (eq? (car expr) op)
       (cond ((extract-linear (cadr expr) var) => (lambda (lin) (handler (car lin) (cdr lin))))
             (else #f))))

(define (handle-log-unary expr var)
  (and (pair? expr) (eq? (car expr) 'tan)
       (cond ((extract-linear (cadr expr) var) =>
              (lambda (lin)
                (let* ((a (car lin)) (b (cdr lin)))
                  (if (= a 0)
                      `(- (log (abs (cos ,var))))
                      `(- (/ (log (abs (cos ,(make-linear a var b)))) ,a)))))
              (else #f)))))

(define (anti-rational num den var)
  (cond ((extract-linear den var) =>
         (lambda (lin)
           (let ((a (car lin)) (b (cdr lin)))
             (cond ((and (eqv? num 1) (eqv? a 1)) `(log ,(make-linear a var b)))
                   ((eqv? a 1) `(* ,num (log ,(make-linear a var b))))
                   (else `(* ,num (/ (log ,(make-linear a var b)) ,a)))))))
        (else (try-atan-form num den var))))

(define (handle-x-over-linear expr var)
  (cond ((extract-linear (caddr expr) var) =>
         (lambda (lin)
           (let ((a (car lin)) (b (cdr lin)))
             `(/ (- ,(make-linear a var b) (* ,b (log ,(make-linear a var b))))
                 ,(* a a)))))
        (else #f)))

;; 单项积分主分发 (在此处添加新 Transcendental 函数的积分换元支持)
(define (antideriv-single expr var)
  (define (handle-sin a b) (scale-result `(- (cos ,(make-linear a var b))) a))
  (define (handle-cos a b) (scale-result `(sin ,(make-linear a var b)) a))
  (define (handle-exp a b) (scale-result `(exp ,(make-linear a var b)) a))
  (define (handle-sqrt a b) `(/ (* 2 (expt ,(make-linear a var b) 1.5)) ,(* 3 a)))
  ;; 新增双曲函数解析换元
  (define (handle-sinh a b) (scale-result `(cosh ,(make-linear a var b)) a))
  (define (handle-cosh a b) (scale-result `(sinh ,(make-linear a var b)) a))

  (and (pair? expr)
       (case (car expr)
         ((expt)
          (let ((base (cadr expr)) (power (caddr expr)))
            (and (number? power)
                 (or (and (eqv? power 2) (pair? base) (eq? (cadr base) var)
                          (case (car base)
                            ((sin) `(- (/ ,var 2) (/ (sin (* 2 ,var)) 4)))
                            ((cos) `(+ (/ ,var 2) (/ (sin (* 2 ,var)) 4)))
                            (else #f)))
                     (cond ((extract-linear base var) =>
                            (lambda (lin)
                              (let ((a (car lin)) (b (cdr lin)))
                                (if (eqv? power -1)
                                    `(/ (log ,(make-linear a var b)) ,a)
                                    `(/ (expt ,(make-linear a var b) ,(+ power 1)) (* ,a ,(+ power 1))))))))))))
         ((sin)  (handle-unary expr 'sin handle-sin var))
         ((cos)  (handle-unary expr 'cos handle-cos var))
         ((tan)  (handle-log-unary expr var))
         ((exp)  (handle-unary expr 'exp handle-exp var))
         ((sqrt) (handle-unary expr 'sqrt handle-sqrt var))
         ;; 新增一元积分分派支持
         ((sinh) (handle-unary expr 'sinh handle-sinh var))
         ((cosh) (handle-unary expr 'cosh handle-cosh var))
         ;; 反三角函数基础单项积分公式：∫ arcsin(x)dx = x*arcsin(x) + sqrt(1-x^2)
         ((asin) (and (eq? (cadr expr) var) `(+ (* ,var (asin ,var)) (sqrt (- 1 (expt ,var 2))))))
         ((acos) (and (eq? (cadr expr) var) `(- (* ,var (acos ,var)) (sqrt (- 1 (expt ,var 2))))))
         ((log)  (and (eq? (cadr expr) var) `(- (* ,var (log ,var)) ,var)))
         ((/)    (let ((num (cadr expr)) (den (caddr expr)))
                   (or (and (number? num) (anti-rational num den var))
                       (and (eq? num var) (handle-x-over-linear expr var)))))
         (else #f))))

(define (extract-constants args)
  (let loop ((args args) (consts '()) (vars '()))
    (if (null? args)
        (cons (apply * (if (null? consts) '(1) consts)) vars)
        (let ((a (car args)))
          (if (number? a) (loop (cdr args) (cons a consts) vars)
              (loop (cdr args) consts (cons a vars)))))))


;; ============================================================================
;; 7. 数值与极限计算 (已扩展 *math-ops* 的映射支持)
;; ============================================================================

;; 极限逼近计算
(define (limit expr var val)
  (let loop ((h 0.1) (i 0))
    (if (>= i 1000) (error 'limit "极限不收敛")
        (let* ((f+h (safe-subst expr var (+ val h)))
               (f-h (safe-subst expr var (- val h)))
               (diff (if (or (eq? f+h '*singular*) (eq? f-h '*singular*)) 1e5 (abs (- f+h f-h)))))
          (if (< diff 1e-10) (/ (+ f+h f-h) 2.0)
              (loop (/ h 2.0) (+ i 1)))))))

;; 安全代入：遇到异常返回 *singular* 标记
(define (safe-subst expr var val)
  (with-exception-handler
    (lambda (exn) '*singular*)
    (lambda () (subst-limit expr var val))))

;; 辅助代入求极限
(define (subst-limit expr var val)
  (cond ((number? expr) expr)
        ((symbol? expr) (if (eq? expr var) val expr))
        ((pair? expr)
         (let ((op (car expr))
               (sub-args (map (lambda (a) (subst-limit a var val)) (cdr expr))))
             (apply (or (get-op op) (lambda xs (error 'limit "不支持的算子"))) sub-args)))
        (else expr)))

(define (factorial n) (if (< n 2) 1 (* n (factorial (- n 1)))))

;; 泰勒展开
(define (taylor f-expr var at n)
  (let loop ((k 0) (terms '()))
    (if (> k n)
        (if (null? terms) 0 (cons '+ (reverse terms)))
        (loop (+ k 1)
              (cons `(* (/ 1 ,(factorial k)) (* (expt (- ,var ,at) ,k) ,(deriv-n-k f-expr var k)))
                    terms)))))

(define (deriv-n-k expr var k)
  (if (= k 0) expr (deriv-n-k (deriv expr var) var (- k 1))))

;; 辅助代入求值
(define (eval-subst expr var val)
  (with-exception-handler
    (lambda (exn) #f)
    (lambda ()
      (cond ((number? expr) expr)
            ((symbol? expr) (if (eq? expr var) val expr))
            ((pair? expr)
             (let ((op (car expr))
                   (args (map (lambda (a) (eval-subst a var val)) (cdr expr))))
               (apply (or (get-op op) (lambda xs (error 'eval-subst "不支持的算子"))) args)))
            (else expr)))))

;; 定积分求值：处理反函数在端点奇异的情况
(define (definite-integral expr var a b)
  (cond ((antideriv expr var) =>
         (lambda (f)
           (let ((fb (eval-subst f var b))
                 (fa (eval-subst f var a)))
             (if (or (not fa) (not fb))
                 ;; 端点奇异：用极限逼近中间点
                 (let ((mid (/ (+ a b) 2.0)))
                   (- (eval-subst f var b) (eval-subst f var mid)))
                 (- fb fa)))))
         (else (error 'definite-integral "无法求得该表达式的不定积分形式" expr))))

;; ============================================================
;; 8. 数值过程映射表 (追加 8 个新算子的数值求值绑定)
;; ============================================================

(define *math-ops*
  `((+ . ,+) (- . ,-) (* . ,*) (/ . ,/) (expt . ,expt) (sqrt . ,sqrt)
    (sin . ,sin) (cos . ,cos) (tan . ,tan) (atan . ,atan) (log . ,log) (exp . ,exp)
    (asin . ,asin) (acos . ,acos) (sinh . ,sinh) (cosh . ,cosh) (tanh . ,tanh)
    (sec . ,sec) (csc . ,csc) (cot . ,cot)
    ;; 追加的高级函数数值映射
    (pi . ,(lambda () pi)) ; 将 pi 包装为无参过程
    (asinh . ,asinh) (acosh . ,acosh) (atanh . ,atanh)
    (sech . ,sech) (csch . ,csch) (coth . ,coth)
    (erf . ,erf) (W . ,W)
    (log10 . ,log10) (log2 . ,log2)))

(define (get-op op)
  (cond ((assq op *math-ops*) => cdr) (else #f)))

;; ============================================================
;; 9. 格式化测试辅助输出
;; ============================================================

(define (show-deriv expr var)
  (show (string-append "d/d" (symbol->string var)) (simplify (deriv expr var))))

(define (show-antideriv expr var)
  (cond ((antideriv expr var) => (lambda (res) (show (string-append "∫" (symbol->string var)) res)))
        (else (show (string-append "∫" (symbol->string var)) "#f"))))

(define (show-taylor f-expr var at n)
  (display "Taylor P") (display n) (display " of ")
  (display f-expr) (display " at ") (display var) (display "=") (display at)
  (display " = ") (display (taylor f-expr var at n)) (newline))

(display "calculus.scm loaded.") (newline)
