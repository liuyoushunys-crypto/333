# initenv_ext.py — builtin registration extracted from primitives_ext.py
import math, sys
from mtypes import (
    Sym, Cell, SchemeString, SchemeVector, SchemeBytevector,
    ErrorObject, NIL, VOID, TRUE, FALSE,
    _pr, _so, _sn, _lst, builtin
)
from primitives import *
from primitives_ext import *

def initenv_ext():
    # ═══════════════════════════════════════════════════════════════
    # SRFI-111: Boxes
    # ═══════════════════════════════════════════════════════════════

    # ═══════════════════════════════════════════════════════════════
    # SRFI-128: Comparators
    # ═══════════════════════════════════════════════════════════════
    builtin('make-comparator', lambda a, b, c, d='custom': make_comparator(a, b, c, d))
    builtin('comparator?', is_comparator)
    builtin('comparator-order?', is_comparator_order)
    builtin('comparator-hashable?', is_comparator_hashable)
    builtin('comparator-test-type', lambda c: (lambda x: TRUE))
    builtin('make-default-comparator', lambda: default_comparator())
    builtin('make-eq-comparator', lambda: lst(COMPARATOR_TAG, lambda a,b: a is b, lambda a,b: False, lambda x: id(x)))
    builtin('make-eqv-comparator', lambda: lst(COMPARATOR_TAG, lambda a,b: a is b or a == b, lambda a,b: False, lambda x: id(x)))
    builtin('make-equal-comparator', lambda: lst(COMPARATOR_TAG, lambda a,b: a == b, lambda a,b: False, lambda x: id(x)))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-141: Division (exact integer division variants)
    # ═══════════════════════════════════════════════════════════════
    builtin('floor-div', floor_div)
    builtin('floor-mod', floor_mod)
    builtin('floor-rem', floor_rem)
    builtin('floor-quotient', floor_div)
    builtin('floor-remainder', floor_rem)
    builtin('floor/', lambda a, b: Cell(floor_div(a, b), floor_rem(a, b)))

    builtin('truncate-div', truncate_div)
    builtin('truncate-rem', truncate_rem)
    builtin('truncate-quotient', truncate_div)
    builtin('truncate-remainder', truncate_rem)
    builtin('truncate/', lambda a, b: Cell(truncate_div(a, b), truncate_rem(a, b)))

    builtin('ceiling-div', ceiling_div)
    builtin('ceiling-rem', ceiling_rem)
    builtin('ceiling-quotient', ceiling_div)
    builtin('ceiling-remainder', ceiling_rem)
    builtin('ceiling/', lambda a, b: Cell(ceiling_div(a, b), ceiling_rem(a, b)))

    builtin('round-div', round_div)
    builtin('round-rem', lambda n, d: int(n) - round_div(n, d) * int(d))
    builtin('round-quotient', round_div)
    builtin('round-remainder', lambda n, d: int(n) - round_div(n, d) * int(d))
    builtin('round/', lambda a, b: Cell(round_div(a, b), int(a) - round_div(a, b) * int(b)))

    builtin('euclidean-div', euclidean_div)
    builtin('euclidean-rem', euclidean_rem)
    builtin('euclidean-quotient', euclidean_div)
    builtin('euclidean-remainder', euclidean_rem)
    builtin('euclidean/', lambda a, b: Cell(euclidean_div(a, b), euclidean_rem(a, b)))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-143: Fixnums (exact integer arithmetic with overflow check)
    # ═══════════════════════════════════════════════════════════════
    builtin('fx-width', lambda: FX_WIDTH)
    builtin('fx-greatest', lambda: FX_GREATEST)
    builtin('fx-least', lambda: FX_LEAST)
    builtin('fx+', fx_add)
    builtin('fx-', fx_sub)
    builtin('fx*', fx_mul)
    builtin('fxdiv', fx_div)
    builtin('fxmod', fx_mod)
    builtin('fxdiv0', lambda x, y: floor_div(x, y))
    builtin('fxmod0', lambda x, y: floor_rem(x, y))
    builtin('fx=?', lambda *a: fx_cmp(lambda x, y: x == y, *a))
    builtin('fx<?', lambda *a: fx_cmp(lambda x, y: x < y, *a))
    builtin('fx>?', lambda *a: fx_cmp(lambda x, y: x > y, *a))
    builtin('fx<=?', lambda *a: fx_cmp(lambda x, y: x <= y, *a))
    builtin('fx>=?', lambda *a: fx_cmp(lambda x, y: x >= y, *a))
    builtin('fxzero?', lambda x: TRUE if fxcheck(x) == 0 else FALSE)
    builtin('fxpositive?', lambda x: TRUE if fxcheck(x) > 0 else FALSE)
    builtin('fxnegative?', lambda x: TRUE if fxcheck(x) < 0 else FALSE)
    builtin('fxodd?', lambda x: TRUE if fxcheck(x) & 1 else FALSE)
    builtin('fxeven?', lambda x: TRUE if not (fxcheck(x) & 1) else FALSE)
    builtin('fxmax', lambda *a: max(fxcheck(x) for x in a))
    builtin('fxmin', lambda *a: min(fxcheck(x) for x in a))
    builtin('fxand', fx_and)
    builtin('fxior', fx_ior)
    builtin('fxxor', fx_xor)
    builtin('fxnot', fx_not)
    builtin('fxlsh', fx_lsh)
    builtin('fxrshl', fx_rshl)
    builtin('fxrsha', fx_rsha)
    builtin('fxfirst-set-bit', lambda x: (x & -x).bit_length() - 1 if x else -1)
    builtin('fxbit-count', lambda x: x.bit_count() if x else 0)
    builtin('fxlength', lambda x: x.bit_length())
    builtin('fxif', lambda a, b, c: (a & b) | (~a & c))
    builtin('fxbit-set?', lambda x, i: TRUE if (x >> i) & 1 else FALSE)
    builtin('fxcopy-bit', lambda x, i, b: x if b else (x | (1 << i)))
    builtin('fxgcd', math.gcd)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-144: Flonums (inexact real arithmetic)
    # ═══════════════════════════════════════════════════════════════
    builtin('flonum?', lambda x: TRUE if is_flonum(x) else FALSE)
    builtin('fl+', fl_add)
    builtin('fl-', fl_sub)
    builtin('fl*', fl_mul)
    builtin('fl/', fl_div)
    builtin('fl=?', lambda *a: fl_cmp(lambda x, y: x == y, *a))
    builtin('fl<?', lambda *a: fl_cmp(lambda x, y: x < y, *a))
    builtin('fl>?', lambda *a: fl_cmp(lambda x, y: x > y, *a))
    builtin('fl<=?', lambda *a: fl_cmp(lambda x, y: x <= y, *a))
    builtin('fl>=?', lambda *a: fl_cmp(lambda x, y: x >= y, *a))
    builtin('flzero?', lambda x: TRUE if float(x) == 0.0 else FALSE)
    builtin('flpositive?', lambda x: TRUE if float(x) > 0.0 else FALSE)
    builtin('flnegative?', lambda x: TRUE if float(x) < 0.0 else FALSE)
    builtin('flodd?', lambda x: TRUE if int(Fraction(x)) % 2 != 0 else FALSE)
    builtin('fleven?', lambda x: TRUE if int(Fraction(x)) % 2 == 0 else FALSE)
    builtin('flfinite?', lambda x: TRUE if isinstance(x, float) and math.isfinite(x) else FALSE)
    builtin('flinfinite?', lambda x: TRUE if isinstance(x, float) and math.isinf(x) else FALSE)
    builtin('flnan?', lambda x: TRUE if isinstance(x, float) and math.isnan(x) else FALSE)
    builtin('flmax', fl_max)
    builtin('flmin', fl_min)
    builtin('flfloor', math.floor)
    builtin('flceiling', math.ceil)
    builtin('flround', round)
    builtin('fltruncate', math.trunc)
    builtin('flsqrt', math.sqrt)
    builtin('flexp', math.exp)
    builtin('flexpt', lambda a, b: float(a) ** float(b))
    builtin('fllog', math.log)
    builtin('flsin', math.sin)
    builtin('flcos', math.cos)
    builtin('fltan', math.tan)
    builtin('flasin', math.asin)
    builtin('flacos', math.acos)
    builtin('flatan', math.atan)
    builtin('flonum->fixnum', lambda x: int(x))
    builtin('fixnum->flonum', lambda x: float(x))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-151: Bitwise operations
    # ═══════════════════════════════════════════════════════════════
    builtin('bitwise-not', bitwise_not)
    builtin('bitwise-and', bitwise_and)
    builtin('bitwise-ior', bitwise_ior)
    builtin('bitwise-xor', bitwise_xor)
    builtin('bitwise-if', bitwise_if)
    builtin('bitwise-merge', bitwise_if)
    builtin('bitwise-length', bitwise_length)
    builtin('bitwise-count', bitwise_count)
    builtin('bitwise-reverse-bit-field', bitwise_reverse_bitfield)
    builtin('bitwise-rotate', bitwise_rotate)
    builtin('bitwise-rotate-bit-field', bitwise_rotate_field)
    builtin('bitwise-copy-bit-field', bitwise_copy_bit_field)
    builtin('bitwise-copy-bit', bitwise_copy_bit)
    builtin('bitwise-bit-field', bitwise_bit_field)
    builtin('bitwise-arithmetic-shift', bitwise_shift)
    builtin('bitwise-arithmetic-shift-right', lambda n, c: bitwise_shift(n, -int(c)))
    builtin('bitwise-shift', bitwise_shift)
    builtin('bitwise-any-bit-set?', lambda n, m: TRUE if (int(n) & int(m)) != 0 else FALSE)
    builtin('integer-length', integer_length)
    builtin('first-set-bit', first_set_bit)
    builtin('bit-count', bitwise_count)
    builtin('bit-field', bitwise_bit_field)
    builtin('bit-shift', bitwise_shift)
    builtin('copy-bit', bitwise_copy_bit)
    builtin('bit-set?', lambda n, i: TRUE if (int(n) >> int(i)) & 1 else FALSE)
    builtin('integer->booleans', integer_to_booleans)

    # ═══════════════════════════════════════════════════════════════
    # Bitvectors
    # ═══════════════════════════════════════════════════════════════
    builtin('bitvector?', bitvector_p)
    builtin('make-bitvector', lambda n, *fill: SchemeVector([fill[0] if fill else FALSE] * int(n)))
    builtin('bitvector-copy', lambda bv, *args: SchemeVector(list(vec(bv))))
    builtin('bitvector-append', lambda *bvs: SchemeVector([x for bv in bvs for x in vec(bv)]))
    builtin('bitvector-length', lambda bv: len(vec(bv)))
    builtin('bitvector-ref', lambda bv, i: TRUE if vec(bv)[i] else FALSE)
    builtin('bitvector-set!', lambda bv, i, v: vec_set(bv, int(i), v))
    builtin('list->bitvector', lambda lst: SchemeVector([x is TRUE or x is True for x in cell_iter(lst)]))
    builtin('bitvector->list', lambda bv: _lst([TRUE if x else FALSE for x in vec(bv)]))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-133: Vector extensions
    # ═══════════════════════════════════════════════════════════════
    builtin('vector-map', vector_map)
    builtin('vector-map!', do_vector_map)
    builtin('vector-for-each', vector_for_each)
    builtin('vector-count', vector_count)
    builtin('vector-any', vector_any)
    builtin('vector-every', vector_every)
    builtin('vector-fold', vector_fold)
    builtin('vector-fold-right', vector_fold_right)
    builtin('vector-unfold', vector_unfold)
    builtin('vector-index', vector_index)
    builtin('vector-skip', vector_skip)
    builtin('vector-swap!', do_vector_swap)
    builtin('vector-reverse!', do_vector_reverse)
    builtin('vector-empty?', vec_empty_q)
    builtin('vector-append', vector_append)
    builtin('vector-copy', vector_copy_fn)
    builtin('vector-copy!', vector_copy_bang)
    builtin('vector-concatenate', vector_concat)
    builtin('vector-reverse', vector_reverse_fn)
    builtin('vector-sort', vector_sort_fn)
    builtin('vector=', vector_equal)
    builtin('reverse-list->vector', lambda lst: SchemeVector(cells(lst)[::-1]))

    # Basic vector operations (re-register for pyb=True override)
    builtin('vector', lambda *a: SchemeVector(list(a)))



    builtin('vector->list', lambda v: _lst(list(v)))

    builtin('vector-fill!', lambda v, x, *a: vec_fill_range(v, x, *a))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-152: String utilities
    # ═══════════════════════════════════════════════════════════════
    builtin('string-take', string_take)
    builtin('string-drop', string_drop)
    builtin('string-take-right', string_take_right)
    builtin('string-drop-right', string_drop_right)
    builtin('string-pad', string_pad)
    builtin('string-pad-right', string_pad_right)
    builtin('string-trim', string_trim)
    builtin('string-trim-right', string_trim_right)
    builtin('string-trim-both', string_trim_both)
    builtin('string-trim-left', string_trim_left_fn)
    builtin('string-replace', string_replace)
    builtin('string-split', string_split)
    builtin('string-join', string_join)
    builtin('string-contains', string_contains)
    builtin('string-prefix?', str_prefix_q)
    builtin('string-suffix?', str_suffix_q)
    builtin('string-prefix-length', str_prefix_len)
    builtin('string-suffix-length', str_suffix_len)
    builtin('string-prefix-length-ci', str_prefix_len_ci)
    builtin('string-suffix-length-ci', str_suffix_len_ci)
    builtin('string-count', string_count)
    builtin('string-map', string_map)
    builtin('string-for-each', string_for_each)
    builtin('string-for-each-index', string_for_each_idx)
    builtin('string-fold', string_fold)
    builtin('string-fold-right', string_fold_right_fn)
    builtin('string-index', string_index_fn)
    builtin('string-index-right', string_index_right_fn)
    builtin('string-skip', string_skip_fn)
    builtin('string-skip-right', string_skip_right_fn)
    builtin('string-any', string_any_fn)
    builtin('string-every', string_every_fn)
    builtin('string-concatenate', string_concat)
    builtin('string-copy!', string_copy_bang)
    builtin('string-xcopy!', string_xcopy_bang)
    builtin('string-delete', string_remove_fn)
    builtin('string-filter', string_filter_fn)
    builtin('string-remove', string_remove_fn)
    builtin('string-reverse', lambda s: SchemeString(''.join(reversed(str(s)))))
    builtin('string-foldcase', lambda s: SchemeString(str(s).lower()))
    builtin('string-titlecase', lambda s: SchemeString(str(s).title()))


    builtin('string-tokenize', string_tokenize_fn)
    builtin('string-unfold', string_unfold_fn)
    builtin('string-tabulate', lambda n, f: SchemeString(''.join(_so(f(i)) for i in range(int(n)))))
    builtin('string->char-set', str_to_char_set)
    builtin('string->vector', str_to_vec)
    builtin('vector->string', vec_to_str)

    # Basic string operations (re-register for pyb=True override)
    builtin('string', lambda *a: SchemeString(''.join(char_val(x) for x in a)))

    builtin('->string', lambda x: x if isinstance(x, (str, SchemeString)) else SchemeString(_pr(x)))

    # ═══════════════════════════════════════════════════════════════
    # String comparison (ci variants)
    # ═══════════════════════════════════════════════════════════════
    builtin('string=?', lambda a, b: TRUE if str(a) == str(b) else FALSE)
    builtin('string<?', lambda a, b: TRUE if str(a) < str(b) else FALSE)
    builtin('string>?', lambda a, b: TRUE if str(a) > str(b) else FALSE)
    builtin('string<=?', lambda a, b: TRUE if str(a) <= str(b) else FALSE)
    builtin('string>=?', lambda a, b: TRUE if str(a) >= str(b) else FALSE)
    builtin('string-ci=?', lambda a, b: TRUE if str(a).lower() == str(b).lower() else FALSE)
    builtin('string-ci<?', lambda a, b: TRUE if str(a).lower() < str(b).lower() else FALSE)
    builtin('string-ci>?', lambda a, b: TRUE if str(a).lower() > str(b).lower() else FALSE)
    builtin('string-ci<=?', lambda a, b: TRUE if str(a).lower() <= str(b).lower() else FALSE)
    builtin('string-ci>=?', lambda a, b: TRUE if str(a).lower() >= str(b).lower() else FALSE)

    # ═══════════════════════════════════════════════════════════════
    # Char operations
    # ═══════════════════════════════════════════════════════════════
    builtin('char-ci=?', char_ci_eq)
    builtin('char-ci<?', lambda a, b: TRUE if str(a).lower() < str(b).lower() else FALSE)
    builtin('char-ci>?', lambda a, b: TRUE if str(a).lower() > str(b).lower() else FALSE)
    builtin('char-ci<=?', lambda a, b: TRUE if str(a).lower() <= str(b).lower() else FALSE)
    builtin('char-ci>=?', lambda a, b: TRUE if str(a).lower() >= str(b).lower() else FALSE)








    builtin('char-ascii?', lambda c: TRUE if ord(char_val(c)) < 128 else FALSE)
    builtin('char-control?', lambda c: TRUE if (n := ord(char_val(c))) < 32 or n == 127 else FALSE)
    builtin('char-iso-control?', lambda c: TRUE if (n := ord(char_val(c))) < 32 or n == 127 else FALSE)
    builtin('ascii?', lambda c: TRUE if ord(_so(c)) < 128 else FALSE)
    builtin('char->name', char_name)
    builtin('digit-value', digit_value)

    # ═══════════════════════════════════════════════════════════════
    # Char-set operations (SRFI-14)
    # ═══════════════════════════════════════════════════════════════
    builtin('char-set', lambda *chars: char_set_make(chars))
    builtin('char-set?', char_set_p)
    builtin('char-set-contains?', char_set_contains)
    builtin('char-set-empty?', char_set_empty)
    builtin('char-set->list', char_set_to_list)
    builtin('char-set->string', char_set_to_string)
    builtin('char-set-count', char_set_count)
    builtin('char-set-copy', char_set_copy)
    builtin('char-set-union', lambda *css: char_set_binop(css, lambda a, b: a or b))
    builtin('char-set-intersection', lambda *css: char_set_binop(css, lambda a, b: a and b))
    builtin('char-set-difference', lambda cs1, *css: char_set_diff(cs1, css))
    builtin('char-set-xor', lambda *css: char_set_xor(css))
    builtin('char-set-complement', char_set_complement)
    builtin('char-set-adjoin', lambda cs, *chars: char_set_adjoin(cs, chars))
    builtin('char-set-delete', lambda cs, *chars: char_set_delete(cs, chars))
    builtin('char-set-any', char_set_any)
    builtin('char-set-every', char_set_every)
    builtin('char-set-filter', lambda pred, cs, *basis: char_set_filter(pred, cs, basis[0] if basis else cs))
    builtin('char-set-fold', char_set_fold)
    builtin('char-set-for-each', char_set_for_each)
    builtin('char-set-map', char_set_map)
    builtin('char-set-hash', lambda cs, *bound: char_set_hash(cs, int(bound[0]) if bound else 65536))
    builtin('char-set=?', char_set_equal)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-158: Generators
    # ═══════════════════════════════════════════════════════════════
    builtin('generator', generator)
    builtin('generator?', lambda x: TRUE if callable(x) else FALSE)
    builtin('make-generator', make_generator)
    builtin('list->generator', list_generator)
    builtin('vector->generator', vector_generator)
    builtin('string->generator', string_generator)
    builtin('generator-map', generator_map)
    builtin('generator-filter', generator_filter)
    builtin('generator-take', generator_take)
    builtin('generator-drop', generator_drop)
    builtin('generator-find', generator_find)
    builtin('generator-count', generator_count)
    builtin('generator-append', generator_append)
    builtin('generator->list', generator_list_and)
    builtin('generator->vector', generator_vector_and)
    builtin('generator->string', generator_string_and)
    builtin('generator-for-each', generator_for_each)
    builtin('generator-fold', generator_fold_fn)
    builtin('make-iota-generator', lambda n, s=1, st=0: generator_iota(int(n), s, st))
    builtin('make-range-generator', lambda s, e, st=1: generator_range(s, e, st))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-117: List queues
    # ═══════════════════════════════════════════════════════════════
    builtin('make-list-queue', lambda f=NIL, b=NIL: make_list_queue(f, b))
    builtin('list-queue', list_queue)
    builtin('list-queue?', is_list_queue)
    builtin('list-queue-front', list_queue_front)
    builtin('list-queue-back', list_queue_back)
    builtin('list-queue-empty?', lq_empty_q)
    builtin('list-queue-add!', do_lq_add)
    builtin('list-queue-add-back!', do_lq_add)
    builtin('list-queue-add-front!', do_lq_add_front)
    builtin('list-queue-remove!', do_lq_remove)
    builtin('list-queue-remove-front!', do_lq_remove)
    builtin('list-queue-list', list_queue_list)
    builtin('list-queue-first', list_queue_first)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-125: Hash tables
    # ═══════════════════════════════════════════════════════════════

    builtin('make-eq-hash-table', make_ht)
    builtin('make-equal-hash-table', make_ht)
    builtin('make-eqv-hash-table', make_ht)
    builtin('make-strong-hash-table', lambda: {})

    builtin('hash-table-clear!', do_ht_clear)


    builtin('hash-table-map', hash_table_map)
    builtin('hash-table-fold', hash_table_fold)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-1: List operations
    # ═══════════════════════════════════════════════════════════════
    builtin('reverse', rvrs)
    builtin('cons*', cons_star)
    builtin('list*', cons_star)
    builtin('list-copy', list_copy_fn)
    builtin('make-list', lambda n, *v: make_list_fn(int(n), v[0] if v else FALSE))
    builtin('iota', lambda n, *a: iota_fn(int(n), a[0] if a else 0, a[1] if len(a) > 1 else 1))
    builtin('first', lambda lst: nth(lst, 0))
    builtin('second', lambda lst: nth(lst, 1))
    builtin('third', lambda lst: nth(lst, 2))
    builtin('fourth', lambda lst: nth(lst, 3))
    builtin('fifth', lambda lst: nth(lst, 4))
    builtin('sixth', lambda lst: nth(lst, 5))
    builtin('seventh', lambda lst: nth(lst, 6))
    builtin('eighth', lambda lst: nth(lst, 7))
    builtin('ninth', lambda lst: nth(lst, 8))
    builtin('tenth', lambda lst: nth(lst, 9))
    builtin('list-head', list_head_fn)

    builtin('take', lambda lst, n: list_take(lst, int(n)))
    builtin('drop', lambda lst, n: list_drop(lst, int(n)))
    builtin('take-right', lambda lst, n: list_take_right(lst, int(n)))
    builtin('drop-right', lambda lst, n: list_drop_right(lst, int(n)))
    builtin('take-while', list_take_while)
    builtin('drop-while', list_drop_while)
    builtin('last', list_last)
    builtin('last-pair', list_last_pair)
    builtin('but-last', list_butlast)
    builtin('length+', length_plus)
    builtin('list-tabulate', lambda n, f: list_tabulate_fn(int(n), f))
    builtin('list-index', list_index_fn)
    builtin('list-set!', list_set_bang)
    builtin('list-find', list_find)
    builtin('list-find-index', list_find_index)
    builtin('list-any', list_any)
    builtin('list-every', list_every)
    builtin('list-filter-map', list_filter_map)
    builtin('list-partition', list_partition)
    builtin('list-remove', list_remove)
    builtin('list-flatten', list_flatten)
    builtin('list-zip', zip_fn)
    builtin('list-sort', list_sort_fn)
    builtin('list-stable-sort', list_sort_fn)
    builtin('list=', list_equal)
    builtin('sorted?', sorted_p_fn)
    builtin('merge', merge_fn)
    builtin('merge!', merge_bang_fn)
    builtin('assq', lambda obj, al: assoc_fn(obj, al, lambda a, b: a is b))
    builtin('assv', lambda obj, al: assoc_fn(obj, al, lambda a, b: a is b or a == b))
    builtin('assoc', lambda obj, al, *eq: assoc_fn(obj, al, eq[0] if eq else (lambda a, b: a is b or a == b)))
    builtin('memq', lambda obj, lst: mem_fn(obj, lst, lambda a, b: a is b))
    builtin('memv', lambda obj, lst: mem_fn(obj, lst, lambda a, b: a is b or a == b))
    builtin('member', lambda obj, lst, *eq: mem_fn(obj, lst, eq[0] if eq else (lambda a, b: a is b or a == b)))
    builtin('find', list_find)
    builtin('fold', fold_left_fn)
    builtin('fold-left', fold_left_fn)
    builtin('fold-right', fold_right_fn)
    builtin('reduce', fold_left_fn)
    builtin('reduce-right', fold_right_fn)
    builtin('any', list_any)
    builtin('every', list_every)
    builtin('count', count_fn)
    builtin('delete', lambda x, lst, *eq: delete_fn(x, lst, eq[0] if eq else None))
    builtin('delete-duplicates', lambda lst, *eq: delete_dups_fn(lst, eq[0] if eq else None))
    builtin('delete-assoc', delete_assoc_fn)
    builtin('alist-cons', lambda k, v, al: Cell(Cell(k, v), al))
    builtin('alist-delete', alist_delete_fn)
    builtin('append-map', append_map_fn)
    builtin('append-reverse', append_rev)
    builtin('concatenate', concatenate_fn)
    builtin('flatten', flatten_fn)
    builtin('filter-map', filter_map_fn)
    builtin('map-in-order', map_fn)
    builtin('pair-for-each', pair_for_each_fn)
    builtin('xcons', lambda d, a: Cell(a, d))
    builtin('zip', zip_fn)
    builtin('unzip1', lambda lst: unzip_n(lst, 1))
    builtin('unzip2', lambda lst: unzip_n(lst, 2))
    builtin('unzip3', lambda lst: unzip_n(lst, 3))
    builtin('unzip4', lambda lst: unzip_n(lst, 4))
    builtin('unzip5', lambda lst: unzip_n(lst, 5))
    builtin('curry', curry_fn)
    builtin('complement', lambda f: lambda *a: FALSE if f(*a) is TRUE else TRUE)
    builtin('flip', lambda f: lambda a, b: f(b, a))
    builtin('const', lambda x: lambda *_: x)
    builtin('iterate', lambda f, n, x: iterate_fn(f, int(n), x))
    builtin('product', product_fn)
    builtin('square', lambda x: x * x)
    builtin('range', lambda s, e, *st: range_fn(int(s), int(e), int(st[0]) if st else 1))
    builtin('interleave', interleave_fn)
    builtin('symbolic-append', lambda *a: Sym(''.join(_sn(x) for x in a)))

    builtin('<>', lambda a, b: TRUE if a != b else FALSE)

    # List predicate helpers
    builtin('circular-list', circular_list)
    builtin('circular-list?', circular_list_p)
    builtin('dotted-list?', dotted_list_p)
    builtin('proper-list?', proper_list_p)
    builtin('null-list?', lambda x: TRUE if x is NIL else FALSE)
    builtin('not-pair?', lambda x: TRUE if not isinstance(x, Cell) else FALSE)
    builtin('ne-list?', lambda x: TRUE if isinstance(x, Cell) and x.cdr is NIL else FALSE)

    # Mutation
    builtin('drop!', lambda lst, n: list_drop(lst, int(n)))
    builtin('take!', lambda lst, n: list_take(lst, int(n)))
    builtin('filter!', filter_fn)
    builtin('flat-map', append_map_fn)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-1: lset-* (set operations on lists)
    # ═══════════════════════════════════════════════════════════════
    builtin('lset-union', lset_union)
    builtin('lset-intersection', lset_intersection)
    builtin('lset-difference', lset_difference)
    builtin('lset-xor', lset_xor)
    builtin('lset-=?', lset_equal)

    # ═══════════════════════════════════════════════════════════════
    # Stream operations
    # ═══════════════════════════════════════════════════════════════
    builtin('nat-stream', lambda n: nat_stream_fn(int(n)))
    builtin('naturals', lambda *a: nat_stream(int(a[0]) if a else 0))
    builtin('sieve', sieve_fn)
    builtin('primes', sieve_fn(nat_stream(2)))

    # ═══════════════════════════════════════════════════════════════
    # Number theory & math
    # ═══════════════════════════════════════════════════════════════
    builtin('scheme-gcd', scheme_gcd_fn)
    builtin('scheme-lcm', scheme_lcm_fn)
    builtin('prime?', prime_p)
    builtin('factor', factor_fn)
    builtin('fib-pair', lambda n: fib_pair(int(n)))
    builtin('fibonacci', lambda n: fib_pair(int(n)).car)
    builtin('binomial', lambda n, k: binomial_fn(int(n), int(k)))
    builtin('factorial', lambda n: factorial_fn(int(n)))
    builtin('quick-expt', lambda b, e: quick_expt_fn(int(b), int(e)))
    builtin('expt-mod', expt_mod)
    builtin('log-base', log_base)
    builtin('degrees->radians', degrees_to_radians)
    builtin('radians->degrees', radians_to_degrees)

    # Hyperbolic math
    builtin('sinh', lambda x: math.sinh(float(x)))
    builtin('cosh', lambda x: math.cosh(float(x)))
    builtin('tanh', lambda x: math.tanh(float(x)))
    builtin('sech', lambda x: 1.0 / math.cosh(float(x)))
    builtin('csch', lambda x: 1.0 / math.sinh(float(x)))
    builtin('coth', lambda x: math.cosh(float(x)) / math.sinh(float(x)))
    builtin('log10', lambda x: math.log10(float(x)))
    builtin('log2', lambda x: math.log2(float(x)))

    # Numeric predicates and conversions
    builtin('nan?', lambda x: x != x)
    builtin('finite?', lambda x: TRUE if isinstance(x, (int, float, Fraction, complex)) and (not isinstance(x, float) or (x == x and x != float('inf') and x != float('-inf'))) else FALSE)
    builtin('infinite?', lambda x: TRUE if isinstance(x, float) and math.isinf(x) else FALSE)
    builtin('exact', lambda x: int(x) if isinstance(x, float) and x == int(x) else (Fraction(x).limit_denominator(1000000) if isinstance(x, float) else x))
    builtin('inexact', lambda x: float(x))
    builtin('exact-nonnegative-integer?', lambda x: TRUE if (isinstance(x, int) and x >= 0) or (isinstance(x, Fraction) and x.denominator == 1 and x.numerator >= 0) else FALSE)
    builtin('exact-rational?', lambda x: TRUE if isinstance(x, (int, Fraction)) else FALSE)
    builtin('ceiling->exact', lambda x: int(math.ceil(float(x))) if isinstance(x, Fraction) else int(math.ceil(x)))
    builtin('floor->exact', lambda x: int(math.floor(float(x))) if isinstance(x, Fraction) else int(math.floor(x)))
    builtin('truncate->exact', lambda x: int(x))
    builtin('round->exact', lambda x: int(round(float(x))) if isinstance(x, Fraction) else int(round(x)))
    builtin('magnitude', lambda z: abs(z) if isinstance(z, complex) else abs(z))
    builtin('make-rectangular', lambda r, i: complex(float(r) if isinstance(r, Fraction) else int(r) if isinstance(r, float) and r == int(r) else r, float(i) if isinstance(i, Fraction) else int(i) if isinstance(i, float) and i == int(i) else i))

    # Basic numeric aliases
    builtin('add1', lambda x: x + 1)
    builtin('sub1', lambda x: x - 1)
    builtin('sub1*', lambda x: x - 1)
    builtin('float', lambda x: float(x))

    # ═══════════════════════════════════════════════════════════════
    # Conditions & errors
    # ═══════════════════════════════════════════════════════════════
    builtin('error?', error_q)
    builtin('file-error?', file_error_q)
    builtin('read-error?', read_error_q)
    builtin('condition-has-type?', lambda c, t: isinstance(c, tuple) and len(c) > 2 and c[1] == t)
    builtin('condition-type?', lambda obj: TRUE if (isinstance(obj, tuple) and len(obj) > 2 and obj[0] == 'condition') or isinstance(obj, ErrorObject) else FALSE)
    builtin('condition/report-string', lambda c: SchemeString(c[2]) if isinstance(c, tuple) and len(c) > 2 else SchemeString(str(c)))
    builtin('raise-continuable', lambda c: do_raise(c))
    builtin('make-error-condition', lambda t, m: ('condition', t, m))
    builtin('condition-message', lambda c: c[2] if isinstance(c, tuple) and len(c) > 2 else str(c))

    # ═══════════════════════════════════════════════════════════════
    # Maybe monad
    # ═══════════════════════════════════════════════════════════════
    builtin('maybe?', maybe_p)
    builtin('just', lambda x: Cell(x, NIL))
    builtin('nothing', lambda: FALSE)
    builtin('just?', just_p)
    builtin('nothing?', nothing_p)
    builtin('maybe-ref', lambda x, *default: x.car if isinstance(x, Cell) else (default[0] if default else FALSE))
    builtin('maybe->values', lambda x: (x.car, TRUE) if isinstance(x, Cell) else (FALSE, FALSE))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-180: JSON
    # ═══════════════════════════════════════════════════════════════
    builtin('json-read', json_read)
    builtin('json-write', json_write)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-207: String-notable (bytevector <-> string)
    # ═══════════════════════════════════════════════════════════════
    builtin('bytevector->string', bytevector_to_string)
    builtin('string->bytevector', string_to_bytevector)

    # ═══════════════════════════════════════════════════════════════
    # Mapping (SRFI-146)
    # ═══════════════════════════════════════════════════════════════
    builtin('mapping', mapping_fn)
    builtin('mapping?', mapping_pred)

    # ═══════════════════════════════════════════════════════════════
    # Textual port I/O
    # ═══════════════════════════════════════════════════════════════
    builtin('textual-port?', lambda p: TRUE if p is TRUE or (isinstance(p, tuple) and p[0] in ('str-port', 'file-port')) else FALSE)
    builtin('char-ready?', lambda *p: TRUE if not p else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'str-port' and p[0][1] and p[0][1][0] else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'file-port' and len(p[0]) > 3 else FALSE)))
    builtin('u8-ready?', lambda *p: TRUE if not p else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'str-port' and p[0][1] and p[0][1][0] else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'file-port' and len(p[0]) > 3 else FALSE)))
    builtin('peek-u8', peek_u8_fn)
    builtin('read-u8', read_u8_fn)
    builtin('write-u8', write_u8)
    builtin('read-line', read_line)
    builtin('read-string', read_string_fn)
    builtin('write-string', write_string)
    builtin('get-output-bytevector', lambda: SchemeBytevector([]))


    # Bytevector (base builtin in initenv.py)
    # ═══════════════════════════════════════════════════════════════
    # Symbol operations
    # ═══════════════════════════════════════════════════════════════
    builtin('symbol=?', symbol_equal_p)
    builtin('number=?', num_equal_p)



    # ═══════════════════════════════════════════════════════════════
    # Environment
    # ═══════════════════════════════════════════════════════════════



    # ═══════════════════════════════════════════════════════════════
    # Random
    # ═══════════════════════════════════════════════════════════════
    builtin('random-integer', random_integer)
    builtin('random-real', random_real)
    builtin('random-seed', random_seed)

    # ═══════════════════════════════════════════════════════════════
    # Various helpers
    # ═══════════════════════════════════════════════════════════════
    builtin('atom?', lambda x: FALSE if isinstance(x, Cell) else TRUE)
    builtin('void?', lambda x: TRUE if x is VOID else FALSE)
    builtin('boolean->string', lambda x: SchemeString('#t') if x is TRUE else SchemeString('#f'))
    builtin('boolean=?', lambda *a: FALSE if any(a[i] != a[i+1] for i in range(len(a)-1)) else TRUE)
    builtin('default-object?', lambda x: TRUE if x is VOID else FALSE)
    builtin('name', lambda x: _sn(x) if isinstance(x, Sym) else SchemeString(_pr(x)))
    builtin('pp', lambda x: (sys.stdout.write(_pr(x) + '\n'), VOID)[-1])
    builtin('array?', lambda x: TRUE if isinstance(x, SchemeVector) else FALSE)
    builtin('cartesian-product', lambda *lists: cartesian_product(list(lists)))
    builtin('combinations', lambda lst, n: combinations_fn(lst, int(n)))
    builtin('permutations', lambda lst: perms_fn(lst))
    builtin('unfold', lambda p, f, g, seed, *thunk: unfold_fn(p, f, g, seed, thunk[0] if thunk else None))
    builtin('unfold-right', lambda p, f, g, seed, *thunk: unfold_right_fn(p, f, g, seed, thunk[0] if thunk else None))
    builtin('describe', lambda x: sys.stdout.write(str(x) + '\n') or VOID)
    builtin('identity', lambda x: x)
    builtin('flexp2', lambda x: 2.0 ** float(x))
