;; ==========================================
;; 字符串扩展操作 多语种支持测试
;; ==========================================

;; 1. string-split / string-join
(test-begin "string-split/join 多语种")
(test-equal "split 中日英" '("中" "日" "英") (string-split "中,日,英" ","))
(test-equal "join あいう" "あ,い,う" (string-join '("あ" "い" "う") ","))
(test-end "string-split/join 多语种")

;; 2. string-trim / string-pad
(test-begin "string-trim/pad 多语种")
(test-equal "trim" (string-trim-both "  中文  ") "中文")
(test-equal "pad left" (string-pad "文" 5 #\中) "中中中中文")
(test-equal "pad right" (string-pad-right "文" 5 #\中) "文中中中中")
(test-end "string-trim/pad 多语种")

;; 3. string-index / string-count
(test-begin "string-index/count 多语种")
(test-equal "index 测" (string-index "中文测试" (lambda (c) (char=? c #\测))) 2)
(test-equal "count 你好" 4 (string-count "你好你好" char-alphabetic?))
(test-equal "any 中文" #t (string-any char-alphabetic? "123中文"))
(test-equal "every 中文" #t (string-every char-alphabetic? "中文测试"))
(test-end "string-index/count 多语种")

;; 4. string-reverse
(test-begin "string-reverse 多语种")
(test-equal "reverse 中文" (string-reverse "中文測試") "試測文中")
(test-equal "reverse あいう" (string-reverse "あいうえお") "おえういあ")
(test-end "string-reverse 多语种")

;; 5. string-map / string-for-each
(test-begin "string-map/for-each 多语种")
(test-equal "map upcase 俄语" "ПРИВЕТ" (string-map char-upcase "привет"))
(test-equal "map downcase 俄语" "привет" (string-map char-downcase "ПРИВЕТ"))
(define acc "")
(string-for-each (lambda (c) (set! acc (string-append acc (string c)))) "日本語")
(test-equal "for-each 日语" acc "日本語")
(test-end "string-map/for-each 多语种")

;; 6. 字符串长度 — 确认是字符数而非字节数
(test-begin "string-length 字符语义")
(test-equal "len 中文 4字" (string-length "你好世界") 4)
(test-equal "len 日语 5字" (string-length "こんにちは") 5)
(test-equal "len 俄语 10字" (string-length "Привет мир") 10)
(test-equal "len emoji" (string-length "😀🌍") 2)   ;; 代理对，Python 算 2 个
(test-end "string-length 字符语义")

;; 7. string->utf8 / utf8->string 编解码
(test-begin "utf8 编解码")
(test-equal "utf8 roundtrip 中文" (utf8->string (string->utf8 "中文")) "中文")
(test-equal "utf8 roundtrip 日语" (utf8->string (string->utf8 "日本語")) "日本語")
(test-equal "utf8 roundtrip 俄语" (utf8->string (string->utf8 "Привет")) "Привет")
(test-equal "utf8 roundtrip emoji" (utf8->string (string->utf8 "😀🌍")) "😀🌍")
(test-equal "utf8 编码 中" (string->utf8 "中") (bytevector 228 184 173))
(test-end "utf8 编解码")

;; 8. string-contains / prefix / suffix
(test-begin "string-contains/prefix/suffix 多语种")
(test-equal "contains 世界" (string-contains "你好世界" "世界") 2)
(test-equal "prefix 你好" (string-prefix? "你好" "你好世界") #t)
(test-equal "suffix 世界" (string-suffix? "世界" "你好世界") #t)
(test-equal "contains 日语" (string-contains "あいうえお" "うえ") 2)
(test-end "string-contains/prefix/suffix 多语种")

;; 9. 字符串与向量转换
(test-begin "string<->vector 多语种")
(test-equal "string->vector" (string->vector "中文") #(#\中 #\文))
(test-equal "vector->string" (vector->string (vector #\中 #\文)) "中文")
(display "string->vector 日语: ")
(display (string->vector "あいう")) (newline)
(test-end "string<->vector 多语种")

;; 10. string->number / number->string 多语种无关 (radix)，仅确认正常工作
(test-begin "string<->number")
(test-equal "number->string" (number->string 255 16) "ff")
(test-equal "string->number" (string->number 1010 2) 10)
(test-end "string<->number")

(display "\n=== ALL MULTILINGUAL STRING TESTS DONE ===\n")
