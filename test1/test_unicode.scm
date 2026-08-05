;; ================================================================
;; char/string 对 UTF-8 与 Unicode 的全方位支持测试
;; 覆盖: ASCII, BMP, 辅助平面, 组合字符, 编解码边界
;; ================================================================

(test-begin "Unicode 码点范围测试")

;; 1.1 不同平面的字符
(test-equal "ASCII (U+0041)"  (char->integer #\A) 65)
(test-equal "Latin-1 (U+00E9)" (char->integer #\é) 233)
(test-equal "CJK (U+4E2D)"    (char->integer #\中) 20013)
(test-equal "emoji (U+1F600)" (char->integer #\😀) 128512)
(test-equal "emoji globe (U+1F30D)" (char->integer #\🌍) 127757)
(test-equal "math script (U+1D400)" (integer->char 119808) #\𝐀)

;; 1.2 整数 ↔ 字符 往返
(test-equal "roundtrip ASCII"     (char->integer (integer->char 65)) 65)
(test-equal "roundtrip CJK"       (char->integer (integer->char 20013)) 20013)
(test-equal "roundtrip emoji"     (char->integer (integer->char 128512)) 128512)
(test-equal "roundtrip supplement" (char->integer (integer->char 119808)) 119808)

(test-end "Unicode 码点范围测试")

;; ================================================================
(test-begin "UTF-8 编解码往返测试")

;; 2.1 基本串
(define u8-ascii  (string->utf8 "ABC"))
(define u8-cjk    (string->utf8 "中文测试"))
(define u8-jp     (string->utf8 "日本語"))
(define u8-ru     (string->utf8 "Привет"))
(define u8-emoji  (string->utf8 "😀🌍"))
(define u8-math   (string->utf8 "𝐀𝐁𝐂"))  ;; 数学粗体字母

(test-equal "ASCII roundtrip"       (utf8->string u8-ascii) "ABC")
(test-equal "CJK roundtrip"         (utf8->string u8-cjk) "中文测试")
(test-equal "Japanese roundtrip"    (utf8->string u8-jp) "日本語")
(test-equal "Russian roundtrip"     (utf8->string u8-ru) "Привет")
(test-equal "Emoji roundtrip"       (utf8->string u8-emoji) "😀🌍")
(test-equal "Math bold roundtrip"   (utf8->string u8-math) "𝐀𝐁𝐂")

;; 2.2 UTF-8 字节验证: CJK 汉字通常是 3 字节
(test-equal "UTF-8 length of '中'"  (bytevector-length u8-cjk) 12)  ;; 4字 × 3字节
(test-equal "UTF-8 length emoji"   (bytevector-length u8-emoji) 8)  ;; 2字 × 4字节
(test-equal "UTF-8 length math"    (bytevector-length u8-math) 12)  ;; 3字 × 4字节

;; 2.3 编码具体字节值验证
(test-equal "UTF-8 bytes of A"     (string->utf8 "A") (bytevector 65))
(test-equal "UTF-8 bytes of é"     (string->utf8 "é") (bytevector 195 169))
(test-equal "UTF-8 bytes of 中"    (string->utf8 "中") (bytevector 228 184 173))
(test-equal "UTF-8 bytes of 😀"    (string->utf8 "😀") (bytevector 240 159 152 128))

;; 2.4 从字节解码
(test-equal "decode ASCII"    (utf8->string (bytevector 65 66 67)) "ABC")
(test-equal "decode é"        (utf8->string (bytevector 195 169)) "é")
(test-equal "decode 中"       (utf8->string (bytevector 228 184 173)) "中")
(test-equal "decode 😀"       (utf8->string (bytevector 240 159 152 128)) "😀")

;; 2.5 UTF-8 子范围切片
(test-equal "utf8 slice start" (utf8->string (string->utf8 "中文") 0 3) "中")
(test-equal "utf8 slice end"   (utf8->string (string->utf8 "中文") 3 6) "文")

(test-end "UTF-8 编解码往返测试")

;; ============================================================
(test-begin "string-length 字符语义验证")

(test-equal "ascii str"    (string-length "Hello") 5)
(test-equal "latin-1 str"  (string-length "café") 4)
(test-equal "cjk str"      (string-length "中文測試") 4)
(test-equal "emoji str"    (string-length "😀🌍") 2)
(display "mixed str length: ") (display (string-length "Hi你好")) (newline)
(test-equal "math bold"    (string-length "𝐀𝐁") 2)
(test-equal "supplement plane" (string-length "𐀀𓀀") 2)  ;; U+10000, U+13000

(test-end "string-length 字符语义验证")

;; ============================================================
(test-begin "组合字符与变体序列")

;; 3.1 组合字符（Combining Diacritical Marks）
;; 用 char->integer/integer->char 构建
(define combining-acute (integer->char 769))  ;; U+0301
(define combo-str (string-append "e" (string combining-acute)))
(display "composed length: ") (display (string-length "é")) (newline)
(display "decomposed length: ") (display (string-length combo-str)) (newline)
(display "decomposed display: ") (display combo-str) (newline)

;; 3.2 变体选择器（Variation Selectors）
(define vs16 (integer->char 65039))  ;; U+FE0F
(define vs16-str (string-append "☹" (string vs16)))
(display "VS16 length: ") (display (string-length vs16-str)) (newline)
(display "VS16 display: ") (display vs16-str) (newline)

;; 3.3 ZWJ 序列（Zero-Width Joiner）
(define zwj-char (integer->char 8205))  ;; U+200D
(define zwj-str (string-append "👨" (string zwj-char) "👩" (string zwj-char) "👧" (string zwj-char) "👦"))
(display "ZWJ seq length: ") (display (string-length zwj-str)) (newline)
(display "ZWJ seq display: ") (display zwj-str) (newline)

;; 3.4 国旗序列（Regional Indicator Symbol）
(define flag-str "🇨🇳")  ;; 中国国旗
(display "flag length: ") (display (string-length flag-str)) (newline)
(display "flag display: ") (display flag-str) (newline)

(test-end "组合字符与变体序列")

;; ============================================================
(test-begin "string 引用/修改 多语种")

;; 4.1 string-ref 通过字符索引
(test-equal "ref ascii"    (string-ref "Hello" 0) #\H)
(test-equal "ref cjk"      (string-ref "中文" 1) #\文)
(test-equal "ref emoji"    (string-ref "😀🌍" 1) #\🌍)
(test-equal "ref math"     (string-ref "𝐀𝐁" 1) #\𝐁)

;; 4.2 substring 字符索引语义
(test-equal "substring cjk"    (substring "中文測試" 0 2) "中文")
(test-equal "substring emoji"  (substring "😀🌍" 1 2) "🌍")
(test-equal "substring mixed"  (substring "Hi你好" 2 4) "你好")
(test-equal "substring whole"  (substring "Hello" 0 5) "Hello")

;; 4.3 string-set! 替换
(define s-mod (string-copy "abcdef"))
(string-set! s-mod 2 #\中)
(test-equal "string-set! cjk" s-mod "ab中def")
(string-set! s-mod 0 #\😀)
(test-equal "string-set! emoji" s-mod "😀b中def")

;; 4.4 string-fill! 
(define s-fill (string-copy "12345"))
(string-fill! s-fill #\文)
(test-equal "string-fill! cjk" s-fill "文文文文文")

(define s-fill2 (string-copy "abcde"))
(string-fill! s-fill2 #\😀)
(test-equal "string-fill! emoji" s-fill2 "😀😀😀😀😀")

(test-end "string 引用/修改 多语种")

;; ============================================================
(test-begin "string 比较 多语种")

(test-equal "cjk =  中文" (string=? "中文" "中文") #t)
(test-equal "cjk =  false" (string=? "中文" "日文") #f)
(test-equal "emoji =" (string=? "😀😀" "😀😀") #t)
(test-equal "mix =" (string=? "Hello中文" "Hello中文") #t)

;; 大小写不敏感（拉丁可用，CJK 无大小写）
(test-equal "ci latin"  (string-ci=? "Hello" "hello") #t)
(test-equal "ci cyrillic" (string-ci=? "ПРИВЕТ" "привет") #t)
(test-equal "ci cjk (same)" (string-ci=? "中文" "中文") #t)
;; CJK 字符大小写不敏感比较 = 直接相等比较
(test-equal "ci cjk (diff)" (string-ci=? "中文" "日文") #f)

;; 字典序（Unicode 码点序）
(test-equal "< cjk"  (string<? "中文" "日文") #t)   ;; 中(20013) < 日(26085)
(test-equal "< emoji" (string>? "😀" "😁") #f)       ;; 😀(128512) < 😁(128513)
(test-equal "< mixed" (string<? "A" "中") #t)        ;; 65 < 20013

(test-end "string 比较 多语种")

;; ============================================================
(test-begin "string 转换函数 多语种")

;; string->list
(test-equal "->list cjk" (string->list "中文") '(#\中 #\文))
(test-equal "->list emoji" (string->list "😀🌍") '(#\😀 #\🌍))
(test-equal "->list mixed" (string->list "A中") '(#\A #\中))

;; list->string
(test-equal "list->string cjk" (list->string '(#\中 #\文)) "中文")
(test-equal "list->string emoji" (list->string '(#\😀 #\🌍)) "😀🌍")

;; string-copy
(test-equal "copy cjk" (string-copy "中文測試") "中文測試")
(test-equal "copy emoji" (string-copy "😀🌍") "😀🌍")
(test-equal "copy slice" (string-copy "中文測試" 0 2) "中文")

;; ->string
(test-equal "->string cjk" (->string "中文") "中文")
(test-equal "->string emoji" (->string "😀🌍") "😀🌍")

;; object->string
(test-equal "object->string cjk" (object->string "中文") "\"中文\"")

;; string-append
(test-equal "append cjk" (string-append "中文" "測試") "中文測試")
(test-equal "append emoji" (string-append "😀" "🌍") "😀🌍")
(test-equal "append mixed" (string-append "Hi" "你好") "Hi你好")

;; string->number / number->string 不影响多语种（不涉及字符）
(test-equal "num<->str" (string->number (number->string 255 16) 16) 255)

(test-end "string 转换函数 多语种")

;; ============================================================
(test-begin "string 搜索 多语种")

;; string-contains
(test-equal "contains cjk" (string-contains "中文測試" "測試") 2)
(test-equal "contains emoji" (string-contains "😀🌍🌎" "🌍") 1)
(test-equal "contains ascii" (string-contains "Hello中文" "中文") 5)
(test-equal "contains not found" (string-contains "中文" "英文") #f)

;; string-prefix?
(test-equal "prefix cjk" (string-prefix? "中文" "中文測試") #t)
(test-equal "prefix emoji" (string-prefix? "😀" "😀🌍") #t)

;; string-suffix?
(test-equal "suffix cjk" (string-suffix? "測試" "中文測試") #t)
(test-equal "suffix emoji" (string-suffix? "🌍" "😀🌍") #t)

;; string-split
(define splitted (string-split "中,日,英" ","))
(test-equal "split cjk count" (length splitted) 3)
(test-equal "split cjk first" (car splitted) "中")
(test-equal "split cjk last"  (car (cdr (cdr splitted))) "英")

;; string-join
(test-equal "join cjk" (string-join '("中" "日" "英") "-") "中-日-英")

;; string-trim
(test-equal "trim ascii" (string-trim-both "  hello  ") "hello")
(test-equal "trim cjk" (string-trim-both "  中文  ") "中文")

(test-end "string 搜索 多语种")

;; ============================================================
(test-begin "char 分类谓词 Unicode")

;; Latin
(test-equal "latin alpha" (char-alphabetic? #\a) #t)
(test-equal "latin é alpha" (char-alphabetic? #\é) #t)
(test-equal "latin upper" (char-upper-case? #\A) #t)
(test-equal "latin lower" (char-lower-case? #\z) #t)

;; CJK
(test-equal "cjk alpha" (char-alphabetic? #\中) #t)
(test-equal "cjk upper?" (char-upper-case? #\中) #f)
(test-equal "cjk lower?" (char-lower-case? #\中) #f)
(test-equal "cjk numeric?" (char-numeric? #\中) #f)

;; 全角数字
(test-equal "fullwidth digit" (char-numeric? #\３) #t)  ;; U+FF13

;; 全角字母
(test-equal "fullwidth alpha" (char-alphabetic? #\Ａ) #t)  ;; U+FF21
(test-equal "fullwidth upper" (char-upper-case? #\Ａ) #t)  ;; Python 认为全角Ａ是大写

;; Emoji
(test-equal "emoji alpha?" (char-alphabetic? #\😀) #f)  ;; emoji 不是字母
(test-equal "emoji numeric?" (char-numeric? #\😀) #f)
(test-equal "emoji whitespace?" (char-whitespace? #\😀) #f)

;; 数学符号
(test-equal "math alpha (bold A)" (char-alphabetic? #\𝐀) #t)  ;; U+1D400, isalpha()=True
(test-equal "math upper" (char-upper-case? #\𝐀) #t)

;; 标点符号
(test-equal "punctuation not alpha" (char-alphabetic? #\,) #f)
(test-equal "punctuation not digit" (char-numeric? #\.) #f)

;; 空白字符
(test-equal "space whitespace" (char-whitespace? #\space) #t)
(test-equal "tab whitespace" (char-whitespace? #\tab) #t)
(test-equal "nbsp whitespace" (char-whitespace? (integer->char 160)) #t)  ;; U+00A0 不换行空格

;; 控制字符
(test-equal "null not alpha" (char-alphabetic? #\nul) #f)
(display "nbsp test: ") (display (char-whitespace? (integer->char 160))) (newline)

(test-end "char 分类谓词 Unicode")

;; ============================================================
(test-begin "string 大小写转换 Unicode")

;; 拉丁
(test-equal "upcase latin" (string-upcase "hello") "HELLO")
(test-equal "downcase latin" (string-downcase "HELLO") "hello")

;; 西里尔
(test-equal "upcase cyrillic" (string-upcase "привет") "ПРИВЕТ")
(test-equal "downcase cyrillic" (string-downcase "ПРИВЕТ") "привет")

;; CJK (大小写不变)
(test-equal "upcase cjk invariant" (string-upcase "中文") "中文")
(test-equal "downcase cjk invariant" (string-downcase "中文") "中文")

;; Emoji (大小写不变)
(test-equal "upcase emoji invariant" (string-upcase "😀🌍") "😀🌍")

;; 混合
(test-equal "upcase mixed" (string-upcase "Hello中文") "HELLO中文")

;; foldcase (更强力的大小写折叠)
(test-equal "foldcase latin" (string-foldcase "HELLO") "hello")
(test-equal "foldcase cyrillic" (string-foldcase "ПРИВЕТ") "привет")
(test-equal "foldcase ß" (string-foldcase "STRASSE") "strasse")
(test-equal "foldcase cjk invariant" (string-foldcase "中文") "中文")

;; titlecase (标题大小写)
(test-equal "titlecase latin" (string-titlecase "hello world") "Hello World")
(test-equal "titlecase cyrillic" (string-titlecase "привет мир") "Привет Мир")
(test-equal "titlecase cjk" (string-titlecase "中文测试") "中文测试")

(test-end "string 大小写转换 Unicode")

;; ============================================================
(test-begin "bytevector ↔ string 边界")

;; 空串
(test-equal "empty string->utf8" (bytevector-length (string->utf8 "")) 0)
(test-equal "empty utf8->string" (utf8->string (bytevector)) "")

;; 单字节
(test-equal "single byte" (utf8->string (bytevector 65)) "A")

;; 非法 UTF-8 序列 — 现在已转为 SchemeError，with-exception-handler 可 catch
(display "invalid UTF-8 now raises SchemeError: ")
(call/cc (lambda (k)
  (with-exception-handler
    (lambda (e) (display "caught (correct)\n") (k #t))
    (lambda () (utf8->string (bytevector 255 254)) (display "NOT CAUGHT\n")))))

;; 3字节序列验证
(test-equal "3-byte seq" (utf8->string (bytevector 228 184 173)) "中")

;; 4字节序列验证 (emoji)
(test-equal "4-byte seq" (utf8->string (bytevector 240 159 152 128)) "😀")

(test-end "bytevector ↔ string 边界")

;; ============================================================
(test-begin "端口读取多字节字符")

;; open-input-string 多语种
(define port-cn (open-input-string "中文測試"))
(test-equal "read from cjk port" (read-char port-cn) #\中)
(test-equal "read 2nd char"      (read-char port-cn) #\文)
(test-equal "read 3rd char"      (read-char port-cn) #\測)
(test-equal "peek char"          (peek-char port-cn) #\試)
(test-equal "peek same"         (read-char port-cn) #\試)
(close-port port-cn)

;; emoji 端口
(define port-emoji (open-input-string "😀🌍"))
(test-equal "read emoji"       (read-char port-emoji) #\😀)
(test-equal "read 2nd emoji"   (read-char port-emoji) #\🌍)
(test-equal "eof after emoji"  (read-char port-emoji) (eof-object))
(close-port port-emoji)

;; 混合端口
(define port-mix (open-input-string "A中😀"))
(test-equal "read A"    (read-char port-mix) #\A)
(test-equal "read 中"   (read-char port-mix) #\中)
(test-equal "read 😀"   (read-char port-mix) #\😀)
(close-port port-mix)

;; read-line 多语种
(define port-ln (open-input-string "第一行\n第二行"))
(test-equal "read-line 1" (read-line port-ln) "第一行")
(test-equal "read-line 2" (read-line port-ln) "第二行")
(close-port port-ln)

;; 二进制端口读多字节
(define port-u8 (open-input-bytevector (string->utf8 "中文")))
(test-equal "read-u8 partial" (read-u8 port-u8) 228)  ;; '中'的第一个字节
(test-equal "read-u8 pos2" (read-u8 port-u8) 184)
(test-equal "read-u8 pos3" (read-u8 port-u8) 173)
(close-port port-u8)

(test-end "端口读取多字节字符")

;; ============================================================
(test-begin "特殊 Unicode 字符")

;; 零宽空格 (U+200B)
(define zwsp-char (integer->char 8203))  ;; U+200B
(define zwsp-str (string zwsp-char))
(define zwsp-bv (string->utf8 zwsp-str))
(test-equal "zero-width space length" (bytevector-length zwsp-bv) 3)
(test-equal "zero-width space" (utf8->string zwsp-bv) zwsp-str)

;; 替代区域 (U+D800~U+DFFF): 现在转为 SchemeError
(display "surrogate rejection test: ")
(call/cc (lambda (k)
  (with-exception-handler
    (lambda (e) (display "SchemeError caught (correct)\n") (k #t))
    (lambda () (integer->char 55296) (display "NOT REJECTED\n")))))
;; 最大码点 U+10FFFF
(define max-char (integer->char 1114111))
(display "max codepoint (U+10FFFF): ") (display max-char) (newline)

;; BOM (U+FEFF) — 零宽不换行空格
(define bom-char (integer->char 65279))  ;; U+FEFF
(display "BOM display: ") (display bom-char) (newline)

(test-end "特殊 Unicode 字符")

;; ============================================================
(test-begin "char 互操作 (char->integer → integer->char)")

(do ((i 0 (+ i 1))) ((= i 256))
  (let ((c (integer->char i)))
    (if (not (char=? c (integer->char (char->integer c))))
      (begin (display "FAIL at ") (display i) (newline)))))
(display "ASCII/Latin-1 roundtrip 0-255: OK\n")

;; BMP 抽样往返
(define bmp-samples '(65 20013 12354 1072 233 955 946 160 169 8482 8592 9731 9835))
(for-each (lambda (cp)
  (let ((c (integer->char cp)))
    (if (not (= cp (char->integer c)))
      (begin (display "BMP FAIL at ") (display cp) (newline)))))
  bmp-samples)
(display "BMP sampling roundtrip: OK\n")

;; 辅助平面抽样 (U+10000 ~ U+10FFFF)
(define smp-samples '(65536 119808 127757 128512 129302 131072 137994 173824))
(for-each (lambda (cp)
  (let ((c (integer->char cp)))
    (if (not (= cp (char->integer c)))
      (begin (display "SMP FAIL at ") (display cp) (newline)))))
  smp-samples)
(display "SMP sampling roundtrip: OK\n")

(test-end "char 互操作")

(display "\n=== ALL UNICODE/UTF-8 TESTS DONE ===\n")
